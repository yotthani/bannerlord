using System;
using System.Collections.Generic;
using System.Linq;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.Proportions;
using FaceLearner.Core.LivingKnowledge;

// Alias to resolve ambiguity - use the Modules.Core version which ProportionsResult uses
using FaceShape = FaceLearner.ML.Modules.Core.FaceShape;

namespace FaceLearner.ML.Modules.Scoring
{
    /// <summary>
    /// Per-feature score result
    /// </summary>
    public class FeatureScoreResult : IDetectionResult
    {
        /// <summary>Overall combined score (0-1)</summary>
        public float Overall { get; set; }
        
        /// <summary>Face shape similarity</summary>
        public float FaceShapeScore { get; set; }
        
        /// <summary>Eye region similarity</summary>
        public float EyesScore { get; set; }
        
        /// <summary>Nose similarity</summary>
        public float NoseScore { get; set; }
        
        /// <summary>Mouth/lips similarity</summary>
        public float MouthScore { get; set; }
        
        /// <summary>Jaw/chin similarity</summary>
        public float JawScore { get; set; }
        
        /// <summary>Eyebrow similarity</summary>
        public float EyebrowsScore { get; set; }
        
        /// <summary>Raw landmark-based score (for comparison)</summary>
        public float LandmarkScore { get; set; }
        
        /// <summary>
        /// Shape match score (1.0 = same shape, lower = wrong shape).
        /// This penalizes generating Diamond faces when target is Round, etc.
        /// </summary>
        public float ShapeMatchScore { get; set; } = 1.0f;
        
        /// <summary>
        /// The raw feature-weighted score before shape correction.
        /// Useful for debugging to see if feature scores are high but shape is wrong.
        /// </summary>
        public float FeatureOnlyScore { get; set; }
        
        /// <summary>The feature with the worst score</summary>
        public FaceFeature WorstFeature { get; set; }
        
        /// <summary>The worst feature's score</summary>
        public float WorstScore { get; set; }
        
        /// <summary>Smile detection info for TARGET face - affects scoring reliability</summary>
        public SmileInfo TargetSmile { get; set; }
        
        /// <summary>Are smile-affected features (Mouth, Jaw, FaceShape) reliable?</summary>
        public bool SmileAffectedFeaturesReliable => TargetSmile == null || !TargetSmile.IsSmiling;
        
        #region IDetectionResult
        
        public float Confidence { get; set; }
        public bool IsReliable => Confidence >= ConfidenceThresholds.Acceptable;
        public string Source { get; set; }
        
        #endregion
        
        /// <summary>
        /// Get all features sorted by score (worst first)
        /// </summary>
        public IEnumerable<(FaceFeature feature, float score)> GetFeaturesByScore()
        {
            var features = new List<(FaceFeature, float)>
            {
                (FaceFeature.FaceShape, FaceShapeScore),
                (FaceFeature.Eyes, EyesScore),
                (FaceFeature.Nose, NoseScore),
                (FaceFeature.Mouth, MouthScore),
                (FaceFeature.Jaw, JawScore),
                (FaceFeature.Eyebrows, EyebrowsScore)
            };
            
            return features.OrderBy(f => f.Item2);
        }
        
        /// <summary>
        /// Get features below a threshold
        /// </summary>
        public IEnumerable<FaceFeature> GetProblemFeatures(float threshold = 0.5f)
        {
            if (FaceShapeScore < threshold) yield return FaceFeature.FaceShape;
            if (EyesScore < threshold) yield return FaceFeature.Eyes;
            if (NoseScore < threshold) yield return FaceFeature.Nose;
            if (MouthScore < threshold) yield return FaceFeature.Mouth;
            if (JawScore < threshold) yield return FaceFeature.Jaw;
            if (EyebrowsScore < threshold) yield return FaceFeature.Eyebrows;
        }
        
        public override string ToString()
        {
            string shapeInfo = ShapeMatchScore < 0.99f ? $" Shape:{ShapeMatchScore:F2}" : "";
            return $"Face:{FaceShapeScore:F2} Eyes:{EyesScore:F2} Nose:{NoseScore:F2} " +
                   $"Mouth:{MouthScore:F2} Jaw:{JawScore:F2} Brows:{EyebrowsScore:F2}" +
                   $"{shapeInfo} (Overall:{Overall:F3})";
        }
    }
    
    /// <summary>
    /// Calculates per-feature similarity scores between two faces.
    /// This enables targeted optimization - if nose score is low, focus on nose morphs.
    /// </summary>
    public class ScoringModule
    {
        // Feature weights (sum to 1.0)
        // REBALANCED 2026-01-27:
        // - Nose increased: highly distinctive, errors very noticeable
        // - FaceShape increased: proportions matter a lot
        // - Eyebrows decreased: least distinctive feature
        private static readonly Dictionary<FaceFeature, float> DefaultWeights = new Dictionary<FaceFeature, float>
        {
            { FaceFeature.Nose, 0.24f },       // INCREASED - nose errors very noticeable
            { FaceFeature.FaceShape, 0.22f }, // Proportions/shape important
            { FaceFeature.Eyes, 0.18f },       // Eyes important but Bannerlord has limited range
            { FaceFeature.Jaw, 0.16f },        // Jaw/chin important
            { FaceFeature.Mouth, 0.12f },      // Mouth/lips
            { FaceFeature.Eyebrows, 0.08f }   // Eyebrows - least distinctive
        };
        
        private Dictionary<FaceFeature, float> _weights;
        
        public ScoringModule()
        {
            _weights = new Dictionary<FaceFeature, float>(DefaultWeights);
        }
        
        public ScoringModule(Dictionary<FaceFeature, float> customWeights)
        {
            _weights = customWeights ?? new Dictionary<FaceFeature, float>(DefaultWeights);
        }
        
        /// <summary>
        /// Compare two faces and get per-feature scores.
        /// Detects smile in TARGET for learning purposes - but doesn't adjust weights!
        /// The HierarchicalKnowledge tree learns how to handle smiles.
        /// </summary>
        public FeatureScoreResult Compare(
            ProportionsResult target, 
            ProportionsResult current,
            float[] targetLandmarks = null,
            float[] currentLandmarks = null)
        {
            var result = new FeatureScoreResult
            {
                Source = "ScoringModule"
            };
            
            // Detect smile in TARGET face - stored for learning purposes
            // NOTE: We DON'T adjust weights - the HierarchicalKnowledge tree
            // learns how to handle smiles via SmileLevel feature
            if (targetLandmarks != null)
            {
                result.TargetSmile = LandmarkUtils.DetectSmile(targetLandmarks);
            }
            
            // Calculate per-feature scores
            result.FaceShapeScore = CompareFeature(target?.FaceGeometry, current?.FaceGeometry);
            result.EyesScore = CompareFeature(target?.Eyes, current?.Eyes);
            result.NoseScore = CompareFeature(target?.Nose, current?.Nose);
            result.MouthScore = CompareFeature(target?.Mouth, current?.Mouth);
            result.JawScore = CompareFeature(target?.Jaw, current?.Jaw);
            result.EyebrowsScore = CompareFeature(target?.Eyebrows, current?.Eyebrows);
            
            // Calculate landmark score if available
            if (targetLandmarks != null && currentLandmarks != null)
            {
                result.LandmarkScore = CalculateLandmarkScore(targetLandmarks, currentLandmarks);
            }
            
            // Calculate weighted overall - NO smile adjustments, let learning handle it
            float featureScore = 
                result.FaceShapeScore * _weights[FaceFeature.FaceShape] +
                result.EyesScore * _weights[FaceFeature.Eyes] +
                result.NoseScore * _weights[FaceFeature.Nose] +
                result.MouthScore * _weights[FaceFeature.Mouth] +
                result.JawScore * _weights[FaceFeature.Jaw] +
                result.EyebrowsScore * _weights[FaceFeature.Eyebrows];
            
            // v3.0.22: Blend weighted average with worst-feature penalty
            // Prevents one terrible feature from being masked by good others
            // Old: nose=0.3 + others=0.95 → 0.79. New: 0.84*0.7 + 0.3*0.3 = 0.68
            float worstFeatureScore = Math.Min(
                Math.Min(result.FaceShapeScore, result.EyesScore),
                Math.Min(result.NoseScore,
                Math.Min(result.MouthScore,
                Math.Min(result.JawScore, result.EyebrowsScore))));
            result.FeatureOnlyScore = featureScore * 0.7f + worstFeatureScore * 0.3f;

            // Calculate shape match correction
            // This is the KEY FIX: penalize wrong face shapes even if individual features look OK
            result.ShapeMatchScore = CalculateShapeMatch(
                target?.FaceGeometry?.Shape, 
                current?.FaceGeometry?.Shape);
            
            // Apply shape correction to overall score
            // v3.0.22: Now uses FeatureOnlyScore (includes worst-feature penalty) instead of raw featureScore
            // If shapes match: Overall = FeatureOnlyScore * 1.0 (no change)
            // If shapes mismatch: Overall = FeatureOnlyScore * 0.4-0.85 (penalty)
            result.Overall = result.FeatureOnlyScore * result.ShapeMatchScore;
            
            // Find worst feature
            var worst = result.GetFeaturesByScore().First();
            result.WorstFeature = worst.feature;
            result.WorstScore = worst.score;
            
            // Confidence based on how many features were analyzed
            int analyzedCount = 0;
            if (target?.FaceGeometry != null && current?.FaceGeometry != null) analyzedCount++;
            if (target?.Eyes != null && current?.Eyes != null) analyzedCount++;
            if (target?.Nose != null && current?.Nose != null) analyzedCount++;
            if (target?.Mouth != null && current?.Mouth != null) analyzedCount++;
            if (target?.Jaw != null && current?.Jaw != null) analyzedCount++;
            if (target?.Eyebrows != null && current?.Eyebrows != null) analyzedCount++;
            
            result.Confidence = analyzedCount / 6f;
            
            return result;
        }
        
        /// <summary>
        /// Calculate shape match score between target and current face shape.
        /// Returns 1.0 for exact match, lower values for mismatches.
        /// 
        /// This is CRITICAL for fixing the "sharp proportions" bug where
        /// Diamond faces get high feature scores but wrong overall shape.
        /// </summary>
        private float CalculateShapeMatch(FaceShape? targetShape, FaceShape? currentShape)
        {
            // If either shape is unknown, use neutral score
            if (!targetShape.HasValue || !currentShape.HasValue ||
                targetShape == FaceShape.Unknown || currentShape == FaceShape.Unknown)
            {
                return 0.9f;  // Slightly conservative but not penalizing
            }
            
            var target = targetShape.Value;
            var current = currentShape.Value;
            
            // Exact match - full score
            if (target == current)
            {
                return 1.0f;
            }
            
            // Shape similarity matrix
            // Higher = more similar shapes, lower = opposing shapes
            
            // VERY SIMILAR shapes (minor differences)
            if ((target == FaceShape.Round && current == FaceShape.Oval) ||
                (target == FaceShape.Oval && current == FaceShape.Round))
            {
                return 0.92f;  // Round/Oval are quite similar
            }
            
            if ((target == FaceShape.Oblong && current == FaceShape.Oval) ||
                (target == FaceShape.Oval && current == FaceShape.Oblong))
            {
                return 0.88f;  // Oblong is like stretched Oval
            }
            
            // SOMEWHAT SIMILAR (share some characteristics)
            if ((target == FaceShape.Square && current == FaceShape.Diamond) ||
                (target == FaceShape.Diamond && current == FaceShape.Square))
            {
                return 0.78f;  // Both have angular features
            }
            
            if ((target == FaceShape.Heart && current == FaceShape.Diamond) ||
                (target == FaceShape.Diamond && current == FaceShape.Heart))
            {
                return 0.75f;  // Both taper toward chin
            }
            
            if ((target == FaceShape.Heart && current == FaceShape.Oval) ||
                (target == FaceShape.Oval && current == FaceShape.Heart))
            {
                return 0.8f;
            }
            
            // OPPOSING shapes (strong penalty)
            // Round ↔ Diamond: Complete opposites (soft vs angular)
            if ((target == FaceShape.Round && current == FaceShape.Diamond) ||
                (target == FaceShape.Diamond && current == FaceShape.Round))
            {
                return 0.5f;  // STRONG penalty - these are opposites
            }
            
            // Round ↔ Square: Round is soft, Square is angular
            if ((target == FaceShape.Round && current == FaceShape.Square) ||
                (target == FaceShape.Square && current == FaceShape.Round))
            {
                return 0.55f;
            }
            
            // Round ↔ Oblong: Width completely different
            if ((target == FaceShape.Round && current == FaceShape.Oblong) ||
                (target == FaceShape.Oblong && current == FaceShape.Round))
            {
                return 0.55f;
            }
            
            // Round ↔ Heart: Different jaw characteristics
            if ((target == FaceShape.Round && current == FaceShape.Heart) ||
                (target == FaceShape.Heart && current == FaceShape.Round))
            {
                return 0.6f;
            }
            
            // Square ↔ Heart: Square has wide jaw, Heart has narrow chin
            if ((target == FaceShape.Square && current == FaceShape.Heart) ||
                (target == FaceShape.Heart && current == FaceShape.Square))
            {
                return 0.6f;
            }
            
            // Default for any other mismatch
            return 0.65f;
        }
        
        /// <summary>
        /// Compare face geometry
        /// BALANCED tolerances - strict enough to penalize wrong proportions,
        /// but realistic for Bannerlord's limited face shape range.
        /// </summary>
        private float CompareFeature(FaceGeometryResult target, FaceGeometryResult current)
        {
            if (target == null || current == null) return 0.5f; // Neutral score
            
            float score = 0;
            float weights = 0;
            
            // Width/height ratio - KEY for face proportions
            // "Face too big" usually means wrong ratio
            score += CompareValue(target.WidthHeightRatio, current.WidthHeightRatio, 0.08f) * 2.5f;
            weights += 2.5f;
            
            // Jaw angle - important for face shape
            score += CompareValue(target.JawAngle, current.JawAngle, 10f) * 1.5f;
            weights += 1.5f;
            
            // Symmetry - keep as is
            score += CompareValue(target.Symmetry, current.Symmetry, 0.10f);
            weights += 1;
            
            // Forehead ratio - important for proportions
            score += CompareValue(target.ForeheadRatio, current.ForeheadRatio, 0.05f) * 1.2f;
            weights += 1.2f;
            
            // Chin ratio - important for proportions
            score += CompareValue(target.ChinRatio, current.ChinRatio, 0.04f) * 1.5f;
            weights += 1.5f;
            
            return score / weights;
        }
        
        /// <summary>
        /// Compare eye analysis
        /// </summary>
        private float CompareFeature(EyeAnalysisResult target, EyeAnalysisResult current)
        {
            if (target == null || current == null) return 0.5f;
            
            float score = 0;
            float weights = 0;
            
            // Size - very important - TIGHTER
            score += CompareValue(target.SizeRatio, current.SizeRatio, 0.03f) * 2.0f;
            weights += 2.0f;
            
            // Spacing - important - TIGHTER
            score += CompareValue(target.InnerSpacing, current.InnerSpacing, 0.03f) * 1.5f;
            weights += 1.5f;
            score += CompareValue(target.OuterSpacing, current.OuterSpacing, 0.03f) * 1.2f;
            weights += 1.2f;
            
            // Openness - TIGHTER
            score += CompareValue(target.Openness, current.Openness, 0.05f) * 1.3f;
            weights += 1.3f;
            
            // Tilt - TIGHTER
            score += CompareValue(target.Tilt, current.Tilt, 5f) * 1.2f;
            weights += 1.2f;
            
            // Position - TIGHTER
            score += CompareValue(target.VerticalPosition, current.VerticalPosition, 0.03f) * 1.2f;
            weights += 1.2f;
            
            return score / weights;
        }
        
        /// <summary>
        /// Compare nose analysis
        /// STRICT tolerances - nose is highly distinctive and should penalize mismatches heavily
        /// </summary>
        private float CompareFeature(NoseAnalysisResult target, NoseAnalysisResult current)
        {
            if (target == null || current == null) return 0.5f;
            
            float score = 0;
            float weights = 0;
            
            // Length and width (most important) - VERY strict tolerances
            // A nose that is "too long and too wide" should score very poorly
            score += CompareValue(target.Length, current.Length, 0.025f) * 2.5f;
            weights += 2.5f;
            score += CompareValue(target.Width, current.Width, 0.025f) * 2.5f;
            weights += 2.5f;
            
            // Bridge - critical for nose shape
            score += CompareValue(target.BridgeWidth, current.BridgeWidth, 0.02f) * 1.5f;
            weights += 1.5f;
            
            // Tip - very important for distinctive noses
            score += CompareValue(target.TipWidth, current.TipWidth, 0.02f) * 1.5f;
            weights += 1.5f;
            score += CompareValue(target.TipAngle, current.TipAngle, 4f) * 1.2f;
            weights += 1.2f;
            
            // Nostril flare - important for wide/narrow noses
            score += CompareValue(target.NostrilFlare, current.NostrilFlare, 0.02f) * 1.3f;
            weights += 1.3f;
            
            return score / weights;
        }
        
        /// <summary>
        /// Compare mouth analysis
        /// </summary>
        private float CompareFeature(MouthAnalysisResult target, MouthAnalysisResult current)
        {
            if (target == null || current == null) return 0.5f;
            
            float score = 0;
            float weights = 0;
            
            // Width (most important) - TIGHTER
            score += CompareValue(target.Width, current.Width, 0.05f) * 2.0f;
            weights += 2.0f;
            
            // Lips - TIGHTER tolerances
            score += CompareValue(target.UpperLipHeight, current.UpperLipHeight, 0.02f) * 1.3f;
            weights += 1.3f;
            score += CompareValue(target.LowerLipHeight, current.LowerLipHeight, 0.02f) * 1.3f;
            weights += 1.3f;
            score += CompareValue(target.Fullness, current.Fullness, 0.05f) * 1.2f;
            weights += 1.2f;
            
            // Position - TIGHTER
            score += CompareValue(target.VerticalPosition, current.VerticalPosition, 0.03f) * 1.2f;
            weights += 1.2f;
            
            return score / weights;
        }
        
        /// <summary>
        /// Compare jaw analysis
        /// BALANCED: Jaw is hard to match in Bannerlord, but errors are visible.
        /// </summary>
        private float CompareFeature(JawAnalysisResult target, JawAnalysisResult current)
        {
            if (target == null || current == null) return 0.5f;
            
            float score = 0;
            float weights = 0;
            
            // Width - moderately strict
            score += CompareValue(target.Width, current.Width, 0.05f) * 1.5f;
            weights += 1.5f;
            
            // Taper (V-shape vs U-shape)
            score += CompareValue(target.Taper, current.Taper, 0.05f) * 1.5f;
            weights += 1.5f;
            
            // Angle
            score += CompareValue(target.Angle, current.Angle, 8f) * 1.0f;
            weights += 1.0f;
            
            // Chin width - important for round vs pointed
            score += CompareValue(target.ChinWidth, current.ChinWidth, 0.04f) * 1.5f;
            weights += 1.5f;
            
            // Chin pointedness - key differentiator
            score += CompareValue(target.ChinPointedness, current.ChinPointedness, 0.05f) * 1.5f;
            weights += 1.5f;
            
            return score / weights;
        }
        
        /// <summary>
        /// Compare eyebrow analysis
        /// </summary>
        private float CompareFeature(EyebrowAnalysisResult target, EyebrowAnalysisResult current)
        {
            if (target == null || current == null) return 0.5f;
            
            float score = 0;
            float weights = 0;
            
            // Length and thickness - TIGHTER
            score += CompareValue(target.Length, current.Length, 0.05f) * 1.2f;
            weights += 1.2f;
            score += CompareValue(target.Thickness, current.Thickness, 0.03f) * 1.3f;
            weights += 1.3f;
            
            // Arch - TIGHTER
            score += CompareValue(target.ArchHeight, current.ArchHeight, 0.03f) * 1.2f;
            weights += 1.2f;
            score += CompareValue(target.ArchPosition, current.ArchPosition, 0.06f);
            weights += 1f;
            
            // Spacing and tilt - TIGHTER
            score += CompareValue(target.Spacing, current.Spacing, 0.03f) * 1.2f;
            weights += 1.2f;
            score += CompareValue(target.Tilt, current.Tilt, 3f) * 1.1f;
            weights += 1.1f;
            
            return score / weights;
        }
        
        /// <summary>
        /// Compare two values with a tolerance, returning 0-1 score
        /// Uses VERY steep falloff to be highly discriminating
        /// </summary>
        private float CompareValue(float target, float current, float tolerance)
        {
            float diff = Math.Abs(target - current);
            float normalized = diff / tolerance;
            // v3.0.22: Tukey biweight (1-x²)² replaces exp(-5*x²)
            // Old exp had "mushy middle": 0.25-0.5 tolerance all scored 0.29-0.73
            // Tukey is more discriminating in the critical range:
            //
            // At diff=0.1*tolerance:  score = 0.96  (was 0.95)
            // At diff=0.25*tolerance: score = 0.82  (was 0.73)  — better for "almost right"
            // At diff=0.5*tolerance:  score = 0.39  (was 0.29)  — clearer "noticeably wrong"
            // At diff=0.75*tolerance: score = 0.10  (was 0.07)
            // At diff>=tolerance:     score = 0.00  (was 0.007) — hard cutoff, cleaner
            if (normalized >= 1.0f) return 0.0f;
            float t = 1.0f - normalized * normalized;
            return t * t;
        }
        
        /// <summary>
        /// Calculate landmark-based score (fallback)
        /// </summary>
        private float CalculateLandmarkScore(float[] target, float[] current)
        {
            if (target == null || current == null) return 0;
            
            int count = Math.Min(target.Length, current.Length);
            if (count == 0) return 0;
            
            float sumSqDiff = 0;
            for (int i = 0; i < count; i++)
            {
                float diff = target[i] - current[i];
                sumSqDiff += diff * diff;
            }
            
            float rmse = (float)Math.Sqrt(sumSqDiff / count);
            return (float)Math.Exp(-rmse * 5);  // Convert to 0-1 score
        }
        
        /// <summary>
        /// Get detailed sub-feature hints for morpher guidance.
        /// Shows WHAT specifically is wrong with each feature.
        /// Example: "Face:0.25 (width:bad, height:bad, roundness:okay)"
        /// </summary>
        public SubFeatureHints GetDetailedHints(
            ProportionsResult target, 
            ProportionsResult current)
        {
            var hints = new SubFeatureHints();
            
            hints.FaceShape = AnalyzeFaceShapeDetails(target?.FaceGeometry, current?.FaceGeometry);
            hints.Eyes = AnalyzeEyesDetails(target?.Eyes, current?.Eyes);
            hints.Nose = AnalyzeNoseDetails(target?.Nose, current?.Nose);
            hints.Mouth = AnalyzeMouthDetails(target?.Mouth, current?.Mouth);
            hints.Jaw = AnalyzeJawDetails(target?.Jaw, current?.Jaw);
            hints.Eyebrows = AnalyzeEyebrowsDetails(target?.Eyebrows, current?.Eyebrows);
            
            return hints;
        }
        
        private SubFeatureAnalysis AnalyzeFaceShapeDetails(FaceGeometryResult target, FaceGeometryResult current)
        {
            var analysis = new SubFeatureAnalysis { Feature = FaceFeature.FaceShape };
            
            if (target == null || current == null)
            {
                analysis.OverallScore = 0.5f;
                return analysis;
            }
            
            // Match tolerances from CompareFeature
            float widthHeight = CompareValue(target.WidthHeightRatio, current.WidthHeightRatio, 0.08f);
            float jawAngle = CompareValue(target.JawAngle, current.JawAngle, 10f);
            float symmetry = CompareValue(target.Symmetry, current.Symmetry, 0.10f);
            float foreheadRatio = CompareValue(target.ForeheadRatio, current.ForeheadRatio, 0.05f);
            float chinRatio = CompareValue(target.ChinRatio, current.ChinRatio, 0.04f);
            
            analysis.SubScores["ratio"] = widthHeight;
            analysis.SubScores["angle"] = jawAngle;
            analysis.SubScores["symmetry"] = symmetry;
            analysis.SubScores["forehead"] = foreheadRatio;
            analysis.SubScores["chin"] = chinRatio;
            
            // Convert to quality
            foreach (var kv in analysis.SubScores)
                analysis.SubQualities[kv.Key] = SubFeatureHints.ScoreToQuality(kv.Value);
            
            // Weighted overall
            analysis.OverallScore = (widthHeight * 2.5f + jawAngle * 1.5f + symmetry * 1.0f + 
                                     foreheadRatio * 1.2f + chinRatio * 1.5f) / 7.7f;
            
            return analysis;
        }
        
        private SubFeatureAnalysis AnalyzeEyesDetails(EyeAnalysisResult target, EyeAnalysisResult current)
        {
            var analysis = new SubFeatureAnalysis { Feature = FaceFeature.Eyes };
            
            if (target == null || current == null)
            {
                analysis.OverallScore = 0.5f;
                return analysis;
            }
            
            float size = CompareValue(target.SizeRatio, current.SizeRatio, 0.03f);
            float innerSpace = CompareValue(target.InnerSpacing, current.InnerSpacing, 0.03f);
            float outerSpace = CompareValue(target.OuterSpacing, current.OuterSpacing, 0.03f);
            float openness = CompareValue(target.Openness, current.Openness, 0.05f);
            float tilt = CompareValue(target.Tilt, current.Tilt, 5f);
            float vPos = CompareValue(target.VerticalPosition, current.VerticalPosition, 0.03f);
            
            analysis.SubScores["size"] = size;
            analysis.SubScores["spacing"] = (innerSpace + outerSpace) / 2f;
            analysis.SubScores["openness"] = openness;
            analysis.SubScores["tilt"] = tilt;
            analysis.SubScores["position"] = vPos;
            
            foreach (var kv in analysis.SubScores)
                analysis.SubQualities[kv.Key] = SubFeatureHints.ScoreToQuality(kv.Value);
            
            analysis.OverallScore = (size * 2.0f + innerSpace * 1.5f + outerSpace * 1.2f + 
                                     openness * 1.3f + tilt * 1.2f + vPos * 1.2f) / 8.4f;
            
            return analysis;
        }
        
        private SubFeatureAnalysis AnalyzeNoseDetails(NoseAnalysisResult target, NoseAnalysisResult current)
        {
            var analysis = new SubFeatureAnalysis { Feature = FaceFeature.Nose };
            
            if (target == null || current == null)
            {
                analysis.OverallScore = 0.5f;
                return analysis;
            }
            
            // Match tolerances from CompareFeature - STRICT
            float length = CompareValue(target.Length, current.Length, 0.025f);
            float width = CompareValue(target.Width, current.Width, 0.025f);
            float bridge = CompareValue(target.BridgeWidth, current.BridgeWidth, 0.02f);
            float tipWidth = CompareValue(target.TipWidth, current.TipWidth, 0.02f);
            float tipAngle = CompareValue(target.TipAngle, current.TipAngle, 4f);
            float nostril = CompareValue(target.NostrilFlare, current.NostrilFlare, 0.02f);
            
            analysis.SubScores["length"] = length;
            analysis.SubScores["width"] = width;
            analysis.SubScores["bridge"] = bridge;
            analysis.SubScores["tip"] = (tipWidth + tipAngle) / 2f;
            analysis.SubScores["nostril"] = nostril;
            
            foreach (var kv in analysis.SubScores)
                analysis.SubQualities[kv.Key] = SubFeatureHints.ScoreToQuality(kv.Value);
            
            analysis.OverallScore = (length * 2.5f + width * 2.5f + bridge * 1.5f + 
                                     tipWidth * 1.5f + tipAngle * 1.2f + nostril * 1.3f) / 10.5f;
            
            return analysis;
        }
        
        private SubFeatureAnalysis AnalyzeMouthDetails(MouthAnalysisResult target, MouthAnalysisResult current)
        {
            var analysis = new SubFeatureAnalysis { Feature = FaceFeature.Mouth };
            
            if (target == null || current == null)
            {
                analysis.OverallScore = 0.5f;
                return analysis;
            }
            
            float width = CompareValue(target.Width, current.Width, 0.05f);
            float upperLip = CompareValue(target.UpperLipHeight, current.UpperLipHeight, 0.02f);
            float lowerLip = CompareValue(target.LowerLipHeight, current.LowerLipHeight, 0.02f);
            float fullness = CompareValue(target.Fullness, current.Fullness, 0.05f);
            float vPos = CompareValue(target.VerticalPosition, current.VerticalPosition, 0.03f);
            
            analysis.SubScores["width"] = width;
            analysis.SubScores["upper"] = upperLip;
            analysis.SubScores["lower"] = lowerLip;
            analysis.SubScores["fullness"] = fullness;
            analysis.SubScores["position"] = vPos;
            
            foreach (var kv in analysis.SubScores)
                analysis.SubQualities[kv.Key] = SubFeatureHints.ScoreToQuality(kv.Value);
            
            analysis.OverallScore = (width * 2.0f + upperLip * 1.3f + lowerLip * 1.3f + 
                                     fullness * 1.2f + vPos * 1.2f) / 7.0f;
            
            return analysis;
        }
        
        private SubFeatureAnalysis AnalyzeJawDetails(JawAnalysisResult target, JawAnalysisResult current)
        {
            var analysis = new SubFeatureAnalysis { Feature = FaceFeature.Jaw };
            
            if (target == null || current == null)
            {
                analysis.OverallScore = 0.5f;
                return analysis;
            }
            
            // Match tolerances from CompareFeature
            float width = CompareValue(target.Width, current.Width, 0.05f);
            float taper = CompareValue(target.Taper, current.Taper, 0.05f);
            float angle = CompareValue(target.Angle, current.Angle, 8f);
            float chinWidth = CompareValue(target.ChinWidth, current.ChinWidth, 0.04f);
            float chinPoint = CompareValue(target.ChinPointedness, current.ChinPointedness, 0.05f);
            
            analysis.SubScores["width"] = width;
            analysis.SubScores["taper"] = taper;
            analysis.SubScores["angle"] = angle;
            analysis.SubScores["chin_w"] = chinWidth;
            analysis.SubScores["chin_pt"] = chinPoint;
            
            foreach (var kv in analysis.SubScores)
                analysis.SubQualities[kv.Key] = SubFeatureHints.ScoreToQuality(kv.Value);
            
            analysis.OverallScore = (width * 1.5f + taper * 1.5f + angle * 1.0f + 
                                     chinWidth * 1.5f + chinPoint * 1.5f) / 7.0f;
            
            return analysis;
        }
        
        private SubFeatureAnalysis AnalyzeEyebrowsDetails(EyebrowAnalysisResult target, EyebrowAnalysisResult current)
        {
            var analysis = new SubFeatureAnalysis { Feature = FaceFeature.Eyebrows };
            
            if (target == null || current == null)
            {
                analysis.OverallScore = 0.5f;
                return analysis;
            }
            
            float length = CompareValue(target.Length, current.Length, 0.05f);
            float thickness = CompareValue(target.Thickness, current.Thickness, 0.03f);
            float archH = CompareValue(target.ArchHeight, current.ArchHeight, 0.03f);
            float archPos = CompareValue(target.ArchPosition, current.ArchPosition, 0.06f);
            float spacing = CompareValue(target.Spacing, current.Spacing, 0.03f);
            float tilt = CompareValue(target.Tilt, current.Tilt, 3f);
            
            analysis.SubScores["length"] = length;
            analysis.SubScores["thick"] = thickness;
            analysis.SubScores["arch"] = (archH + archPos) / 2f;
            analysis.SubScores["spacing"] = spacing;
            analysis.SubScores["tilt"] = tilt;
            
            foreach (var kv in analysis.SubScores)
                analysis.SubQualities[kv.Key] = SubFeatureHints.ScoreToQuality(kv.Value);
            
            analysis.OverallScore = (length * 1.2f + thickness * 1.3f + archH * 1.2f + 
                                     archPos * 1.0f + spacing * 1.2f + tilt * 1.1f) / 7.0f;
            
            return analysis;
        }
    }
}
