using System;
using TaleWorlds.Library;

namespace BattleActionBar
{
    internal static class BattleActionBarLog
    {
        public static void Debug(string msg)
        {
            try
            {
                if (BattleActionBarSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[BattleActionBar] {msg}", Colors.Cyan));
            }
            catch { }
        }

        public static void Error(string op, Exception ex)
        {
            try
            {
                if (BattleActionBarSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[BattleActionBar] {op} error: {ex.Message}", Colors.Red));
            }
            catch { }
        }
    }
}
