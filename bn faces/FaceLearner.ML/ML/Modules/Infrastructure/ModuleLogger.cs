using System;

namespace FaceLearner.ML.Modules.Infrastructure
{
    /// <summary>
    /// Centralized logging for all modules.
    /// Routes to SubModule.Log when available, falls back to console.
    /// </summary>
    public static class ModuleLogger
    {
        private static Action<string> _logAction;
        
        /// <summary>
        /// Initialize the logger with a custom log action.
        /// Call this from SubModule.OnSubModuleLoad()
        /// </summary>
        public static void Initialize(Action<string> logAction)
        {
            _logAction = logAction;
        }
        
        /// <summary>
        /// Log a message
        /// </summary>
        public static void Log(string message)
        {
            if (_logAction != null)
            {
                _logAction(message);
            }
            else
            {
                // Fallback to console
                Console.WriteLine($"[FaceLearner] {message}");
            }
        }
        
        /// <summary>
        /// Log with module prefix
        /// </summary>
        public static void Log(string module, string message)
        {
            Log($"{module}: {message}");
        }
    }
}

namespace FaceLearner.ML.Modules
{
    // Extension to make SubModule.Log accessible from modules
    // This is a workaround until we fully migrate to ModuleLogger
    internal static class SubModule
    {
        public static void Log(string message)
        {
            Infrastructure.ModuleLogger.Log(message);
        }
    }
}
