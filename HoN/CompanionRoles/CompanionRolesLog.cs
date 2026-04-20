using System;
using TaleWorlds.Library;

namespace CompanionRoles
{
    /// <summary>
    /// Module-local logging helpers (replaces HoN's ModSettings.DebugLog/ErrorLog).
    /// All output is silent unless DebugMode is enabled in CompanionRolesSettings.
    /// </summary>
    internal static class CompanionRolesLog
    {
        public static void Debug(string message)
        {
            try
            {
                if (CompanionRolesSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[CompanionRoles] {message}", Colors.Gray));
            }
            catch { }
        }

        public static void Error(string operation, Exception ex)
        {
            try
            {
                if (CompanionRolesSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[CompanionRoles] {operation} error: {ex.Message}", Colors.Red));
            }
            catch { }
        }
    }
}
