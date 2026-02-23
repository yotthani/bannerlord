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
        // v3.0.25: Rebalanced — identity features (eyes/nose/mouth) now 55% instead of 35%
        // Foundation+Structure still gates via soft-gating, but direct weight reduced
        private static readonly Dictionary<MainPhase, float> MainPhaseWeights = new Dictionary<MainPhase, float>
        {
            { MainPhase.Foundation, 0.20f },      // 20% — generic proportions
            { MainPhase.Structure, 0.25f },       // 25%
            { MainPhase.MajorFeatures, 0.45f },   // 45% (was 40%) — eyes/nose/mouth = identity
            { MainPhase.FineDetails, 0.10f }      // 10% (was 15%) — only Eyebrows now (Ears/FineDetails removed v3.0.30)
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

            // v3.0.31: Photo→Render bias correction for FaceWidth/FaceHeight.
            // Photos have faces filling ~80-90% of bounding box; renders ~50-60%.
            // This means photo landmarks are systematically wider/taller in normalized space.
            // Observed offsets: FaceWidth ~+0.15, FaceHeight ~+0.15 in photos vs renders.
            // Apply correction by subtracting bias from target (photo) values before comparison.
            float[] biasCorrection = GetBiasCorrection(phase);

            float sumMatch = 0f;
            float minMatch = 1f;  // Track worst feature match
            int count = Math.Min(targetFeatures.Length, currentFeatures.Length);

            for (int i = 0; i < count; i++)
            {
                // Apply bias correction: photo values are systematically higher
                float correctedTarget = targetFeatures[i];
                if (biasCorrection != null && i < biasCorrection.Length)
                    correctedTarget = Math.Max(0f, Math.Min(1f, targetFeatures[i] - biasCorrection[i]));

                // NORMALIZED SCORING: Divide diff by expected range for that feature!
                float diff = Math.Abs(correctedTarget - currentFeatures[i]);
                float expectedRange = i < expectedRanges.Length ? expectedRanges[i] : 0.2f;
                float normalizedDiff = diff / Math.Max(expectedRange, 0.01f);
                
                // v3.0.26: Extended Tukey (1-x²)² with 1.5x range.
                // v3.0.25 was too harsh: (1-x²)³ with cutoff at 1.0 → NormDiff>1.0 got Match=0.000
                // and optimizer had NO gradient signal. Now: cutoff at 1.5, (1-x²)² for gentler curve.
                // NormDiff=0.5→0.79, NormDiff=1.0→0.31, NormDiff=1.5→0.0
                float match;
                if (normalizedDiff >= 1.5f)
                    match = 0.0f;
                else
                {
                    float scaled = normalizedDiff / 1.5f;
                    float t = 1.0f - scaled * scaled;
                    match = t * t;  // (1-x²)²
                }
                sumMatch += match;
                minMatch = Math.Min(minMatch, match);
            }
            
            float avgMatch = sumMatch / count;
            
            // v3.0.26: 50/50 avg/min — balanced. v3.0.25's 35/65 was too harsh,
            // one bad feature crushed the entire SubPhase to near-zero.
            return avgMatch * 0.50f + minMatch * 0.50f;
        }
        
        /// <summary>
        /// Get expected value ranges for each feature in a sub-phase.
        /// Based on observed real-world facial proportions.
        /// </summary>
        private float[] GetExpectedRanges(SubPhase phase)
        {
            // v3.0.26: Ranges widened back from v3.0.25's aggressive tightening.
            // v3.0.25 ranges (0.10-0.14) caused most features to hit NormDiff>1.0 → Match=0.000
            // leaving the optimizer with zero gradient signal.
            // New: ranges accommodate real photo→render variation (0.08-0.15 typical diffs)
            // while the extended Tukey (cutoff 1.5x) ensures gradients exist up to 1.5× range.
            // NOTE: Feature formulas now use Offset+Scale (v3.0.26) so diffs should be smaller.
            switch (phase)
            {
                case SubPhase.FaceWidth:
                    // v3.0.35: Widened from 0.20 to 0.30. Without fixed bias correction,
                    // photo→render FaceWidth diffs can be 0.00-0.20 depending on bounding box.
                    // 0.30 range means NormDiff=0.20→0.67→Match=0.64. Still penalizes, but
                    // doesn't destroy the score. NormDiff=0.10→0.33→Match=0.90 (good match).
                    return new[] { 0.30f };

                case SubPhase.FaceHeight:
                    // v3.0.35: Widened from 0.20 to 0.30. Same reasoning as FaceWidth.
                    return new[] { 0.30f };

                case SubPhase.FaceShape:
                    // FaceRatio, UpperJaw, Cheekbone, MidJaw, LowerJaw, NearChin, ChinArea
                    // v3.0.35: Cheekbone and MidJaw widened from 0.15 to 0.18. Man 1285416547 had
                    // Cheekbone diff=0.11 (Match=0.58) and MidJaw diff=0.10 (Match=0.64) — these
                    // contour values are relative but still affected by bbox crop differences.
                    return new[] { 0.18f, 0.18f, 0.18f, 0.18f, 0.18f, 0.20f, 0.22f };

                case SubPhase.Forehead:
                    // v3.0.38: ForeheadWidth tightened from 0.25 to 0.18.
                    // v3.0.36 widened to 0.25 because of bbox bias (Photo ~0.82 vs Render ~0.65).
                    // Now we have bias correction (0.12) so corrected diff should be ~0.05.
                    // With 0.18 range: diff=0.05→NormDiff=0.28→Match=0.93. Good.
                    return new[] { 0.18f, 0.18f };  // Height, Width

                case SubPhase.Jaw:
                    // Width, AngleL, AngleR, Taper, Curvature
                    return new[] { 0.15f, 0.18f, 0.18f, 0.18f, 0.20f };

                case SubPhase.Chin:
                    // Width, Height, Pointedness, Drop
                    return new[] { 0.15f, 0.12f, 0.18f, 0.18f };

                case SubPhase.Cheeks:
                    // v3.0.35: CheekWidth widened from 0.12 to 0.18.
                    // Man 1285416547 had Photo=0.89, Render=0.78, Diff=0.11 → Match=0.390 with 0.12 range.
                    // CheekWidth is derived from Dist(JAW_R2,JAW_L2)/faceWidthRaw which varies with bbox.
                    return new[] { 0.15f, 0.18f };  // Height, Width (was 0.12)

                case SubPhase.Nose:
                    // v3.0.38: Tightened from v3.0.36 (0.25/0.30/0.30/0.18/0.25).
                    // v3.0.36 ranges were too generous — NoseWidth diff=0.15 got Match=0.71,
                    // NoseLength diff=0.09 got Match=0.92. Visually wrong noses scored 0.93+.
                    // Typical photo→render diffs: 0.04-0.15. Ranges should make 0.10 diff ≈ 0.75 match.
                    // With Tukey (1-x²)², range 0.18: diff=0.10→NormDiff=0.56→Match=0.74. Good.
                    // Engine ceiling still exists (NoseLength can be 0.16+ off) but Tukey extends
                    // to 1.5x range, so diff=0.27 still gets Match>0 gradient signal.
                    // Width, Length, Bridge, Tip, NostrilFlare
                    return new[] { 0.18f, 0.20f, 0.20f, 0.15f, 0.18f };

                case SubPhase.Eyes:
                    // v3.0.38: EyeDistance has systematic bbox bias (Photo always higher).
                    // Adding bias correction instead of wide range. Range tightened 0.25→0.20.
                    // EyeHeight: 0.22→0.18. Monolid vs closed eye distinction now comes from
                    // EyeOpenness (Width/Height aspect ratio).
                    // Width, Height, Distance, Angle, VertPos, Openness
                    return new[] { 0.18f, 0.18f, 0.20f, 0.18f, 0.15f, 0.20f };

                case SubPhase.Mouth:
                    // v3.0.38: Tightened from v3.0.36.
                    // MouthWidth: 0.25→0.20. Diff=0.19 (smiling Asian man) was getting Match=0.57.
                    //   With 0.20: NormDiff=0.95→Match=0.36. Better penalty for wide mouth mismatch.
                    // MouthHeight: 0.28→0.22. Smile cap (v3.0.37) should reduce photo values already.
                    // LowerLip: 0.25→0.20. Engine lip range is limited but 0.25 was too generous.
                    // Width, Height, UpperLip, LowerLip, VertPos
                    return new[] { 0.20f, 0.22f, 0.18f, 0.20f, 0.15f };

                case SubPhase.Eyebrows:
                    // v3.0.36: BrowArch widened 0.18→0.22 (engine brow arch limited).
                    // BrowAngle widened 0.20→0.25 (bbox + engine: Photo=0.73 vs Render=0.53).
                    // Height, Arch, Thickness, Angle
                    return new[] { 0.18f, 0.22f, 0.18f, 0.25f };

                case SubPhase.Ears:
                    return new[] { 0.20f };

                case SubPhase.FineDetails:
                    return new[] { 0.20f };

                default:
                    return new[] { 0.20f };
            }
        }

        /// <summary>
        /// v3.0.31: Photo→Render bias correction per sub-phase.
        /// Photo landmarks are normalized to a tighter bounding box (face fills more of the frame)
        /// while render landmarks have more empty space around the face.
        /// This causes systematic differences in FaceWidth, FaceHeight, and some other features.
        /// Returns null for phases with no bias, or float[] of per-feature correction values.
        /// Correction is SUBTRACTED from photo (target) values before comparison.
        /// </summary>
        private float[] GetBiasCorrection(SubPhase phase)
        {
            switch (phase)
            {
                case SubPhase.FaceWidth:
                    // v3.0.35: REMOVED fixed bias. The 0.18 bias helped faces where the photo
                    // fills the bounding box tightly (FaceWidth ~0.58) but DESTROYED faces where
                    // the photo has more background (FaceWidth ~0.41 already matching render).
                    // Example: Man 1285416547 — Photo=0.41, Render=0.41, but after -0.18 bias
                    // corrected to 0.23 → Match=0.397 → Foundation=0.48 → Score=0.30!
                    // Instead: wider Range (0.30) absorbs the photo/render variance without
                    // needing a fixed bias that's wrong for half the faces.
                    return null;  // No bias — wider range handles photo/render variance

                case SubPhase.FaceHeight:
                    // v3.0.41: Re-added moderate bias. v3.0.35 removed it but 20-face test shows
                    // consistent Photo 0.76-0.89, Render 0.63-0.65 (avg diff ~0.18).
                    // The 0.30 range absorbs some but NormDiff=0.60→Match=0.64 is harsh.
                    // Moderate -0.12 bias: Photo 0.83-0.12=0.71 vs Render 0.64 → diff=0.07 → Match=0.95.
                    // For tight crops (Photo 0.76): 0.76-0.12=0.64 vs Render 0.65 → diff=0.01 → Match=1.0.
                    return new[] { -0.12f };

                // FaceShape contours are RELATIVE (normalized by maxWidth) → much less bias

                case SubPhase.Chin:
                    // v3.0.40: ChinPointedness bias — Photo chinPointedness is systematically LOWER
                    // than renders (engine can't make chins as round as real faces).
                    // Observed: Photo ~0.07-0.18, Render ~0.23-0.27 for round chins.
                    // v3.0.34: was -0.10, but that left 0.076→0.176 still too far from render ~0.24.
                    // v3.0.40: increased to -0.15 so photo 0.076→0.226 (closer to render minimum).
                    // Negative correction = photo value INCREASES (we subtract negative = add).
                    return new[] { 0f, 0f, -0.15f, 0f };  // [Width, Height, Pointedness, Drop]

                case SubPhase.Eyes:
                    // v3.0.41: Added EyeOpenness bias. Engine renders consistently show lower
                    // EyeOpenness than photos (Photo 0.45-0.66, Render 0.28-0.41, avg diff ~0.20).
                    // Engine eye meshes have narrower openings than real eyes.
                    // EyeDistance bbox bias: Photo ~0.97, Render ~0.80 (Photo crop includes more forehead).
                    // [Width, Height, Distance, Angle, VertPos, Openness]
                    return new[] { 0f, 0f, 0.10f, 0f, 0f, -0.18f };

                case SubPhase.Forehead:
                    // v3.0.41: ForeheadWidth bias increased from 0.03 to 0.08.
                    // v3.0.40 data (20 faces): Photo 0.52-0.82, Render 0.58-0.67 (avg diff ~0.08).
                    // 0.03 was too low — still leaving ForeheadWidth Match at 0.60-0.70.
                    // 0.08 = moderate correction. Photo 0.73-0.08=0.65 vs Render 0.65 → Match=1.0.
                    // Wide crops (0.82) will have corrected 0.74 vs 0.65 → diff=0.09 → Match=0.88.
                    // [Height, Width]
                    return new[] { 0f, 0.08f };

                case SubPhase.Nose:
                    // v3.0.41: Added NoseWidth bias. All nose features systematically differ:
                    //   NoseWidth:  Photo 0.44-0.56, Render 0.38-0.42 (avg diff ~0.10)
                    //   NoseLength: Photo 0.34-0.86, Render 0.51-0.54 (avg diff ~0.17)
                    //   NoseBridge: Photo 0.11-0.62, Render 0.27-0.34 (avg diff ~0.17)
                    // Engine noses are proportionally longer/higher bridge than real faces,
                    // and narrower overall. Negative bias = photo value INCREASES.
                    // [Width, Length, Bridge, Tip, NostrilFlare]
                    return new[] { -0.08f, -0.18f, -0.18f, 0f, 0f };

                case SubPhase.Eyebrows:
                    // v3.0.41: Added BrowHeight bias. Engine brow landmarks are systematically lower:
                    //   BrowHeight: Photo 0.59-0.75, Render 0.50-0.54 (avg diff ~0.16)
                    //   BrowThick:  Photo 0.21-0.25, Render 0.12-0.20 (avg diff ~0.08) — keep 0.12 from v3.0.34
                    // BrowAngle also shows bias: Photo 0.51-0.70, Render 0.48-0.56 (avg diff ~0.10)
                    // [Height, Arch, Thickness, Angle]
                    return new[] { -0.14f, 0f, 0.12f, -0.10f };

                case SubPhase.Mouth:
                    // v3.0.41: Mouth features have systematic photo→render bias.
                    // Engine mouths are proportionally narrower/shorter than real faces:
                    //   MouthWidth:  Photo 0.59-0.78, Render 0.53-0.57 (avg diff ~0.14)
                    //   MouthHeight: Photo 0.49-0.81, Render 0.54-0.65 (avg diff ~0.12)
                    //   UpperLip:    Photo 0.27-0.38, Render 0.16-0.25 (avg diff ~0.10)
                    //   LowerLip:    varies (sometimes Photo < Render), no clear bias
                    // [Width, Height, UpperLip, LowerLip, VertPos]
                    return new[] { -0.12f, -0.10f, -0.08f, 0f, 0f };

                default:
                    return null;  // No bias correction needed
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
            
            // v3.0.26: 50/50 avg/min — balanced. v3.0.25's 35/65 was too punishing.
            return avg * 0.50f + min * 0.50f;
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
            // v3.0.35: Relaxed gating — floor 0.15 was too aggressive.
            // Example: Man 1285416547 — Foundation=0.48 → fGate=0.31 → MajorFeatures=0.946*0.45*0.31=0.13!
            // The MajorFeatures were EXCELLENT but got destroyed by a borderline Foundation score
            // that was wrong due to FaceWidth bias overcorrection.
            // New thresholds: lowered to 0.30/0.60, so Foundation=0.48 → fGate=0.55 (was 0.31).
            float fGate = SoftGate(scores.FoundationScore, 0.30f, 0.60f);
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
            const float floor = 0.25f;  // v3.0.35: was 0.15. Too low caused cascade destruction when
                                        // FaceWidth bias was wrong. 0.25 still penalizes but doesn't zero out.
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
                { SubPhase.Eyes, new[] { "EyeWidth", "EyeHeight", "EyeDistance", "EyeAngle", "EyeVertPos", "EyeOpenness" } },
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
                    float[] biasCorrection = GetBiasCorrection(subPhase);  // v3.0.34: Show corrected diffs in report

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
                        // v3.0.34: Apply bias correction in report so diff/normDiff/match match actual scoring
                        float correctedTarget = targetFeatures[i];
                        if (biasCorrection != null && i < biasCorrection.Length)
                            correctedTarget = Math.Max(0f, Math.Min(1f, targetFeatures[i] - biasCorrection[i]));
                        float diff = Math.Abs(correctedTarget - currentFeatures[i]);
                        float range = i < expectedRanges.Length ? expectedRanges[i] : 0.2f;
                        float normDiff = diff / Math.Max(range, 0.01f);

                        // v3.0.26: Extended Tukey (1-x²)² with 1.5x range (matches scoring)
                        float match;
                        if (normDiff >= 1.5f)
                            match = 0.0f;
                        else
                        {
                            float scaled = normDiff / 1.5f;
                            float t = 1.0f - scaled * scaled;
                            match = t * t;
                        }

                        // Flag features with poor match
                        string flag = match < 0.5f ? " ◄◄ BAD" : match < 0.75f ? " ◄" : "";

                        // v3.0.34: Show corrected photo value when bias correction is applied
                        string photoStr = (biasCorrection != null && i < biasCorrection.Length)
                            ? $"{correctedTarget:F4}*"  // * = bias-corrected
                            : $"{targetFeatures[i]:F4} ";
                        sb.AppendLine($"  {name,-20} {photoStr,8} {currentFeatures[i],8:F4} {diff,8:F4} {range,8:F3} {normDiff,8:F3} {match,8:F3}{flag}");
                    }
                    sb.AppendLine();
                }
            }

            // Summary section: worst features (v3.0.34: with bias correction)
            sb.AppendLine("── WORST FEATURES (Match < 0.50) ──────────────────────────────────");
            foreach (var subPhase in Enum.GetValues(typeof(SubPhase)).Cast<SubPhase>())
            {
                if (!featureNames.ContainsKey(subPhase)) continue;

                float[] targetFeatures = target.GetFeaturesForPhase(subPhase);
                float[] currentFeatures = current.GetFeaturesForPhase(subPhase);
                float[] expectedRanges = GetExpectedRanges(subPhase);
                float[] worstBias = GetBiasCorrection(subPhase);  // v3.0.34
                string[] names = featureNames[subPhase];
                int count = Math.Min(targetFeatures.Length, currentFeatures.Length);

                for (int i = 0; i < count; i++)
                {
                    // v3.0.34: Apply bias correction to match actual scoring
                    float corrTarget = targetFeatures[i];
                    if (worstBias != null && i < worstBias.Length)
                        corrTarget = Math.Max(0f, Math.Min(1f, targetFeatures[i] - worstBias[i]));
                    float diff = Math.Abs(corrTarget - currentFeatures[i]);
                    float range = i < expectedRanges.Length ? expectedRanges[i] : 0.15f;
                    float normDiff = diff / Math.Max(range, 0.01f);

                    // v3.0.26: Must match CalculateSubPhaseScore — extended Tukey (1-x²)² with 1.5x range
                    float match;
                    if (normDiff >= 1.5f) match = 0.0f;
                    else { float sc = normDiff / 1.5f; float t2 = 1.0f - sc * sc; match = t2 * t2; }

                    if (match < 0.50f)
                    {
                        string name = i < names.Length ? names[i] : $"Feature[{i}]";
                        sb.AppendLine($"  {subPhase}/{name}: Photo={corrTarget:F4} Render={currentFeatures[i]:F4} Diff={diff:F4} NormDiff={normDiff:F3} Match={match:F3}");
                    }
                }
            }

            // v3.0.25: Saturation warnings — features where BOTH photo and render are near-extreme
            sb.AppendLine("── SATURATION WARNINGS (Both Photo & Render > 0.90) ──────────────");
            bool anySaturation = false;
            foreach (var subPhase in Enum.GetValues(typeof(SubPhase)).Cast<SubPhase>())
            {
                if (!featureNames.ContainsKey(subPhase)) continue;
                float[] targetFeatures = target.GetFeaturesForPhase(subPhase);
                float[] currentFeatures = current.GetFeaturesForPhase(subPhase);
                string[] names = featureNames[subPhase];
                int count = Math.Min(targetFeatures.Length, currentFeatures.Length);
                for (int i = 0; i < count; i++)
                {
                    bool bothHigh = targetFeatures[i] > 0.90f && currentFeatures[i] > 0.90f;
                    bool bothLow = targetFeatures[i] < 0.10f && currentFeatures[i] < 0.10f;
                    if (bothHigh || bothLow)
                    {
                        string name = i < names.Length ? names[i] : $"Feature[{i}]";
                        string side = bothHigh ? "HIGH" : "LOW";
                        sb.AppendLine($"  ⚠ {subPhase}/{name}: Photo={targetFeatures[i]:F4} Render={currentFeatures[i]:F4} [{side}] — no discriminative value");
                        anySaturation = true;
                    }
                }
            }
            if (!anySaturation)
                sb.AppendLine("  (none — all features have discriminative range)");
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
