using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FaceLearner.ML.Modules;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.Scoring;
using FaceLearner.ML.Modules.Proportions;

// Alias to avoid conflict with local FaceShape enum
using ModuleFaceShape = FaceLearner.ML.Modules.Core.FaceShape;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Adapter that integrates the new Module System with LearningOrchestrator.
    /// 
    /// LIGHTWEIGHT VERSION: Uses existing landmarks from main detector instead of
    /// creating its own FaceAnalyzer. This avoids duplicate ONNX sessions and freezes.
    /// 
    /// Benefits:
    /// 1. Feature-based scoring (know WHICH feature is wrong)
    /// 2. No duplicate landmark detection
    /// 3. Fast - only calculates proportions from landmarks
    /// </summary>
    public class ModuleIntegration : IDisposable
    {
        private ProportionsModule _proportionsModule;
        private ScoringModule _scoringModule;
        private bool _isReady;
        
        // Cached analysis results
        private ProportionsResult _targetProportions;
        private ProportionsResult _currentProportions;
        private float[] _targetLandmarks;
        private FeatureScoreResult _lastFeatureScore;
        
        // Feature weights for guided mutation
        private static readonly float[] FeatureWeights = new float[]
        {
            1.0f,   // FaceShape
            1.2f,   // Eyes (most important)
            0.9f,   // Nose
            1.0f,   // Mouth
            0.8f,   // Jaw
            0.6f    // Eyebrows
        };
        
        public bool IsReady => _isReady;
        
        /// <summary>
        /// Last feature-based score result
        /// </summary>
        public FeatureScoreResult LastFeatureScore => _lastFeatureScore;
        
        /// <summary>
        /// Which feature is currently the worst match?
        /// </summary>
        public FaceFeature? WorstFeature => _lastFeatureScore?.WorstFeature;
        
        /// <summary>
        /// Score of the worst feature
        /// </summary>
        public float WorstFeatureScore => _lastFeatureScore?.WorstScore ?? 0f;
        
        /// <summary>
        /// Initialize the module integration (lightweight - no FaceAnalyzer)
        /// </summary>
        public bool Initialize(string basePath)
        {
            try
            {
                // Only create the lightweight modules we actually need
                _proportionsModule = new ProportionsModule();
                _proportionsModule.Initialize(basePath);
                
                _scoringModule = new ScoringModule();
                
                _isReady = true;
                SubModule.Log("ModuleIntegration: ✓ Ready (lightweight mode - uses existing landmarks)");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"ModuleIntegration: Failed to initialize - {ex.Message}");
                _isReady = false;
                return false;
            }
        }
        
        #region Target Analysis
        
        /// <summary>Cached smile info for target face</summary>
        private SmileInfo _targetSmile;
        
        /// <summary>Get smile info for current target</summary>
        public SmileInfo TargetSmile => _targetSmile;
        
        /// <summary>
        /// Set target landmarks for scoring.
        /// Call this once when target changes.
        /// </summary>
        public void SetTargetLandmarks(float[] landmarks)
        {
            if (!_isReady || landmarks == null || landmarks.Length < 936)
            {
                _targetLandmarks = null;
                _targetProportions = null;
                _targetSmile = null;
                return;
            }
            
            try
            {
                _targetLandmarks = landmarks;
                _targetProportions = _proportionsModule.Analyze(landmarks, null);
                
                // Detect smile - this affects which features we can trust
                _targetSmile = LandmarkUtils.DetectSmile(landmarks);
                
                if (_targetProportions != null && _targetProportions.Confidence > 0)
                {
                    var shape = _targetProportions.FaceGeometry?.Shape ?? ModuleFaceShape.Unknown;
                    
                    // Log with smile info
                    string smileWarning = "";
                    if (_targetSmile != null && _targetSmile.IsBigSmile)
                    {
                        smileWarning = $" ⚠️ BIG SMILE detected (score={_targetSmile.SmileScore:F2}) - Mouth/Jaw/Face unreliable!";
                    }
                    else if (_targetSmile != null && _targetSmile.IsSmiling)
                    {
                        smileWarning = $" 😊 Smile detected (score={_targetSmile.SmileScore:F2}) - Mouth/Jaw reduced weight";
                    }
                    
                    SubModule.Log($"ModuleIntegration: Target proportions cached (Shape={shape}, Conf={_targetProportions.Confidence:F2}){smileWarning}");
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"ModuleIntegration: SetTargetLandmarks failed - {ex.Message}");
                _targetProportions = null;
                _targetSmile = null;
            }
        }
        
        /// <summary>
        /// Check if target is ready for scoring
        /// </summary>
        public bool HasTarget => _targetProportions != null;
        
        /// <summary>
        /// Get the target proportions for body estimation
        /// </summary>
        public ProportionsResult TargetProportions => _targetProportions;
        
        #endregion
        
        #region Scoring
        
        /// <summary>
        /// Calculate feature-based match score using landmarks.
        /// This is the primary scoring method - fast and doesn't need images.
        /// Now includes shape match correction.
        /// </summary>
        public float CalculateFeatureScore(float[] currentLandmarks)
        {
            if (!_isReady || _targetProportions == null || currentLandmarks == null)
            {
                return 0f;
            }
            
            try
            {
                // Analyze current landmarks
                _currentProportions = _proportionsModule.Analyze(currentLandmarks, null);
                
                if (_currentProportions == null)
                {
                    return 0f;
                }
                
                // Get per-feature scores (now includes shape match)
                _lastFeatureScore = _scoringModule.Compare(
                    _targetProportions,
                    _currentProportions,
                    _targetLandmarks,
                    currentLandmarks);
                
                // Log shape mismatch warnings for debugging (only occasionally to avoid spam)
                if (_lastFeatureScore != null && _lastFeatureScore.ShapeMatchScore < 0.7f)
                {
                    var targetShape = _targetProportions?.FaceGeometry?.Shape;
                    var currentShape = _currentProportions?.FaceGeometry?.Shape;
                    // Only log every 50 iterations to avoid spam
                    if (_shapeMismatchLogCounter++ % 50 == 0)
                    {
                        SubModule.Log($"ModuleIntegration: ⚠️ Shape mismatch! Target={targetShape}, Current={currentShape}, Penalty={_lastFeatureScore.ShapeMatchScore:F2}");
                    }
                }
                
                return _lastFeatureScore?.Overall ?? 0f;
            }
            catch (Exception ex)
            {
                SubModule.Log($"ModuleIntegration: Scoring failed - {ex.Message}");
                return 0f;
            }
        }
        
        private int _shapeMismatchLogCounter = 0;
        
        /// <summary>
        /// Calculate feature-based match score (legacy - with bitmap).
        /// Note: This is slower as it can't use pre-detected landmarks.
        /// </summary>
        public float CalculateFeatureScore(Bitmap currentRender)
        {
            // Legacy method - return 0 since we don't have our own detector anymore
            // Caller should use the landmark-based version
            return 0f;
        }
        
        /// <summary>
        /// Calculate score using raw landmarks (backward compatible)
        /// </summary>
        public float CalculateLandmarkScore(float[] currentLandmarks, float[] targetLandmarks)
        {
            return CompareLandmarks(targetLandmarks, currentLandmarks);
        }
        
        /// <summary>
        /// Compare raw landmarks
        /// </summary>
        public float CompareLandmarks(float[] target, float[] current)
        {
            if (target == null || current == null) return 0f;
            
            int count = Math.Min(target.Length, current.Length);
            if (count == 0) return 0f;
            
            float sumSqDiff = 0;
            for (int i = 0; i < count; i++)
            {
                float diff = target[i] - current[i];
                sumSqDiff += diff * diff;
            }
            
            float rmse = (float)Math.Sqrt(sumSqDiff / count);
            return (float)Math.Exp(-rmse * 5);
        }
        
        /// <summary>
        /// Get detailed feature breakdown as string
        /// </summary>
        public string GetFeatureBreakdown()
        {
            if (_lastFeatureScore == null)
            {
                return "No score available";
            }
            
            return _lastFeatureScore.ToString();
        }
        
        /// <summary>
        /// Get feature scores as dictionary for learning
        /// </summary>
        public Dictionary<string, float> GetFeatureBreakdown(float[] currentLandmarks)
        {
            var result = new Dictionary<string, float>();
            
            if (_scoringModule == null || _targetProportions == null || currentLandmarks == null)
                return result;
            
            try
            {
                var currentProps = _proportionsModule?.Analyze(currentLandmarks, null);
                if (currentProps == null) return result;
                
                var score = _scoringModule.Compare(_targetProportions, currentProps);
                if (score == null) return result;
                
                result["Face"] = score.FaceShapeScore;
                result["Eyes"] = score.EyesScore;
                result["Nose"] = score.NoseScore;
                result["Mouth"] = score.MouthScore;
                result["Jaw"] = score.JawScore;
                result["Brows"] = score.EyebrowsScore;
                result["ShapeMatch"] = score.ShapeMatchScore;
                result["FeatureOnly"] = score.FeatureOnlyScore;
            }
            catch
            {
                // Ignore errors
            }
            
            return result;
        }
        
        /// <summary>
        /// Get current shape match status for debugging/UI
        /// </summary>
        public (bool isMatching, float score, string targetShape, string currentShape) GetShapeMatchStatus()
        {
            if (_lastFeatureScore == null || _targetProportions == null || _currentProportions == null)
            {
                return (true, 1.0f, "Unknown", "Unknown");
            }
            
            var targetShape = _targetProportions.FaceGeometry?.Shape.ToString() ?? "Unknown";
            var currentShape = _currentProportions.FaceGeometry?.Shape.ToString() ?? "Unknown";
            var score = _lastFeatureScore.ShapeMatchScore;
            var isMatching = score >= 0.85f;
            
            return (isMatching, score, targetShape, currentShape);
        }
        
        /// <summary>
        /// Get detailed sub-feature hints for morpher guidance.
        /// Returns analysis like: "Face:0.25 (ratio:bad, angle:bad, cheeks:okay)"
        /// </summary>
        public SubFeatureHints GetDetailedHints(float[] currentLandmarks)
        {
            if (_scoringModule == null || _targetProportions == null || currentLandmarks == null)
                return null;
            
            try
            {
                var currentProps = _proportionsModule?.Analyze(currentLandmarks, null);
                if (currentProps == null) return null;
                
                return _scoringModule.GetDetailedHints(_targetProportions, currentProps);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Get mutation priorities based on detailed hints.
        /// Returns: { FaceShape: ["ratio", "angle"], Jaw: ["width", "chin_pt"] }
        /// </summary>
        public Dictionary<FaceFeature, List<string>> GetMutationPriorities(float[] currentLandmarks, float threshold = 0.55f)
        {
            var hints = GetDetailedHints(currentLandmarks);
            if (hints == null)
                return new Dictionary<FaceFeature, List<string>>();
            
            return hints.GetMutationPriorities(threshold);
        }
        
        /// <summary>
        /// Get problem features (below threshold)
        /// </summary>
        public FaceFeature[] GetProblemFeatures(float threshold = 0.5f)
        {
            if (_lastFeatureScore == null)
            {
                return Array.Empty<FaceFeature>();
            }
            
            var problems = new System.Collections.Generic.List<FaceFeature>();
            foreach (var feature in _lastFeatureScore.GetProblemFeatures(threshold))
            {
                problems.Add(feature);
            }
            return problems.ToArray();
        }
        
        #endregion
        
        #region Guided Mutation
        
        /// <summary>
        /// Get mutation guidance based on feature scores.
        /// Returns which morph indices should be mutated more/less.
        /// </summary>
        public MutationGuidance GetMutationGuidance()
        {
            if (_lastFeatureScore == null)
            {
                return MutationGuidance.Default;
            }
            
            var guidance = new MutationGuidance();
            
            // Find worst features
            var worstFeatures = _lastFeatureScore.GetFeaturesByScore().ToArray();
            
            // Map features to morph index ranges (approximate)
            // This is game-specific and may need calibration
            foreach (var (feature, score) in worstFeatures)
            {
                float priority = 1f - score;  // Lower score = higher priority
                
                switch (feature)
                {
                    case FaceFeature.Eyes:
                        // Eye morphs typically indices 0-15 in Bannerlord
                        guidance.AddRange(0, 15, priority * 1.5f);
                        break;
                        
                    case FaceFeature.Nose:
                        // Nose morphs typically 16-25
                        guidance.AddRange(16, 25, priority * 1.3f);
                        break;
                        
                    case FaceFeature.Mouth:
                        // Mouth morphs typically 26-35
                        guidance.AddRange(26, 35, priority * 1.2f);
                        break;
                        
                    case FaceFeature.FaceShape:
                        // Face shape morphs typically 36-45
                        guidance.AddRange(36, 45, priority * 1.1f);
                        break;
                        
                    case FaceFeature.Jaw:
                        // Jaw morphs typically 46-55
                        guidance.AddRange(46, 55, priority);
                        break;
                        
                    case FaceFeature.Eyebrows:
                        // Eyebrow morphs typically 56-63
                        guidance.AddRange(56, 63, priority * 0.8f);
                        break;
                }
            }
            
            return guidance;
        }
        
        #endregion
        
        public void Dispose()
        {
            _proportionsModule?.Dispose();
            _proportionsModule = null;
            _scoringModule = null;
            _targetProportions = null;
            _currentProportions = null;
            _targetLandmarks = null;
            _lastFeatureScore = null;
            _isReady = false;
        }
    }
    
    #region Data Classes
    
    /// <summary>
    /// Demographics result for target character setup
    /// </summary>
    public struct TargetDemographics
    {
        public bool IsValid;
        public bool IsFemale;
        public float GenderConfidence;
        public bool HasFacialHair;
        public int Age;
        public float AgeConfidence;
        public float SkinTone;
        public float SkinToneConfidence;
        public string Race;
        public float RaceConfidence;
        public FaceShape FaceShape;
        
        public static TargetDemographics Unknown => new TargetDemographics
        {
            IsValid = false,
            IsFemale = false,
            GenderConfidence = 0,
            Age = 30,
            AgeConfidence = 0,
            SkinTone = 0.5f,
            SkinToneConfidence = 0
        };
    }
    
    /// <summary>
    /// Mutation guidance based on feature scores
    /// </summary>
    public class MutationGuidance
    {
        private float[] _morphPriorities;
        
        public MutationGuidance(int morphCount = 70)
        {
            _morphPriorities = new float[morphCount];
            for (int i = 0; i < morphCount; i++)
            {
                _morphPriorities[i] = 1f;  // Default priority
            }
        }
        
        /// <summary>
        /// Add priority to a range of morphs
        /// </summary>
        public void AddRange(int start, int end, float priority)
        {
            for (int i = start; i <= end && i < _morphPriorities.Length; i++)
            {
                _morphPriorities[i] = Math.Max(_morphPriorities[i], priority);
            }
        }
        
        /// <summary>
        /// Get priority for a specific morph index
        /// </summary>
        public float GetPriority(int morphIndex)
        {
            if (morphIndex < 0 || morphIndex >= _morphPriorities.Length)
                return 1f;
            return _morphPriorities[morphIndex];
        }
        
        /// <summary>
        /// Should this morph be included in mutation?
        /// Higher priority = more likely to mutate
        /// </summary>
        public bool ShouldMutate(int morphIndex, Random random)
        {
            float priority = GetPriority(morphIndex);
            return random.NextDouble() < priority;
        }
        
        /// <summary>
        /// Get indices sorted by priority (highest first)
        /// </summary>
        public int[] GetPrioritizedIndices()
        {
            var indices = new int[_morphPriorities.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            
            Array.Sort(indices, (a, b) => _morphPriorities[b].CompareTo(_morphPriorities[a]));
            return indices;
        }
        
        public static MutationGuidance Default => new MutationGuidance();
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Helper to convert between Module FaceShape and local FaceShape
    /// </summary>
    internal static class FaceShapeConverter
    {
        public static FaceShape Convert(ModuleFaceShape? shape)
        {
            if (!shape.HasValue) return FaceShape.Unknown;
            
            switch (shape.Value)
            {
                case ModuleFaceShape.Oval: return FaceShape.Oval;
                case ModuleFaceShape.Round: return FaceShape.Round;
                case ModuleFaceShape.Square: return FaceShape.Square;
                case ModuleFaceShape.Heart: return FaceShape.Heart;
                case ModuleFaceShape.Oblong: return FaceShape.Oblong;
                case ModuleFaceShape.Diamond: return FaceShape.Diamond;
                default: return FaceShape.Unknown;
            }
        }
    }
    
    #endregion
}
