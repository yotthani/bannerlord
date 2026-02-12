using System;
using System.Drawing;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.FaceParsing;

namespace FaceLearner.ML.Modules.Demographics.SkinTone
{
    /// <summary>
    /// Skin tone analysis result using ITA (Individual Typology Angle)
    /// </summary>
    public class SkinToneResult : IDetectionResult
    {
        /// <summary>ITA angle in degrees (-90 to +90)</summary>
        public float ItaAngle { get; set; }
        
        /// <summary>Fitzpatrick skin type (I-VI)</summary>
        public int FitzpatrickType { get; set; }
        
        /// <summary>Normalized skin tone (0=very dark, 1=very light)</summary>
        public float NormalizedTone { get; set; }
        
        /// <summary>Detected skin type category</summary>
        public SkinType SkinType { get; set; }
        
        /// <summary>Average skin L* value (CIELAB lightness)</summary>
        public float Lightness { get; set; }
        
        /// <summary>Average skin a* value (CIELAB red-green)</summary>
        public float RedGreen { get; set; }
        
        /// <summary>Average skin b* value (CIELAB yellow-blue)</summary>
        public float YellowBlue { get; set; }
        
        /// <summary>Number of skin pixels sampled</summary>
        public int SampleCount { get; set; }
        
        public float Confidence { get; set; }
        public bool IsReliable => Confidence >= ConfidenceThresholds.Acceptable;
        public string Source { get; set; }
    }
    
    /// <summary>
    /// Skin type categories based on ITA
    /// </summary>
    public enum SkinType
    {
        VeryLight,  // ITA > 55 (Fitzpatrick I)
        Light,      // ITA 41-55 (Fitzpatrick II)
        Intermediate, // ITA 28-41 (Fitzpatrick III)
        Tan,        // ITA 10-28 (Fitzpatrick IV)
        Brown,      // ITA -30 to 10 (Fitzpatrick V)
        Dark        // ITA < -30 (Fitzpatrick VI)
    }
    
    /// <summary>
    /// Skin tone detection using ITA (Individual Typology Angle).
    /// ITA is calculated from CIELAB L* and b* values.
    /// </summary>
    public class SkinToneModule
    {
        // ITA thresholds for skin type classification
        private const float ITA_VERY_LIGHT = 55f;
        private const float ITA_LIGHT = 41f;
        private const float ITA_INTERMEDIATE = 28f;
        private const float ITA_TAN = 10f;
        private const float ITA_BROWN = -30f;
        
        public SkinToneModule()
        {
        }
        
        /// <summary>
        /// Analyze skin tone from image using face parsing mask
        /// </summary>
        public SkinToneResult Analyze(Bitmap image, FaceParsingResult parsing)
        {
            var result = new SkinToneResult
            {
                Source = "SkinToneModule.ITA"
            };
            
            if (image == null || parsing == null || !parsing.HasRegion(FaceRegion.Skin))
            {
                result.Confidence = 0f;
                return result;
            }
            
            // Sample skin pixels using the parsing mask
            var (avgL, avgA, avgB, count) = SampleSkinPixels(image, parsing);
            
            if (count < 100)
            {
                // Not enough skin pixels
                result.Confidence = 0.2f;
                return result;
            }
            
            // Calculate ITA (Individual Typology Angle)
            // ITA = arctan((L* - 50) / b*) × (180/π)
            float ita = (float)(Math.Atan2(avgL - 50, avgB) * 180 / Math.PI);
            
            result.ItaAngle = ita;
            result.Lightness = avgL;
            result.RedGreen = avgA;
            result.YellowBlue = avgB;
            result.SampleCount = count;
            
            // Classify skin type
            result.SkinType = ClassifySkinType(ita);
            result.FitzpatrickType = GetFitzpatrickType(ita);
            
            // Normalize to 0-1 scale for Bannerlord
            // ITA ranges from ~-60 (very dark) to ~70 (very light)
            result.NormalizedTone = (ita + 60) / 130f;
            result.NormalizedTone = Math.Max(0f, Math.Min(1f, result.NormalizedTone));
            
            // Confidence based on sample count and consistency
            result.Confidence = Math.Min(0.95f, 0.5f + count / 5000f);
            
            return result;
        }
        
        /// <summary>
        /// Analyze skin tone from image with simple face region (no parsing)
        /// Falls back to sampling central face area
        /// </summary>
        public SkinToneResult AnalyzeSimple(Bitmap image, Rectangle faceRect)
        {
            var result = new SkinToneResult
            {
                Source = "SkinToneModule.Simple"
            };
            
            if (image == null || faceRect.Width < 10 || faceRect.Height < 10)
            {
                result.Confidence = 0f;
                return result;
            }
            
            // Sample from forehead and cheek regions (avoiding eyes, mouth)
            var samples = SampleFaceRegions(image, faceRect);
            
            if (samples.count < 50)
            {
                result.Confidence = 0.2f;
                return result;
            }
            
            float ita = (float)(Math.Atan2(samples.avgL - 50, samples.avgB) * 180 / Math.PI);
            
            result.ItaAngle = ita;
            result.Lightness = samples.avgL;
            result.RedGreen = samples.avgA;
            result.YellowBlue = samples.avgB;
            result.SampleCount = samples.count;
            result.SkinType = ClassifySkinType(ita);
            result.FitzpatrickType = GetFitzpatrickType(ita);
            result.NormalizedTone = Math.Max(0f, Math.Min(1f, (ita + 60) / 130f));
            result.Confidence = 0.6f;  // Lower confidence without parsing
            
            return result;
        }
        
        private SkinType ClassifySkinType(float ita)
        {
            if (ita > ITA_VERY_LIGHT) return SkinType.VeryLight;
            if (ita > ITA_LIGHT) return SkinType.Light;
            if (ita > ITA_INTERMEDIATE) return SkinType.Intermediate;
            if (ita > ITA_TAN) return SkinType.Tan;
            if (ita > ITA_BROWN) return SkinType.Brown;
            return SkinType.Dark;
        }
        
        private int GetFitzpatrickType(float ita)
        {
            if (ita > ITA_VERY_LIGHT) return 1;
            if (ita > ITA_LIGHT) return 2;
            if (ita > ITA_INTERMEDIATE) return 3;
            if (ita > ITA_TAN) return 4;
            if (ita > ITA_BROWN) return 5;
            return 6;
        }
        
        private (float avgL, float avgA, float avgB, int count) SampleSkinPixels(
            Bitmap image, FaceParsingResult parsing)
        {
            double sumL = 0, sumA = 0, sumB = 0;
            int count = 0;
            
            int maskW = parsing.Mask.GetLength(0);
            int maskH = parsing.Mask.GetLength(1);
            
            float scaleX = (float)image.Width / maskW;
            float scaleY = (float)image.Height / maskH;
            
            // Sample every few pixels for performance
            int step = Math.Max(1, Math.Min(maskW, maskH) / 100);
            
            for (int my = 0; my < maskH; my += step)
            {
                for (int mx = 0; mx < maskW; mx += step)
                {
                    if (parsing.Mask[mx, my] == (byte)FaceRegion.Skin)
                    {
                        int ix = (int)(mx * scaleX);
                        int iy = (int)(my * scaleY);
                        
                        if (ix >= 0 && ix < image.Width && iy >= 0 && iy < image.Height)
                        {
                            var pixel = image.GetPixel(ix, iy);
                            var (L, a, b) = RgbToLab(pixel.R, pixel.G, pixel.B);
                            sumL += L;
                            sumA += a;
                            sumB += b;
                            count++;
                        }
                    }
                }
            }
            
            if (count == 0)
                return (50, 0, 0, 0);
            
            return ((float)(sumL / count), (float)(sumA / count), (float)(sumB / count), count);
        }
        
        private (float avgL, float avgA, float avgB, int count) SampleFaceRegions(
            Bitmap image, Rectangle faceRect)
        {
            double sumL = 0, sumA = 0, sumB = 0;
            int count = 0;
            
            // Define sampling regions (forehead and cheeks)
            // Forehead: top 15-25% of face, middle 40%
            int foreheadTop = faceRect.Top + (int)(faceRect.Height * 0.15);
            int foreheadBot = faceRect.Top + (int)(faceRect.Height * 0.25);
            int foreheadLeft = faceRect.Left + (int)(faceRect.Width * 0.3);
            int foreheadRight = faceRect.Left + (int)(faceRect.Width * 0.7);
            
            // Left cheek: 40-60% down, 15-35% from left
            int cheekTop = faceRect.Top + (int)(faceRect.Height * 0.4);
            int cheekBot = faceRect.Top + (int)(faceRect.Height * 0.6);
            int leftCheekLeft = faceRect.Left + (int)(faceRect.Width * 0.15);
            int leftCheekRight = faceRect.Left + (int)(faceRect.Width * 0.35);
            int rightCheekLeft = faceRect.Left + (int)(faceRect.Width * 0.65);
            int rightCheekRight = faceRect.Left + (int)(faceRect.Width * 0.85);
            
            int step = Math.Max(1, faceRect.Width / 30);
            
            // Sample forehead
            for (int y = foreheadTop; y < foreheadBot; y += step)
            {
                for (int x = foreheadLeft; x < foreheadRight; x += step)
                {
                    if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                    {
                        var pixel = image.GetPixel(x, y);
                        var (L, a, b) = RgbToLab(pixel.R, pixel.G, pixel.B);
                        sumL += L;
                        sumA += a;
                        sumB += b;
                        count++;
                    }
                }
            }
            
            // Sample cheeks
            for (int y = cheekTop; y < cheekBot; y += step)
            {
                for (int x = leftCheekLeft; x < leftCheekRight; x += step)
                {
                    if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                    {
                        var pixel = image.GetPixel(x, y);
                        var (L, a, b) = RgbToLab(pixel.R, pixel.G, pixel.B);
                        sumL += L;
                        sumA += a;
                        sumB += b;
                        count++;
                    }
                }
                for (int x = rightCheekLeft; x < rightCheekRight; x += step)
                {
                    if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                    {
                        var pixel = image.GetPixel(x, y);
                        var (L, a, b) = RgbToLab(pixel.R, pixel.G, pixel.B);
                        sumL += L;
                        sumA += a;
                        sumB += b;
                        count++;
                    }
                }
            }
            
            if (count == 0)
                return (50, 0, 0, 0);
            
            return ((float)(sumL / count), (float)(sumA / count), (float)(sumB / count), count);
        }
        
        /// <summary>
        /// Convert RGB to CIELAB color space
        /// </summary>
        private (float L, float a, float b) RgbToLab(int r, int g, int b)
        {
            // RGB to XYZ (sRGB, D65)
            double rn = r / 255.0;
            double gn = g / 255.0;
            double bn = b / 255.0;
            
            // Gamma correction
            rn = rn > 0.04045 ? Math.Pow((rn + 0.055) / 1.055, 2.4) : rn / 12.92;
            gn = gn > 0.04045 ? Math.Pow((gn + 0.055) / 1.055, 2.4) : gn / 12.92;
            bn = bn > 0.04045 ? Math.Pow((bn + 0.055) / 1.055, 2.4) : bn / 12.92;
            
            rn *= 100;
            gn *= 100;
            bn *= 100;
            
            // sRGB to XYZ
            double x = rn * 0.4124564 + gn * 0.3575761 + bn * 0.1804375;
            double y = rn * 0.2126729 + gn * 0.7151522 + bn * 0.0721750;
            double z = rn * 0.0193339 + gn * 0.1191920 + bn * 0.9503041;
            
            // XYZ to LAB (D65 reference)
            x /= 95.047;
            y /= 100.000;
            z /= 108.883;
            
            x = x > 0.008856 ? Math.Pow(x, 1.0 / 3.0) : (7.787 * x) + (16.0 / 116.0);
            y = y > 0.008856 ? Math.Pow(y, 1.0 / 3.0) : (7.787 * y) + (16.0 / 116.0);
            z = z > 0.008856 ? Math.Pow(z, 1.0 / 3.0) : (7.787 * z) + (16.0 / 116.0);
            
            float L = (float)((116.0 * y) - 16.0);
            float a_out = (float)(500.0 * (x - y));
            float b_out = (float)(200.0 * (y - z));
            
            return (L, a_out, b_out);
        }
    }
}
