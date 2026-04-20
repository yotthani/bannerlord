using System;
using TaleWorlds.Library;

namespace FiefManagement
{
    internal static class FiefManagementLog
    {
        public static void Debug(string message)
        {
            try
            {
                if (FiefManagementSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[FiefManagement] {message}", Colors.Gray));
            }
            catch { }
        }

        public static void Debug(string feature, string message)
        {
            try
            {
                if (FiefManagementSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[{feature}] {message}", Colors.Gray));
            }
            catch { }
        }

        public static void Error(string feature, string operation, Exception ex)
        {
            try
            {
                if (FiefManagementSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[{feature}] {operation} error: {ex.Message}", Colors.Red));
            }
            catch { }
        }
    }

    internal static class SafeExec
    {
        public static bool Execute(string feature, string op, Action action)
        {
            try { action?.Invoke(); return true; }
            catch (Exception ex) { FiefManagementLog.Error(feature, op, ex); return false; }
        }

        public static T Execute<T>(string feature, string op, Func<T> func, T defaultValue = default)
        {
            try { return func != null ? func() : defaultValue; }
            catch (Exception ex) { FiefManagementLog.Error(feature, op, ex); return defaultValue; }
        }
    }
}
