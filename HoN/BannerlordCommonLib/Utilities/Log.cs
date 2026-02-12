using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BannerlordCommonLib.Utilities
{
    /// <summary>
    /// Simple logging utility for Bannerlord mods.
    /// Displays messages in the game's bottom-left message area.
    /// </summary>
    public static class Log
    {
        /// <summary>Standard info message (white).</summary>
        public static void Info(string prefix, string message) =>
            Display(prefix, message, Colors.White);
        
        /// <summary>Success message (green).</summary>
        public static void Success(string prefix, string message) =>
            Display(prefix, message, Colors.Green);
        
        /// <summary>Warning message (yellow).</summary>
        public static void Warning(string prefix, string message) =>
            Display(prefix, message, Colors.Yellow);
        
        /// <summary>Error message (red).</summary>
        public static void Error(string prefix, string message) =>
            Display(prefix, message, Colors.Red);
        
        /// <summary>Debug message (cyan) - only shows if debug mode.</summary>
        public static void Debug(string prefix, string message, bool debugEnabled = true)
        {
            if (debugEnabled)
                Display(prefix, message, Colors.Cyan);
        }
        
        private static void Display(string prefix, string message, Color color)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"[{prefix}] {message}", color));
        }
    }
}
