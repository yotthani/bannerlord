using System;
using System.IO;

namespace DualWield.Core
{
    /// <summary>
    /// Writes debug messages to a file (Documents/DualWield_debug.log).
    /// Unlike InformationManager.DisplayMessage, this persists after the game closes
    /// and doesn't disappear from the combat log after a few seconds.
    /// </summary>
    public static class DualWieldLog
    {
        private static readonly string LogPath;
        private static readonly object Lock = new object();

        static DualWieldLog()
        {
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                LogPath = Path.Combine(docs, "DualWield_debug.log");
            }
            catch
            {
                LogPath = null;
            }
        }

        /// <summary>
        /// Clears the log file. Call at session start.
        /// </summary>
        public static void Clear()
        {
            if (LogPath == null) return;
            try
            {
                lock (Lock)
                {
                    File.WriteAllText(LogPath, $"=== DualWield Debug Log — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                }
            }
            catch { }
        }

        /// <summary>
        /// Appends a timestamped message to the log file.
        /// </summary>
        public static void Log(string message)
        {
            if (LogPath == null) return;
            try
            {
                lock (Lock)
                {
                    File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch { }
        }
    }
}
