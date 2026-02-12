using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace FaceLearner.ML
{
    /// <summary>
    /// Robust skin tone detection using ensemble of multiple methods.
    /// Combines: RGB Direct, FairFace Race, Hair/Eye correlation.
    /// 
    /// Key insight: Each method has different failure modes:
    /// - RGB Direct: Fails on unusual lighting
    /// - FairFace: Can misclassify race
    /// - Hair/Eye: Only hints, not definitive
    /// 
    /// By combining them with proper voting, we get much better accuracy.
    /// </summary>
    public class ColorEnsemble : IDisposable
    {
        private FairFaceDetector _fairFace;
        private bool _disposed;
        
        // Signal structure
        public struct Signal
        {
            public string Source;      // Where this signal came from
            public float Value;        // 0.0 (pale) to 1.0 (dark)
            public float Confidence;   // How confident are we?
            public string Reasoning;   // Why this value?
            
            public override string ToString() => 
                $"{Source}:{Value:F2}(conf={Confidence:F2})";
        }
        
        public struct EnsembleResult
        {
            public float Value;           // Final skin tone 0-1
            public float Confidence;      // Overall confidence
            public List<Signal> Signals;  // All signals used
            public string Decision;       // Explains the decision
        }
        
        // Race to skin tone ranges
        // v2.8.13 UPDATE: Better granularity for medium-dark tones
        // Bannerlord scale: 0 = LIGHT skin (pale), 1 = DARK skin (dark brown/black)
        // 
        // Skin Tone Scale (visual reference):
        // 0.00-0.15: Very pale (porcelain, ivory)
        // 0.15-0.30: Light (fair, cream)
        // 0.30-0.45: Light-Medium (beige, light tan)
        // 0.45-0.60: Medium (olive, tan, café au lait)
        // 0.60-0.75: Medium-Dark (caramel, brown)
        // 0.75-0.90: Dark (dark brown, espresso)
        // 0.90-1.00: Very Dark (deep brown, ebony)
        //
        private static readonly Dictionary<string, (float Min, float Max)> RaceSkinRanges = 
            new Dictionary<string, (float, float)>
        {
            { "White", (0.05f, 0.25f) },           // Light skin (tightened max)
            { "East Asian", (0.10f, 0.35f) },      // Light to light-medium
            { "Southeast Asian", (0.25f, 0.50f) }, // Medium range
            { "Middle Eastern", (0.20f, 0.50f) },  // Light-medium to medium
            { "Latino_Hispanic", (0.25f, 0.55f) }, // Medium range (tightened)
            { "Latino", (0.25f, 0.55f) },          // Same as above
            { "Indian", (0.35f, 0.70f) },          // Medium to medium-dark
            { "Black", (0.65f, 0.95f) },           // Dark skin (raised min to 0.65!)
        };
        
        public bool Initialize(FairFaceDetector fairFace)
        {
            _fairFace = fairFace;
            SubModule.Log("ColorEnsemble: Initialized");
            return true;
        }
        
        /// <summary>
        /// Detect skin tone using all available methods and vote
        /// </summary>
        public EnsembleResult Detect(string imagePath, float[] landmarks, 
            float? predetectedSkinTone = null, string fairFaceRace = null, float fairFaceConf = 0f)
        {
            var signals = new List<Signal>();
            
            // === SIGNAL 1: RGB Direct (pixel sampling) ===
            var (rgbDirect, isDarkImage) = predetectedSkinTone.HasValue 
                ? (predetectedSkinTone.Value, false) 
                : DetectRGBDirect(imagePath, landmarks);
            float rgbConfidence = 0.6f; // Base confidence for RGB
            
            signals.Add(new Signal
            {
                Source = "RGB_Direct",
                Value = rgbDirect,
                Confidence = rgbConfidence,
                Reasoning = isDarkImage ? "Pixel sampling (DARK image corrected)" : "Pixel sampling avg"
            });
            
            // === SIGNAL 2: FairFace Race ===
            string race = fairFaceRace;
            float raceConf = fairFaceConf;
            
            if (_fairFace != null && _fairFace.IsLoaded && string.IsNullOrEmpty(race))
            {
                try
                {
                    using (var bitmap = new Bitmap(imagePath))
                    {
                        var (_, _, _, ffRace, ffRaceConf) = _fairFace.Detect(bitmap);
                        race = ffRace;
                        raceConf = ffRaceConf;
                    }
                }
                catch { }
            }
            
            if (!string.IsNullOrEmpty(race) && RaceSkinRanges.TryGetValue(race, out var range))
            {
                // Map race to middle of expected range
                float raceMidpoint = (range.Min + range.Max) / 2f;
                
                signals.Add(new Signal
                {
                    Source = "FairFace_Race",
                    Value = raceMidpoint,
                    Confidence = raceConf,
                    Reasoning = $"{race} → range [{range.Min:F2}-{range.Max:F2}]"
                });
            }
            
            // === SIGNAL 3: RGB Analysis for dark skin detection ===
            // v2.7.5 FIX: Now HIGH rgbDirect = dark skin
            // Check if this might be actual dark skin
            if (rgbDirect > 0.60f)
            {
                var darkSkinCheck = CheckForDarkSkin(imagePath, landmarks);
                if (darkSkinCheck.IsDarkSkin)
                {
                    signals.Add(new Signal
                    {
                        Source = "DarkSkin_Check",
                        Value = darkSkinCheck.EstimatedTone,
                        Confidence = darkSkinCheck.Confidence,
                        Reasoning = darkSkinCheck.Reasoning
                    });
                }
            }
            
            // === COMPUTE FINAL RESULT ===
            return ComputeFinal(signals, race, raceConf, rgbDirect, isDarkImage);
        }
        
        /// <summary>
        /// Compute final skin tone from all signals
        /// </summary>
        private EnsembleResult ComputeFinal(List<Signal> signals, string race, float raceConf, float rgbDirect, bool isDarkImage)
        {
            var result = new EnsembleResult { Signals = signals };
            
            // v2.8.19: Handle dark/underexposed images
            // FairFace is unreliable on dark images - often misclassifies white people as Black
            // Trust RGB correction more in these cases
            
            if (isDarkImage)
            {
                SubModule.Log($"  SkinTone: DARK IMAGE - FairFace race detection unreliable, trusting corrected RGB");
                
                // For dark images with Black classification, be very skeptical
                if (race == "Black" && rgbDirect < 0.55f)
                {
                    // Corrected RGB says lighter skin than Black would have
                    // This is likely a white person in dark lighting (like the bearded man)
                    result.Value = rgbDirect * 0.85f; // Bias 15% lighter
                    result.Confidence = 0.50f;
                    result.Decision = $"DARK image + Black ({raceConf:F2}) but corrected RGB={rgbDirect:F2} → likely misclassified white";
                    SubModule.Log($"  SkinTone: {result.Decision} → {result.Value:F2}");
                    return Clamp(result);
                }
                
                // For dark images with any race, reduce race confidence and trust RGB more
                raceConf *= 0.6f; // Reduce race confidence by 40% for dark images
            }
            
            // Check for strong agreement first
            if (signals.Count >= 2)
            {
                float avgValue = (float)signals.Average(s => s.Value);
                float maxDev = (float)signals.Max(s => Math.Abs(s.Value - avgValue));
                
                if (maxDev < 0.15f)
                {
                    // Signals agree - use weighted average
                    result.Value = WeightedAverage(signals);
                    result.Confidence = 0.85f;
                    result.Decision = "All signals agree";
                    return Clamp(result);
                }
            }
            
            // === HANDLE LATINO MISCLASSIFICATION ===
            // FairFace frequently misclassifies white people in poor lighting as "Latino"
            // If RGB suggests light-medium skin (< 0.50) and race is Latino, be skeptical
            if ((race == "Latino" || race == "Latino_Hispanic") && rgbDirect < 0.50f)
            {
                // RGB suggests lighter skin than typical Latino
                // Bias towards lighter skin - likely misclassified white person
                float conservativeValue = rgbDirect * 0.85f; // Shift 15% lighter
                
                // Lower race confidence threshold for light-skinned "Latinos"
                if (raceConf < 0.92f)
                {
                    result.Value = conservativeValue;
                    result.Confidence = 0.55f;
                    result.Decision = $"Latino ({raceConf:F2}) with light RGB ({rgbDirect:F2}) → likely misclassified white";
                    SubModule.Log($"  SkinTone: {result.Decision} → {result.Value:F2}");
                    return Clamp(result);
                }
            }
            
            // === HANDLE WHITE RACE ===
            // If FairFace says White with good confidence, use tighter bounds
            if (race == "White" && raceConf >= 0.60f)
            {
                // White skin: 0.05 - 0.30
                float whiteValue = Math.Min(0.30f, rgbDirect);
                result.Value = whiteValue;
                result.Confidence = raceConf;
                result.Decision = $"White (conf={raceConf:F2}) → max 0.30, RGB={rgbDirect:F2}";
                SubModule.Log($"  SkinTone: {result.Decision} → {result.Value:F2}");
                return Clamp(result);
            }
            
            // === RACE-BASED BOUNDS (for non-White, non-Latino-misclassified) ===
            if (RaceSkinRanges.TryGetValue(race, out var bounds) && raceConf >= 0.50f)
            {
                // Clamp RGB value within race bounds, weighted by confidence
                float clampedValue = Math.Max(bounds.Min, Math.Min(bounds.Max, rgbDirect));
                
                // Blend between raw RGB and clamped value based on race confidence
                // Higher confidence = more clamping to race bounds
                result.Value = rgbDirect * (1f - raceConf) + clampedValue * raceConf;
                result.Confidence = 0.6f + raceConf * 0.3f;
                result.Decision = $"{race} (conf={raceConf:F2}) → bounds [{bounds.Min:F2}-{bounds.Max:F2}], RGB={rgbDirect:F2}";
                SubModule.Log($"  SkinTone: {result.Decision} → {result.Value:F2}");
                return Clamp(result);
            }
            
            // === SPECIAL CASES ===
            
            // Dark skin verification - if RGB suggests dark but race says light, verify
            if (rgbDirect > 0.55f && (race == "White" || race == "East Asian") && raceConf < 0.70f)
            {
                // RGB says dark, race says light but low confidence - trust RGB
                result.Value = rgbDirect;
                result.Confidence = 0.55f;
                result.Decision = $"RGB dark ({rgbDirect:F2}) overrides low-conf {race} ({raceConf:F2})";
                return Clamp(result);
            }
            
            // High-confidence Black → ensure minimum darkness but allow gradation
            if (race == "Black" && raceConf >= 0.70f)
            {
                // Allow full range 0.55-0.95, but ensure minimum 0.55
                float darkness = Math.Max(0.55f, rgbDirect);
                result.Value = darkness;
                result.Confidence = raceConf;
                result.Decision = $"Black (conf={raceConf:F2}) → min 0.55, using {darkness:F2}";
                return Clamp(result);
            }
            
            // DarkSkin_Check signal present → use it
            var darkSkinSignal = signals.FirstOrDefault(s => s.Source == "DarkSkin_Check");
            if (darkSkinSignal.Source != null && darkSkinSignal.Confidence > 0.5f)
            {
                result.Value = darkSkinSignal.Value;
                result.Confidence = darkSkinSignal.Confidence;
                result.Decision = $"Dark skin check: {darkSkinSignal.Reasoning}";
                return Clamp(result);
            }
            
            // Fallback: trust RGB but bias slightly lighter for ambiguous cases
            result.Value = rgbDirect * 0.9f; // 10% lighter bias
            result.Confidence = 0.5f;
            result.Decision = $"Fallback to RGB ({rgbDirect:F2}) with light bias";
            return Clamp(result);
        }
        
        /// <summary>
        /// Check if image contains dark skin that was misdetected as light
        /// </summary>
        private (bool IsDarkSkin, float EstimatedTone, float Confidence, string Reasoning) 
            CheckForDarkSkin(string imagePath, float[] landmarks)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    // Sample skin area (cheeks)
                    var samples = new List<Color>();
                    
                    if (landmarks != null && landmarks.Length >= 136)
                    {
                        // Sample from cheek landmarks (1-4 and 12-15 for dlib 68)
                        int[] cheekIndices = { 1, 2, 3, 4, 12, 13, 14, 15 };
                        foreach (int i in cheekIndices)
                        {
                            int x = (int)(landmarks[i * 2] * bitmap.Width);
                            int y = (int)(landmarks[i * 2 + 1] * bitmap.Height);
                            
                            if (x > 0 && x < bitmap.Width && y > 0 && y < bitmap.Height)
                            {
                                samples.Add(bitmap.GetPixel(x, y));
                            }
                        }
                    }
                    
                    if (samples.Count < 3)
                    {
                        // Fallback: sample center
                        samples.Add(bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2));
                    }
                    
                    // Calculate average RGB
                    float avgR = (float)samples.Average(c => c.R);
                    float avgG = (float)samples.Average(c => c.G);
                    float avgB = (float)samples.Average(c => c.B);
                    float avgRGB = (avgR + avgG + avgB) / 3f;
                    
                    // Dark skin characteristics:
                    // - avgRGB < 80 (dark pixels)
                    // - R > G > B (warm undertone typical of melanin)
                    bool isDark = avgRGB < 80f;
                    bool hasWarmUndertone = avgR > avgG && avgG > avgB;
                    
                    if (isDark && hasWarmUndertone)
                    {
                        // v2.7.5 FIX: Map avgRGB 30-80 to skin tone 0.65-0.90 (HIGH = dark)
                        float darkness = Math.Max(0, (80f - avgRGB) / 50f);
                        float estimatedTone = 0.65f + darkness * 0.25f;  // 0.65-0.90
                        
                        return (true, estimatedTone, 0.75f, 
                            $"avgRGB={avgRGB:F0}, warm undertone → dark skin");
                    }
                    else if (isDark)
                    {
                        // Dark but no clear warm undertone - might be shadow or dark skin
                        float estimatedTone = 0.55f + (80f - avgRGB) / 80f * 0.30f;
                        return (true, estimatedTone, 0.5f, 
                            $"avgRGB={avgRGB:F0}, possibly dark skin or shadow");
                    }
                }
            }
            catch { }
            
            return (false, 0f, 0f, "");
        }
        
        /// <summary>
        /// Detect skin tone directly from RGB values
        /// Simple mapping without "underexposed" heuristics
        /// </summary>
        /// <summary>
        /// Detect skin tone from RGB pixel sampling.
        /// Returns (skinTone, isDarkImage) tuple.
        /// </summary>
        private (float skinTone, bool isDarkImage) DetectRGBDirect(string imagePath, float[] landmarks)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    var samples = new List<float>();
                    var rawRGBs = new List<float>();
                    
                    // Sample from multiple skin regions
                    int w = bitmap.Width;
                    int h = bitmap.Height;
                    
                    // Center regions (likely face)
                    int[][] regions = {
                        new[] { w/2, h/3 },      // Forehead
                        new[] { w/3, h/2 },      // Left cheek
                        new[] { 2*w/3, h/2 },    // Right cheek
                        new[] { w/2, 2*h/3 },    // Lower face
                    };
                    
                    // Calculate overall image brightness to detect dark images
                    float totalBrightness = 0f;
                    int sampleCount = 0;
                    
                    foreach (var region in regions)
                    {
                        int x = Math.Max(0, Math.Min(w-1, region[0]));
                        int y = Math.Max(0, Math.Min(h-1, region[1]));
                        
                        var color = bitmap.GetPixel(x, y);
                        float avgRGB = (color.R + color.G + color.B) / 3f;
                        rawRGBs.Add(avgRGB);
                        totalBrightness += avgRGB;
                        sampleCount++;
                    }
                    
                    float avgBrightness = totalBrightness / sampleCount;
                    
                    // Detect dark/underexposed image
                    // Normal face images have avgBrightness ~120-180
                    // Dark images have avgBrightness < 100
                    bool isDarkImage = avgBrightness < 100f;
                    float brightnessCorrection = 1f;
                    
                    if (isDarkImage)
                    {
                        // Image is underexposed - normalize brightness
                        // Target brightness ~140 for normal exposure
                        brightnessCorrection = 140f / Math.Max(avgBrightness, 30f);
                        brightnessCorrection = Math.Min(brightnessCorrection, 2.0f); // Cap at 2x correction
                        SubModule.Log($"  RGB: Dark image detected (avg={avgBrightness:F0}), correction={brightnessCorrection:F2}");
                    }
                    
                    foreach (var rawRGB in rawRGBs)
                    {
                        // Apply brightness correction for dark images
                        float correctedRGB = Math.Min(255f, rawRGB * brightnessCorrection);
                        
                        // v2.9.1 FIX: Better calibration for skin tones
                        // Bannerlord uses 0=LIGHT, 1=DARK
                        // Typical skin pixel values:
                        //   Light skin (White): avgRGB ~180-220 → skinTone 0.05-0.20
                        //   Medium skin: avgRGB ~140-180 → skinTone 0.20-0.45
                        //   Olive/Tan: avgRGB ~110-140 → skinTone 0.45-0.60
                        //   Brown: avgRGB ~70-110 → skinTone 0.60-0.75
                        //   Dark: avgRGB ~40-70 → skinTone 0.75-0.90
                        
                        float skinTone;
                        if (correctedRGB >= 180f)
                        {
                            // Light skin: 180-255 → 0.05-0.20
                            skinTone = 0.20f - (correctedRGB - 180f) / 75f * 0.15f;
                        }
                        else if (correctedRGB >= 140f)
                        {
                            // Medium-light skin: 140-180 → 0.20-0.40
                            skinTone = 0.40f - (correctedRGB - 140f) / 40f * 0.20f;
                        }
                        else if (correctedRGB >= 110f)
                        {
                            // Medium skin: 110-140 → 0.40-0.55
                            skinTone = 0.55f - (correctedRGB - 110f) / 30f * 0.15f;
                        }
                        else if (correctedRGB >= 70f)
                        {
                            // Brown skin: 70-110 → 0.55-0.75
                            skinTone = 0.75f - (correctedRGB - 70f) / 40f * 0.20f;
                        }
                        else
                        {
                            // Dark skin: 40-70 → 0.75-0.90
                            skinTone = 0.90f - (correctedRGB - 40f) / 30f * 0.15f;
                            skinTone = Math.Min(0.95f, skinTone); // Cap at 0.95
                        }
                        
                        skinTone = Math.Max(0.05f, Math.Min(0.95f, skinTone));
                        samples.Add(skinTone);
                    }
                    
                    float result = (float)samples.Average();
                    
                    // v2.8.18: More detailed logging
                    if (isDarkImage)
                    {
                        SubModule.Log($"  RGB Direct: rawAvg={rawRGBs.Average():F0} (DARK) corrected→{samples.Average()*255/0.85:F0} → skinTone={result:F2}");
                    }
                    else
                    {
                        SubModule.Log($"  RGB Direct: rawAvg={rawRGBs.Average():F0} → skinTone={result:F2}");
                    }
                    
                    return (result, isDarkImage);
                }
            }
            catch
            {
                return (0.30f, false); // Default to medium-light
            }
        }
        
        private float WeightedAverage(List<Signal> signals)
        {
            float sumWeighted = 0f;
            float sumWeights = 0f;
            
            foreach (var s in signals)
            {
                float weight = s.Confidence;
                sumWeighted += s.Value * weight;
                sumWeights += weight;
            }
            
            return sumWeights > 0 ? sumWeighted / sumWeights : 0.3f;
        }
        
        private EnsembleResult Clamp(EnsembleResult result)
        {
            result.Value = Math.Max(0.05f, Math.Min(0.95f, result.Value));
            return result;
        }
        
        #region Eye Color Detection
        
        /// <summary>
        /// Detect eye color from image
        /// Returns: 0 = light (blue/green), 1 = dark (brown/black)
        /// Bannerlord: 0 = light, 1 = dark (same direction!)
        /// </summary>
        public EnsembleResult DetectEyeColor(string imagePath, float[] landmarks)
        {
            var result = new EnsembleResult { Signals = new List<Signal>() };
            
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    int w = bitmap.Width;
                    int h = bitmap.Height;
                    
                    // Approximate eye positions from image center
                    // Eyes are typically at ~30% from top, 35% and 65% from sides
                    int leftEyeX = (int)(w * 0.35f);
                    int rightEyeX = (int)(w * 0.65f);
                    int eyeY = (int)(h * 0.38f);
                    
                    // Sample iris colors (looking for darkest pixels in eye region)
                    float leftIris = SampleIrisRegion(bitmap, leftEyeX, eyeY, 4);
                    float rightIris = SampleIrisRegion(bitmap, rightEyeX, eyeY, 4);
                    float avgIris = (leftIris + rightIris) / 2f;
                    
                    // Map luminance to eye color
                    // Dark iris (low luminance) = brown/black = high value
                    // Light iris (high luminance) = blue/green = low value
                    float eyeColor = 1.0f - (avgIris / 180f);
                    eyeColor = Math.Max(0.0f, Math.Min(1.0f, eyeColor));
                    
                    result.Signals.Add(new Signal
                    {
                        Source = "IrisLuminance",
                        Value = eyeColor,
                        Confidence = 0.7f,
                        Reasoning = $"Avg iris luminance: {avgIris:F0}"
                    });
                    
                    result.Value = eyeColor;
                    result.Confidence = 0.7f;
                    result.Decision = $"Eye color from iris sampling: {eyeColor:F2}";
                }
            }
            catch
            {
                result.Value = 0.5f;
                result.Confidence = 0.3f;
                result.Decision = "Eye detection failed - using default";
            }
            
            return result;
        }
        
        private float SampleIrisRegion(Bitmap bitmap, int cx, int cy, int radius)
        {
            float minLuminance = 255f;
            float totalDark = 0f;
            int darkCount = 0;
            
            // First pass: find minimum luminance (pupil/iris)
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
                        continue;
                    
                    var c = bitmap.GetPixel(x, y);
                    float lum = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
                    if (lum < minLuminance) minLuminance = lum;
                }
            }
            
            // Second pass: average dark pixels (iris area)
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
                        continue;
                    
                    var c = bitmap.GetPixel(x, y);
                    float lum = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
                    if (lum < minLuminance + 50)
                    {
                        totalDark += lum;
                        darkCount++;
                    }
                }
            }
            
            return darkCount > 0 ? totalDark / darkCount : 128f;
        }
        
        #endregion
        
        #region Hair Color Detection
        
        /// <summary>
        /// Detect hair color from image
        /// Returns: (color, wasDetected)
        /// Color: 0 = light (blonde), 1 = dark (black)
        /// wasDetected: false if likely bald
        /// Bannerlord: 0 = light, 1 = dark (same direction!)
        /// </summary>
        public (EnsembleResult result, bool hairDetected) DetectHairColor(string imagePath, float[] landmarks, float skinTone)
        {
            var result = new EnsembleResult { Signals = new List<Signal>() };
            bool hairDetected = true;
            
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    int w = bitmap.Width;
                    int h = bitmap.Height;
                    
                    // Sample hair region (top of image, above forehead)
                    int hairX = w / 2;
                    int hairY = h / 6;  // Top 1/6 of image
                    int sampleRadius = Math.Min(25, w / 8);
                    
                    float totalR = 0, totalG = 0, totalB = 0;
                    int samples = 0;
                    
                    for (int dy = -sampleRadius; dy <= sampleRadius; dy += 2)
                    {
                        for (int dx = -sampleRadius; dx <= sampleRadius; dx += 2)
                        {
                            int x = hairX + dx;
                            int y = hairY + dy;
                            if (x < 0 || x >= w || y < 0 || y >= h)
                                continue;
                            
                            var c = bitmap.GetPixel(x, y);
                            totalR += c.R;
                            totalG += c.G;
                            totalB += c.B;
                            samples++;
                        }
                    }
                    
                    if (samples > 0)
                    {
                        float avgR = totalR / samples;
                        float avgG = totalG / samples;
                        float avgB = totalB / samples;
                        float hairLuminance = 0.299f * avgR + 0.587f * avgG + 0.114f * avgB;
                        
                        // Check if hair region looks like skin (bald detection)
                        // Sample skin region for comparison
                        float skinLuminance = GetSkinLuminance(bitmap, w / 2, (int)(h * 0.4f), 10);
                        float lumDiff = Math.Abs(hairLuminance - skinLuminance);
                        
                        if (lumDiff < 25)
                        {
                            // Hair region looks like skin = likely bald
                            hairDetected = false;
                            result.Value = skinTone;  // Match skin for bald
                            result.Confidence = 0.5f;
                            result.Decision = "Likely bald - hair matches skin tone";
                        }
                        else
                        {
                            // Hair detected
                            // Map luminance: light hair (high lum) = low value, dark hair = high value
                            float hairColor = 1.0f - (hairLuminance / 255f);
                            hairColor = Math.Max(0.0f, Math.Min(1.0f, hairColor));
                            
                            result.Signals.Add(new Signal
                            {
                                Source = "HairLuminance",
                                Value = hairColor,
                                Confidence = 0.75f,
                                Reasoning = $"Hair luminance: {hairLuminance:F0}"
                            });
                            
                            result.Value = hairColor;
                            result.Confidence = 0.75f;
                            result.Decision = $"Hair color: {hairColor:F2} (lum={hairLuminance:F0})";
                        }
                    }
                }
            }
            catch
            {
                result.Value = 0.5f;
                result.Confidence = 0.3f;
                result.Decision = "Hair detection failed - using default";
                hairDetected = false;
            }
            
            return (result, hairDetected);
        }
        
        private float GetSkinLuminance(Bitmap bitmap, int cx, int cy, int radius)
        {
            float total = 0f;
            int count = 0;
            
            for (int dy = -radius; dy <= radius; dy += 2)
            {
                for (int dx = -radius; dx <= radius; dx += 2)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
                        continue;
                    
                    var c = bitmap.GetPixel(x, y);
                    total += 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
                    count++;
                }
            }
            
            return count > 0 ? total / count : 128f;
        }
        
        #endregion
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
