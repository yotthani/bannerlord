using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Legacy learning phase enum - kept for backward compatibility with saved data
    /// New system uses DynamicPhase instead
    /// </summary>
    public enum LearningPhase
    {
        Exploration,
        Refinement,
        Convergence,
        Plateau
    }
    
    /// <summary>
    /// Statistics for a specific strategy/configuration
    /// </summary>
    [Serializable]
    public class StrategyStats
    {
        public int TimesUsed;
        public float TotalScoreGain;
        public float TotalScoreLoss;
        public int SuccessCount;  // Score improved
        public int FailCount;     // Score got worse
        
        public float SuccessRate => TimesUsed > 0 ? (float)SuccessCount / TimesUsed : 0.5f;
        public float AverageGain => TimesUsed > 0 ? TotalScoreGain / TimesUsed : 0f;
        public float NetGain => TotalScoreGain - TotalScoreLoss;
        public float Effectiveness => TimesUsed > 10 ? (SuccessRate * 0.5f + (AverageGain * 10f) * 0.5f) : 0.5f;
    }
    
    /// <summary>
    /// Phase transition record for learning optimal transitions
    /// </summary>
    [Serializable]
    public class PhaseTransitionRecord
    {
        public LearningPhase FromPhase;
        public LearningPhase ToPhase;

        public float ScoreAtTransition;
        public float ScoreAfter50Iterations;
        public bool WasSuccessful; // Did score improve after transition?
    }
    
    /// <summary>
    /// Meta-learning memory for the orchestrator
    /// Learns which strategies work best and adapts over time
    /// </summary>
    public class OrchestratorMemory
    {
        private string _savePath;
        private bool _isDirty;
        
        // Strategy effectiveness tracking
        private Dictionary<string, StrategyStats> _strategyStats = new Dictionary<string, StrategyStats>();
        
        // Phase transition learning
        private List<PhaseTransitionRecord> _transitionHistory = new List<PhaseTransitionRecord>();
        
        // Learned optimal parameters (start with defaults, adapt over time)
        public float LearnedExplorationStep { get; private set; } = 0.3f;
        public float LearnedRefinementStep { get; private set; } = 0.1f;
        public float LearnedConvergenceStep { get; private set; } = 0.03f;
        public float LearnedPlateauStep { get; private set; } = 0.4f;
        
        public float LearnedExplorationToRefinementThreshold { get; private set; } = 0.3f;
        public float LearnedRefinementToConvergenceThreshold { get; private set; } = 0.6f;
        
        public int LearnedMaxIterations { get; private set; } = 500;
        public float LearnedTreeUsageBase { get; private set; } = 0.2f;
        
        // Score range effectiveness (which step sizes work best at different score levels)
        private Dictionary<int, StrategyStats> _stepSizeByScoreRange = new Dictionary<int, StrategyStats>();
        
        // Track what worked for different morph counts
        private Dictionary<int, StrategyStats> _morphCountStats = new Dictionary<int, StrategyStats>();
        
        public int TotalExperiences => _strategyStats.Values.Sum(s => s.TimesUsed);
        
        public OrchestratorMemory(string savePath)
        {
            _savePath = savePath;
            InitializeDefaults();
        }
        

        private void InitializeDefaults()
        {
            // Initialize score range buckets (0-10%, 10-20%, etc.)
            for (int i = 0; i < 10; i++)
            {
                _stepSizeByScoreRange[i] = new StrategyStats();
            }
            
            // Initialize morph count buckets (1-12 morphs changed)
            for (int i = 1; i <= 12; i++)
            {
                _morphCountStats[i] = new StrategyStats();
            }
        }
        
        /// <summary>
        /// Record the result of a mutation strategy
        /// </summary>
        public void RecordMutation(LearningPhase phase, float stepSize, int numMorphsChanged, 
            float scoreBefore, float scoreAfter, bool usedTreeSuggestion)
        {
            float scoreDelta = scoreAfter - scoreBefore;
            bool wasSuccess = scoreDelta > 0;
            
            // Track by strategy key - bucket step sizes for better aggregation
            string stepBucket = GetStepSizeBucket(stepSize);
            string strategyKey = $"{phase}_{stepBucket}_{(usedTreeSuggestion ? "tree" : "random")}";
            if (!_strategyStats.ContainsKey(strategyKey))
                _strategyStats[strategyKey] = new StrategyStats();
            
            var stats = _strategyStats[strategyKey];
            stats.TimesUsed++;
            if (wasSuccess)
            {
                stats.SuccessCount++;
                stats.TotalScoreGain += scoreDelta;
            }
            else
            {
                stats.FailCount++;
                stats.TotalScoreLoss += Math.Abs(scoreDelta);
            }
            
            // Track by score range (bucket by 0.1)
            int scoreRange = Math.Min(9, (int)(scoreBefore * 10));
            if (!_stepSizeByScoreRange.ContainsKey(scoreRange))
                _stepSizeByScoreRange[scoreRange] = new StrategyStats();
            var rangeStats = _stepSizeByScoreRange[scoreRange];
            rangeStats.TimesUsed++;
            if (wasSuccess)
            {
                rangeStats.SuccessCount++;
                rangeStats.TotalScoreGain += scoreDelta;
            }
            else
            {
                rangeStats.FailCount++;
                rangeStats.TotalScoreLoss += Math.Abs(scoreDelta);
            }
            
            // Track by morph count
            int morphBucket = Math.Min(12, Math.Max(1, numMorphsChanged));
            if (!_morphCountStats.ContainsKey(morphBucket))
                _morphCountStats[morphBucket] = new StrategyStats();
            
            var morphStats = _morphCountStats[morphBucket];
            morphStats.TimesUsed++;
            if (wasSuccess)
            {
                morphStats.SuccessCount++;
                morphStats.TotalScoreGain += scoreDelta;
            }
            else
            {
                morphStats.FailCount++;
            }
            
            _isDirty = true;
            
            // Periodically adapt parameters
            if (TotalExperiences % 500 == 0 && TotalExperiences > 0)
            {
                AdaptParameters();
            }
        }
        
        /// <summary>
        /// Bucket step sizes for better aggregation (fewer, more meaningful categories)
        /// </summary>
        private string GetStepSizeBucket(float stepSize)
        {
            if (stepSize < 0.05f) return "micro";      // 0.00-0.05
            if (stepSize < 0.10f) return "tiny";       // 0.05-0.10
            if (stepSize < 0.15f) return "small";      // 0.10-0.15
            if (stepSize < 0.20f) return "medium-s";   // 0.15-0.20
            if (stepSize < 0.25f) return "medium";     // 0.20-0.25
            if (stepSize < 0.30f) return "medium-l";   // 0.25-0.30
            if (stepSize < 0.40f) return "large";      // 0.30-0.40
            return "huge";                              // 0.40+
        }
        
        /// <summary>
        /// Record a phase transition and its outcome
        /// </summary>
        public void RecordPhaseTransition(LearningPhase from, LearningPhase to, 
            float scoreAtTransition, float scoreAfter50)
        {
            var record = new PhaseTransitionRecord
            {
                FromPhase = from,
                ToPhase = to,
                ScoreAtTransition = scoreAtTransition,
                ScoreAfter50Iterations = scoreAfter50,
                WasSuccessful = scoreAfter50 > scoreAtTransition
            };
            
            _transitionHistory.Add(record);
            
            // Keep only recent history
            while (_transitionHistory.Count > 1000)
                _transitionHistory.RemoveAt(0);
            
            _isDirty = true;
        }
        
        /// <summary>
        /// Adapt parameters based on accumulated experience
        /// </summary>
        private void AdaptParameters()
        {
            SubModule.Log($"OrchestratorMemory: Adapting parameters ({TotalExperiences} experiences)");
            
            // Analyze which step sizes work best at different score ranges
            AdaptStepSizes();
            
            // Analyze phase transitions
            AdaptPhaseThresholds();
            
            // Analyze morph count effectiveness
            AdaptMorphCounts();
            
            // Analyze tree usage effectiveness
            AdaptTreeUsage();
        }
        
        private void AdaptStepSizes()
        {
            // For each phase, find which step sizes had best results
            var explorationStats = _strategyStats
                .Where(kv => kv.Key.StartsWith("Exploration_"))
                .OrderByDescending(kv => kv.Value.Effectiveness)
                .FirstOrDefault();
            
            if (explorationStats.Value != null && explorationStats.Value.TimesUsed > 50)
            {
                // Extract step size from key
                var parts = explorationStats.Key.Split('_');
                if (parts.Length >= 2 && float.TryParse(parts[1], out float bestStep))
                {
                    // Blend toward best step (slow adaptation)
                    LearnedExplorationStep = LearnedExplorationStep * 0.8f + bestStep * 0.2f;
                }
            }
            
            // Similar for other phases...
            var refinementStats = _strategyStats
                .Where(kv => kv.Key.StartsWith("Refinement_"))
                .OrderByDescending(kv => kv.Value.Effectiveness)
                .FirstOrDefault();
            
            if (refinementStats.Value != null && refinementStats.Value.TimesUsed > 50)
            {
                var parts = refinementStats.Key.Split('_');
                if (parts.Length >= 2 && float.TryParse(parts[1], out float bestStep))
                {
                    LearnedRefinementStep = LearnedRefinementStep * 0.8f + bestStep * 0.2f;
                }
            }
        }
        
        private void AdaptPhaseThresholds()
        {
            // Analyze successful transitions
            var successfulExpToRef = _transitionHistory
                .Where(t => t.FromPhase == LearningPhase.Exploration && 
                           t.ToPhase == LearningPhase.Refinement &&
                           t.WasSuccessful)
                .ToList();
            
            if (successfulExpToRef.Count > 20)
            {
                float avgSuccessfulScore = successfulExpToRef.Average(t => t.ScoreAtTransition);
                // Blend toward successful transition scores
                LearnedExplorationToRefinementThreshold = 
                    LearnedExplorationToRefinementThreshold * 0.7f + avgSuccessfulScore * 0.3f;
            }
            
            var successfulRefToConv = _transitionHistory
                .Where(t => t.FromPhase == LearningPhase.Refinement && 
                           t.ToPhase == LearningPhase.Convergence &&
                           t.WasSuccessful)
                .ToList();
            
            if (successfulRefToConv.Count > 20)
            {
                float avgSuccessfulScore = successfulRefToConv.Average(t => t.ScoreAtTransition);
                LearnedRefinementToConvergenceThreshold = 
                    LearnedRefinementToConvergenceThreshold * 0.7f + avgSuccessfulScore * 0.3f;
            }
        }
        
        private void AdaptMorphCounts()
        {
            // Find optimal morph count for each phase
            // This data is used by GetOptimalMorphCount()
        }
        
        private void AdaptTreeUsage()
        {
            // Compare tree vs random strategies
            var treeStats = _strategyStats
                .Where(kv => kv.Key.EndsWith("_tree"))
                .ToList();
            
            var randomStats = _strategyStats
                .Where(kv => kv.Key.EndsWith("_random"))
                .ToList();
            
            if (treeStats.Count > 0 && randomStats.Count > 0)
            {
                float treeEffectiveness = treeStats.Average(kv => kv.Value.Effectiveness);
                float randomEffectiveness = randomStats.Average(kv => kv.Value.Effectiveness);
                
                // Adjust base tree usage based on relative effectiveness
                float ratio = treeEffectiveness / (treeEffectiveness + randomEffectiveness + 0.001f);
                LearnedTreeUsageBase = LearnedTreeUsageBase * 0.9f + ratio * 0.1f;
                LearnedTreeUsageBase = Math.Max(0.1f, Math.Min(0.5f, LearnedTreeUsageBase));
            }
        }
        
        /// <summary>
        /// Get recommended step size for current situation
        /// </summary>
        public float GetRecommendedStepSize(LearningPhase phase, float currentScore)
        {
            float baseStep = phase switch
            {
                LearningPhase.Exploration => LearnedExplorationStep,
                LearningPhase.Refinement => LearnedRefinementStep,
                LearningPhase.Convergence => LearnedConvergenceStep,
                LearningPhase.Plateau => LearnedPlateauStep,
                _ => 0.1f
            };
            
            // Adjust based on score range effectiveness
            int scoreRange = Math.Min(9, (int)(currentScore * 10));
            if (_stepSizeByScoreRange.ContainsKey(scoreRange))
            {
                var rangeStats = _stepSizeByScoreRange[scoreRange];
                if (rangeStats.TimesUsed > 100)
                {
                    // If this score range has low success, try different step
                    if (rangeStats.SuccessRate < 0.3f)
                        baseStep *= 1.5f; // Be more aggressive
                    else if (rangeStats.SuccessRate > 0.6f)
                        baseStep *= 0.8f; // Current is working, be more precise
                }
            }
            
            return baseStep;
        }
        
        /// <summary>
        /// Get recommended number of morphs to change
        /// </summary>
        public int GetRecommendedMorphCount(LearningPhase phase, float currentScore)
        {
            // Find best performing morph count for this phase
            int bestCount = phase switch
            {
                LearningPhase.Exploration => 5,
                LearningPhase.Refinement => 3,
                LearningPhase.Convergence => 2,
                LearningPhase.Plateau => 8,
                _ => 3
            };
            
            // Check if we have learned better values
            var relevantStats = _morphCountStats
                .Where(kv => kv.Value.TimesUsed > 50)
                .OrderByDescending(kv => kv.Value.Effectiveness)
                .Take(3)
                .ToList();
            
            if (relevantStats.Count > 0)
            {
                // Bias toward effective counts
                bestCount = relevantStats.First().Key;
            }
            
            return bestCount;
        }
        
        /// <summary>
        /// Should we use tree suggestions or random exploration?
        /// </summary>
        public bool ShouldUseTree(LearningPhase phase, float currentScore, int treeExperiments, Random random)
        {
            float knowledgeFactor = Math.Min(1f, treeExperiments / 1000f);
            
            float phaseFactor = phase switch
            {
                LearningPhase.Exploration => 0.3f,
                LearningPhase.Refinement => 0.7f,
                LearningPhase.Convergence => 0.8f,
                LearningPhase.Plateau => 0.2f,
                _ => 0.5f
            };
            
            float probability = LearnedTreeUsageBase + knowledgeFactor * phaseFactor;
            return random.NextDouble() < probability;
        }
        
        /// <summary>
        /// Get stats summary for logging
        /// </summary>
        public string GetStatsSummary()
        {
            return $"Exp:{TotalExperiences} | Steps:[E:{LearnedExplorationStep:F2} R:{LearnedRefinementStep:F2} C:{LearnedConvergenceStep:F2}] | Tree:{LearnedTreeUsageBase:F2}";
        }
        
        public bool Load()
        {
            if (!File.Exists(_savePath)) return false;
            
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(_savePath)))
                {
                    // Version
                    int version = reader.ReadInt32();
                    if (version < 1) return false;
                    
                    // Learned parameters
                    LearnedExplorationStep = reader.ReadSingle();
                    LearnedRefinementStep = reader.ReadSingle();
                    LearnedConvergenceStep = reader.ReadSingle();
                    LearnedPlateauStep = reader.ReadSingle();
                    LearnedExplorationToRefinementThreshold = reader.ReadSingle();
                    LearnedRefinementToConvergenceThreshold = reader.ReadSingle();
                    LearnedMaxIterations = reader.ReadInt32();
                    LearnedTreeUsageBase = reader.ReadSingle();
                    
                    // Strategy stats
                    int stratCount = reader.ReadInt32();
                    _strategyStats.Clear();
                    for (int i = 0; i < stratCount; i++)
                    {
                        string key = reader.ReadString();
                        var stats = new StrategyStats
                        {
                            TimesUsed = reader.ReadInt32(),
                            TotalScoreGain = reader.ReadSingle(),
                            TotalScoreLoss = reader.ReadSingle(),
                            SuccessCount = reader.ReadInt32(),
                            FailCount = reader.ReadInt32()
                        };
                        _strategyStats[key] = stats;
                    }
                    
                    // Score range stats
                    int rangeCount = reader.ReadInt32();
                    for (int i = 0; i < rangeCount; i++)
                    {
                        int range = reader.ReadInt32();
                        var stats = new StrategyStats
                        {
                            TimesUsed = reader.ReadInt32(),
                            TotalScoreGain = reader.ReadSingle(),
                            TotalScoreLoss = reader.ReadSingle(),
                            SuccessCount = reader.ReadInt32(),
                            FailCount = reader.ReadInt32()
                        };
                        _stepSizeByScoreRange[range] = stats;
                    }
                }
                
                SubModule.Log($"OrchestratorMemory loaded: {GetStatsSummary()}");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"OrchestratorMemory load error: {ex.Message}");
                return false;
            }
        }
        
        public void Save()
        {
            // Always save for debugging (was: if (!_isDirty) return;)
            SubModule.Log($"OrchestratorMemory: Saving to {_savePath} (dirty={_isDirty})");
            
            try
            {
                string dir = Path.GetDirectoryName(_savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                using (var writer = new BinaryWriter(File.Create(_savePath)))
                {
                    // Version
                    writer.Write(1);
                    
                    // Learned parameters
                    writer.Write(LearnedExplorationStep);
                    writer.Write(LearnedRefinementStep);
                    writer.Write(LearnedConvergenceStep);
                    writer.Write(LearnedPlateauStep);
                    writer.Write(LearnedExplorationToRefinementThreshold);
                    writer.Write(LearnedRefinementToConvergenceThreshold);
                    writer.Write(LearnedMaxIterations);
                    writer.Write(LearnedTreeUsageBase);
                    
                    // Strategy stats
                    writer.Write(_strategyStats.Count);
                    foreach (var kv in _strategyStats)
                    {
                        writer.Write(kv.Key);
                        writer.Write(kv.Value.TimesUsed);
                        writer.Write(kv.Value.TotalScoreGain);
                        writer.Write(kv.Value.TotalScoreLoss);
                        writer.Write(kv.Value.SuccessCount);
                        writer.Write(kv.Value.FailCount);
                    }
                    
                    // Score range stats
                    writer.Write(_stepSizeByScoreRange.Count);
                    foreach (var kv in _stepSizeByScoreRange)
                    {
                        writer.Write(kv.Key);
                        writer.Write(kv.Value.TimesUsed);
                        writer.Write(kv.Value.TotalScoreGain);
                        writer.Write(kv.Value.TotalScoreLoss);
                        writer.Write(kv.Value.SuccessCount);
                        writer.Write(kv.Value.FailCount);
                    }
                }
                
                _isDirty = false;
                SubModule.Log($"OrchestratorMemory: Saved ({_strategyStats.Count} strategies)");
            }
            catch (Exception ex)
            {
                SubModule.Log($"OrchestratorMemory save error: {ex.Message}");
            }
        }
    }
}
