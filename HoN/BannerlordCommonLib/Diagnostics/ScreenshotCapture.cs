using System;
using System.IO;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace BannerlordCommonLib.Diagnostics
{
    /// <summary>
    /// Captures screenshots for error reporting.
    /// </summary>
    public static class ScreenshotCapture
    {
        private static string _captureFolder;
        
        public static void Initialize(string modFolder)
        {
            _captureFolder = Path.Combine(BasePath.Name, "Modules", modFolder, "Screenshots");
            Directory.CreateDirectory(_captureFolder);
        }
        
        /// <summary>
        /// Captures screenshot and returns path.
        /// </summary>
        public static string Capture(string prefix = "error")
        {
            try
            {
                var filename = $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
                var path = Path.Combine(_captureFolder, filename);
                
                // Use game's built-in screenshot capability
                Utilities.TakeScreenshot(path);
                
                return File.Exists(path) ? path : null;
            }
            catch { return null; }
        }
        
        /// <summary>
        /// Captures screenshot with user description for error report.
        /// </summary>
        public static ErrorScreenshot CaptureWithDescription(string description, string category = "General")
        {
            var screenshotPath = Capture(category.ToLower().Replace(" ", "_"));
            
            return new ErrorScreenshot
            {
                Timestamp = DateTime.UtcNow,
                ScreenshotPath = screenshotPath,
                Description = description,
                Category = category,
                CurrentScreen = GetCurrentScreen(),
                CurrentMission = GetCurrentMission()
            };
        }
        
        private static string GetCurrentScreen()
        {
            try { return TaleWorlds.ScreenSystem.ScreenManager.TopScreen?.GetType().Name; }
            catch { return "Unknown"; }
        }
        
        private static string GetCurrentMission()
        {
            try { return TaleWorlds.MountAndBlade.Mission.Current?.SceneName; }
            catch { return null; }
        }
    }
    
    public class ErrorScreenshot
    {
        public DateTime Timestamp { get; set; }
        public string ScreenshotPath { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string CurrentScreen { get; set; }
        public string CurrentMission { get; set; }
    }
}
