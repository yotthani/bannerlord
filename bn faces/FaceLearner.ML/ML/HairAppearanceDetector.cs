using System;
using System.Drawing;
using System.Linq;

namespace FaceLearner.ML
{
    /// <summary>
    /// Detects hair and appearance features that indicate gender.
    /// Uses image analysis to detect:
    /// - Long hair (strong female signal)
    /// - Hair color patterns (blonde is often female in datasets)
    /// - Makeup indicators (red lips, eye makeup)
    /// 
    /// This helps correct AI model biases where women with certain
    /// facial bone structures are misidentified as male.
    /// </summary>
    public static class HairAppearanceDetector
    {
        /// <summary>
        /// Analyze image for hair and appearance features
        /// </summary>
        /// <param name="imagePath">Path to face image</param>
        /// <returns>femaleScore: -1 to +1 (negative=male, positive=female), confidence: 0-1</returns>
        public static (float femaleScore, float confidence, string details) Analyze(string imagePath)
        {
            try
            {
                using (var bmp = new Bitmap(imagePath))
                {
                    return Analyze(bmp);
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"HairAppearance: Error - {ex.Message}");
                return (0f, 0f, "error");
            }
        }
        
        /// <summary>
        /// Analyze bitmap for hair and appearance features
        /// </summary>
        public static (float femaleScore, float confidence, string details) Analyze(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            
            float totalScore = 0f;
            float totalWeight = 0f;
            string details = "";
            
            // === 1. HAIR DETECTION ===
            // Check the top 1/3 and sides of image for hair pixels
            var (hairScore, hairConf, hairColor) = DetectHair(bmp);
            if (hairConf > 0.3f)
            {
                totalScore += hairScore * 3.0f;  // High weight for hair
                totalWeight += 3.0f;
                details += $"Hair:{hairScore:F2}({hairColor}) ";
            }
            
            // === 2. LIP COLOR DETECTION ===
            // Red/pink lips = makeup = female signal
            var (lipScore, lipConf) = DetectLipColor(bmp);
            if (lipConf > 0.3f)
            {
                totalScore += lipScore * 2.5f;
                totalWeight += 2.5f;
                details += $"Lips:{lipScore:F2} ";
            }
            
            // === 3. SKIN SMOOTHNESS ===
            // Smoother skin (less texture) often indicates female (or makeup)
            var (skinScore, skinConf) = DetectSkinTexture(bmp);
            if (skinConf > 0.3f)
            {
                totalScore += skinScore * 1.5f;
                totalWeight += 1.5f;
                details += $"Skin:{skinScore:F2} ";
            }
            
            // === 4. EYE AREA CONTRAST ===
            // Eye makeup creates higher contrast around eyes
            var (eyeScore, eyeConf) = DetectEyeContrast(bmp);
            if (eyeConf > 0.3f)
            {
                totalScore += eyeScore * 2.0f;
                totalWeight += 2.0f;
                details += $"Eyes:{eyeScore:F2} ";
            }
            
            // Calculate final score
            if (totalWeight < 1f)
                return (0f, 0f, "insufficient_data");
            
            float finalScore = totalScore / totalWeight;
            float confidence = Math.Min(0.9f, totalWeight / 8f);  // More signals = higher confidence
            
            return (finalScore, confidence, details.Trim());
        }
        
        /// <summary>
        /// Detect hair presence and characteristics
        /// Returns positive for long/styled hair, negative for short/no hair
        /// </summary>
        private static (float score, float confidence, string colorDesc) DetectHair(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            
            // Sample the top portion (above eyes) and sides
            int topRegionHeight = height / 4;
            int sideRegionWidth = width / 5;
            
            // Count hair-like pixels (dark, brown, blonde, red tones)
            int hairPixelsTop = 0;
            int hairPixelsLeft = 0;
            int hairPixelsRight = 0;
            int totalSampledTop = 0;
            int totalSampledSides = 0;
            
            float avgHue = 0f;
            float avgSat = 0f;
            float avgVal = 0f;
            int hsvCount = 0;
            
            // Sample top region
            for (int y = 0; y < topRegionHeight; y += 2)
            {
                for (int x = width / 6; x < width * 5 / 6; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    totalSampledTop++;
                    
                    if (IsHairPixel(pixel))
                    {
                        hairPixelsTop++;
                        var hsv = RgbToHsv(pixel);
                        avgHue += hsv.h;
                        avgSat += hsv.s;
                        avgVal += hsv.v;
                        hsvCount++;
                    }
                }
            }
            
            // Sample side regions (for long hair detection)
            int sideStartY = height / 3;
            int sideEndY = height * 2 / 3;
            
            // Left side
            for (int y = sideStartY; y < sideEndY; y += 3)
            {
                for (int x = 0; x < sideRegionWidth; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    totalSampledSides++;
                    if (IsHairPixel(pixel)) hairPixelsLeft++;
                }
            }
            
            // Right side  
            for (int y = sideStartY; y < sideEndY; y += 3)
            {
                for (int x = width - sideRegionWidth; x < width; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    totalSampledSides++;
                    if (IsHairPixel(pixel)) hairPixelsRight++;
                }
            }
            
            // Calculate hair ratios
            float topRatio = totalSampledTop > 0 ? (float)hairPixelsTop / totalSampledTop : 0;
            float sideRatio = totalSampledSides > 0 ? (float)(hairPixelsLeft + hairPixelsRight) / totalSampledSides : 0;
            
            // Determine hair color
            string hairColor = "unknown";
            if (hsvCount > 0)
            {
                avgHue /= hsvCount;
                avgSat /= hsvCount;
                avgVal /= hsvCount;
                
                if (avgVal < 0.25f)
                    hairColor = "black";
                else if (avgVal > 0.7f && avgSat < 0.3f)
                    hairColor = "blonde";
                else if (avgVal > 0.6f && avgSat > 0.3f && avgHue > 20 && avgHue < 50)
                    hairColor = "blonde";
                else if (avgHue > 0 && avgHue < 30 && avgSat > 0.5f)
                    hairColor = "red";
                else if (avgVal > 0.2f && avgVal < 0.5f)
                    hairColor = "brown";
                else
                    hairColor = "other";
            }
            
            // === v3.0.24: BEARD DETECTION ===
            // Sample the lower-center face region (chin/jaw area) for hair-like pixels.
            // A beard is a VERY strong male signal that other methods can't detect.
            int beardTop = height * 55 / 100;
            int beardBottom = height * 90 / 100;
            int beardLeft = width * 20 / 100;
            int beardRight = width * 80 / 100;
            int beardHairPixels = 0;
            int beardSampled = 0;

            for (int y = beardTop; y < beardBottom; y += 3)
            {
                for (int x = beardLeft; x < beardRight; x += 3)
                {
                    if (x >= width || y >= height) continue;
                    var pixel = bmp.GetPixel(x, y);
                    beardSampled++;
                    if (IsHairPixel(pixel)) beardHairPixels++;
                }
            }

            float beardRatio = beardSampled > 0 ? (float)beardHairPixels / beardSampled : 0f;
            bool likelyBeard = beardRatio > 0.25f;  // 25%+ of chin/jaw area is hair-like = beard

            // Score calculation
            float score = 0f;

            if (likelyBeard)
            {
                // Beard detected — very strong male signal
                score -= 0.6f;
                // Side hair detection is unreliable with a beard (beard extends to sides)
                if (sideRatio > 0.15f)
                    score -= 0.1f;  // Reduced — might just be beard, not long hair
            }
            else if (sideRatio > 0.15f)
            {
                // No beard: side hair = long hair = female signal (original logic)
                score += 0.5f;
            }

            if (!likelyBeard && sideRatio < 0.05f && topRatio < 0.3f)
            {
                // Very little hair visible = likely short/bald = male signal
                score -= 0.3f;
            }

            // Blonde hair is statistically more common in women (in Western datasets)
            // But don't weight too heavily - can cause bias
            if (!likelyBeard && hairColor == "blonde" && sideRatio > 0.1f)
            {
                score += 0.2f;  // Slight female bonus for blonde + long
            }

            float confidence = Math.Min(0.8f, (topRatio + sideRatio) * 2f);
            if (likelyBeard) confidence = Math.Max(confidence, 0.6f);  // Beard detection is reliable

            // Add beard info to color description for downstream parsing
            string colorResult = hairColor;
            if (likelyBeard)
                colorResult += $",Beard:{beardRatio:F2}";

            return (score, confidence, colorResult);
        }
        
        /// <summary>
        /// Check if a pixel is likely hair (not skin, not background)
        /// </summary>
        private static bool IsHairPixel(Color pixel)
        {
            var hsv = RgbToHsv(pixel);
            
            // Exclude very bright pixels (likely background/highlights)
            if (hsv.v > 0.95f) return false;
            
            // Exclude skin tones (orangish, medium saturation)
            if (hsv.h > 10 && hsv.h < 40 && hsv.s > 0.2f && hsv.s < 0.6f && hsv.v > 0.4f && hsv.v < 0.85f)
                return false;
            
            // Hair is typically:
            // - Low saturation (black, brown, gray) OR
            // - Yellow-ish (blonde) OR
            // - Red-ish (red hair)
            
            // Black/dark hair
            if (hsv.v < 0.35f && hsv.s < 0.5f) return true;
            
            // Brown hair
            if (hsv.h > 15 && hsv.h < 45 && hsv.s > 0.2f && hsv.v > 0.15f && hsv.v < 0.55f) return true;
            
            // Blonde hair
            if (hsv.h > 35 && hsv.h < 55 && hsv.s > 0.15f && hsv.v > 0.5f) return true;
            
            // Red hair  
            if (hsv.h < 25 && hsv.s > 0.4f && hsv.v > 0.3f && hsv.v < 0.7f) return true;
            
            return false;
        }
        
        /// <summary>
        /// Detect lip color - red/pink lips indicate makeup
        /// </summary>
        private static (float score, float confidence) DetectLipColor(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            
            // Sample the lower-middle area where lips should be
            int lipRegionTop = height * 55 / 100;
            int lipRegionBottom = height * 75 / 100;
            int lipRegionLeft = width * 30 / 100;
            int lipRegionRight = width * 70 / 100;
            
            int redLipPixels = 0;
            int pinkLipPixels = 0;
            int totalSampled = 0;
            
            for (int y = lipRegionTop; y < lipRegionBottom; y += 2)
            {
                for (int x = lipRegionLeft; x < lipRegionRight; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    var hsv = RgbToHsv(pixel);
                    totalSampled++;
                    
                    // Detect red/pink lips (makeup)
                    // Red lipstick: low hue (0-15), high saturation
                    if ((hsv.h < 15 || hsv.h > 340) && hsv.s > 0.4f && hsv.v > 0.3f)
                    {
                        redLipPixels++;
                    }
                    // Pink lips: slightly higher hue, medium-high saturation
                    else if (hsv.h > 320 && hsv.h < 360 && hsv.s > 0.25f && hsv.v > 0.4f)
                    {
                        pinkLipPixels++;
                    }
                }
            }
            
            float redRatio = totalSampled > 0 ? (float)redLipPixels / totalSampled : 0;
            float pinkRatio = totalSampled > 0 ? (float)pinkLipPixels / totalSampled : 0;
            
            float score = 0f;
            
            // Strong red lips = very likely makeup = female
            if (redRatio > 0.05f)
            {
                score += 0.6f;
            }
            else if (pinkRatio > 0.1f)
            {
                score += 0.3f;
            }
            
            float confidence = Math.Min(0.7f, (redRatio + pinkRatio) * 5f);
            
            return (score, confidence);
        }
        
        /// <summary>
        /// Detect skin texture - smoother = often female
        /// </summary>
        private static (float score, float confidence) DetectSkinTexture(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            
            // Sample cheek area for texture analysis
            int cheekTop = height * 40 / 100;
            int cheekBottom = height * 60 / 100;
            int cheekLeftStart = width * 15 / 100;
            int cheekLeftEnd = width * 35 / 100;
            int cheekRightStart = width * 65 / 100;
            int cheekRightEnd = width * 85 / 100;
            
            float totalVariance = 0f;
            int sampleCount = 0;
            
            // Calculate local variance (texture indicator)
            for (int y = cheekTop; y < cheekBottom - 2; y += 3)
            {
                // Left cheek
                for (int x = cheekLeftStart; x < cheekLeftEnd - 2; x += 3)
                {
                    float variance = CalculateLocalVariance(bmp, x, y);
                    totalVariance += variance;
                    sampleCount++;
                }
                
                // Right cheek
                for (int x = cheekRightStart; x < cheekRightEnd - 2; x += 3)
                {
                    float variance = CalculateLocalVariance(bmp, x, y);
                    totalVariance += variance;
                    sampleCount++;
                }
            }
            
            if (sampleCount == 0) return (0f, 0f);
            
            float avgVariance = totalVariance / sampleCount;
            
            // Lower variance = smoother skin = slight female signal
            // But this is weak - many factors affect skin appearance
            float score = 0f;
            if (avgVariance < 15f)  // Very smooth
            {
                score += 0.2f;
            }
            else if (avgVariance > 40f)  // Rough/textured
            {
                score -= 0.15f;
            }
            
            return (score, 0.4f);  // Low confidence - not very reliable
        }
        
        private static float CalculateLocalVariance(Bitmap bmp, int x, int y)
        {
            float sum = 0;
            float sumSq = 0;
            int count = 0;
            
            for (int dy = 0; dy < 3; dy++)
            {
                for (int dx = 0; dx < 3; dx++)
                {
                    var pixel = bmp.GetPixel(x + dx, y + dy);
                    float gray = (pixel.R + pixel.G + pixel.B) / 3f;
                    sum += gray;
                    sumSq += gray * gray;
                    count++;
                }
            }
            
            float mean = sum / count;
            float variance = (sumSq / count) - (mean * mean);
            return Math.Max(0, variance);
        }
        
        /// <summary>
        /// Detect eye contrast - eye makeup creates high contrast
        /// </summary>
        private static (float score, float confidence) DetectEyeContrast(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            
            // Eye region (approximate)
            int eyeTop = height * 25 / 100;
            int eyeBottom = height * 45 / 100;
            int eyeLeft = width * 20 / 100;
            int eyeRight = width * 80 / 100;
            
            float minVal = 255f;
            float maxVal = 0f;
            
            for (int y = eyeTop; y < eyeBottom; y += 2)
            {
                for (int x = eyeLeft; x < eyeRight; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    float gray = (pixel.R + pixel.G + pixel.B) / 3f;
                    minVal = Math.Min(minVal, gray);
                    maxVal = Math.Max(maxVal, gray);
                }
            }
            
            float contrast = maxVal - minVal;
            
            float score = 0f;
            // Very high contrast in eye region can indicate makeup
            if (contrast > 180f)
            {
                score += 0.25f;
            }
            
            return (score, 0.5f);
        }
        
        /// <summary>
        /// Convert RGB to HSV
        /// </summary>
        private static (float h, float s, float v) RgbToHsv(Color c)
        {
            float r = c.R / 255f;
            float g = c.G / 255f;
            float b = c.B / 255f;
            
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;
            
            float h = 0f;
            if (delta > 0.0001f)
            {
                if (max == r)
                    h = 60f * (((g - b) / delta) % 6);
                else if (max == g)
                    h = 60f * (((b - r) / delta) + 2);
                else
                    h = 60f * (((r - g) / delta) + 4);
            }
            if (h < 0) h += 360f;
            
            float s = max > 0.0001f ? delta / max : 0f;
            float v = max;
            
            return (h, s, v);
        }
    }
}
