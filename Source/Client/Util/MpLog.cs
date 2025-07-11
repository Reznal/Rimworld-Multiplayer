using System.Diagnostics;

namespace Multiplayer.Client.Util
{
    public static class MpLog
    {
        public static void Log(string msg)
        {
            Verse.Log.Message($"{Multiplayer.username} {TickPatch.Timer} {msg}");
            //SaveableMpLogs.AddLog("LOG", msg);
        }

        public static void Warn(string msg)
        {
            Verse.Log.Warning($"{Multiplayer.username} {TickPatch.Timer} {msg}");
            //SaveableMpLogs.AddLog("WARN", msg);
        }

        public static void Error(string msg)
        {
            Verse.Log.Error($"{Multiplayer.username} {TickPatch.Timer} {msg}");
            //SaveableMpLogs.AddLog("ERROR", msg);
        }

        [Conditional("DEBUG")]
        public static void Debug(string msg)
        {
            Verse.Log.Message($"{Multiplayer.username} {TickPatch.Timer} {msg}");
            SaveableMpLogs.AddLog("DEBUG", msg);
        }
    }
}
