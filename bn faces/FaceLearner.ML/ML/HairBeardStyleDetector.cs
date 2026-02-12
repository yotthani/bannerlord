using System;
using System.Drawing;

namespace FaceLearner.ML
{
    /// <summary>
    /// Detects hair style (length) and beard presence from face images.
    /// Maps to Bannerlord Hair/Beard indices.
    /// 
    /// Note: This is used for PHOTO DETECTION only, not during learning.
    /// During learning, we focus on face morphs and let hair/beard be random.
    /// </summary>
    public static class HairBeardStyleDetector
    {
        /// <summary>
        /// Result of hair/beard style detection
        /// </summary>
        public class StyleResult
        {
            /// <summary>Hair style index for Bannerlord (0-based)</summary>
            public int HairIndex { get; set; }
            
            /// <summary>Beard style index for Bannerlord (0-based, 0=none)</summary>
            public int BeardIndex { get; set; }
            
            /// <summary>Detected hair length category</summary>
            public HairLength HairLength { get; set; }
            
            /// <summary>Detected beard type</summary>
            public BeardType BeardType { get; set; }
            
            /// <summary>Confidence in hair detection (0-1)</summary>
            public float HairConfidence { get; set; }
            
            /// <summary>Confidence in beard detection (0-1)</summary>
            public float BeardConfidence { get; set; }
            
            /// <summary>Is the detected person female? (affects beard expectation)</summary>
            public bool IsFemale { get; set; }
        }
        
        public enum HairLength
        {
            Bald,       // Very little or no hair on top
            Short,      // Hair visible but not past ears
            Medium,     // Hair around ear level
            Long        // Hair past ears/on shoulders
        }
        
        public enum BeardType
        {
            None,       // Clean shaven
            Stubble,    // 5 o'clock shadow
            Short,      // Short trimmed beard
            Medium,     // Medium length beard
            Full        // Full long beard
        }
        
        /// <summary>
        /// Detect hair and beard style from image
        /// </summary>
        public static StyleResult Detect(string imagePath, float[] landmarks, bool isFemale)
        {
            try
            {
                using (var bmp = new Bitmap(imagePath))
                {
                    return Detect(bmp, landmarks, isFemale);
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"HairBeardStyle: Error - {ex.Message}");
                return new StyleResult
                {
                    HairIndex = 0,
                    BeardIndex = 0,
                    HairLength = HairLength.Short,
                    BeardType = BeardType.None,
                    HairConfidence = 0f,
                    BeardConfidence = 0f,
                    IsFemale = isFemale
                };
            }
        }
        
        /// <summary>
        /// Detect hair and beard style from bitmap
        /// </summary>
        public static StyleResult Detect(Bitmap bmp, float[] landmarks, bool isFemale)
        {
            var result = new StyleResult { IsFemale = isFemale };
            
            int width = bmp.Width;
            int height = bmp.Height;
            
            // === HAIR LENGTH DETECTION ===
            var (hairLength, hairConf) = DetectHairLength(bmp, width, height);
            result.HairLength = hairLength;
            result.HairConfidence = hairConf;
            result.HairIndex = MapHairLengthToIndex(hairLength, isFemale);
            
            // === BEARD DETECTION (only for males) ===
            if (!isFemale)
            {
                var (beardType, beardConf) = DetectBeard(bmp, width, height, landmarks);
                result.BeardType = beardType;
                result.BeardConfidence = beardConf;
                result.BeardIndex = MapBeardTypeToIndex(beardType);
            }
            else
            {
                result.BeardType = BeardType.None;
                result.BeardConfidence = 1.0f;
                result.BeardIndex = 0;
            }
            
            SubModule.Log($"  Style detected: Hair={result.HairLength}(idx={result.HairIndex}, conf={result.HairConfidence:F2}) Beard={result.BeardType}(idx={result.BeardIndex}, conf={result.BeardConfidence:F2})");
            
            return result;
        }
        
        /// <summary>
        /// Detect hair length by analyzing top and side regions
        /// </summary>
        private static (HairLength length, float confidence) DetectHairLength(Bitmap bmp, int width, int height)
        {
            // Sample regions
            int topRegionHeight = height / 4;
            int sideRegionWidth = width / 5;
            int sideStartY = height / 3;
            int sideEndY = height * 2 / 3;
            
            int hairPixelsTop = 0;
            int hairPixelsLeft = 0;
            int hairPixelsRight = 0;
            int totalTop = 0;
            int totalSides = 0;
            
            // Sample top region
            for (int y = 0; y < topRegionHeight; y += 2)
            {
                for (int x = width / 6; x < width * 5 / 6; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    totalTop++;
                    if (IsHairPixel(pixel)) hairPixelsTop++;
                }
            }
            
            // Sample left side
            for (int y = sideStartY; y < sideEndY; y += 3)
            {
                for (int x = 0; x < sideRegionWidth; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    totalSides++;
                    if (IsHairPixel(pixel)) hairPixelsLeft++;
                }
            }
            
            // Sample right side
            for (int y = sideStartY; y < sideEndY; y += 3)
            {
                for (int x = width - sideRegionWidth; x < width; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    totalSides++;
                    if (IsHairPixel(pixel)) hairPixelsRight++;
                }
            }
            
            // Also check below ear level for very long hair
            int belowEarPixels = 0;
            int totalBelowEar = 0;
            int belowEarY = height * 2 / 3;
            for (int y = belowEarY; y < height - 10; y += 3)
            {
                // Check sides only (not center which would be body/clothing)
                for (int x = 0; x < width / 4; x += 2)
                {
                    totalBelowEar++;
                    if (IsHairPixel(bmp.GetPixel(x, y))) belowEarPixels++;
                }
                for (int x = width * 3 / 4; x < width; x += 2)
                {
                    totalBelowEar++;
                    if (IsHairPixel(bmp.GetPixel(x, y))) belowEarPixels++;
                }
            }
            
            float topRatio = totalTop > 0 ? (float)hairPixelsTop / totalTop : 0;
            float sideRatio = totalSides > 0 ? (float)(hairPixelsLeft + hairPixelsRight) / totalSides : 0;
            float belowEarRatio = totalBelowEar > 0 ? (float)belowEarPixels / totalBelowEar : 0;
            
            // Determine hair length
            HairLength length;
            float confidence;
            
            if (topRatio < 0.15f)
            {
                // Very little hair on top
                length = HairLength.Bald;
                confidence = 0.7f + (0.15f - topRatio) * 2f;
            }
            else if (belowEarRatio > 0.20f)
            {
                // Significant hair below ear level
                length = HairLength.Long;
                confidence = 0.6f + belowEarRatio;
            }
            else if (sideRatio > 0.15f)
            {
                // Hair visible on sides at ear level
                length = HairLength.Medium;
                confidence = 0.5f + sideRatio;
            }
            else if (sideRatio > 0.05f)
            {
                // Some hair on sides but not much
                length = HairLength.Short;
                confidence = 0.5f + topRatio;
            }
            else
            {
                // Default to short
                length = HairLength.Short;
                confidence = 0.4f;
            }
            
            confidence = Math.Min(0.9f, confidence);
            
            return (length, confidence);
        }
        
        /// <summary>
        /// Detect beard by analyzing chin/jaw region
        /// </summary>
        private static (BeardType type, float confidence) DetectBeard(Bitmap bmp, int width, int height, float[] landmarks)
        {
            // Chin/jaw region is roughly the bottom 1/3 of face, center portion
            int chinStartY = height * 2 / 3;
            int chinEndY = height - 5;
            int chinStartX = width / 4;
            int chinEndX = width * 3 / 4;
            
            // If we have landmarks, use them for more precise chin location
            if (landmarks != null && landmarks.Length >= 34)
            {
                // Landmark 8 is typically chin (index 16-17 in 2D)
                // Adjust region based on landmarks
                float chinY = landmarks[8 * 2 + 1] * height; // Y of chin landmark
                chinStartY = (int)Math.Max(chinY - height * 0.15f, height / 2);
                chinEndY = (int)Math.Min(chinY + height * 0.05f, height - 2);
            }
            
            // Count dark pixels in chin region (potential beard)
            int darkPixels = 0;
            int totalPixels = 0;
            float avgDarkness = 0f;
            int darknessCount = 0;
            
            for (int y = chinStartY; y < chinEndY; y += 2)
            {
                for (int x = chinStartX; x < chinEndX; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    totalPixels++;
                    
                    // Check if pixel is dark (potential beard)
                    float brightness = (pixel.R + pixel.G + pixel.B) / 3f / 255f;
                    
                    // Beard pixels are typically darker than skin
                    // But not as dark as black (could be shadow)
                    if (brightness < 0.45f && brightness > 0.05f)
                    {
                        // Check if it's hair-like (not skin tone)
                        if (!IsSkinTone(pixel))
                        {
                            darkPixels++;
                            avgDarkness += brightness;
                            darknessCount++;
                        }
                    }
                }
            }
            
            float darkRatio = totalPixels > 0 ? (float)darkPixels / totalPixels : 0;
            if (darknessCount > 0) avgDarkness /= darknessCount;
            
            // Also check the "mustache region" (between nose and upper lip)
            int mustachePixels = 0;
            int mustacheTotal = 0;
            int mustacheY = height / 2 + height / 8;
            int mustacheHeight = height / 12;
            
            for (int y = mustacheY; y < mustacheY + mustacheHeight; y += 2)
            {
                for (int x = width / 3; x < width * 2 / 3; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    mustacheTotal++;
                    float brightness = (pixel.R + pixel.G + pixel.B) / 3f / 255f;
                    if (brightness < 0.40f && brightness > 0.05f && !IsSkinTone(pixel))
                    {
                        mustachePixels++;
                    }
                }
            }
            float mustacheRatio = mustacheTotal > 0 ? (float)mustachePixels / mustacheTotal : 0;
            
            // Determine beard type
            BeardType type;
            float confidence;
            
            // Combine chin and mustache detection
            float totalBeardScore = darkRatio * 0.7f + mustacheRatio * 0.3f;
            
            if (totalBeardScore < 0.05f)
            {
                type = BeardType.None;
                confidence = 0.7f;
            }
            else if (totalBeardScore < 0.12f)
            {
                type = BeardType.Stubble;
                confidence = 0.5f + totalBeardScore * 2f;
            }
            else if (totalBeardScore < 0.25f)
            {
                type = BeardType.Short;
                confidence = 0.5f + totalBeardScore;
            }
            else if (totalBeardScore < 0.40f)
            {
                type = BeardType.Medium;
                confidence = 0.5f + totalBeardScore * 0.5f;
            }
            else
            {
                type = BeardType.Full;
                confidence = 0.6f + totalBeardScore * 0.3f;
            }
            
            confidence = Math.Min(0.85f, confidence);
            
            return (type, confidence);
        }
        
        /// <summary>
        /// Map hair length to Bannerlord hair index.
        /// Bannerlord has different hair options for male/female.
        /// These are approximate mappings - actual game may have more styles.
        /// </summary>
        private static int MapHairLengthToIndex(HairLength length, bool isFemale)
        {
            // Bannerlord typically has ~15-20 hair styles per gender
            // We map to rough categories:
            
            if (isFemale)
            {
                // Female hair styles (usually more long options)
                switch (length)
                {
                    case HairLength.Bald: return 0;     // Bald/very short
                    case HairLength.Short: return 2;   // Short feminine
                    case HairLength.Medium: return 5;  // Medium length
                    case HairLength.Long: return 8;    // Long hair
                    default: return 3;
                }
            }
            else
            {
                // Male hair styles
                switch (length)
                {
                    case HairLength.Bald: return 0;    // Bald
                    case HairLength.Short: return 1;  // Short/cropped
                    case HairLength.Medium: return 3; // Medium
                    case HairLength.Long: return 6;   // Longer male hair
                    default: return 1;
                }
            }
        }
        
        /// <summary>
        /// Map beard type to Bannerlord beard index.
        /// </summary>
        private static int MapBeardTypeToIndex(BeardType type)
        {
            // Bannerlord typically has ~10-15 beard styles
            switch (type)
            {
                case BeardType.None: return 0;      // Clean shaven
                case BeardType.Stubble: return 1;   // Stubble/5 o'clock shadow
                case BeardType.Short: return 2;     // Short beard
                case BeardType.Medium: return 4;    // Medium beard
                case BeardType.Full: return 6;      // Full beard
                default: return 0;
            }
        }
        
        /// <summary>
        /// Check if pixel is likely hair (dark, not skin)
        /// </summary>
        private static bool IsHairPixel(Color pixel)
        {
            float r = pixel.R / 255f;
            float g = pixel.G / 255f;
            float b = pixel.B / 255f;
            float brightness = (r + g + b) / 3f;
            
            // Hair is typically darker than background
            if (brightness > 0.85f) return false;  // Too bright (background/white)
            if (brightness < 0.02f) return false;  // Too dark (pure black border)
            
            // Exclude obvious skin tones
            if (IsSkinTone(pixel)) return false;
            
            // Hair can be: black, brown, blonde, red, gray
            // Black hair: very dark
            if (brightness < 0.20f) return true;
            
            // Brown hair: medium dark with warm tones
            if (brightness < 0.45f && r > g * 0.9f) return true;
            
            // Blonde hair: lighter but with yellow tones
            if (brightness > 0.50f && brightness < 0.80f)
            {
                if (r > 0.6f && g > 0.5f && b < g) return true;  // Yellow/golden
            }
            
            // Red hair
            if (r > 0.5f && r > g * 1.2f && r > b * 1.5f) return true;
            
            // Gray hair
            if (Math.Abs(r - g) < 0.1f && Math.Abs(g - b) < 0.1f && brightness > 0.4f && brightness < 0.75f)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Check if pixel is skin tone (to exclude from hair detection)
        /// </summary>
        private static bool IsSkinTone(Color pixel)
        {
            float r = pixel.R / 255f;
            float g = pixel.G / 255f;
            float b = pixel.B / 255f;
            
            // Skin tones typically have R > G > B with specific ratios
            // This covers a wide range from light to dark skin
            
            // Light skin: pinkish
            if (r > 0.6f && g > 0.4f && b > 0.3f && r > g && g > b * 0.8f)
            {
                float rgRatio = g / (r + 0.001f);
                if (rgRatio > 0.6f && rgRatio < 0.95f) return true;
            }
            
            // Medium skin: tan/olive
            if (r > 0.4f && r < 0.8f && g > 0.3f && g < 0.7f)
            {
                float rgRatio = g / (r + 0.001f);
                float rbRatio = b / (r + 0.001f);
                if (rgRatio > 0.65f && rgRatio < 0.95f && rbRatio > 0.4f && rbRatio < 0.85f)
                    return true;
            }
            
            // Darker skin
            if (r > 0.2f && r < 0.6f && r > g * 0.95f && r > b * 1.1f)
            {
                return true;
            }
            
            return false;
        }
    }
}
