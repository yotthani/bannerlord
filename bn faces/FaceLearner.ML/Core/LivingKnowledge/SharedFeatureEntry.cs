using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Shared knowledge entry for a specific feature:value combination.
    /// Stores BASE morphs that are independent of other traits.
    /// Example: "LipFullness:Thin" → base morphs for thin lips
    /// </summary>
    [Serializable]
    public class SharedFeatureEntry
    {
        #region Properties
        
        /// <summary>Feature key, e.g., "LipFullness:Thin"</summary>
        public string FeatureKey { get; set; }
        
        /// <summary>Base morph values for this feature</summary>
        public Dictionary<int, float> BaseMorphs { get; set; } = new Dictionary<int, float>();
        
        /// <summary>Number of times this entry was updated</summary>
        public int LearnCount { get; set; }
        
        /// <summary>Confidence in this entry (0-1)</summary>
        public float Confidence { get; set; }
        
        /// <summary>Contexts where this was learned (for deduplication analysis)</summary>
        public HashSet<string> LearnedContexts { get; set; } = new HashSet<string>();
        
        #endregion
        
        #region Learning
        
        /// <summary>Update base morphs with new observation</summary>
        public void UpdateFromObservation(Dictionary<int, float> observedDeltas, string context, float learningRate = 0.15f)
        {
            LearnCount++;
            LearnedContexts.Add(context);
            
            // Limit context tracking
            while (LearnedContexts.Count > 50)
                LearnedContexts.Remove(LearnedContexts.First());
            
            // Exponential moving average
            foreach (var kv in observedDeltas)
            {
                if (!BaseMorphs.ContainsKey(kv.Key))
                    BaseMorphs[kv.Key] = 0f;
                
                BaseMorphs[kv.Key] = BaseMorphs[kv.Key] * (1 - learningRate) + kv.Value * learningRate;
            }
            
            // Confidence increases with diversity
            float contextDiversity = Math.Min(1f, LearnedContexts.Count / 10f);
            Confidence = Math.Min(1f, (LearnCount / 20f) * (0.5f + contextDiversity * 0.5f));
        }
        
        #endregion
        
        #region Delta Calculation
        
        /// <summary>Get contextual delta (what differs from base)</summary>
        public Dictionary<int, float> GetContextualDelta(Dictionary<int, float> observedDeltas)
        {
            var contextDelta = new Dictionary<int, float>();
            
            foreach (var kv in observedDeltas)
            {
                float baseDelta = BaseMorphs.ContainsKey(kv.Key) ? BaseMorphs[kv.Key] : 0f;
                float diff = kv.Value - baseDelta;
                
                if (Math.Abs(diff) > 0.02f)
                    contextDelta[kv.Key] = diff;
            }
            
            return contextDelta;
        }
        
        #endregion
    }
}
