using System;
using TaleWorlds.Library;

namespace TransferbuttonMenu
{
    internal static class TransferbuttonMenuLog
    {
        public static void Debug(string feature, string msg)
        {
            try
            {
                if (TransferbuttonMenuSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[{feature}] {msg}", Colors.Cyan));
            }
            catch { }
        }

        public static void Error(string feature, string op, Exception ex)
        {
            try
            {
                if (TransferbuttonMenuSettings.Get().DebugMode)
                    InformationManager.DisplayMessage(new InformationMessage($"[{feature}] {op} error: {ex.Message}", Colors.Red));
            }
            catch { }
        }
    }
}
