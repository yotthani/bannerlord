using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Checks image quality to skip extremely poor quality images.
    /// Uses Laplacian variance for blur detection.
    /// </summary>
    public static class ImageQualityChecker
    {
        // Thresholds calibrated for face learning
        // Lower = more blurry, Higher = sharper
        private const float MINIMUM_SHARPNESS = 50f;   // Below this = skip image
        private const float WARNING_SHARPNESS = 150f;  // Below this = log warning
        
        // Minimum resolution
        private const int MINIMUM_WIDTH = 64;
        private const int MINIMUM_HEIGHT = 64;
        
        /// <summary>
        /// Check if image quality is sufficient for learning
        /// </summary>
        public static ImageQualityResult CheckQuality(string imagePath)
        {
            var result = new ImageQualityResult { ImagePath = imagePath };
            
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                result.IsUsable = false;
                result.Reason = "File not found";
                return result;
            }
            
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    result.Width = bitmap.Width;
                    result.Height = bitmap.Height;
                    
                    // Check minimum resolution
                    if (bitmap.Width < MINIMUM_WIDTH || bitmap.Height < MINIMUM_HEIGHT)
                    {
                        result.IsUsable = false;
                        result.Reason = $"Too small ({bitmap.Width}x{bitmap.Height})";
                        return result;
                    }
                    
                    // Calculate sharpness using Laplacian variance
                    result.Sharpness = CalculateLaplacianVariance(bitmap);
                    
                    // Determine usability
                    if (result.Sharpness < MINIMUM_SHARPNESS)
                    {
                        result.IsUsable = false;
                        result.Reason = $"Too blurry (sharpness={result.Sharpness:F1})";
                    }
                    else if (result.Sharpness < WARNING_SHARPNESS)
                    {
                        result.IsUsable = true;
                        result.IsLowQuality = true;
                        result.Reason = $"Low quality (sharpness={result.Sharpness:F1})";
                    }
                    else
                    {
                        result.IsUsable = true;
                        result.IsLowQuality = false;
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsUsable = false;
                result.Reason = $"Error: {ex.Message}";
            }
            
            return result;
        }
        
        /// <summary>
        /// Calculate Laplacian variance - measure of image sharpness.
        /// Higher values = sharper image, Lower values = more blur.
        /// </summary>
        private static float CalculateLaplacianVariance(Bitmap bitmap)
        {
            // Resize to standard size for consistent measurement
            int targetSize = 128;
            using (var resized = new Bitmap(bitmap, targetSize, targetSize))
            {
                // Convert to grayscale and apply Laplacian kernel
                float[] laplacian = new float[targetSize * targetSize];
                
                // Lock bits for fast access
                BitmapData data = resized.LockBits(
                    new Rectangle(0, 0, targetSize, targetSize),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);
                
                try
                {
                    unsafe
                    {
                        byte* ptr = (byte*)data.Scan0;
                        int stride = data.Stride;
                        
                        // Laplacian kernel: [0, 1, 0]
                        //                   [1,-4, 1]
                        //                   [0, 1, 0]
                        for (int y = 1; y < targetSize - 1; y++)
                        {
                            for (int x = 1; x < targetSize - 1; x++)
                            {
                                // Get grayscale values for 3x3 neighborhood
                                float center = GetGray(ptr, stride, x, y);
                                float top = GetGray(ptr, stride, x, y - 1);
                                float bottom = GetGray(ptr, stride, x, y + 1);
                                float left = GetGray(ptr, stride, x - 1, y);
                                float right = GetGray(ptr, stride, x + 1, y);
                                
                                // Apply Laplacian
                                float lap = top + bottom + left + right - 4 * center;
                                laplacian[y * targetSize + x] = lap;
                            }
                        }
                    }
                }
                finally
                {
                    resized.UnlockBits(data);
                }
                
                // Calculate variance
                float sum = 0, sumSq = 0;
                int count = 0;
                
                for (int i = 0; i < laplacian.Length; i++)
                {
                    float v = laplacian[i];
                    if (v != 0)  // Skip border pixels
                    {
                        sum += v;
                        sumSq += v * v;
                        count++;
                    }
                }
                
                if (count == 0) return 0;
                
                float mean = sum / count;
                float variance = (sumSq / count) - (mean * mean);
                
                return variance;
            }
        }
        
        /// <summary>
        /// Get grayscale value at pixel position (unsafe, no bounds check)
        /// </summary>
        private static unsafe float GetGray(byte* ptr, int stride, int x, int y)
        {
            byte* pixel = ptr + y * stride + x * 3;
            return (pixel[0] * 0.114f + pixel[1] * 0.587f + pixel[2] * 0.299f);
        }
    }
    
    /// <summary>
    /// Result of image quality check
    /// </summary>
    public class ImageQualityResult
    {
        public string ImagePath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float Sharpness { get; set; }
        public bool IsUsable { get; set; }
        public bool IsLowQuality { get; set; }
        public string Reason { get; set; }
        
        public override string ToString()
        {
            if (!IsUsable)
                return $"SKIP: {Reason}";
            if (IsLowQuality)
                return $"LOW: {Reason}";
            return $"OK (sharpness={Sharpness:F1})";
        }
    }
}
