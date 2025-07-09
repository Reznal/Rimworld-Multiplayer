using System;
using System.Linq;
using System.Text;
using Multiplayer.Client.Desyncs;
using Multiplayer.Client.Util;
using Multiplayer.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Client.DebugUi
{
    /// <summary>
    /// Enhanced expandable debug panel that organizes existing comprehensive debug information
    /// with modern UI/UX while preserving all developer functionality
    /// </summary>
    public static class SyncDebugPanel
    {
        // Panel state
        private static bool isExpanded = false;
        private static Vector2 scrollPosition = Vector2.zero;
        
        // Panel dimensions
        private const float CompactHeight = 40f;
        private const float ExpandedMaxHeight = 400f;
        private const float PanelWidth = 350f;
        private const float Margin = 8f;
        
        // Visual constants
        private const float SectionSpacing = 15f;
        private const float LineHeight = 18f;
        private const float LabelColumnWidth = 0.45f; // 45% for labels, 55% for values
        
        /// <summary>
        /// Main entry point for the enhanced debug panel
        /// </summary>
        public static float DoSyncDebugPanel(float y)
        {
            // Safety checks
            if (Multiplayer.session == null || !MpVersion.IsDebug || !Multiplayer.ShowDevInfo || Multiplayer.WriterLog == null) return 0;

            try
            {
                float x = Margin;
                float panelHeight = isExpanded ? CalculateExpandedHeight() : CompactHeight;
                
                Rect panelRect = new Rect(x, y, PanelWidth, panelHeight);
                
                // Draw panel background
                Widgets.DrawBoxSolid(panelRect, new Color(0f, 0f, 0f, 0.7f));
                Widgets.DrawBox(panelRect);
                
                if (isExpanded)
                {
                    DrawExpandedPanel(panelRect);
                }
                else
                {
                    DrawCompactPanel(panelRect);
                }
                
                return panelHeight + Margin;
            }
            catch (Exception ex)
            {
                // Fallback in case of any errors - don't crash the game
                Log.Error($"SyncDebugPanel error: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Draw the compact status summary view
        /// </summary>
        private static void DrawCompactPanel(Rect rect)
        {
            Rect contentRect = rect.ContractedBy(Margin);
            
            try
            {
                // Get status information
                var syncStatus = GetSyncStatus();
                var performanceStatus = GetPerformanceStatus();
                var errorStatus = GetErrorStatus();
                var tickStatus = GetTickStatus();
                
                // Draw compact status indicators with text values
                float currentX = contentRect.x + 2f;
                float centerY = contentRect.y + (contentRect.height / 2f);
                
                // Sync status [🟢 SYNC]
                currentX = DrawCompactStatusBadge(currentX, centerY, syncStatus.icon, syncStatus.text, syncStatus.color, syncStatus.tooltip);
                currentX += 4f; // spacing between badges
                
                // Performance status [⚡ 45.2]
                currentX = DrawCompactStatusBadge(currentX, centerY, performanceStatus.icon, performanceStatus.text, performanceStatus.color, performanceStatus.tooltip);
                currentX += 4f;
                
                // RNG status [📊 0]
                currentX = DrawCompactStatusBadge(currentX, centerY, errorStatus.icon, errorStatus.text, errorStatus.color, errorStatus.tooltip);
                currentX += 4f;
                
                // Tick status [🎯 3]
                currentX = DrawCompactStatusBadge(currentX, centerY, tickStatus.icon, tickStatus.text, tickStatus.color, tickStatus.tooltip);
                
                // Expand button [v]
                float buttonWidth = 20f;
                Rect expandRect = new Rect(contentRect.xMax - buttonWidth - 2f, 
                                         contentRect.y + (contentRect.height - 16f) / 2f, 
                                         buttonWidth, 16f);
                
                // Draw expand button as a badge (no border for consistency)
                Widgets.DrawBoxSolid(expandRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
                
                using (MpStyle.Set(GameFont.Tiny).Set(Color.white).Set(TextAnchor.MiddleCenter))
                {
                    if (Widgets.ButtonText(expandRect, "v"))
                    {
                        isExpanded = true;
                    }
                }
                
                // Click anywhere else to expand
                if (Event.current.type == EventType.MouseDown && 
                    Event.current.button == 0 && 
                    contentRect.Contains(Event.current.mousePosition) &&
                    !expandRect.Contains(Event.current.mousePosition))
                {
                    isExpanded = true;
                    Event.current.Use();
                }
            }
            catch (Exception ex)
            {
                // Fallback display for compact mode
                using (MpStyle.Set(GameFont.Tiny).Set(Color.red).Set(TextAnchor.MiddleLeft))
                {
                    Widgets.Label(contentRect, $"Debug Panel Error: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Draw a compact status badge with icon and text [🟢 SYNC]
        /// </summary>
        private static float DrawCompactStatusBadge(float x, float centerY, string icon, string text, Color color, string tooltip)
        {
            // Calculate badge dimensions
            string badgeText = $"{icon} {text}";
            float textWidth = Text.CalcSize(badgeText).x + 8f; // padding
            float badgeHeight = 16f;
            
            Rect badgeRect = new Rect(x, centerY - badgeHeight / 2f, textWidth, badgeHeight);
            
            // Draw badge background (no border for cleaner look)
            Widgets.DrawBoxSolid(badgeRect, new Color(0.1f, 0.1f, 0.1f, 0.7f));
            
            // Draw badge text
            using (MpStyle.Set(GameFont.Tiny).Set(color).Set(TextAnchor.MiddleCenter))
            {
                Widgets.Label(badgeRect, badgeText);
            }
            
            // Add tooltip
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(badgeRect, tooltip);
            }
            
            return x + textWidth;
        }
        
        /// <summary>
        /// Draw the expanded comprehensive debug view
        /// </summary>
        private static void DrawExpandedPanel(Rect rect)
        {
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Rect contentRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            
            // Draw header
            DrawPanelHeader(headerRect);
            
            // Draw scrollable content
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, GetContentHeight());
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            float currentY = 0f;
            
            // Status Summary Section
            currentY += DrawStatusSummarySection(viewRect.x + Margin, currentY, viewRect.width - Margin * 2);
            currentY += SectionSpacing;
            
            // RNG States Section
            currentY += DrawRNGStatesSection(viewRect.x + Margin, currentY, viewRect.width - Margin * 2);
            currentY += SectionSpacing;
            
            // Performance Section
            currentY += DrawPerformanceSection(viewRect.x + Margin, currentY, viewRect.width - Margin * 2);
            currentY += SectionSpacing;
            
            // Network & Sync Section
            currentY += DrawNetworkSyncSection(viewRect.x + Margin, currentY, viewRect.width - Margin * 2);
            currentY += SectionSpacing;
            
            // Detailed Debug Section (existing comprehensive information)
            currentY += DrawDetailedDebugSection(viewRect.x + Margin, currentY, viewRect.width - Margin * 2);
            
            Widgets.EndScrollView();
        }
        
        /// <summary>
        /// Draw the panel header with title and controls
        /// </summary>
        private static void DrawPanelHeader(Rect rect)
        {
            // Draw border around entire header for expanded mode
            Widgets.DrawBox(rect);
            
            try
            {
                // Get status information - same as compact mode
                var syncStatus = GetSyncStatus();
                var performanceStatus = GetPerformanceStatus();
                var errorStatus = GetErrorStatus();
                var tickStatus = GetTickStatus();
                
                // Use SAME content area as compact mode (contract by margin)
                Rect contentRect = rect.ContractedBy(Margin);
                
                // Draw status badges - EXACTLY same layout as compact mode
                float currentX = contentRect.x + 2f; // Same margin as compact
                float centerY = contentRect.y + (contentRect.height / 2f);
                
                // Sync status [● SYNC]
                currentX = DrawCompactStatusBadge(currentX, centerY, syncStatus.icon, syncStatus.text, syncStatus.color, syncStatus.tooltip);
                currentX += 4f; // Same spacing as compact
                
                // Performance status [▲ 45.2]
                currentX = DrawCompactStatusBadge(currentX, centerY, performanceStatus.icon, performanceStatus.text, performanceStatus.color, performanceStatus.tooltip);
                currentX += 4f;
                
                            // RNG status [■ 0]
            currentX = DrawCompactStatusBadge(currentX, centerY, errorStatus.icon, errorStatus.text, errorStatus.color, errorStatus.tooltip);
                currentX += 4f;
                
                // Tick status [♦ 3]
                currentX = DrawCompactStatusBadge(currentX, centerY, tickStatus.icon, tickStatus.text, tickStatus.color, tickStatus.tooltip);
                
                // Collapse button [^] - same style as expand button but no extra border
                float buttonWidth = 20f;
                Rect collapseRect = new Rect(contentRect.xMax - buttonWidth - 2f, // Same positioning as compact
                                           contentRect.y + (contentRect.height - 16f) / 2f, 
                                           buttonWidth, 16f);
                
                // Draw collapse button with same styling as compact expand button (no border)
                Widgets.DrawBoxSolid(collapseRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
                
                using (MpStyle.Set(GameFont.Tiny).Set(Color.white).Set(TextAnchor.MiddleCenter))
                {
                    if (Widgets.ButtonText(collapseRect, "^"))
                    {
                        isExpanded = false;
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to simple header
                using (MpStyle.Set(GameFont.Small).Set(Color.white).Set(TextAnchor.MiddleLeft))
                {
                    Rect titleRect = new Rect(rect.x + Margin, rect.y, rect.width - 100f, rect.height);
                    Widgets.Label(titleRect, "MULTIPLAYER DEBUG");
                }
                
                Rect collapseRect = new Rect(rect.xMax - 80f, rect.y + 5f, 70f, 20f);
                if (Widgets.ButtonText(collapseRect, "Collapse"))
                {
                    isExpanded = false;
                }
            }
        }
        
        /// <summary>
        /// Draw status summary section with key indicators
        /// </summary>
        private static float DrawStatusSummarySection(float x, float y, float width)
        {
            float startY = y;
            
            // Section header
            y = DrawSectionHeader(x, y, width, "STATUS SUMMARY");
            
            var syncStatus = GetSyncStatus();
            var performanceStatus = GetPerformanceStatus(); 
            var errorStatus = GetErrorStatus();
            var tickStatus = GetTickStatus();
            
            // Status lines
            y = DrawStatusLine(x, y, width, "Sync Status:", syncStatus.text, syncStatus.color);
            y = DrawStatusLine(x, y, width, "Performance:", performanceStatus.text, performanceStatus.color);
            y = DrawStatusLine(x, y, width, "Status:", errorStatus.text, errorStatus.color);
            y = DrawStatusLine(x, y, width, "Tick Status:", tickStatus.text, tickStatus.color);
            
            return y - startY;
        }
        
        /// <summary>
        /// Draw RNG states comparison section
        /// </summary>
        private static float DrawRNGStatesSection(float x, float y, float width)
        {
            float startY = y;
            
            y = DrawSectionHeader(x, y, width, "RNG STATES");
            
            if (Find.CurrentMap?.AsyncTime() != null)
            {
                var async = Find.CurrentMap.AsyncTime();
                var worldAsync = Multiplayer.AsyncWorldTime;
                
                // Current Map RNG state
                string mapRngLow = $"{(uint)async.randState:X8}";
                string mapRngHigh = $"{(uint)(async.randState >> 32):X8}";
                y = DrawStatusLine(x, y, width, "Current Map:", $"{mapRngHigh} | {mapRngLow}", Color.white);
                
                // World RNG state  
                string worldRngLow = $"{(uint)worldAsync.randState:X8}";
                string worldRngHigh = $"{(uint)(worldAsync.randState >> 32):X8}";
                y = DrawStatusLine(x, y, width, "World RNG:", $"{worldRngHigh} | {worldRngLow}", Color.white);
                
                // Round Mode
                var roundMode = RoundMode.GetCurrentRoundMode();
                y = DrawStatusLine(x, y, width, "Round Mode:", $"{roundMode}", Color.white);
            }
            else
            {
                y = DrawStatusLine(x, y, width, "RNG States:", "No current map", Color.gray);
            }
            
            return y - startY;
        }
        
        /// <summary>
        /// Draw performance metrics section
        /// </summary>
        private static float DrawPerformanceSection(float x, float y, float width)
        {
            float startY = y;
            
            y = DrawSectionHeader(x, y, width, "PERFORMANCE");
            
            // TPS
            float tps = IngameUIPatch.tps;
            Color tpsColor = tps > 40f ? Color.green : tps > 20f ? Color.yellow : Color.red;
            y = DrawStatusLine(x, y, width, "Map TPS:", $"{tps:F1}", tpsColor);
            
            // Frame time
            float frameTime = Time.deltaTime * 1000f;
            Color frameColor = frameTime < 20f ? Color.green : frameTime < 35f ? Color.yellow : Color.red;
            y = DrawStatusLine(x, y, width, "Frame Time:", $"{frameTime:F1}ms", frameColor);
            
            // Server time per tick
            float serverTpt = TickPatch.serverTimePerTick;
            y = DrawStatusLine(x, y, width, "Server TPT:", $"{serverTpt:F1}ms", Color.white);
            
            // Average frame time
            y = DrawStatusLine(x, y, width, "Avg Frame:", $"{TickPatch.avgFrameTime:F1}ms", Color.white);
            
            return y - startY;
        }
        
        /// <summary>
        /// Draw network and sync status section
        /// </summary>
        private static float DrawNetworkSyncSection(float x, float y, float width)
        {
            float startY = y;
            
            y = DrawSectionHeader(x, y, width, "NETWORK & SYNC");
            
            if (Multiplayer.session != null)
            {
                // Player count
                int playerCount = Multiplayer.session.players.Count;
                bool hasDesynced = Multiplayer.session.players.Any(p => p.status == PlayerStatus.Desynced);
                Color playerColor = hasDesynced ? Color.red : Color.green;
                y = DrawStatusLine(x, y, width, "Players:", $"{playerCount}", playerColor);
                
                // Commands
                y = DrawStatusLine(x, y, width, "Received Cmds:", $"{Multiplayer.session.receivedCmds}", Color.white);
                y = DrawStatusLine(x, y, width, "Remote Sent:", $"{Multiplayer.session.remoteSentCmds}", Color.white);
                y = DrawStatusLine(x, y, width, "Remote Tick:", $"{Multiplayer.session.remoteTickUntil}", Color.white);
                
                // Server status
                string serverStatus = TickPatch.serverFrozen ? "Frozen" : "Running";
                Color serverColor = TickPatch.serverFrozen ? Color.yellow : Color.green;
                y = DrawStatusLine(x, y, width, "Server:", serverStatus, serverColor);
            }
            else
            {
                y = DrawStatusLine(x, y, width, "Network:", "No active session", Color.gray);
            }
            
            return y - startY;
        }
        
        /// <summary>
        /// Draw detailed debug section with existing comprehensive information
        /// </summary>
        private static float DrawDetailedDebugSection(float x, float y, float width)
        {
            float startY = y;
            
            if (Multiplayer.ShowDevInfo && Find.CurrentMap != null)
            {
                var async = Find.CurrentMap.AsyncTime();
                
                // ===== CORE SYSTEM DATA =====
                y = DrawSectionHeader(x, y, width, "CORE SYSTEM");
                y = DrawStatusLine(x, y, width, "Faction Stack:", $"{FactionContext.stack.Count}", Color.white);
                y = DrawStatusLine(x, y, width, "Player Faction:", $"{Faction.OfPlayer.loadID}", Color.white);
                y = DrawStatusLine(x, y, width, "Real Player:", $"{Multiplayer.RealPlayerFaction?.loadID}", Color.white);
                y = DrawStatusLine(x, y, width, "Next Thing ID:", $"{Find.UniqueIDsManager.nextThingID}", Color.white);
                y = DrawStatusLine(x, y, width, "Next Job ID:", $"{Find.UniqueIDsManager.nextJobID}", Color.white);
                y = DrawStatusLine(x, y, width, "Game Ticks:", $"{Find.TickManager.TicksGame}", Color.white);
                y = DrawStatusLine(x, y, width, "Time Speed:", $"{Find.TickManager.CurTimeSpeed}", Color.white);
                y += SectionSpacing;
                
                // ===== TIMING & SYNC DATA =====
                y = DrawSectionHeader(x, y, width, "TIMING & SYNC");
                int timerLag = TickPatch.tickUntil - TickPatch.Timer;
                Color lagColor = timerLag > 30 ? Color.red : timerLag > 15 ? Color.yellow : Color.green;
                y = DrawStatusLine(x, y, width, "Timer Lag:", $"{timerLag}", lagColor);
                y = DrawStatusLine(x, y, width, "Timer:", $"{TickPatch.Timer}", Color.white);
                y = DrawStatusLine(x, y, width, "Tick Until:", $"{TickPatch.tickUntil}", Color.white);
                y = DrawStatusLine(x, y, width, "Raw Tick Timer:", $"{TickPatch.tickTimer.ElapsedMilliseconds}ms", Color.white);
                y = DrawStatusLine(x, y, width, "World Settlements:", $"{Find.World.worldObjects.settlements.Count}", Color.white);
                y += SectionSpacing;
                
                // ===== GAME STATE DATA =====
                y = DrawSectionHeader(x, y, width, "GAME STATE");
                y = DrawStatusLine(x, y, width, "Classic Mode:", $"{Find.IdeoManager.classicMode}", Color.white);
                y = DrawStatusLine(x, y, width, "Client Opinions:", $"{Multiplayer.game.sync.knownClientOpinions.Count}", Color.white);
                y = DrawStatusLine(x, y, width, "First Opinion Tick:", $"{Multiplayer.game.sync.knownClientOpinions.FirstOrDefault()?.startTick}", Color.white);
                y = DrawStatusLine(x, y, width, "Map Ticks:", $"{async.mapTicks}", Color.white);
                y = DrawStatusLine(x, y, width, "Frozen At:", $"{TickPatch.frozenAt}", Color.white);
                y += SectionSpacing;
                
                // ===== RNG & DEBUG DATA =====
                y = DrawSectionHeader(x, y, width, "RNG & DEBUG");
                y = DrawStatusLine(x, y, width, "Rand Calls:", $"{DeferredStackTracing.acc}", Color.white);
                y = DrawStatusLine(x, y, width, "Max Trace Depth:", $"{DeferredStackTracing.maxTraceDepth}", Color.white);
                y = DrawStatusLine(x, y, width, "Hash Entries:", $"{DeferredStackTracingImpl.hashtableEntries}/{DeferredStackTracingImpl.hashtableSize}", Color.white);
                y = DrawStatusLine(x, y, width, "Hash Collisions:", $"{DeferredStackTracingImpl.collisions}", Color.white);
                y += SectionSpacing;
                
                // ===== COMMAND & SYNC DATA =====
                y = DrawSectionHeader(x, y, width, "COMMAND & SYNC");
                y = DrawStatusLine(x, y, width, "Async Commands:", $"{async.cmds.Count}", Color.white);
                y = DrawStatusLine(x, y, width, "World Commands:", $"{Multiplayer.AsyncWorldTime.cmds.Count}", Color.white);
                y = DrawStatusLine(x, y, width, "Force Normal Speed:", $"{async.slower.forceNormalSpeedUntil}", Color.white);
                y = DrawStatusLine(x, y, width, "Async Time Status:", $"{Multiplayer.GameComp.asyncTime}", Color.white);
                y = DrawStatusLine(x, y, width, "Buffered Changes:", $"{SyncFieldUtil.bufferedChanges.Sum(kv => kv.Value.Count)}", Color.white);
                y += SectionSpacing;
                
                // ===== MEMORY & PERFORMANCE DATA =====
                y = DrawSectionHeader(x, y, width, "MEMORY & PERFORMANCE");
                y = DrawStatusLine(x, y, width, "World Pawns:", $"{Find.WorldPawns.AllPawnsAliveOrDead.Count}", Color.white);
                y = DrawStatusLine(x, y, width, "Pool Free Items:", $"{SimplePool<StackTraceLogItemRaw>.FreeItemsCount}", Color.white);
                
                // Calculated server performance
                var calcStpt = TickPatch.tickUntil - TickPatch.Timer <= 3 ? TickPatch.serverTimePerTick * 1.2f :
                    TickPatch.tickUntil - TickPatch.Timer >= 7 ? TickPatch.serverTimePerTick * 0.8f :
                    TickPatch.serverTimePerTick;
                y = DrawStatusLine(x, y, width, "Calc Server TPT:", $"{calcStpt:F1}ms", Color.white);
                y += SectionSpacing;
                
                // ===== MAP MANAGEMENT DATA =====
                y = DrawSectionHeader(x, y, width, "MAP MANAGEMENT");
                y = DrawStatusLine(x, y, width, "Haul Destinations:", $"{Find.CurrentMap.haulDestinationManager.AllHaulDestinationsListForReading.Count}", Color.white);
                y = DrawStatusLine(x, y, width, "Designations:", $"{Find.CurrentMap.designationManager.designationsByDef.Count}", Color.white);
                y = DrawStatusLine(x, y, width, "Haulable Items:", $"{Find.CurrentMap.listerHaulables.ThingsPotentiallyNeedingHauling().Count}", Color.white);
                y = DrawStatusLine(x, y, width, "Mining Designations:", $"{Find.CurrentMap.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine).Count()}", Color.white);
                y = DrawStatusLine(x, y, width, "First Ideology ID:", $"{Find.IdeoManager.IdeosInViewOrder.FirstOrDefault()?.id}", Color.white);
                
                // Faction-specific data (if available)
                if (Find.CurrentMap.ParentFaction != null)
                {
                    int faction = Find.CurrentMap.ParentFaction.loadID;
                    MultiplayerMapComp comp = Find.CurrentMap.MpComp();
                    FactionMapData data = comp.factionData.TryGetValue(faction);
                    
                    if (data != null)
                    {
                        y = DrawStatusLine(x, y, width, "Faction Haulables:", $"{data.listerHaulables.ThingsPotentiallyNeedingHauling().Count}", Color.white);
                        y = DrawStatusLine(x, y, width, "Faction Haul Groups:", $"{data.haulDestinationManager.AllGroupsListForReading.Count}", Color.white);
                    }
                }
            }
            
            return y - startY;
        }
        
        // Helper methods for UI drawing
        
        private static void DrawStatusIcon(Rect rect, string icon, Color color, string tooltip)
        {
            try
            {
                // Draw background for debugging
                Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
                
                using (MpStyle.Set(color).Set(TextAnchor.MiddleCenter).Set(GameFont.Small))
                {
                    Widgets.Label(rect, icon);
                }
                
                if (!string.IsNullOrEmpty(tooltip))
                {
                    TooltipHandler.TipRegion(rect, tooltip);
                }
            }
            catch (Exception ex)
            {
                // Fallback: draw a simple colored rectangle
                Widgets.DrawBoxSolid(rect, color);
                Log.Warning($"DrawStatusIcon error: {ex.Message}");
            }
        }
        
        private static float DrawSectionHeader(float x, float y, float width, string title)
        {
            Rect headerRect = new Rect(x, y, width, LineHeight + 2f);
            using (MpStyle.Set(GameFont.Small).Set(Color.cyan).Set(TextAnchor.MiddleLeft))
            {
                Widgets.Label(headerRect, $"── {title} ──");
            }
            return y + LineHeight + 6f; // More spacing after headers
        }
        
        private static float DrawStatusLine(float x, float y, float width, string label, string value, Color valueColor)
        {
            // Use consistent column widths and add padding
            float labelWidth = width * LabelColumnWidth;
            float valueWidth = width * (1f - LabelColumnWidth);
            
            // Label with right padding
            using (MpStyle.Set(GameFont.Tiny).Set(Color.white).Set(TextAnchor.MiddleLeft))
            {
                Rect labelRect = new Rect(x, y, labelWidth - 4f, LineHeight);
                Widgets.Label(labelRect, label);
            }
            
            // Value with left padding
            using (MpStyle.Set(GameFont.Tiny).Set(valueColor).Set(TextAnchor.MiddleLeft))
            {
                Rect valueRect = new Rect(x + labelWidth + 4f, y, valueWidth - 4f, LineHeight);
                Widgets.Label(valueRect, value);
            }
            
            return y + LineHeight + 1f; // Small padding between lines
        }
        
        // Status calculation methods
        
        private static (string icon, Color color, string text, string tooltip) GetSyncStatus()
        {
            try
            {
                // Check if there's an active multiplayer session and desync detection
                if (Multiplayer.session != null)
                {
                    bool hasDesynced = Multiplayer.session.players.Any(p => p.status == PlayerStatus.Desynced);
                    
                    if (hasDesynced)
                        return ("●", Color.red, "DESYNC", "Players have desynced!");
                    
                    // Check if we're in a valid sync state
                    if (Multiplayer.session.desynced)
                        return ("●", Color.red, "DESYNC", "Session has desynced");
                    
                    return ("●", Color.green, "SYNC", "All players are synchronized");
                }
                
                return ("●", Color.yellow, "N/A", "No active session");
            }
            catch (Exception ex)
            {
                Log.Warning($"GetSyncStatus error: {ex.Message}");
                return ("?", Color.gray, "ERR", "Error getting sync status");
            }
        }
        
        private static (string icon, Color color, string text, string tooltip) GetPerformanceStatus()
        {
            try
            {
                float tps = IngameUIPatch.tps;
                
                if (tps > 40f)
                    return ("▲", Color.green, $"{tps:F1}", "Performance is good");
                else if (tps > 20f)
                    return ("▲", Color.yellow, $"{tps:F1}", "Performance is moderate");
                else
                    return ("▲", Color.red, $"{tps:F1}", "Performance is poor");
            }
            catch (Exception ex)
            {
                Log.Warning($"GetPerformanceStatus error: {ex.Message}");
                return ("?", Color.gray, "ERR", "Error getting performance status");
            }
        }
        
        private static (string icon, Color color, string text, string tooltip) GetErrorStatus()
        {
            try
            {
                // For now, return a placeholder - we can add a more useful metric here later
                return ("■", Color.gray, "N/A", "No critical metric");
            }
            catch (Exception ex)
            {
                Log.Warning($"GetErrorStatus error: {ex.Message}");
                return ("?", Color.gray, "ERR", "Error getting status");
            }
        }
        
        private static (string icon, Color color, string text, string tooltip) GetTickStatus()
        {
            try
            {
                int behind = TickPatch.tickUntil - TickPatch.Timer;
                
                if (behind <= 5)
                    return ("♦", Color.green, $"{behind}", "Timing is good");
                else if (behind <= 15)
                    return ("♦", Color.yellow, $"{behind}", "Slightly behind");
                else
                    return ("♦", Color.red, $"{behind}", "Significantly behind");
            }
            catch (Exception ex)
            {
                Log.Warning($"GetTickStatus error: {ex.Message}");
                return ("?", Color.gray, "ERR", "Error getting tick status");
            }
        }
        
        // Utility methods
        
        private static float CalculateExpandedHeight()
        {
            return Math.Min(ExpandedMaxHeight, GetContentHeight() + 50f);
        }
        
        private static float GetContentHeight()
        {
            // Calculate actual content height dynamically by counting lines
            float height = 0f;
            
            // Status summary section
            height += LineHeight + 6f; // Header
            height += 4 * (LineHeight + 1f); // 4 status lines
            height += SectionSpacing;
            
            // RNG states section  
            height += LineHeight + 6f; // Header
            if (Find.CurrentMap?.AsyncTime() != null)
            {
                // Base RNG lines: Current Map, World, Round Mode
                height += 3 * (LineHeight + 1f);
            }
            else
            {
                height += 1 * (LineHeight + 1f); // 1 "No current map" line
            }
            height += SectionSpacing;
            
            // Performance section
            height += LineHeight + 6f; // Header
            height += 4 * (LineHeight + 1f); // 4 performance lines
            height += SectionSpacing;
            
            // Network & sync section
            height += LineHeight + 6f; // Header
            if (Multiplayer.session != null)
            {
                height += 5 * (LineHeight + 1f); // 5 network lines
            }
            else
            {
                height += 1 * (LineHeight + 1f); // 1 "No active session" line
            }
            height += SectionSpacing;
            
            // Detailed debug section (organized into 6 subsections)
            if (Multiplayer.ShowDevInfo && Find.CurrentMap != null)
            {
                // Core System: 7 lines + header
                height += LineHeight + 6f + 7 * (LineHeight + 1f) + SectionSpacing;
                // Timing & Sync: 5 lines + header  
                height += LineHeight + 6f + 5 * (LineHeight + 1f) + SectionSpacing;
                // Game State: 5 lines + header
                height += LineHeight + 6f + 5 * (LineHeight + 1f) + SectionSpacing;
                // Map Management: 5-7 lines + header (including faction data) - moved to bottom
                height += LineHeight + 6f + 7 * (LineHeight + 1f);
                // RNG & Debug: 4 lines + header
                height += LineHeight + 6f + 4 * (LineHeight + 1f) + SectionSpacing;
                // Command & Sync: 5 lines + header
                height += LineHeight + 6f + 5 * (LineHeight + 1f) + SectionSpacing;
                // Memory & Performance: 3 lines + header
                height += LineHeight + 6f + 3 * (LineHeight + 1f);
            }
            else
            {
                height += 1 * (LineHeight + 1f); // Minimal fallback
            }
            
            // Small bottom padding to prevent cutoff
            height += 20f;
            
            return height;
        }
    }
} 
