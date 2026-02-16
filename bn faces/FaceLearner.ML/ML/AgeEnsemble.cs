using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace FaceLearner.ML
{
    /// <summary>
    /// Robust age detection using ensemble of multiple methods.
    /// Combines: FairFace Age, Hair Color (gray detection), Skin Texture.
    /// 
    /// Key insight: AI models systematically underestimate age for older people.
    /// - Mick Jagger: AI says 38, actually ~65
    /// - Most models trained on younger faces (dataset bias)
    /// 
    /// By detecting gray hair and skin texture, we can correct this.
    /// </summary>
    public class AgeEnsemble : IDisposable
    {
        private FairFaceDetector _fairFace;
        private bool _disposed;
        
        // Signal structure
        public struct Signal
        {
            public string Source;      // Where this signal came from
            public float Value;        // Age in years
            public float Confidence;   // How confident are we?
            public string Reasoning;   // Why this value?
            
            public override string ToString() => 
                $"{Source}:{Value:F0}y(conf={Confidence:F2})";
        }
        
        public struct EnsembleResult
        {
            public float Age;             // Final age estimate
            public float Confidence;      // Overall confidence
            public List<Signal> Signals;  // All signals used
            public string Decision;       // Explains the decision
            public string AgeGroup;       // "Young", "Middle", "Mature", "Senior"
        }
        
        public bool Initialize(FairFaceDetector fairFace)
        {
            _fairFace = fairFace;
            SubModule.Log("AgeEnsemble: Initialized");
            return true;
        }
        
        /// <summary>
        /// Detect age using all available methods and vote
        /// </summary>
        public EnsembleResult Detect(string imagePath, float[] landmarks,
            float? predetectedAge = null, bool? isFemale = null)
        {
            var signals = new List<Signal>();
            
            // === SIGNAL 1: FairFace Age ===
            float ffAge = predetectedAge ?? 30f;
            float ffConf = 0.5f;
            
            if (_fairFace != null && _fairFace.IsLoaded && predetectedAge == null)
            {
                try
                {
                    using (var bitmap = new Bitmap(imagePath))
                    {
                        var (_, _, age, _, _) = _fairFace.Detect(bitmap);
                        ffAge = age;
                        ffConf = 0.6f; // Base confidence for FairFace age
                    }
                }
                catch { }
            }
            else if (predetectedAge.HasValue)
            {
                ffConf = 0.6f;
            }
            
            signals.Add(new Signal
            {
                Source = "FairFace_Age",
                Value = ffAge,
                Confidence = ffConf,
                Reasoning = $"AI prediction"
            });
            
            // === SIGNAL 2: Hair Color Analysis ===
            var hairAnalysis = AnalyzeHairForAge(imagePath, landmarks);
            if (hairAnalysis.HasSignal)
            {
                signals.Add(new Signal
                {
                    Source = "Hair_Color",
                    Value = hairAnalysis.EstimatedAge,
                    Confidence = hairAnalysis.Confidence,
                    Reasoning = hairAnalysis.Reasoning
                });
            }
            
            // === SIGNAL 3: Skin Texture Analysis ===
            var skinAnalysis = AnalyzeSkinTexture(imagePath, landmarks);
            if (skinAnalysis.HasSignal)
            {
                signals.Add(new Signal
                {
                    Source = "Skin_Texture",
                    Value = skinAnalysis.EstimatedAge,
                    Confidence = skinAnalysis.Confidence,
                    Reasoning = skinAnalysis.Reasoning
                });
            }
            
            // === SIGNAL 4: Face Structure (nasolabial folds, jowls) ===
            var faceAnalysis = AnalyzeFaceStructure(imagePath, landmarks);
            if (faceAnalysis.HasSignal)
            {
                signals.Add(new Signal
                {
                    Source = "Face_Structure",
                    Value = faceAnalysis.EstimatedAge,
                    Confidence = faceAnalysis.Confidence,
                    Reasoning = faceAnalysis.Reasoning
                });
            }
            
            // === COMPUTE FINAL RESULT ===
            return ComputeFinal(signals, ffAge, isFemale);
        }
        
        /// <summary>
        /// Compute final age from all signals
        /// </summary>
        private EnsembleResult ComputeFinal(List<Signal> signals, float ffAge, bool? isFemale)
        {
            var result = new EnsembleResult { Signals = signals };
            
            // Get non-FairFace signals (physical evidence)
            var physicalSignals = signals.Where(s => s.Source != "FairFace_Age").ToList();
            
            // Key insight: If physical signals suggest OLDER than FairFace, trust them
            // FairFace systematically underestimates older people
            
            float maxPhysicalAge = physicalSignals.Count > 0 
                ? (float)physicalSignals.Max(s => s.Value) 
                : ffAge;
            
            float avgPhysicalAge = physicalSignals.Count > 0
                ? (float)physicalSignals.Average(s => s.Value)
                : ffAge;
            
            // Rule 1: Strong gray hair signal → definitely older
            // BUT: Add sanity check - if FairFace says VERY young, "gray hair" might be a hat/cap!
            var hairSignal = signals.FirstOrDefault(s => s.Source == "Hair_Color");
            if (hairSignal.Source != null && hairSignal.Confidence >= 0.7f && hairSignal.Value >= 55f)
            {
                // SANITY CHECK: Gray hair in someone under 30 is VERY rare
                // If FairFace says < 30 but we detect "gray hair", it's probably a hat, cap, or bad detection
                // Carlton Baugh case: Young man with baseball cap detected as "gray hair"
                if (ffAge < 30f)
                {
                    SubModule.Log($"  AgeEnsemble: GRAY HAIR SKEPTICISM - FairFace says {ffAge:F0} (young) but hair says {hairSignal.Value:F0}");
                    SubModule.Log($"    → Likely false positive (hat/cap/background) - NOT overriding to old age");
                    // Don't trust gray hair signal for young faces - skip this rule
                }
                else
                {
                    // FairFace also says older (>30), so gray hair is plausible
                    result.Age = Math.Max(hairSignal.Value, ffAge);
                    result.Confidence = hairSignal.Confidence;
                    result.Decision = $"Gray hair detected → age ≥ {hairSignal.Value:F0}";
                    return FinalizeResult(result);
                }
            }
            
            // Rule 2: Multiple physical signals agree on older age
            if (physicalSignals.Count >= 2)
            {
                float physicalAvg = (float)physicalSignals.Average(s => s.Value);
                float physicalMin = (float)physicalSignals.Min(s => s.Value);
                
                // NEW: Check if Skin_Texture is the only "old" signal with low confidence
                // This catches the case where image noise looks like wrinkles
                var skinTextureSignal = physicalSignals.FirstOrDefault(s => s.Source == "Skin_Texture");
                bool skinTextureUnreliable = skinTextureSignal.Source != null && 
                                             skinTextureSignal.Confidence < 0.35f &&
                                             skinTextureSignal.Reasoning?.Contains("[low quality") == true;
                
                // If only Skin_Texture suggests old and it's unreliable, don't trust it
                if (skinTextureUnreliable)
                {
                    var otherOldSignals = physicalSignals
                        .Where(s => s.Source != "Skin_Texture" && s.Value > ffAge + 10f)
                        .ToList();
                    
                    if (otherOldSignals.Count == 0)
                    {
                        // Only unreliable Skin_Texture says old - trust FairFace instead
                        result.Age = ffAge;
                        result.Confidence = 0.5f;
                        result.Decision = $"Skin texture unreliable (low quality image) → trusting FairFace {ffAge:F0}";
                        return FinalizeResult(result);
                    }
                }
                
                // If physical signals suggest 10+ years older than FairFace
                if (physicalAvg > ffAge + 10f)
                {
                    // Trust physical evidence over AI
                    result.Age = physicalAvg;
                    result.Confidence = 0.75f;
                    result.Decision = $"Physical signals ({physicalSignals.Count}) suggest older: {physicalAvg:F0} vs FF:{ffAge:F0}";
                    return FinalizeResult(result);
                }
            }
            
            // Rule 3: Single strong physical signal suggesting older
            if (maxPhysicalAge > ffAge + 15f && physicalSignals.Any(s => s.Confidence >= 0.6f))
            {
                var strongestOlder = physicalSignals
                    .Where(s => s.Value > ffAge + 10f)
                    .OrderByDescending(s => s.Confidence)
                    .FirstOrDefault();
                
                if (strongestOlder.Source != null)
                {
                    // Blend: 60% physical, 40% FairFace
                    result.Age = strongestOlder.Value * 0.6f + ffAge * 0.4f;
                    result.Confidence = strongestOlder.Confidence * 0.8f;
                    result.Decision = $"{strongestOlder.Source} suggests {strongestOlder.Value:F0}, blended with FF:{ffAge:F0}";
                    return FinalizeResult(result);
                }
            }
            
            // Rule 4: Physical signals suggest younger than FairFace
            // This is rare - usually trust FairFace for younger people
            if (avgPhysicalAge < ffAge - 10f && physicalSignals.Count >= 2)
            {
                // Slight correction toward physical
                result.Age = ffAge * 0.7f + avgPhysicalAge * 0.3f;
                result.Confidence = 0.6f;
                result.Decision = $"Physical suggests younger, slight correction";
                return FinalizeResult(result);
            }
            
            // Rule 5: FairFace says old (>40) BUT no physical aging evidence
            // This catches cases where image quality/compression artifacts are mistaken for wrinkles
            // Example: Young woman with JPEG artifacts → FairFace says 48
            if (ffAge >= 40f)
            {
                var hairSignalCheck = signals.FirstOrDefault(s => s.Source == "Hair_Color");
                var skinSignalCheck = signals.FirstOrDefault(s => s.Source == "Skin_Texture");
                
                // Check if we have evidence of aging
                bool hasGrayHair = hairSignalCheck.Source != null && hairSignalCheck.Value >= 45f && hairSignalCheck.Confidence >= 0.5f;
                bool hasWrinkles = skinSignalCheck.Source != null && skinSignalCheck.Value >= 45f && skinSignalCheck.Confidence >= 0.5f;
                
                // If FairFace says 40+ but NO physical aging signs found
                if (!hasGrayHair && !hasWrinkles)
                {
                    // FairFace is likely wrong - reduce age significantly
                    // The higher FairFace's claim, the more suspicious it is without evidence
                    float reduction = (ffAge - 40f) * 0.6f;  // 48 → reduce by ~5
                    float correctedAge = Math.Max(25f, ffAge - reduction - 10f);  // 48 → ~33
                    
                    result.Age = correctedAge;
                    result.Confidence = 0.55f;
                    result.Decision = $"FairFace says {ffAge:F0} but no aging signs → reduced to {correctedAge:F0}";
                    SubModule.Log($"  AgeEnsemble: OVERESTIMATE CORRECTION - FF={ffAge:F0} but no gray hair or wrinkles");
                    return FinalizeResult(result);
                }
            }
            
            // Rule 6: Default - use weighted average with FairFace bias
            float totalWeight = 0f;
            float weightedSum = 0f;
            
            foreach (var s in signals)
            {
                float weight = s.Confidence;
                // Give extra weight to FairFace for young ages (it's accurate there)
                if (s.Source == "FairFace_Age" && s.Value < 40f)
                    weight *= 1.3f;
                
                weightedSum += s.Value * weight;
                totalWeight += weight;
            }
            
            result.Age = totalWeight > 0 ? weightedSum / totalWeight : ffAge;
            result.Confidence = 0.6f;
            result.Decision = "Weighted average of all signals";
            return FinalizeResult(result);
        }
        
        private EnsembleResult FinalizeResult(EnsembleResult result)
        {
            // Clamp age to reasonable range
            result.Age = Math.Max(18f, Math.Min(80f, result.Age));
            
            // Set age group
            if (result.Age < 30f)
                result.AgeGroup = "Young";
            else if (result.Age < 50f)
                result.AgeGroup = "Middle";
            else if (result.Age < 65f)
                result.AgeGroup = "Mature";
            else
                result.AgeGroup = "Senior";
            
            return result;
        }
        
        /// <summary>
        /// Analyze hair color for gray/white indicating older age
        /// NOW: Detects hats (unusual colors) and baldness (skin-like colors)
        /// </summary>
        private (bool HasSignal, float EstimatedAge, float Confidence, string Reasoning) 
            AnalyzeHairForAge(string imagePath, float[] landmarks)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    // Sample hair region (above eyebrows)
                    int hairY = bitmap.Height / 6;  // Top 1/6 of image
                    int hairX = bitmap.Width / 2;
                    int sampleRadius = Math.Min(30, bitmap.Width / 8);
                    
                    float totalGray = 0f;
                    float totalWhite = 0f;
                    float totalColorful = 0f;
                    float totalSkinLike = 0f;  // NEW: Bald detection
                    float totalHighSat = 0f;   // NEW: Hat detection (red/blue hats)
                    int samples = 0;
                    
                    for (int dy = -sampleRadius; dy <= sampleRadius; dy += 3)
                    {
                        for (int dx = -sampleRadius; dx <= sampleRadius; dx += 3)
                        {
                            int x = hairX + dx;
                            int y = hairY + dy;
                            
                            if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
                                continue;
                            
                            var color = bitmap.GetPixel(x, y);
                            float r = color.R, g = color.G, b = color.B;
                            float avg = (r + g + b) / 3f;
                            float saturation = (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b))) / 
                                              (Math.Max(r, Math.Max(g, b)) + 0.001f);
                            
                            // NEW: Detect highly saturated colors (hats, headwear)
                            // Red cardinal hat, blue cap, etc.
                            if (saturation > 0.50f)
                            {
                                totalHighSat++;
                            }
                            
                            // NEW: Detect skin-like colors (bald head)
                            // Skin is typically: R > G > B, low saturation, medium brightness
                            bool isSkinLike = r > g && g > b && 
                                             saturation < 0.35f && 
                                             avg > 100f && avg < 220f;
                            if (isSkinLike)
                            {
                                totalSkinLike++;
                            }
                            
                            // White/very light gray hair: high luminance, low saturation
                            if (avg > 180f && saturation < 0.15f)
                                totalWhite++;
                            // Gray hair: medium luminance, low saturation
                            else if (avg > 100f && avg < 180f && saturation < 0.20f)
                                totalGray++;
                            // Colorful hair (not gray)
                            else if (saturation > 0.25f || avg < 80f)
                                totalColorful++;
                            
                            samples++;
                        }
                    }
                    
                    if (samples < 10) 
                        return (false, 0f, 0f, "Not enough samples");
                    
                    float highSatRatio = totalHighSat / samples;
                    float skinLikeRatio = totalSkinLike / samples;
                    float whiteRatio = totalWhite / samples;
                    float grayRatio = totalGray / samples;
                    float grayTotal = whiteRatio + grayRatio;
                    
                    // NEW: Check for hat (high saturation in hair region)
                    // Cardinal's red hat, baseball cap, etc.
                    if (highSatRatio > 0.30f)
                    {
                        // Likely wearing a hat - hair signal unreliable
                        return (false, 0f, 0f, $"Hat detected ({highSatRatio:P0} high-sat pixels) - hair unreliable");
                    }
                    
                    // NEW: Check for baldness (skin-like color in hair region)
                    if (skinLikeRatio > 0.40f)
                    {
                        // Person appears bald - no hair to analyze
                        // This is IMPORTANT: bald people can still be old!
                        // Return a weak "possibly older" signal since baldness correlates with age
                        return (true, 45f, 0.30f, $"Bald ({skinLikeRatio:P0} skin-like) - weak older signal");
                    }
                    
                    // Interpret results
                    if (whiteRatio > 0.40f)
                    {
                        // Predominantly white hair → likely 65+
                        return (true, 70f, 0.80f, $"White hair ({whiteRatio:P0})");
                    }
                    else if (grayTotal > 0.50f)
                    {
                        // Majority gray/white → likely 55-70
                        float age = 55f + grayTotal * 20f;
                        return (true, age, 0.75f, $"Gray hair ({grayTotal:P0})");
                    }
                    else if (grayTotal > 0.25f)
                    {
                        // Some gray → likely 45-60
                        float age = 45f + grayTotal * 40f;
                        return (true, age, 0.60f, $"Some gray ({grayTotal:P0})");
                    }
                    else if (grayTotal > 0.10f)
                    {
                        // Slight gray → possibly 40-50
                        return (true, 45f, 0.40f, $"Slight gray ({grayTotal:P0})");
                    }
                    
                    // No significant gray detected
                    return (false, 0f, 0f, "No gray hair detected");
                }
            }
            catch
            {
                return (false, 0f, 0f, "Analysis failed");
            }
        }
        
        /// <summary>
        /// Analyze skin texture for wrinkles/aging signs
        /// Uses edge detection in face regions
        /// NOW: Adjusts confidence based on image quality (noisy images = less reliable)
        /// </summary>
        private (bool HasSignal, float EstimatedAge, float Confidence, string Reasoning) 
            AnalyzeSkinTexture(string imagePath, float[] landmarks)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    int w = bitmap.Width;
                    int h = bitmap.Height;
                    
                    // === STEP 1: Estimate image quality ===
                    // High-frequency noise/JPEG artifacts look like "texture" but aren't wrinkles
                    float imageQuality = EstimateImageQuality(bitmap);
                    
                    // Analyze forehead and cheek regions for wrinkle lines
                    // High local contrast = more wrinkles = older
                    
                    // Forehead region (upper middle)
                    var foreheadContrast = CalculateLocalContrast(bitmap, w/2, h/5, w/6);
                    
                    // Cheek regions
                    var leftCheekContrast = CalculateLocalContrast(bitmap, w/3, h/2, w/8);
                    var rightCheekContrast = CalculateLocalContrast(bitmap, 2*w/3, h/2, w/8);
                    
                    float avgContrast = (foreheadContrast + leftCheekContrast + rightCheekContrast) / 3f;
                    
                    // === STEP 2: Adjust confidence based on image quality ===
                    // Poor quality (< 0.5) = texture might be noise, not wrinkles
                    float qualityMultiplier = imageQuality;
                    string qualityNote = imageQuality < 0.5f ? " [low quality img]" : "";
                    
                    // Map contrast to age
                    // Low contrast (smooth skin) = younger
                    // High contrast (wrinkles) = older
                    // Typical range: 5-25 for smooth to wrinkled
                    
                    if (avgContrast > 20f)
                    {
                        // High texture = likely older (but might be image noise!)
                        float age = 55f + (avgContrast - 20f) * 1.5f;
                        float conf = 0.55f * qualityMultiplier;
                        return (conf > 0.2f, Math.Min(75f, age), conf, $"High skin texture ({avgContrast:F1}){qualityNote}");
                    }
                    else if (avgContrast > 15f)
                    {
                        // Medium-high texture
                        float age = 45f + (avgContrast - 15f) * 2f;
                        float conf = 0.45f * qualityMultiplier;
                        return (conf > 0.2f, age, conf, $"Medium skin texture ({avgContrast:F1}){qualityNote}");
                    }
                    else if (avgContrast < 8f)
                    {
                        // Very smooth = likely younger (quality doesn't matter here)
                        return (true, 30f, 0.40f, $"Smooth skin ({avgContrast:F1})");
                    }
                    
                    // Inconclusive
                    return (false, 0f, 0f, $"Normal texture ({avgContrast:F1})");
                }
            }
            catch
            {
                return (false, 0f, 0f, "Analysis failed");
            }
        }
        
        /// <summary>
        /// Estimate image quality - low quality images have noise/compression artifacts
        /// that look like "texture" but aren't real wrinkles
        /// Returns 0.0 (very poor) to 1.0 (excellent)
        /// </summary>
        private float EstimateImageQuality(Bitmap bitmap)
        {
            try
            {
                int w = bitmap.Width;
                int h = bitmap.Height;
                
                // Sample a small region that should be relatively smooth (upper cheek area)
                // Real wrinkles are larger-scale; noise/compression is high-frequency
                int sampleX = w / 2;
                int sampleY = h / 3;
                int sampleSize = Math.Min(20, w / 10);
                
                // Calculate micro-variance (pixel-to-pixel differences)
                // High micro-variance = noisy image
                float totalMicroVariance = 0f;
                int samples = 0;
                
                for (int y = sampleY; y < sampleY + sampleSize && y < h - 1; y++)
                {
                    for (int x = sampleX; x < sampleX + sampleSize && x < w - 1; x++)
                    {
                        var c1 = bitmap.GetPixel(x, y);
                        var c2 = bitmap.GetPixel(x + 1, y);
                        var c3 = bitmap.GetPixel(x, y + 1);
                        
                        float l1 = (c1.R + c1.G + c1.B) / 3f;
                        float l2 = (c2.R + c2.G + c2.B) / 3f;
                        float l3 = (c3.R + c3.G + c3.B) / 3f;
                        
                        // Pixel-to-pixel difference
                        totalMicroVariance += Math.Abs(l1 - l2) + Math.Abs(l1 - l3);
                        samples++;
                    }
                }
                
                if (samples == 0) return 0.5f;
                
                float avgMicroVariance = totalMicroVariance / samples;
                
                // Map micro-variance to quality
                // Low variance (< 3) = smooth, high quality
                // Medium variance (3-8) = some noise
                // High variance (> 8) = noisy/compressed, low quality
                
                if (avgMicroVariance < 3f)
                    return 1.0f;  // Excellent quality
                else if (avgMicroVariance < 5f)
                    return 0.8f;  // Good quality
                else if (avgMicroVariance < 8f)
                    return 0.6f;  // Medium quality
                else if (avgMicroVariance < 12f)
                    return 0.4f;  // Poor quality - texture signals unreliable
                else
                    return 0.2f;  // Very poor - heavy noise/compression
            }
            catch
            {
                return 0.5f;  // Unknown, use default
            }
        }
        
        /// <summary>
        /// Calculate local contrast in a region (indicates texture/wrinkles)
        /// </summary>
        private float CalculateLocalContrast(Bitmap bitmap, int cx, int cy, int radius)
        {
            float totalVariance = 0f;
            int samples = 0;
            
            for (int y = cy - radius; y <= cy + radius; y += 2)
            {
                for (int x = cx - radius; x <= cx + radius; x += 2)
                {
                    if (x < 1 || x >= bitmap.Width - 1 || y < 1 || y >= bitmap.Height - 1)
                        continue;
                    
                    // Get 3x3 neighborhood luminance
                    float center = GetLuminance(bitmap.GetPixel(x, y));
                    float top = GetLuminance(bitmap.GetPixel(x, y - 1));
                    float bottom = GetLuminance(bitmap.GetPixel(x, y + 1));
                    float left = GetLuminance(bitmap.GetPixel(x - 1, y));
                    float right = GetLuminance(bitmap.GetPixel(x + 1, y));
                    
                    // Laplacian (edge detection)
                    float laplacian = Math.Abs(top + bottom + left + right - 4 * center);
                    totalVariance += laplacian;
                    samples++;
                }
            }
            
            return samples > 0 ? totalVariance / samples : 0f;
        }
        
        private float GetLuminance(Color c)
        {
            return 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
        }
        
        /// <summary>
        /// Analyze face structure for age-related changes
        /// (nasolabial folds, jowls, etc.)
        /// </summary>
        private (bool HasSignal, float EstimatedAge, float Confidence, string Reasoning) 
            AnalyzeFaceStructure(string imagePath, float[] landmarks)
        {
            // This is harder to detect reliably without a trained model
            // For now, return no signal - can be enhanced later
            
            if (landmarks == null || landmarks.Length < 936)
                return (false, 0f, 0f, "No landmarks");
            
            // Could analyze:
            // - Distance from nose to mouth corners (nasolabial fold depth)
            // - Jaw line smoothness (jowls)
            // - Eye area (crow's feet region texture)
            
            // For now, skip this - hair and skin texture are more reliable
            return (false, 0f, 0f, "Not implemented");
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
