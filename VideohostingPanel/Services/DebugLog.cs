using System;
using System.IO;

namespace VideohostingPanel.Services
{
    internal static class DebugLog
    {
        private static readonly object Sync = new();

        private static readonly string LogPath =
            Path.Combine(AppContext.BaseDirectory, "debug.log");

        public static void Write(string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

            System.Diagnostics.Debug.WriteLine(line);

            try
            {
                lock (Sync)
                    File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
