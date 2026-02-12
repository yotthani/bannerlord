using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FaceLearner.ML;
using FaceLearner.ML.Modules.Core;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Feature Intelligence System - Implements:
    /// 1. Adaptive Feature Weights - dynamically adjust based on difficulty
    /// 2. Feature Correlation Learning - learn which morphs affect multiple features
    /// 3. Progressive Feature Focus - broad first, then focus on weak features
    /// 4. Feature History Tracking - track progress, detect regressions
    /// </summary>
    public class FeatureIntelligence
    {
        #region Constants
        
        private static readonly string[] ALL_FEATURES = { "Face", "Eyes", "Nose", "Mouth", "Jaw", "Brows" };
        
        // Default weights (same as ScoringModule)
        private static readonly Dictionary<string, float> DEFAULT_WEIGHTS = new Dictionary<string, float>
        {
            { "Eyes", 0.25f },
            { "Nose", 0.18f },
            { "Face", 0.17f },
            { "Mouth", 0.15f },
            { "Jaw", 0.13f },
            { "Brows", 0.12f }
        };
        
        // How much to boost weight for difficult features (max multiplier)
        private const float MAX_WEIGHT_BOOST = 2.0f;
        
        // Thresholds for difficulty classification
        private const float EASY_THRESHOLD = 0.6f;    // Above this = easy to match
        private const float MEDIUM_THRESHOLD = 0.4f;  // Between medium and easy
        private const float HARD_THRESHOLD = 0.25f;   // Below this = very hard
        
        // History tracking
        private const int HISTORY_WINDOW = 50;        // Track last N iterations
        private const int REGRESSION_WINDOW = 10;     // Check last N for regression
        private const float REGRESSION_THRESHOLD = 0.05f;  // Score drop to count as regression
        
        // Progressive focus phases
        private const int BROAD_PHASE_ITERATIONS = 20;     // First N iterations: broad optimization
        private const int NARROWING_PHASE_ITERATIONS = 40; // Next N: start focusing
        // After that: full focus on weak features
        
        #endregion
        
        #region Fields
        
        // === 1. Adaptive Feature Weights ===
        private Dictionary<string, float> _currentWeights = new Dictionary<string, float>();
        private Dictionary<string, float> _featureDifficulty = new Dictionary<string, float>();
        private Dictionary<string, int> _featureImprovementCount = new Dictionary<string, int>();
        private Dictionary<string, int> _featureAttemptCount = new Dictionary<string, int>();
        
        // === 2. Feature Correlation Learning ===
        // Maps morph index -> which features it affects and how strongly
        private Dictionary<int, Dictionary<string, float>> _morphFeatureCorrelations = new Dictionary<int, Dictionary<string, float>>();
        // Recent morph changes for correlation learning
        private List<MorphChangeRecord> _recentMorphChanges = new List<MorphChangeRecord>();
        private const int MAX_CHANGE_RECORDS = 100;
        
        // === 3. Progressive Feature Focus ===
        private int _currentIteration = 0;
        private FocusPhase _currentFocusPhase = FocusPhase.Broad;
        private List<string> _focusedFeatures = new List<string>();
        private float _broadPhaseAverageScore = 0f;
        
        // === 4. Feature History Tracking ===
        private Dictionary<string, Queue<FeatureHistoryEntry>> _featureHistory = new Dictionary<string, Queue<FeatureHistoryEntry>>();
        private Dictionary<string, RegressionInfo> _regressionAlerts = new Dictionary<string, RegressionInfo>();
        
        // Persistence
        private string _savePath;
        private bool _isDirty = false;
        
        #endregion
        
        #region Data Classes
        
        private class MorphChangeRecord
        {
            public int MorphIndex;
            public float Delta;
            public Dictionary<string, float> ScoresBefore;
            public Dictionary<string, float> ScoresAfter;
            public DateTime Timestamp;
        }
        
        private class FeatureHistoryEntry
        {
            public int Iteration;
            public float Score;
            public DateTime Timestamp;
        }
        
        public class RegressionInfo
        {
            public string Feature;
            public float PeakScore;
            public float CurrentScore;
            public int PeakIteration;
            public int CurrentIteration;
            public float DropAmount => PeakScore - CurrentScore;
            public bool IsActive => DropAmount > REGRESSION_THRESHOLD;
        }
        
        #endregion
        
        #region Constructor & Persistence
        
        public FeatureIntelligence(string savePath = null)
        {
            _savePath = savePath;
            
            // Initialize with default weights
            foreach (var kv in DEFAULT_WEIGHTS)
            {
                _currentWeights[kv.Key] = kv.Value;
                _featureDifficulty[kv.Key] = 0.5f;  // Assume medium difficulty initially
                _featureImprovementCount[kv.Key] = 0;
                _featureAttemptCount[kv.Key] = 0;
                _featureHistory[kv.Key] = new Queue<FeatureHistoryEntry>();
            }
            
            if (!string.IsNullOrEmpty(savePath) && File.Exists(savePath))
            {
                Load();
            }
        }
        
        public void Save()
        {
            if (string.IsNullOrEmpty(_savePath) || !_isDirty) return;
            
            try
            {
                using (var writer = new BinaryWriter(File.Open(_savePath, FileMode.Create)))
                {
                    // Version
                    writer.Write(1);
                    
                    // Feature difficulties
                    writer.Write(_featureDifficulty.Count);
                    foreach (var kv in _featureDifficulty)
                    {
                        writer.Write(kv.Key);
                        writer.Write(kv.Value);
                    }
                    
                    // Morph correlations
                    writer.Write(_morphFeatureCorrelations.Count);
                    foreach (var morphKv in _morphFeatureCorrelations)
                    {
                        writer.Write(morphKv.Key);
                        writer.Write(morphKv.Value.Count);
                        foreach (var featKv in morphKv.Value)
                        {
                            writer.Write(featKv.Key);
                            writer.Write(featKv.Value);
                        }
                    }
                    
                    // Improvement counts
                    writer.Write(_featureImprovementCount.Count);
                    foreach (var kv in _featureImprovementCount)
                    {
                        writer.Write(kv.Key);
                        writer.Write(kv.Value);
                        writer.Write(_featureAttemptCount.ContainsKey(kv.Key) ? _featureAttemptCount[kv.Key] : 0);
                    }
                }
                
                _isDirty = false;
                SubModule.Log($"[FeatureIntelligence] Saved to {_savePath}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"[FeatureIntelligence] Save failed: {ex.Message}");
            }
        }
        
        public void Load()
        {
            if (string.IsNullOrEmpty(_savePath) || !File.Exists(_savePath)) return;
            
            try
            {
                using (var reader = new BinaryReader(File.Open(_savePath, FileMode.Open)))
                {
                    int version = reader.ReadInt32();
                    
                    // Feature difficulties
                    int diffCount = reader.ReadInt32();
                    for (int i = 0; i < diffCount; i++)
                    {
                        string feat = reader.ReadString();
                        float diff = reader.ReadSingle();
                        _featureDifficulty[feat] = diff;
                    }
                    
                    // Morph correlations
                    int morphCount = reader.ReadInt32();
                    for (int i = 0; i < morphCount; i++)
                    {
                        int morphIdx = reader.ReadInt32();
                        int featCount = reader.ReadInt32();
                        _morphFeatureCorrelations[morphIdx] = new Dictionary<string, float>();
                        for (int j = 0; j < featCount; j++)
                        {
                            string feat = reader.ReadString();
                            float corr = reader.ReadSingle();
                            _morphFeatureCorrelations[morphIdx][feat] = corr;
                        }
                    }
                    
                    // Improvement counts
                    int impCount = reader.ReadInt32();
                    for (int i = 0; i < impCount; i++)
                    {
                        string feat = reader.ReadString();
                        int imps = reader.ReadInt32();
                        int atts = reader.ReadInt32();
                        _featureImprovementCount[feat] = imps;
                        _featureAttemptCount[feat] = atts;
                    }
                }
                
                // Recalculate weights after loading
                RecalculateAdaptiveWeights();
                
                SubModule.Log($"[FeatureIntelligence] Loaded from {_savePath}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"[FeatureIntelligence] Load failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 1. Adaptive Feature Weights
        
        /// <summary>
        /// Get current adaptive weights for scoring.
        /// Difficult features get higher weights to prioritize them.
        /// </summary>
        public Dictionary<string, float> GetAdaptiveWeights()
        {
            return new Dictionary<string, float>(_currentWeights);
        }
        
        /// <summary>
        /// Get the adaptive weight for a specific feature.
        /// </summary>
        public float GetWeight(string feature)
        {
            return _currentWeights.ContainsKey(feature) ? _currentWeights[feature] : 0.15f;
        }
        
        /// <summary>
        /// Update feature difficulty based on observed scores.
        /// Call this after each iteration with the current feature scores.
        /// </summary>
        public void UpdateFeatureDifficulty(Dictionary<string, float> featureScores)
        {
            if (featureScores == null || featureScores.Count == 0) return;
            
            foreach (var kv in featureScores)
            {
                string feature = kv.Key;
                float score = kv.Value;
                
                if (!_featureDifficulty.ContainsKey(feature)) continue;
                
                // Update difficulty estimate (EMA with factor 0.1)
                // Lower scores = higher difficulty
                float observedDifficulty = 1.0f - score;
                _featureDifficulty[feature] = _featureDifficulty[feature] * 0.9f + observedDifficulty * 0.1f;
            }
            
            RecalculateAdaptiveWeights();
            _isDirty = true;
        }
        
        /// <summary>
        /// Record that we attempted to improve a feature and whether it succeeded.
        /// </summary>
        public void RecordImprovementAttempt(string feature, bool succeeded)
        {
            // Only track features we know about (skip ShapeMatch, FeatureOnly, etc.)
            if (!_featureDifficulty.ContainsKey(feature))
            {
                return;  // Silently ignore unknown features
            }
            
            if (!_featureAttemptCount.ContainsKey(feature))
            {
                _featureAttemptCount[feature] = 0;
                _featureImprovementCount[feature] = 0;
            }
            
            _featureAttemptCount[feature]++;
            if (succeeded)
            {
                _featureImprovementCount[feature]++;
            }
            
            // Update difficulty based on success rate
            if (_featureAttemptCount[feature] >= 10)
            {
                float successRate = (float)_featureImprovementCount[feature] / _featureAttemptCount[feature];
                // Low success rate = high difficulty
                float rateBasedDifficulty = 1.0f - successRate;
                
                // Blend with score-based difficulty
                _featureDifficulty[feature] = _featureDifficulty[feature] * 0.7f + rateBasedDifficulty * 0.3f;
                
                RecalculateAdaptiveWeights();
            }
            
            _isDirty = true;
        }
        
        private void RecalculateAdaptiveWeights()
        {
            // Calculate boost multipliers based on difficulty
            Dictionary<string, float> boosts = new Dictionary<string, float>();
            float totalWeight = 0f;
            
            foreach (var feature in ALL_FEATURES)
            {
                float difficulty = _featureDifficulty.ContainsKey(feature) ? _featureDifficulty[feature] : 0.5f;
                float baseWeight = DEFAULT_WEIGHTS.ContainsKey(feature) ? DEFAULT_WEIGHTS[feature] : 0.15f;
                
                // Higher difficulty = higher boost
                // difficulty 0.0 (easy) -> boost 1.0
                // difficulty 0.5 (medium) -> boost 1.5
                // difficulty 1.0 (hard) -> boost 2.0
                float boost = 1.0f + difficulty * (MAX_WEIGHT_BOOST - 1.0f);
                
                boosts[feature] = baseWeight * boost;
                totalWeight += boosts[feature];
            }
            
            // Normalize to sum to 1.0
            foreach (var feature in ALL_FEATURES)
            {
                _currentWeights[feature] = boosts[feature] / totalWeight;
            }
        }
        
        /// <summary>
        /// Get difficulty rating for a feature (0=easy, 1=hard).
        /// </summary>
        public float GetDifficulty(string feature)
        {
            return _featureDifficulty.ContainsKey(feature) ? _featureDifficulty[feature] : 0.5f;
        }
        
        /// <summary>
        /// Get a summary of feature difficulties.
        /// </summary>
        public string GetDifficultySummary()
        {
            var sorted = _featureDifficulty.OrderByDescending(kv => kv.Value).ToList();
            return string.Join(", ", sorted.Select(kv => $"{kv.Key}:{kv.Value:F2}"));
        }
        
        #endregion
        
        #region 2. Feature Correlation Learning
        
        /// <summary>
        /// Record a morph change with before/after feature scores.
        /// This data is used to learn which morphs affect which features.
        /// </summary>
        public void RecordMorphChange(int morphIndex, float delta, 
            Dictionary<string, float> scoresBefore, Dictionary<string, float> scoresAfter)
        {
            if (scoresBefore == null || scoresAfter == null) return;
            if (Math.Abs(delta) < 0.01f) return;  // Ignore tiny changes
            
            var record = new MorphChangeRecord
            {
                MorphIndex = morphIndex,
                Delta = delta,
                ScoresBefore = new Dictionary<string, float>(scoresBefore),
                ScoresAfter = new Dictionary<string, float>(scoresAfter),
                Timestamp = DateTime.Now
            };
            
            _recentMorphChanges.Add(record);
            
            // Limit history size
            while (_recentMorphChanges.Count > MAX_CHANGE_RECORDS)
            {
                _recentMorphChanges.RemoveAt(0);
            }
            
            // Update correlations
            UpdateCorrelationsFromRecord(record);
            _isDirty = true;
        }
        
        private void UpdateCorrelationsFromRecord(MorphChangeRecord record)
        {
            if (!_morphFeatureCorrelations.ContainsKey(record.MorphIndex))
            {
                _morphFeatureCorrelations[record.MorphIndex] = new Dictionary<string, float>();
            }
            
            var correlations = _morphFeatureCorrelations[record.MorphIndex];
            
            foreach (string feature in ALL_FEATURES)
            {
                if (!record.ScoresBefore.ContainsKey(feature) || 
                    !record.ScoresAfter.ContainsKey(feature)) continue;
                
                float scoreDelta = record.ScoresAfter[feature] - record.ScoresBefore[feature];
                
                // Correlation = how much the feature changed relative to morph change
                // Normalized by morph delta magnitude
                float correlation = scoreDelta / Math.Max(Math.Abs(record.Delta), 0.01f);
                
                // EMA update
                if (!correlations.ContainsKey(feature))
                {
                    correlations[feature] = correlation;
                }
                else
                {
                    correlations[feature] = correlations[feature] * 0.8f + correlation * 0.2f;
                }
            }
        }
        
        /// <summary>
        /// Get the features most affected by a morph (sorted by correlation strength).
        /// </summary>
        public List<(string feature, float correlation)> GetMorphCorrelations(int morphIndex)
        {
            if (!_morphFeatureCorrelations.ContainsKey(morphIndex))
            {
                return new List<(string, float)>();
            }
            
            return _morphFeatureCorrelations[morphIndex]
                .Select(kv => (kv.Key, Math.Abs(kv.Value)))
                .OrderByDescending(x => x.Item2)
                .ToList();
        }
        
        /// <summary>
        /// Get morphs that most strongly affect a specific feature.
        /// Returns morph indices sorted by correlation strength.
        /// </summary>
        public List<(int morphIndex, float correlation)> GetMorphsForFeature(string feature)
        {
            var result = new List<(int, float)>();
            
            foreach (var kv in _morphFeatureCorrelations)
            {
                if (kv.Value.TryGetValue(feature, out float corr))
                {
                    result.Add((kv.Key, Math.Abs(corr)));
                }
            }
            
            return result.OrderByDescending(x => x.Item2).ToList();
        }
        
        /// <summary>
        /// Check if a morph affects multiple features (potential conflict).
        /// Returns true if the morph significantly affects 2+ features.
        /// </summary>
        public bool IsMorphConflicting(int morphIndex, float threshold = 0.1f)
        {
            if (!_morphFeatureCorrelations.ContainsKey(morphIndex)) return false;
            
            int significantFeatures = _morphFeatureCorrelations[morphIndex]
                .Count(kv => Math.Abs(kv.Value) > threshold);
            
            return significantFeatures >= 2;
        }
        
        /// <summary>
        /// Get a list of "safe" morphs for a feature - morphs that affect mainly this feature.
        /// </summary>
        public List<int> GetSafeMorphsForFeature(string feature, float minCorrelation = 0.1f)
        {
            var result = new List<int>();
            
            foreach (var kv in _morphFeatureCorrelations)
            {
                var correlations = kv.Value;
                
                // Check if this morph has strong correlation with target feature
                if (!correlations.TryGetValue(feature, out float targetCorr)) continue;
                if (Math.Abs(targetCorr) < minCorrelation) continue;
                
                // Check if it doesn't have strong correlation with other features
                bool isSafe = true;
                foreach (var otherFeature in ALL_FEATURES)
                {
                    if (otherFeature == feature) continue;
                    if (correlations.TryGetValue(otherFeature, out float otherCorr))
                    {
                        // If other feature has correlation >= 50% of target, it's not safe
                        if (Math.Abs(otherCorr) >= Math.Abs(targetCorr) * 0.5f)
                        {
                            isSafe = false;
                            break;
                        }
                    }
                }
                
                if (isSafe)
                {
                    result.Add(kv.Key);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get correlation statistics summary.
        /// </summary>
        public string GetCorrelationSummary()
        {
            int totalMorphs = _morphFeatureCorrelations.Count;
            int conflicting = _morphFeatureCorrelations.Keys.Count(m => IsMorphConflicting(m));
            
            return $"Correlations: {totalMorphs} morphs tracked, {conflicting} multi-feature";
        }
        
        #endregion
        
        #region 3. Progressive Feature Focus
        
        /// <summary>
        /// Update iteration count and recalculate focus phase.
        /// Call this at the start of each iteration.
        /// </summary>
        public void UpdateIteration(int iteration, Dictionary<string, float> currentScores)
        {
            _currentIteration = iteration;
            
            // Update history tracking
            if (currentScores != null)
            {
                RecordFeatureHistory(iteration, currentScores);
            }
            
            // Determine focus phase
            if (iteration <= BROAD_PHASE_ITERATIONS)
            {
                _currentFocusPhase = FocusPhase.Broad;
                _focusedFeatures.Clear();
                
                // Track average score during broad phase
                if (currentScores != null && currentScores.Count > 0)
                {
                    float avg = currentScores.Values.Average();
                    _broadPhaseAverageScore = _broadPhaseAverageScore * 0.9f + avg * 0.1f;
                }
            }
            else if (iteration <= NARROWING_PHASE_ITERATIONS)
            {
                _currentFocusPhase = FocusPhase.Narrowing;
                
                // Focus on bottom 2-3 features
                if (currentScores != null)
                {
                    _focusedFeatures = currentScores
                        .OrderBy(kv => kv.Value)
                        .Take(3)
                        .Select(kv => kv.Key)
                        .ToList();
                }
            }
            else
            {
                _currentFocusPhase = FocusPhase.Focused;
                
                // Focus on bottom 1-2 features
                if (currentScores != null)
                {
                    _focusedFeatures = currentScores
                        .OrderBy(kv => kv.Value)
                        .Take(2)
                        .Select(kv => kv.Key)
                        .ToList();
                }
            }
        }
        
        /// <summary>
        /// Get current focus phase.
        /// </summary>
        public FocusPhase CurrentPhase => _currentFocusPhase;
        
        /// <summary>
        /// Get the features currently being focused on.
        /// Empty during Broad phase.
        /// </summary>
        public List<string> FocusedFeatures => new List<string>(_focusedFeatures);
        
        /// <summary>
        /// Get probability that we should focus on weak features vs broad mutation.
        /// Returns 0.0 during Broad phase, increases through Narrowing to Focused.
        /// </summary>
        public float GetFocusProbability()
        {
            switch (_currentFocusPhase)
            {
                case FocusPhase.Broad:
                    return 0.0f;
                    
                case FocusPhase.Narrowing:
                    // Gradually increase from 0.3 to 0.6
                    float progress = (float)(_currentIteration - BROAD_PHASE_ITERATIONS) / 
                                   (NARROWING_PHASE_ITERATIONS - BROAD_PHASE_ITERATIONS);
                    return 0.3f + progress * 0.3f;
                    
                case FocusPhase.Focused:
                    return 0.7f;  // 70% chance to focus on weak features
                    
                default:
                    return 0.3f;
            }
        }
        
        /// <summary>
        /// Should we focus mutation on weak features this iteration?
        /// </summary>
        public bool ShouldFocusOnWeakFeatures(Random random)
        {
            return random.NextDouble() < GetFocusProbability();
        }
        
        /// <summary>
        /// Reset focus state for a new target.
        /// </summary>
        public void ResetForNewTarget()
        {
            _currentIteration = 0;
            _currentFocusPhase = FocusPhase.Broad;
            _focusedFeatures.Clear();
            _broadPhaseAverageScore = 0f;
            
            // Clear history for new target
            foreach (var key in _featureHistory.Keys.ToList())
            {
                _featureHistory[key].Clear();
            }
            _regressionAlerts.Clear();
        }
        
        /// <summary>
        /// Get focus phase summary.
        /// </summary>
        public string GetFocusSummary()
        {
            string focused = _focusedFeatures.Count > 0 
                ? string.Join(",", _focusedFeatures) 
                : "none";
            return $"Phase:{_currentFocusPhase} Iter:{_currentIteration} Focus:[{focused}] P:{GetFocusProbability():F2}";
        }
        
        #endregion
        
        #region 4. Feature History Tracking
        
        private void RecordFeatureHistory(int iteration, Dictionary<string, float> scores)
        {
            foreach (var kv in scores)
            {
                string feature = kv.Key;
                float score = kv.Value;
                
                if (!_featureHistory.ContainsKey(feature))
                {
                    _featureHistory[feature] = new Queue<FeatureHistoryEntry>();
                }
                
                var history = _featureHistory[feature];
                
                history.Enqueue(new FeatureHistoryEntry
                {
                    Iteration = iteration,
                    Score = score,
                    Timestamp = DateTime.Now
                });
                
                // Limit history size
                while (history.Count > HISTORY_WINDOW)
                {
                    history.Dequeue();
                }
                
                // Check for regression
                CheckForRegression(feature);
            }
        }
        
        private void CheckForRegression(string feature)
        {
            if (!_featureHistory.ContainsKey(feature)) return;
            
            var history = _featureHistory[feature].ToList();
            if (history.Count < REGRESSION_WINDOW) return;
            
            // Find peak score in history
            var peak = history.OrderByDescending(h => h.Score).First();
            var recent = history.Skip(Math.Max(0, history.Count - 3)).ToList();
            float recentAvg = recent.Average(h => h.Score);
            
            // Check if recent scores are significantly below peak
            float drop = peak.Score - recentAvg;
            
            if (drop > REGRESSION_THRESHOLD)
            {
                _regressionAlerts[feature] = new RegressionInfo
                {
                    Feature = feature,
                    PeakScore = peak.Score,
                    CurrentScore = recentAvg,
                    PeakIteration = peak.Iteration,
                    CurrentIteration = _currentIteration
                };
            }
            else if (_regressionAlerts.ContainsKey(feature))
            {
                // Regression resolved
                _regressionAlerts.Remove(feature);
            }
        }
        
        /// <summary>
        /// Get active regression alerts.
        /// </summary>
        public List<RegressionInfo> GetRegressionAlerts()
        {
            return _regressionAlerts.Values.Where(r => r.IsActive).ToList();
        }
        
        /// <summary>
        /// Check if a specific feature is regressing.
        /// </summary>
        public bool IsFeatureRegressing(string feature)
        {
            return _regressionAlerts.TryGetValue(feature, out var info) && info.IsActive;
        }
        
        /// <summary>
        /// Get the trend for a feature over recent iterations.
        /// Returns positive for improving, negative for declining.
        /// </summary>
        public float GetFeatureTrend(string feature)
        {
            if (!_featureHistory.ContainsKey(feature)) return 0f;
            
            var history = _featureHistory[feature].ToList();
            if (history.Count < 5) return 0f;
            
            // Simple linear regression over last 10 points
            var recent = history.Skip(Math.Max(0, history.Count - 10)).ToList();
            if (recent.Count < 5) return 0f;
            
            float sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = recent.Count;
            
            for (int i = 0; i < n; i++)
            {
                float x = i;
                float y = recent[i].Score;
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }
            
            // Slope of regression line
            float slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            return slope;
        }
        
        /// <summary>
        /// Get history statistics for a feature.
        /// </summary>
        public (float min, float max, float avg, float current) GetFeatureStats(string feature)
        {
            if (!_featureHistory.ContainsKey(feature) || _featureHistory[feature].Count == 0)
            {
                return (0f, 0f, 0f, 0f);
            }
            
            var history = _featureHistory[feature].ToList();
            float min = history.Min(h => h.Score);
            float max = history.Max(h => h.Score);
            float avg = history.Average(h => h.Score);
            float current = history.Last().Score;
            
            return (min, max, avg, current);
        }
        
        /// <summary>
        /// Get history tracking summary.
        /// </summary>
        public string GetHistorySummary()
        {
            var regressions = GetRegressionAlerts();
            if (regressions.Count == 0)
            {
                return "No regressions detected";
            }
            
            return $"REGRESSIONS: {string.Join(", ", regressions.Select(r => $"{r.Feature}↓{r.DropAmount:F2}"))}";
        }
        
        #endregion
        
        #region Combined Decision Making
        
        /// <summary>
        /// Get a comprehensive recommendation for the current iteration.
        /// This combines all four systems to provide actionable guidance.
        /// </summary>
        public MutationRecommendation GetRecommendation(
            Dictionary<string, float> currentScores,
            Random random)
        {
            var rec = new MutationRecommendation();
            
            // 1. Update state
            if (currentScores != null)
            {
                UpdateFeatureDifficulty(currentScores);
                UpdateIteration(_currentIteration + 1, currentScores);
            }
            
            // 2. Check for regressions - highest priority
            var regressions = GetRegressionAlerts();
            if (regressions.Count > 0)
            {
                var worst = regressions.OrderByDescending(r => r.DropAmount).First();
                rec.Priority = MutationPriority.FixRegression;
                rec.TargetFeature = worst.Feature;
                rec.Reason = $"Regression detected: {worst.Feature} dropped {worst.DropAmount:F2}";
                rec.RecommendedMorphs = GetSafeMorphsForFeature(worst.Feature);
                return rec;
            }
            
            // 3. Progressive focus logic
            if (ShouldFocusOnWeakFeatures(random) && _focusedFeatures.Count > 0)
            {
                // Pick a random focused feature
                string target = _focusedFeatures[random.Next(_focusedFeatures.Count)];
                rec.Priority = MutationPriority.FocusWeak;
                rec.TargetFeature = target;
                rec.Reason = $"Progressive focus on weak feature: {target}";
                rec.RecommendedMorphs = GetSafeMorphsForFeature(target);
                return rec;
            }
            
            // 4. Default: broad optimization with adaptive weights
            rec.Priority = MutationPriority.BroadOptimization;
            rec.TargetFeature = null;
            rec.Reason = $"Broad optimization (phase: {_currentFocusPhase})";
            rec.AdaptiveWeights = GetAdaptiveWeights();
            return rec;
        }
        
        /// <summary>
        /// Get full status summary.
        /// </summary>
        public string GetFullSummary()
        {
            return $"[FeatureIntelligence]\n" +
                   $"  Difficulty: {GetDifficultySummary()}\n" +
                   $"  {GetCorrelationSummary()}\n" +
                   $"  {GetFocusSummary()}\n" +
                   $"  {GetHistorySummary()}";
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    public enum FocusPhase
    {
        Broad,      // Optimize all features equally
        Narrowing,  // Start focusing on weak features
        Focused     // Heavily focus on 1-2 weakest features
    }
    
    public enum MutationPriority
    {
        FixRegression,      // Fix a regressing feature
        FocusWeak,          // Focus on weak features
        BroadOptimization   // Optimize broadly
    }
    
    public class MutationRecommendation
    {
        public MutationPriority Priority { get; set; }
        public string TargetFeature { get; set; }
        public string Reason { get; set; }
        public List<int> RecommendedMorphs { get; set; } = new List<int>();
        public Dictionary<string, float> AdaptiveWeights { get; set; }
    }
    
    #endregion
}
