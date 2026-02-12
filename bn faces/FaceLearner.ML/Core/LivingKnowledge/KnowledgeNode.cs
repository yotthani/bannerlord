using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// A node in the hierarchical knowledge tree.
    /// Each node represents a feature combination and its learned morph adjustments.
    /// </summary>
    [Serializable]
    public class KnowledgeNode
    {
        #region Constants
        
        private const int MAX_OUTCOMES = 30;
        
        #endregion
        
        #region Core Properties
        
        /// <summary>Path in tree, e.g. "Wide/Female/Young/ThinLips"</summary>
        public string Path { get; set; }
        
        /// <summary>Feature category this node represents</summary>
        public FeatureCategory Feature { get; set; }
        
        /// <summary>Feature value, e.g. "Wide", "Female", "Young"</summary>
        public string Value { get; set; }
        
        /// <summary>Children nodes (more specific features)</summary>
        public List<KnowledgeNode> Children { get; set; } = new List<KnowledgeNode>();
        
        #endregion
        
        #region Morph Data
        
        /// <summary>Learned morph DELTAS (not absolute values). Key = morph index</summary>
        public Dictionary<int, float> MorphDeltas { get; set; } = new Dictionary<int, float>();
        
        /// <summary>Morph variance tracking for each morph</summary>
        public Dictionary<int, float> MorphVariance { get; set; } = new Dictionary<int, float>();
        
        #endregion
        
        #region Learning Statistics
        
        public int UseCount { get; set; }
        public int SuccessCount { get; set; }
        public float ConfidenceScore { get; set; }
        
        #endregion
        
        #region Self-Organization Metrics
        
        /// <summary>Outcome variance - high variance = needs splitting</summary>
        public float OutcomeVariance { get; set; }
        
        public List<float> RecentOutcomes { get; set; } = new List<float>();
        
        /// <summary>Health metric 0-1, decays if not used/successful</summary>
        public float Health { get; set; } = 1.0f;
        
        public DateTime LastUsed { get; set; } = DateTime.Now;
        public DateTime LastRefinement { get; set; } = DateTime.MinValue;
        
        public int SplitCount { get; set; }
        public int MergeCount { get; set; }
        public bool NeedsSplit { get; set; }
        public bool NeedsMerge { get; set; }
        
        /// <summary>Sub-pattern scores for dynamic splitting. Key = "FeatureCategory:Value"</summary>
        public Dictionary<string, float> SubPatternScores { get; set; } = new Dictionary<string, float>();
        public Dictionary<string, int> SubPatternCounts { get; set; } = new Dictionary<string, int>();
        
        #endregion
        
        #region Computed Properties
        
        public float SuccessRate => UseCount > 0 ? (float)SuccessCount / UseCount : 0.5f;
        public bool IsStable => OutcomeVariance < 0.15f && UseCount >= 10;
        public bool IsHighVariance => OutcomeVariance > 0.25f && UseCount >= 5;
        public bool IsStale => (DateTime.Now - LastUsed).TotalHours > 24 && UseCount < 5;
        
        #endregion
        
        #region Outcome Recording
        
        /// <summary>Record outcome with optional feature context for sub-pattern detection</summary>
        public void RecordOutcome(float outcome, Dictionary<FeatureCategory, string> features = null)
        {
            RecentOutcomes.Add(outcome);
            while (RecentOutcomes.Count > MAX_OUTCOMES)
                RecentOutcomes.RemoveAt(0);
            
            UpdateVariance();
            LastUsed = DateTime.Now;
            
            TrackSubPatterns(features, outcome);
            
            NeedsSplit = IsHighVariance && Children.Count == 0 && UseCount >= 8;
        }
        
        /// <summary>Simple outcome recording for variance tracking</summary>
        public void RecordOutcome(float outcome)
        {
            RecentOutcomes.Add(outcome);
            if (RecentOutcomes.Count > MAX_OUTCOMES)
                RecentOutcomes.RemoveAt(0);
            
            UpdateVariance();
        }
        
        private void UpdateVariance()
        {
            if (RecentOutcomes.Count >= 3)
            {
                float mean = RecentOutcomes.Average();
                OutcomeVariance = RecentOutcomes.Sum(x => (x - mean) * (x - mean)) / RecentOutcomes.Count;
                OutcomeVariance = (float)Math.Sqrt(OutcomeVariance);
            }
        }
        
        private void TrackSubPatterns(Dictionary<FeatureCategory, string> features, float outcome)
        {
            if (features == null) return;
            
            foreach (var kv in features)
            {
                string key = $"{kv.Key}:{kv.Value}";
                if (!SubPatternScores.ContainsKey(key))
                {
                    SubPatternScores[key] = 0;
                    SubPatternCounts[key] = 0;
                }
                
                SubPatternCounts[key]++;
                float alpha = 1f / SubPatternCounts[key];
                SubPatternScores[key] = SubPatternScores[key] * (1 - alpha) + outcome * alpha;
            }
        }
        
        #endregion
        
        #region Health Management
        
        /// <summary>Update health based on usage and success</summary>
        public void UpdateHealth()
        {
            float hoursSinceUse = (float)(DateTime.Now - LastUsed).TotalHours;
            float usageDecay = Math.Max(0.9f, 1f - hoursSinceUse * 0.001f);
            float successBoost = 0.5f + SuccessRate * 0.5f;
            float confFactor = Math.Min(1f, UseCount / 20f);
            
            Health = Math.Max(0.1f, Math.Min(1f, usageDecay * successBoost * (0.5f + confFactor * 0.5f)));
        }
        
        #endregion
        
        #region Split/Merge Analysis
        
        /// <summary>Get best sub-pattern for splitting</summary>
        public (string feature, string value, float scoreDiff)? GetBestSplitCandidate()
        {
            if (SubPatternCounts.Count < 2) return null;
            
            var patterns = SubPatternScores
                .Where(kv => SubPatternCounts[kv.Key] >= 3)
                .OrderByDescending(kv => Math.Abs(kv.Value - (RecentOutcomes.Count > 0 ? RecentOutcomes.Average() : 0.5f)))
                .ToList();
            
            if (patterns.Count < 2) return null;
            
            var best = patterns.First();
            var worst = patterns.Last();
            float diff = Math.Abs(best.Value - worst.Value);
            
            if (diff > 0.1f)
            {
                var parts = best.Key.Split(':');
                if (parts.Length == 2)
                    return (parts[0], parts[1], diff);
            }
            
            return null;
        }
        
        /// <summary>Get feature with highest variance in outcomes</summary>
        public string GetBestSplitFeature()
        {
            if (SubPatternScores.Count == 0) return null;
            
            return SubPatternScores
                .Where(kv => Math.Abs(kv.Value) > 0.1f)
                .OrderByDescending(kv => Math.Abs(kv.Value))
                .FirstOrDefault().Key;
        }
        
        /// <summary>Check if node needs attention</summary>
        public bool NeedsRefinement()
        {
            if (UseCount >= 20 && OutcomeVariance > 0.02f && Children.Count < 5)
                return true;
            
            if (UseCount >= 30 && SuccessRate < 0.3f)
                return true;
            
            if (Children.Count >= 2 && HasSimilarChildren())
                return true;
            
            return false;
        }
        
        private bool HasSimilarChildren()
        {
            for (int i = 0; i < Children.Count - 1; i++)
            {
                for (int j = i + 1; j < Children.Count; j++)
                {
                    if (AreSimilar(Children[i], Children[j]))
                        return true;
                }
            }
            return false;
        }
        
        private bool AreSimilar(KnowledgeNode a, KnowledgeNode b)
        {
            if (a.MorphDeltas.Count == 0 || b.MorphDeltas.Count == 0)
                return false;
            
            var commonKeys = a.MorphDeltas.Keys.Intersect(b.MorphDeltas.Keys).ToList();
            if (commonKeys.Count < 3) return false;
            
            float totalDiff = commonKeys.Sum(key => Math.Abs(a.MorphDeltas[key] - b.MorphDeltas[key]));
            float avgDiff = totalDiff / commonKeys.Count;
            
            return avgDiff < 0.05f;
        }
        
        #endregion
        
        #region Morph Learning
        
        /// <summary>Update morphs from observed deltas with learning rate</summary>
        public void UpdateFromObservation(Dictionary<int, float> observedDeltas, string context, float learningRate = 0.15f)
        {
            foreach (var kv in observedDeltas)
            {
                int idx = kv.Key;
                float observedDelta = kv.Value;
                
                if (!MorphDeltas.ContainsKey(idx))
                {
                    MorphDeltas[idx] = observedDelta * learningRate;
                    MorphVariance[idx] = 0.1f;
                }
                else
                {
                    float existing = MorphDeltas[idx];
                    float diff = observedDelta - existing;
                    
                    MorphDeltas[idx] += diff * learningRate;
                    
                    if (!MorphVariance.ContainsKey(idx))
                        MorphVariance[idx] = 0.1f;
                    MorphVariance[idx] = MorphVariance[idx] * 0.9f + Math.Abs(diff) * 0.1f;
                }
            }
        }
        
        /// <summary>Get contextual delta adjustment</summary>
        public Dictionary<int, float> GetContextualDelta(Dictionary<int, float> observedDeltas)
        {
            var result = new Dictionary<int, float>();
            
            foreach (var kv in observedDeltas)
            {
                int idx = kv.Key;
                float observed = kv.Value;
                
                if (MorphDeltas.ContainsKey(idx))
                {
                    float expected = MorphDeltas[idx];
                    float variance = MorphVariance.ContainsKey(idx) ? MorphVariance[idx] : 0.1f;
                    
                    float diff = observed - expected;
                    if (Math.Abs(diff) > variance * 2)
                    {
                        result[idx] = diff;
                    }
                }
                else
                {
                    result[idx] = observed;
                }
            }
            
            return result;
        }
        
        #endregion
    }
}
