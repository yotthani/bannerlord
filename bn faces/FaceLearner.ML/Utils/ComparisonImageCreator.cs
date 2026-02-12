using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using FaceLearner.ML;

namespace FaceLearner.ML.Utils
{
    /// <summary>
    /// Creates side-by-side comparison images of target photos and Bannerlord renders
    /// </summary>
    public static class ComparisonImageCreator
    {
        private static string _comparisonsFolder;
        private static bool _initialized = false;
        
        public static void Initialize(string basePath)
        {
            _comparisonsFolder = Path.Combine(basePath, "Comparisons");
            if (!Directory.Exists(_comparisonsFolder))
            {
                Directory.CreateDirectory(_comparisonsFolder);
                SubModule.Log($"ComparisonImageCreator: Created folder: {_comparisonsFolder}");
            }
            else
            {
                SubModule.Log($"ComparisonImageCreator: Using folder: {_comparisonsFolder}");
            }
            _initialized = true;
        }
        
        /// <summary>
        /// Create a side-by-side comparison image
        /// </summary>
        /// <param name="targetImagePath">Path to the original target photo</param>
        /// <param name="renderImagePath">Path to the Bannerlord render screenshot</param>
        /// <param name="score">Final score achieved</param>
        /// <param name="targetId">ID of the target for filename</param>
        /// <param name="info">Additional info (age, gender, etc)</param>
        public static void CreateComparison(string targetImagePath, string renderImagePath, 
            float score, string targetId, string info = "")
        {
            if (!_initialized) 
            {
                SubModule.Log("ComparisonImageCreator: Not initialized!");
                return;
            }
            
            try
            {
                if (string.IsNullOrEmpty(targetImagePath) || !File.Exists(targetImagePath))
                {
                    SubModule.Log($"ComparisonImageCreator: Target image not found: {targetImagePath}");
                    return;
                }
                if (string.IsNullOrEmpty(renderImagePath) || !File.Exists(renderImagePath))
                {
                    SubModule.Log($"ComparisonImageCreator: Render image not found: {renderImagePath}");
                    return;
                }
                
                using (var targetImg = LoadAndResizeImage(targetImagePath, 400, 400, isRender: false))
                using (var renderImg = LoadAndResizeImage(renderImagePath, 400, 400, isRender: true))
                {
                    if (targetImg == null || renderImg == null) 
                    {
                        SubModule.Log($"ComparisonImageCreator: Failed to load images");
                        return;
                    }
                    
                    // Create composite: [Target] | [Render] with header
                    int width = 820;  // 400 + 20 gap + 400
                    int height = 480; // 400 + 80 header
                    
                    using (var composite = new Bitmap(width, height))
                    using (var g = Graphics.FromImage(composite))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        
                        // Background
                        g.Clear(Color.FromArgb(26, 21, 18)); // Dark brown like Bannerlord UI
                        
                        // Header text
                        using (var headerFont = new Font("Segoe UI", 14, FontStyle.Bold))
                        using (var subFont = new Font("Segoe UI", 10))
                        using (var brush = new SolidBrush(Color.FromArgb(220, 200, 170)))
                        {
                            string scoreText = $"Score: {score:F3}";
                            string headerText = $"Target: {Path.GetFileNameWithoutExtension(targetId)}";
                            
                            g.DrawString(headerText, headerFont, brush, 10, 10);
                            g.DrawString(scoreText, headerFont, brush, width - 150, 10);
                            
                            if (!string.IsNullOrEmpty(info))
                                g.DrawString(info, subFont, brush, 10, 35);
                            
                            // Labels
                            g.DrawString("Original", subFont, brush, 180, 55);
                            g.DrawString("Bannerlord", subFont, brush, 580, 55);
                        }
                        
                        // Draw images
                        g.DrawImage(targetImg, 10, 75, 390, 390);
                        g.DrawImage(renderImg, 420, 75, 390, 390);
                        
                        // Border around images
                        using (var pen = new Pen(Color.FromArgb(100, 80, 60), 2))
                        {
                            g.DrawRectangle(pen, 10, 75, 390, 390);
                            g.DrawRectangle(pen, 420, 75, 390, 390);
                        }
                        
                        // Save
                        string filename = $"{score:F3}_{SanitizeFilename(targetId)}.png";
                        string outputPath = Path.Combine(_comparisonsFolder, filename);
                        composite.Save(outputPath, ImageFormat.Png);
                        SubModule.Log($"ComparisonImageCreator: Saved to {outputPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"ComparisonImageCreator ERROR: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Also save the "current" comparison that gets overwritten each time
        /// for quick viewing during learning
        /// </summary>
        public static void UpdateCurrentComparison(string targetImagePath, string renderImagePath,
            float score, string info = "")
        {
            if (!_initialized) return;
            
            try
            {
                if (string.IsNullOrEmpty(targetImagePath) || !File.Exists(targetImagePath))
                    return;
                if (string.IsNullOrEmpty(renderImagePath) || !File.Exists(renderImagePath))
                    return;
                
                using (var targetImg = LoadAndResizeImage(targetImagePath, 300, 300, isRender: false))
                using (var renderImg = LoadAndResizeImage(renderImagePath, 300, 300, isRender: true))
                {
                    if (targetImg == null || renderImg == null) return;
                    
                    int width = 620;
                    int height = 360;
                    
                    using (var composite = new Bitmap(width, height))
                    using (var g = Graphics.FromImage(composite))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        
                        g.Clear(Color.FromArgb(26, 21, 18));
                        
                        using (var font = new Font("Segoe UI", 12, FontStyle.Bold))
                        using (var brush = new SolidBrush(Color.FromArgb(220, 200, 170)))
                        {
                            g.DrawString($"Score: {score:F3}  {info}", font, brush, 10, 8);
                            g.DrawString("Target", font, brush, 130, 32);
                            g.DrawString("Current Best", font, brush, 420, 32);
                        }
                        
                        g.DrawImage(targetImg, 10, 55, 295, 295);
                        g.DrawImage(renderImg, 315, 55, 295, 295);
                        
                        string outputPath = Path.Combine(_comparisonsFolder, "_CURRENT.png");
                        composite.Save(outputPath, ImageFormat.Png);
                    }
                }
            }
            catch { }
        }
        
        private static Bitmap LoadAndResizeImage(string path, int maxWidth, int maxHeight, bool isRender = false)
        {
            // Note: isRender parameter kept for API compatibility but no longer used
            // BGR correction was removed as Bannerlord renders are now correctly saved as RGB
            if (!File.Exists(path)) return null;
            
            try
            {
                using (var original = new Bitmap(path))
                {
                    // Calculate scale to fit
                    float scale = Math.Min((float)maxWidth / original.Width, 
                                          (float)maxHeight / original.Height);
                    
                    int newWidth = (int)(original.Width * scale);
                    int newHeight = (int)(original.Height * scale);
                    
                    var resized = new Bitmap(maxWidth, maxHeight);
                    using (var g = Graphics.FromImage(resized))
                    {
                        g.Clear(Color.FromArgb(40, 35, 30));
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        
                        // Center the image
                        int x = (maxWidth - newWidth) / 2;
                        int y = (maxHeight - newHeight) / 2;
                        
                        // Draw the image centered
                        g.DrawImage(original, x, y, newWidth, newHeight);
                    }
                    return resized;
                }
            }
            catch
            {
                return null;
            }
        }
        
        private static string SanitizeFilename(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 50 ? name.Substring(0, 50) : name;
        }
        
        /// <summary>
        /// Clean up old comparison images to save disk space
        /// Keeps only the most recent N images
        /// </summary>
        public static int CleanupOldComparisons(int keepCount = 100)
        {
            if (!_initialized || string.IsNullOrEmpty(_comparisonsFolder))
                return 0;
            
            try
            {
                var dir = new DirectoryInfo(_comparisonsFolder);
                if (!dir.Exists) return 0;
                
                var files = dir.GetFiles("*.png")
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(keepCount)
                    .ToList();
                
                int deleted = 0;
                foreach (var file in files)
                {
                    try
                    {
                        file.Delete();
                        deleted++;
                    }
                    catch { }
                }
                
                if (deleted > 0)
                {
                    SubModule.Log($"  Cleanup: Deleted {deleted} old comparison images");
                }
                
                return deleted;
            }
            catch (Exception ex)
            {
                SubModule.Log($"  Cleanup error: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Clean up temp folder
        /// </summary>
        public static int CleanupTempFolder(string basePath)
        {
            try
            {
                string tempFolder = Path.Combine(basePath, "Temp");
                if (!Directory.Exists(tempFolder)) return 0;
                
                var dir = new DirectoryInfo(tempFolder);
                var files = dir.GetFiles()
                    .Where(f => f.CreationTime < DateTime.Now.AddHours(-1))  // Keep recent files
                    .ToList();
                
                int deleted = 0;
                foreach (var file in files)
                {
                    try
                    {
                        file.Delete();
                        deleted++;
                    }
                    catch { }
                }
                
                if (deleted > 0)
                {
                    SubModule.Log($"  Cleanup: Deleted {deleted} old temp files");
                }
                
                return deleted;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Get current size of comparisons folder in MB
        /// </summary>
        public static float GetComparisonsFolderSizeMB()
        {
            if (!_initialized || string.IsNullOrEmpty(_comparisonsFolder))
                return 0;
            
            try
            {
                var dir = new DirectoryInfo(_comparisonsFolder);
                if (!dir.Exists) return 0;
                
                long totalBytes = dir.GetFiles().Sum(f => f.Length);
                return totalBytes / (1024f * 1024f);
            }
            catch
            {
                return 0;
            }
        }
    }
}
