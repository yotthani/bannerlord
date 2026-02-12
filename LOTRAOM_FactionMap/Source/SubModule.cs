using System;
using System.IO;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace LOTRAOM.FactionMap
{
    public class SubModule : MBSubModuleBase
    {
        public static SubModule Instance { get; private set; }
        public static string ModulePath { get; private set; }

        private static string _logFilePath;
        private Harmony _harmony;

        internal static void Log(string msg)
        {
            FileLog(msg);
        }

        internal static void LogError(string msg)
        {
            FileLog("ERROR: " + msg);
        }

        internal static void FileLog(string msg)
        {
            try
            {
                if (_logFilePath == null && ModulePath != null)
                    _logFilePath = System.IO.Path.Combine(ModulePath, "factionmap_debug.log");
                if (_logFilePath != null)
                    System.IO.File.AppendAllText(_logFilePath,
                        $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
            }
            catch { /* ignore file write errors */ }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Instance = this;
            // Use Utilities.GetBasePath() for absolute path (needed for LoadTextureFromPath)
            string basePath = Utilities.GetBasePath();
            ModulePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, "Modules", "LOTRAOM.FactionMap"));

            // Initialize log file (clear previous)
            _logFilePath = System.IO.Path.Combine(ModulePath, "factionmap_debug.log");
            try { System.IO.File.WriteAllText(_logFilePath, $"=== FactionMap Debug Log — {DateTime.Now} ===\n"); }
            catch { }
            FileLog($"ModulePath: {ModulePath}");

            try
            {
                _harmony = new Harmony("com.lotraom.factionmap");
                _harmony.PatchAll();
                FileLog("Harmony patches applied OK");
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(ModulePath, "harmony_error.txt"),
                    $"Harmony patch error: {ex}");
                FileLog($"Harmony patch FAILED: {ex.Message}");
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Log("Faction Map Selection loaded!");
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll("com.lotraom.factionmap");
        }
    }
}
