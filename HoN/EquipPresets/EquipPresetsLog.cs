using System;
using TaleWorlds.Library;

namespace EquipPresets
{
    /// <summary>
    /// Replaces HoN's ModSettings.DebugLog/ErrorLog and SafeExec.Execute.
    /// Provides the same surface area used in the HoN code, namespace-local.
    /// </summary>
    internal static class EquipPresetsLog
    {
        public static void Debug(string message)
        {
            try
            {
                if (EquipPresetsSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[EquipPresets] {message}", Colors.Gray));
            }
            catch { }
        }

        public static void Debug(string feature, string message)
        {
            try
            {
                if (EquipPresetsSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[{feature}] {message}", Colors.Gray));
            }
            catch { }
        }

        public static void Error(string feature, string operation, Exception ex)
        {
            try
            {
                if (EquipPresetsSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[{feature}] {operation} error: {ex.Message}", Colors.Red));
            }
            catch { }
        }
    }

    /// <summary>Drop-in replacement for HoN's SafeExecutor static methods.</summary>
    internal static class SafeExec
    {
        public static bool Execute(string feature, string op, Action action)
        {
            try { action?.Invoke(); return true; }
            catch (Exception ex) { EquipPresetsLog.Error(feature, op, ex); return false; }
        }

        public static T Execute<T>(string feature, string op, Func<T> func, T defaultValue = default)
        {
            try { return func != null ? func() : defaultValue; }
            catch (Exception ex) { EquipPresetsLog.Error(feature, op, ex); return defaultValue; }
        }
    }
}
