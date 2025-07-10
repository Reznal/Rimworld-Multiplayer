using System;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using Multiplayer.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Client.Util;

public class SaveableMpLogs
{
    private const int MaxFiles = 10;
    private const string FilePrefix = "MpLog-";
    private const string FileExtension = ".log";

    private static string _currentLogFile = null;

    public static void InitMpLogs()
    {
        _currentLogFile = FindFileNameForNextFile();

        try
        {
            using var stream = File.Open(_currentLogFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.WriteLine(GetLogDetails());
        }
        catch (Exception e)
        {
            Log.Error($"Exception writing initial log info: {e}");
        }
    }

    public static void ResetMpLogs() => _currentLogFile = null;

    public static void AddLog(string logText)
    {
        if (Multiplayer.Client == null)
            return;

        if (_currentLogFile == null)
            InitMpLogs();

        int ticks = Find.TickManager.ticksGameInt;
        int mapTicks = Find.CurrentMap?.AsyncTime()?.mapTicks ?? -1;

        try
        {
            using var stream = File.Open(_currentLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.WriteLine($"[{ticks}] [{mapTicks}] {logText}");
        }
        catch (Exception e)
        {
            Log.Error($"Exception writing log info: {e}");
        }
    }

    private static string GetLogDetails()
    {
        var logDetails = new StringBuilder()
            .AppendLine($"Multiplayer Log - {DateTime.Now}")
            .AppendLine("\n###Version Data###")
            .AppendLine($"Multiplayer Mod Version|||{MpVersion.Version}")
            .AppendLine($"Rimworld Version and Rev|||{VersionControl.CurrentVersionStringWithRev}")
            .AppendLine("\n###Debug Options###")
            .AppendLine($"Multiplayer Debug Build - Client|||{MpVersion.IsDebug}")
            .AppendLine($"Multiplayer Debug Mode - Host|||{Multiplayer.GameComp.debugMode}")
            .AppendLine($"Rimworld Developer Mode - Client|||{Prefs.DevMode}")
            .AppendLine("\n###Server Info###")
            .AppendLine($"Async time active|||{Multiplayer.GameComp.asyncTime}")
            .AppendLine($"Multifaction active|||{Multiplayer.GameComp.multifaction}")
            .AppendLine("\n###OS Info###")
            .AppendLine($"OS Type|||{SystemInfo.operatingSystemFamily}")
            .AppendLine($"OS Name and Version|||{SystemInfo.operatingSystem}")
            .AppendLine("\n======================================================")
            .AppendLine("###Log Start###")
            .AppendLine("======================================================");
        return logDetails.ToString();
    }

    private static string FindFileNameForNextFile()
    {
        // Get player directory
        string directory = Path.Combine(Multiplayer.MpLogsDir);//, Multiplayer.username);

        // Ensure the directory exists
        Directory.CreateDirectory(directory);

        // Get all existing logs
        FileInfo[] files = new DirectoryInfo(directory).GetFiles($"{FilePrefix}*{FileExtension}");

        // Delete any pushing us over the limit, and reserve room for one more
        if (files.Length > MaxFiles - 1)
            files.OrderByDescending(f => f.LastWriteTime).Skip(MaxFiles - 1).Do(DeleteFileSilent);

        // Find the current max number
        int max = 0;
        foreach (FileInfo file in files)
        {
            // Get name without extension and prefix
            string parsedName = Path.GetFileNameWithoutExtension(file.Name)[FilePrefix.Length..];

            // Try to parse the number and update max if it's greater
            if (int.TryParse(parsedName, out int result) && result > max)
                max = result;
        }

        return Path.Combine(directory, $"{FilePrefix}{max + 1:00}{FileExtension}");
    }

    private static void DeleteFileSilent(FileInfo file)
    {
        try
        {
            file.Delete();
        }
        catch (IOException)
        {
        }
    }
}
