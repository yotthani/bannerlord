using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BannerlordCommonLib.Diagnostics
{
    /// <summary>
    /// Captures exceptions and misbehaviors for analysis.
    /// </summary>
    public static class ErrorCapture
    {
        private static List<CapturedError> _errors = new();
        private static List<ErrorScreenshot> _screenshots = new();
        private static bool _initialized;
        private static string _sessionId;
        
        public static IReadOnlyList<CapturedError> Errors => _errors;
        public static IReadOnlyList<ErrorScreenshot> Screenshots => _screenshots;
        
        public static void Initialize()
        {
            if (_initialized) return;
            _sessionId = Guid.NewGuid().ToString("N")[..8];
            AppDomain.CurrentDomain.UnhandledException += (s, e) => 
                Capture(e.ExceptionObject as Exception, "Unhandled", ErrorSeverity.Critical);
            _initialized = true;
        }
        
        public static void Capture(Exception ex, string source, ErrorSeverity severity = ErrorSeverity.Error)
        {
            if (ex == null) return;
            lock (_errors)
            {
                _errors.Add(new CapturedError
                {
                    Timestamp = DateTime.UtcNow,
                    SessionId = _sessionId,
                    Severity = severity,
                    Source = source,
                    ExceptionType = ex.GetType().Name,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }
        
        public static void LogMisbehavior(string category, string message)
        {
            lock (_errors)
            {
                _errors.Add(new CapturedError
                {
                    Timestamp = DateTime.UtcNow,
                    SessionId = _sessionId,
                    Severity = ErrorSeverity.Misbehavior,
                    Source = category,
                    Message = message
                });
            }
        }
        
        /// <summary>
        /// Add screenshot with description to error flow.
        /// </summary>
        public static void AddScreenshot(string description, string category = "Visual")
        {
            var screenshot = ScreenshotCapture.CaptureWithDescription(description, category);
            if (screenshot != null)
            {
                lock (_screenshots) { _screenshots.Add(screenshot); }
            }
        }
        
        public static string Export(string folder)
        {
            Directory.CreateDirectory(folder);
            var data = new { SessionId = _sessionId, Errors = _errors, Screenshots = _screenshots };
            var path = Path.Combine(folder, $"errors_{_sessionId}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            return path;
        }
        
        public static void Clear() { _errors.Clear(); _screenshots.Clear(); }
    }
    
    public class CapturedError
    {
        public DateTime Timestamp { get; set; }
        public string SessionId { get; set; }
        public ErrorSeverity Severity { get; set; }
        public string Source { get; set; }
        public string ExceptionType { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
    }
    
    public enum ErrorSeverity { Info, Warning, Error, Critical, Misbehavior, Performance }
}
