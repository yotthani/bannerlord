using System;
using System.IO;
using System.Reflection;

namespace DualWieldPrototype
{
    internal static class DualWieldPrototypeLogger
    {
        private static readonly object Sync = new object();

        public static void Log(string message)
        {
            if (!DualWieldPrototypeSettings.Get().DebugFileLogging)
            {
                return;
            }

            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string moduleBin = Path.GetDirectoryName(assemblyPath) ?? string.Empty;
                string moduleRoot = Directory.GetParent(moduleBin)?.Parent?.FullName ?? moduleBin;
                string logPath = Path.Combine(moduleRoot, "dualwieldprototype.log");
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";

                lock (Sync)
                {
                    File.AppendAllText(logPath, line);
                }
            }
            catch
            {
            }
        }
    }
}
