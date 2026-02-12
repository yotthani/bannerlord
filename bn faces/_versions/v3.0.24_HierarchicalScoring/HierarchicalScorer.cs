using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Hierarchical scorer that evaluates facial similarity level by level.
    /// 
    /// Key principle: If foundational features don't match, higher-level 
    /// features contribute less to the score (gating mechanism).
    /// 
    /// A perfect eye in a wrong face shape should NOT result in a good score!
    /// </summary>
    public class HierarchicalScorer
    {
        private readonly FeatureExtractor _extractor = new FeatureExtractor();
        private RaceModifier _activeRaceModifier;
        
        // Weights for each main phase
        private static readonly Dictionary<MainPhase, float> MainPhaseWeights = new Dictionary<MainPhase, float>
        {
            { MainPhase.Foundation, 0.35f },      // 35% - most important
            { MainPhase.Structure, 0.30f },       // 30%
            { MainPhase.MajorFeatures, 0.25f },   // 25%
            { MainPhase.FineDetails, 0.10f }      // 10% - least important alone
        };
        
        // Gate threshold - below this, higher levels are penalized
        private const float GateThreshold = 0.50f;
        
        /// <summary>
        /// Set active race modifier (or null for human/neutral)
        /// </summary>
        public void SetRace(string raceId)
        {
            _activeRaceModifier = raceId != null ? RaceModifiers.Get(raceId) : null;
        }
        
        /// <summary>
        /// Calculate hierarchical score from landmarks
        /// </summary>
        public HierarchicalScore Calculate(float[] targetLandmarks, float[] currentLandmarks)
        {
            var target = _extractor.Extract(targetLandmarks);
            var current = _extractor.Extract(currentLandmarks);
            
            return Calculate(target, current);
        }
        
        /// <summary>
        /// Calculate hierarchical score from feature sets
        /// </summary>
        public HierarchicalScore Calculate(FeatureSet target, FeatureSet current)
        {
            return Calculate(target, current, null, null);
        }

        /// <summary>
        /// v3.0.22: Calculate hierarchical score with optional skin tone comparison.
        /// Skin tone is a SOFT factor (max 15% penalty) because the game engine's skin
        /// gradients are approximate and lighting affects perceived tone in renders.
        /// </summary>
        public HierarchicalScore Calculate(FeatureSet target, FeatureSet current,
            float? targetSkinTone, float? currentSkinTone)
        {
            // Apply race modifier to target if active
            if (_activeRaceModifier != null)
            {
                target = ApplyRaceModifier(target, _activeRaceModifier);
            }

            var result = new HierarchicalScore();

            // Calculate scores for each sub-phase
            foreach (SubPhase subPhase in Enum.GetValues(typeof(SubPhase)))
            {
                float score = CalculateSubPhaseScore(subPhase, target, current);
                result.SubPhaseScores[subPhase] = score;
            }

            // Aggregate to main phase scores
            result.FoundationScore = AggregateMainPhase(MainPhase.Foundation, result.SubPhaseScores);
            result.StructureScore = AggregateMainPhase(MainPhase.Structure, result.SubPhaseScores);
            result.MajorFeaturesScore = AggregateMainPhase(MainPhase.MajorFeatures, result.SubPhaseScores);
            result.FineDetailsScore = AggregateMainPhase(MainPhase.FineDetails, result.SubPhaseScores);

            // Calculate total with gating
            float gatedTotal = CalculateTotalWithGating(result);

            // v3.0.22: Apply soft skin tone factor
            result.SkinToneFactor = CalculateSkinToneFactor(targetSkinTone, currentSkinTone);
            result.Total = gatedTotal * result.SkinToneFactor;

            return result;
        }

        /// <summary>
        /// v3.0.22: Calculate soft skin tone match factor (0.85-1.0).
        /// Max 15% penalty because game engine skin gradients are approximate.
        /// Within same Fitzpatrick type (diff &lt; 0.15): no penalty.
        /// 3+ types off (diff &gt; 0.60): max 15% penalty.
        /// </summary>
        private float CalculateSkinToneFactor(float? targetSkinTone, float? currentSkinTone)
        {
            if (!targetSkinTone.HasValue || !currentSkinTone.HasValue)
                return 1.0f;

            float diff = Math.Abs(targetSkinTone.Value - currentSkinTone.Value);

            if (diff < 0.15f) return 1.0f;          // Same Fitzpatrick type
            if (diff > 0.60f) return 0.85f;          // 3+ types off, max penalty

            // Linear interpolation: 1.0 at diff=0.15, 0.85 at diff=0.60
            return 1.0f - (diff - 0.15f) * (0.15f / 0.45f);
        }
        
        // Debug counter to limit logging
        private static int _debugLogCount = 0;
        
        /// <summary>
        /// Calculate score for a single sub-phase
        /// </summary>
        public float CalculateSubPhaseScore(SubPhase phase, FeatureSet target, FeatureSet current)
        {
            float[] targetFeatures = target.GetFeaturesForPhase(phase);
            float[] currentFeatures = current.GetFeaturesForPhase(phase);
            float[] expectedRanges = GetExpectedRanges(phase);
            
            if (targetFeatures.Length == 0 || currentFeatures.Length == 0)
                return 0.5f;  // Neutral score if no features
            
            float sumMatch = 0f;
            float minMatch = 1f;  // Track worst feature match
            int count = Math.Min(targetFeatures.Length, currentFeatures.Length);
            
            for (int i = 0; i < count; i++)
            {
                // NORMALIZED SCORING: Divide diff by expected range for that feature!
                float diff = Math.Abs(targetFeatures[i] - currentFeatures[i]);
                float expectedRange = i < expectedRanges.Length ? expectedRanges[i] : 0.2f;
                float normalizedDiff = diff / Math.Max(expectedRange, 0.01f);
                
                // v3.0.23: Tukey biweight (1-x²)² replaces exp(-x²*12) for better discrimination.
                // Old exponential created a "mushy middle" where 25-50% errors all scored 0.3-0.7.
                // Tukey: 0.96 at 10%, 0.82 at 25%, 0.39 at 50%, 0.10 at 75%, 0.0 at 100%.
                float match;
                if (normalizedDiff >= 1.0f)
                    match = 0.0f;
                else
                {
                    float t = 1.0f - normalizedDiff * normalizedDiff;
                    match = t * t;
                }
                sumMatch += match;
                minMatch = Math.Min(minMatch, match);
            }
            
            float avgMatch = sumMatch / count;
            
            // v3.0.20: KEEP STRICT 50/50 min-priority for SCORING
            // This forces the optimizer to fix ALL bad features, not just boost one.
            // The LEARNING system is where we're more flexible (feature-level learning).
            return avgMatch * 0.5f + minMatch * 0.5f;
        }
        
        /// <summary>
        /// Get expected value ranges for each feature in a sub-phase.
        /// Based on observed real-world facial proportions.
        /// </summary>
        private float[] GetExpectedRanges(SubPhase phase)
        {
            switch (phase)
            {
                case SubPhase.FaceWidth:
                    return new[] { 0.25f };  // v3.0.23b: Relaxed from 0.15 — Dlib has landmark jitter

                case SubPhase.FaceHeight:
                    return new[] { 0.25f };  // v3.0.23b: Relaxed from 0.15

                case SubPhase.FaceShape:
                    // v3.0.23b: Contour profile — 7 values: FaceRatio + 6 contour widths.
                    // Relaxed from 0.08 to 0.15 — landmarks have jitter and photo/render
                    // lighting differences cause systematic contour shifts.
                    // FaceRatio, UpperJaw, Cheekbone, MidJaw, LowerJaw, NearChin, ChinArea
                    return new[] { 0.20f, 0.15f, 0.15f, 0.15f, 0.18f, 0.20f, 0.25f };

                case SubPhase.Forehead:
                    return new[] { 0.25f, 0.20f };  // Height, Width

                case SubPhase.Jaw:
                    // v3.0.23b: Moderate — jaw is important but Bannerlord morphs have limits
                    // Width, AngleL, AngleR, Taper, Curvature
                    return new[] { 0.18f, 0.15f, 0.15f, 0.20f, 0.25f };

                case SubPhase.Chin:
                    // v3.0.23b: Moderate — chin is key but landmark accuracy varies
                    // Width, Height, Pointedness, Drop
                    return new[] { 0.18f, 0.15f, 0.25f, 0.20f };

                case SubPhase.Cheeks:
                    return new[] { 0.18f, 0.15f };  // v3.0.24: Height range widened (new faceHeightRaw-based formula)

                case SubPhase.Nose:
                    // v3.0.24: Ranges adjusted for recalibrated multipliers (features now have real spread)
                    return new[] { 0.22f, 0.20f, 0.20f, 0.18f, 0.22f };  // Width, Length, Bridge, Tip, NostrilFlare

                case SubPhase.Eyes:
                    // v3.0.24: Ranges adjusted for recalibrated multipliers + new EyeAngle/VertPos formulas
                    return new[] { 0.18f, 0.18f, 0.18f, 0.20f, 0.18f };  // Width, Height, Distance, Angle, VertPos

                case SubPhase.Mouth:
                    // v3.0.24: Ranges adjusted for recalibrated multipliers + new VertPos formula
                    return new[] { 0.22f, 0.20f, 0.20f, 0.20f, 0.18f };  // Width, Height, UpperLip, LowerLip, VertPos

                case SubPhase.Eyebrows:
                    // v3.0.24: Angle range widened for new brow slope formula
                    return new[] { 0.18f, 0.20f, 0.18f, 0.22f };  // Height, Arch, Thickness, Angle
                    
                case SubPhase.Ears:
                    return new[] { 0.20f };  // Placeholder
                    
                case SubPhase.FineDetails:
                    return new[] { 0.20f };  // Placeholder
                    
                default:
                    return new[] { 0.20f };  // Default
            }
        }
        
        /// <summary>
        /// Get score for a specific sub-phase only (for focused optimization)
        /// </summary>
        public float GetSubPhaseScore(SubPhase phase, float[] targetLandmarks, float[] currentLandmarks)
        {
            var target = _extractor.Extract(targetLandmarks);
            var current = _extractor.Extract(currentLandmarks);
            
            if (_activeRaceModifier != null)
            {
                target = ApplyRaceModifier(target, _activeRaceModifier);
            }
            
            return CalculateSubPhaseScore(phase, target, current);
        }
        
        /// <summary>
        /// Aggregate sub-phase scores to main phase score.
        /// Uses weighted combination of average and minimum to prevent
        /// one bad sub-phase from being hidden by good ones.
        /// </summary>
        private float AggregateMainPhase(MainPhase mainPhase, Dictionary<SubPhase, float> subScores)
        {
            var subPhases = MorphGroups.GetSubPhases(mainPhase);
            if (subPhases.Length == 0) return 0f;
            
            float sum = 0f;
            float min = 1f;
            int count = 0;
            
            foreach (var sp in subPhases)
            {
                if (subScores.TryGetValue(sp, out float score))
                {
                    sum += score;
                    min = Math.Min(min, score);
                    count++;
                }
            }
            
            if (count == 0) return 0f;
            
            float avg = sum / count;
            
            // v3.0.20: KEEP STRICT 50/50 for SCORING
            // This forces optimizer to improve ALL sub-phases, not just one.
            // Feature-level LEARNING handles partial successes separately.
            return avg * 0.5f + min * 0.5f;
        }
        
        /// <summary>
        /// v3.0.22: SOFT GATING replaces disabled gating.
        /// Old gating caused cascade-to-zero. This version uses smoothstep with a floor of 0.3,
        /// so a bad foundation REDUCES higher-level contributions (to 30%) but never zeros them out.
        /// This preserves optimizer gradient while preventing wrong-shape faces from scoring 0.85+.
        ///
        /// Example: Foundation=0.3 → fGate=0.3, so Structure contributes only 30%.
        /// Example: Foundation=0.7 → fGate=1.0, so Structure contributes fully.
        /// </summary>
        private float CalculateTotalWithGating(HierarchicalScore scores)
        {
            // Foundation gates Structure; Foundation+Structure gate MajorFeatures and FineDetails
            float fGate = SoftGate(scores.FoundationScore, 0.35f, 0.65f);
            float sGate = SoftGate(scores.StructureScore, 0.30f, 0.60f);

            float total =
                scores.FoundationScore * MainPhaseWeights[MainPhase.Foundation] +
                scores.StructureScore * MainPhaseWeights[MainPhase.Structure] * fGate +
                scores.MajorFeaturesScore * MainPhaseWeights[MainPhase.MajorFeatures] * fGate * sGate +
                scores.FineDetailsScore * MainPhaseWeights[MainPhase.FineDetails] * fGate * sGate;

            return total;
        }

        /// <summary>
        /// Smooth gate function: returns [floor..1.0] based on score relative to thresholds.
        /// Uses smoothstep interpolation for gradient-friendly transitions.
        /// Floor=0.3 ensures we never zero out a phase (preserves optimizer signal).
        /// </summary>
        private float SoftGate(float score, float lowThreshold, float highThreshold)
        {
            const float floor = 0.3f;
            if (score >= highThreshold) return 1.0f;
            if (score <= lowThreshold) return floor;
            float t = (score - lowThreshold) / (highThreshold - lowThreshold);
            return floor + (1.0f - floor) * t * t * (3f - 2f * t); // smoothstep
        }
        
        /// <summary>
        /// Apply race modifier to target features.
        /// This shifts what we're trying to match towards race characteristics.
        /// </summary>
        private FeatureSet ApplyRaceModifier(FeatureSet target, RaceModifier mod)
        {
            var modified = target.Clone();
            
            // Ebene 1: Face shape
            modified.FaceWidth *= mod.FaceWidthScale;
            modified.FaceHeight *= mod.FaceHeightScale;
            
            // Ebene 2: Structure
            modified.JawAngleLeft += mod.JawAngleShift / 360f;
            modified.JawAngleRight += mod.JawAngleShift / 360f;
            modified.ChinWidth *= mod.ChinWidthScale;
            modified.ForeheadHeight *= mod.ForeheadHeightScale;
            
            // Ebene 3: Features
            modified.NoseWidth *= mod.NoseWidthScale;
            modified.NoseLength *= mod.NoseLengthScale;
            modified.EyeWidth *= mod.EyeWidthScale;
            modified.MouthWidth *= mod.MouthWidthScale;
            
            // Clamp all values to 0-1
            ClampFeatures(modified);
            
            return modified;
        }
        
        private void ClampFeatures(FeatureSet f)
        {
            f.FaceWidth = Clamp(f.FaceWidth);
            f.FaceHeight = Clamp(f.FaceHeight);
            f.JawAngleLeft = Clamp(f.JawAngleLeft);
            f.JawAngleRight = Clamp(f.JawAngleRight);
            f.ChinWidth = Clamp(f.ChinWidth);
            f.ForeheadHeight = Clamp(f.ForeheadHeight);
            f.NoseWidth = Clamp(f.NoseWidth);
            f.NoseLength = Clamp(f.NoseLength);
            f.EyeWidth = Clamp(f.EyeWidth);
            f.MouthWidth = Clamp(f.MouthWidth);
        }
        
        private float Clamp(float v) => Math.Max(0f, Math.Min(1f, v));

        /// <summary>
        /// v3.0.24: Generate detailed feature comparison report for diagnostics.
        /// Shows Photo vs Render feature values per SubPhase with diffs, normalized diffs, and scores.
        /// Used to calibrate expected ranges based on real data instead of guessing.
        /// </summary>
        public string GenerateFeatureReport(FeatureSet target, FeatureSet current, HierarchicalScore hierarchicalScore)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine($"║  FEATURE COMPARISON REPORT  —  Total Score: {hierarchicalScore.Total:F3}");
            sb.AppendLine($"║  Foundation={hierarchicalScore.FoundationScore:F3}  Structure={hierarchicalScore.StructureScore:F3}  Features={hierarchicalScore.MajorFeaturesScore:F3}  Details={hierarchicalScore.FineDetailsScore:F3}");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // Feature name labels per SubPhase
            var featureNames = new Dictionary<SubPhase, string[]>
            {
                { SubPhase.FaceWidth, new[] { "FaceWidth" } },
                { SubPhase.FaceHeight, new[] { "FaceHeight" } },
                { SubPhase.FaceShape, new[] { "FaceRatio", "ContourUpperJaw", "ContourCheekbone", "ContourMidJaw", "ContourLowerJaw", "ContourNearChin", "ContourChinArea" } },
                { SubPhase.Forehead, new[] { "ForeheadHeight", "ForeheadWidth" } },
                { SubPhase.Jaw, new[] { "JawWidth", "JawAngleL", "JawAngleR", "JawTaper", "JawCurvature" } },
                { SubPhase.Chin, new[] { "ChinWidth", "ChinHeight", "ChinPointedness", "ChinDrop" } },
                { SubPhase.Cheeks, new[] { "CheekHeight", "CheekWidth" } },
                { SubPhase.Nose, new[] { "NoseWidth", "NoseLength", "NoseBridge", "NoseTip", "NostrilWidth" } },
                { SubPhase.Eyes, new[] { "EyeWidth", "EyeHeight", "EyeDistance", "EyeAngle", "EyeVertPos" } },
                { SubPhase.Mouth, new[] { "MouthWidth", "MouthHeight", "UpperLip", "LowerLip", "MouthVertPos" } },
                { SubPhase.Eyebrows, new[] { "BrowHeight", "BrowArch", "BrowThick", "BrowAngle" } },
            };

            // MainPhase groupings for display
            var mainPhaseMap = new Dictionary<MainPhase, SubPhase[]>
            {
                { MainPhase.Foundation, new[] { SubPhase.FaceWidth, SubPhase.FaceHeight, SubPhase.FaceShape } },
                { MainPhase.Structure, new[] { SubPhase.Forehead, SubPhase.Jaw, SubPhase.Chin, SubPhase.Cheeks } },
                { MainPhase.MajorFeatures, new[] { SubPhase.Nose, SubPhase.Eyes, SubPhase.Mouth } },
                { MainPhase.FineDetails, new[] { SubPhase.Eyebrows } },
            };

            foreach (var mainKv in mainPhaseMap)
            {
                float mainScore = 0f;
                switch (mainKv.Key)
                {
                    case MainPhase.Foundation: mainScore = hierarchicalScore.FoundationScore; break;
                    case MainPhase.Structure: mainScore = hierarchicalScore.StructureScore; break;
                    case MainPhase.MajorFeatures: mainScore = hierarchicalScore.MajorFeaturesScore; break;
                    case MainPhase.FineDetails: mainScore = hierarchicalScore.FineDetailsScore; break;
                }

                sb.AppendLine($"── {mainKv.Key} (Score: {mainScore:F3}) ──────────────────────────────────");

                foreach (var subPhase in mainKv.Value)
                {
                    float[] targetFeatures = target.GetFeaturesForPhase(subPhase);
                    float[] currentFeatures = current.GetFeaturesForPhase(subPhase);
                    float[] expectedRanges = GetExpectedRanges(subPhase);

                    float subScore = hierarchicalScore.SubPhaseScores.ContainsKey(subPhase)
                        ? hierarchicalScore.SubPhaseScores[subPhase] : 0f;

                    sb.AppendLine($"  [{subPhase}] Score: {subScore:F3}");
                    sb.AppendLine($"  {"Feature",-20} {"Photo",8} {"Render",8} {"Diff",8} {"Range",8} {"NrmDiff",8} {"Match",8}");
                    sb.AppendLine($"  {"-------",-20} {"-----",8} {"------",8} {"----",8} {"-----",8} {"-------",8} {"-----",8}");

                    string[] names = featureNames.ContainsKey(subPhase) ? featureNames[subPhase] : new string[0];
                    int count = Math.Min(targetFeatures.Length, currentFeatures.Length);

                    for (int i = 0; i < count; i++)
                    {
                        string name = i < names.Length ? names[i] : $"Feature[{i}]";
                        float diff = Math.Abs(targetFeatures[i] - currentFeatures[i]);
                        float range = i < expectedRanges.Length ? expectedRanges[i] : 0.2f;
                        float normDiff = diff / Math.Max(range, 0.01f);

                        float match;
                        if (normDiff >= 1.0f)
                            match = 0.0f;
                        else
                        {
                            float t = 1.0f - normDiff * normDiff;
                            match = t * t;
                        }

                        // Flag features with poor match
                        string flag = match < 0.5f ? " ◄◄ BAD" : match < 0.75f ? " ◄" : "";

                        sb.AppendLine($"  {name,-20} {targetFeatures[i],8:F4} {currentFeatures[i],8:F4} {diff,8:F4} {range,8:F3} {normDiff,8:F3} {match,8:F3}{flag}");
                    }
                    sb.AppendLine();
                }
            }

            // Summary section: worst features
            sb.AppendLine("── WORST FEATURES (Match < 0.50) ──────────────────────────────────");
            foreach (var subPhase in Enum.GetValues(typeof(SubPhase)).Cast<SubPhase>())
            {
                if (!featureNames.ContainsKey(subPhase)) continue;

                float[] targetFeatures = target.GetFeaturesForPhase(subPhase);
                float[] currentFeatures = current.GetFeaturesForPhase(subPhase);
                float[] expectedRanges = GetExpectedRanges(subPhase);
                string[] names = featureNames[subPhase];
                int count = Math.Min(targetFeatures.Length, currentFeatures.Length);

                for (int i = 0; i < count; i++)
                {
                    float diff = Math.Abs(targetFeatures[i] - currentFeatures[i]);
                    float range = i < expectedRanges.Length ? expectedRanges[i] : 0.2f;
                    float normDiff = diff / Math.Max(range, 0.01f);

                    float match;
                    if (normDiff >= 1.0f) match = 0.0f;
                    else { float t2 = 1.0f - normDiff * normDiff; match = t2 * t2; }

                    if (match < 0.50f)
                    {
                        string name = i < names.Length ? names[i] : $"Feature[{i}]";
                        sb.AppendLine($"  {subPhase}/{name}: Photo={targetFeatures[i]:F4} Render={currentFeatures[i]:F4} Diff={diff:F4} NormDiff={normDiff:F3} Match={match:F3}");
                    }
                }
            }
            sb.AppendLine();

            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Result of hierarchical scoring
    /// </summary>
    public class HierarchicalScore
    {
        /// <summary>Total score after gating (0-1)</summary>
        public float Total { get; set; }
        
        /// <summary>Foundation (face shape) score</summary>
        public float FoundationScore { get; set; }
        
        /// <summary>Structure (forehead, jaw, chin) score</summary>
        public float StructureScore { get; set; }
        
        /// <summary>Major features (nose, eyes, mouth) score</summary>
        public float MajorFeaturesScore { get; set; }
        
        /// <summary>Fine details score</summary>
        public float FineDetailsScore { get; set; }
        
        /// <summary>v3.0.22: Skin tone match factor (0.85-1.0). Applied as soft multiplier.</summary>
        public float SkinToneFactor { get; set; } = 1.0f;

        /// <summary>Individual sub-phase scores</summary>
        public Dictionary<SubPhase, float> SubPhaseScores { get; set; } = new Dictionary<SubPhase, float>();
        
        /// <summary>Get the worst sub-phase (for focused improvement)</summary>
        public SubPhase GetWorstSubPhase()
        {
            return SubPhaseScores.OrderBy(kv => kv.Value).First().Key;
        }
        
        /// <summary>Get the worst sub-phase within a main phase</summary>
        public SubPhase GetWorstSubPhase(MainPhase mainPhase)
        {
            var subPhases = MorphGroups.GetSubPhases(mainPhase);
            return SubPhaseScores
                .Where(kv => subPhases.Contains(kv.Key))
                .OrderBy(kv => kv.Value)
                .First().Key;
        }
        
        /// <summary>Get sub-phases that need improvement (score below threshold)</summary>
        public SubPhase[] GetWeakSubPhases(float threshold = 0.6f)
        {
            return SubPhaseScores
                .Where(kv => kv.Value < threshold)
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToArray();
        }
        
        public override string ToString()
        {
            return $"Total={Total:F2} [Found={FoundationScore:F2} Struct={StructureScore:F2} " +
                   $"Feat={MajorFeaturesScore:F2} Detail={FineDetailsScore:F2}]";
        }
        
        /// <summary>Detailed breakdown for logging</summary>
        public string ToDetailedString()
        {
            var lines = new List<string>
            {
                $"=== Hierarchical Score: {Total:F3} ===",
                $"Foundation:    {FoundationScore:F3}",
            };
            
            foreach (var sp in MorphGroups.GetSubPhases(MainPhase.Foundation))
            {
                if (SubPhaseScores.TryGetValue(sp, out float score))
                    lines.Add($"  {sp}: {score:F3}");
            }
            
            lines.Add($"Structure:     {StructureScore:F3}");
            foreach (var sp in MorphGroups.GetSubPhases(MainPhase.Structure))
            {
                if (SubPhaseScores.TryGetValue(sp, out float score))
                    lines.Add($"  {sp}: {score:F3}");
            }
            
            lines.Add($"MajorFeatures: {MajorFeaturesScore:F3}");
            foreach (var sp in MorphGroups.GetSubPhases(MainPhase.MajorFeatures))
            {
                if (SubPhaseScores.TryGetValue(sp, out float score))
                    lines.Add($"  {sp}: {score:F3}");
            }
            
            lines.Add($"FineDetails:   {FineDetailsScore:F3}");
            foreach (var sp in MorphGroups.GetSubPhases(MainPhase.FineDetails))
            {
                if (SubPhaseScores.TryGetValue(sp, out float score))
                    lines.Add($"  {sp}: {score:F3}");
            }
            
            return string.Join("\n", lines);
        }
    }
}
