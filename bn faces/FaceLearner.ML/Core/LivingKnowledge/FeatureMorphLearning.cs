using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Feature-Morph Learning System
    /// 
    /// PURPOSE: Learn which specific morphs affect which facial features,
    /// and what values work best for specific feature characteristics.
    /// 
    /// KEY INSIGHT: We should NOT learn "best overall morphs" but instead
    /// learn "best morphs for each feature" separately. The final face
    /// is a MERGE of all best-per-feature morphs.
    /// 
    /// STRUCTURE:
    /// - Each feature (Eyes, Nose, Mouth, etc.) has its own morph subset
    /// - We track: which morphs correlate with this feature's score
    /// - We learn: what values work for specific characteristics (wide nose, thin lips, etc.)
    /// </summary>
    public class FeatureMorphLearning
    {
        // Feature -> Morph correlations (which morphs affect this feature)
        private Dictionary<string, MorphCorrelations> _featureCorrelations = new();
        
        // Feature -> Characteristic -> Best morphs (e.g., "Nose" -> "Wide" -> morphs)
        private Dictionary<string, Dictionary<string, LearnedMorphSet>> _featureCharacteristics = new();
        
        // Morph index ranges per feature (from MorphDefinitions)
        private static readonly Dictionary<string, (int start, int end)> FeatureMorphRanges = new()
        {
            { "Face", (0, 9) },      // FaceShape morphs
            { "Eyes", (10, 19) },    // Eye morphs
            { "Brows", (20, 24) },   // Eyebrow morphs
            { "Nose", (25, 34) },    // Nose morphs
            { "Mouth", (35, 44) },   // Mouth/Lip morphs
            { "Jaw", (45, 54) },     // Jaw morphs
            { "Ears", (55, 58) },    // Ear morphs
            { "Chin", (59, 61) }     // Chin morphs
        };
        
        private readonly string _savePath;

        private int _totalExperiments;
        
        public int TotalExperiments => _totalExperiments;
        public bool IsEmpty => _totalExperiments == 0;
        
        public FeatureMorphLearning(string savePath)
        {
            _savePath = savePath;
            
            // Initialize correlations for each feature
            foreach (var feature in FeatureMorphRanges.Keys)
            {
                _featureCorrelations[feature] = new MorphCorrelations(feature);
                _featureCharacteristics[feature] = new Dictionary<string, LearnedMorphSet>();
            }
            
            Load();
        }
        
        /// <summary>
        /// Record an experiment: what morphs were changed and how did each feature score change?
        /// This is the CORE learning function - called after every iteration.
        /// </summary>

        public void RecordExperiment(
            float[] previousMorphs,
            float[] currentMorphs,
            Dictionary<string, float> previousFeatureScores,
            Dictionary<string, float> currentFeatureScores)
        {
            if (previousMorphs == null || currentMorphs == null) return;
            if (previousFeatureScores == null || currentFeatureScores == null) return;
            
            _totalExperiments++;
            
            // Calculate morph deltas
            var morphDeltas = new float[currentMorphs.Length];
            for (int i = 0; i < currentMorphs.Length; i++)
            {
                morphDeltas[i] = currentMorphs[i] - previousMorphs[i];
            }
            
            // For each feature, record how morph changes affected its score
            foreach (var feature in FeatureMorphRanges.Keys)
            {
                if (!currentFeatureScores.TryGetValue(feature, out float currentScore)) continue;
                if (!previousFeatureScores.TryGetValue(feature, out float previousScore)) continue;
                
                float scoreDelta = currentScore - previousScore;
                
                // Only learn from significant changes
                if (Math.Abs(scoreDelta) < 0.005f) continue;
                
                // Record correlations: which morphs changed when this feature improved/declined?
                _featureCorrelations[feature].RecordExperiment(morphDeltas, scoreDelta);
                
                // If score improved significantly, record the morphs that helped
                if (scoreDelta > 0.02f)
                {
                    RecordFeatureImprovement(feature, currentScore, currentMorphs, morphDeltas);
                }
            }
        }
        
        /// <summary>
        /// Record a successful improvement for a feature
        /// </summary>
        private void RecordFeatureImprovement(string feature, float score, float[] morphs, float[] deltas)
        {
            // Categorize the score level
            string scoreLevel = score switch
            {
                > 0.85f => "Excellent",
                > 0.70f => "Good",
                > 0.55f => "Medium",
                _ => "Low"
            };
            
            if (!_featureCharacteristics[feature].TryGetValue(scoreLevel, out var learnedSet))
            {
                learnedSet = new LearnedMorphSet(feature, scoreLevel);
                _featureCharacteristics[feature][scoreLevel] = learnedSet;
            }
            
            // Get relevant morph indices for this feature
            var (startIdx, endIdx) = FeatureMorphRanges[feature];
            
            // Record only the morphs in this feature's range that changed significantly
            for (int i = startIdx; i <= endIdx && i < morphs.Length; i++)
            {
                if (Math.Abs(deltas[i]) > 0.01f)  // Only significant changes
                {
                    learnedSet.RecordMorphValue(i, morphs[i], score);
                }
            }
        }
        
        /// <summary>
        /// Get the best known morphs for a specific feature at a target score level.
        /// Returns only the morphs relevant to this feature, not all 62.
        /// </summary>
        public Dictionary<int, float> GetBestMorphsForFeature(string feature, float targetScore = 0.7f)
        {
            var result = new Dictionary<int, float>();
            
            if (!_featureCharacteristics.TryGetValue(feature, out var characteristics))
                return result;
            
            // Find the best learned set at or above target score
            string bestLevel = targetScore switch
            {
                > 0.80f => "Excellent",
                > 0.65f => "Good", 
                > 0.50f => "Medium",
                _ => "Low"
            };
            
            // Try best level first, then fall back
            string[] levels = bestLevel switch
            {
                "Excellent" => new[] { "Excellent", "Good", "Medium" },
                "Good" => new[] { "Good", "Excellent", "Medium" },
                "Medium" => new[] { "Medium", "Good", "Low" },
                _ => new[] { "Low", "Medium", "Good" }
            };
            
            foreach (var level in levels)
            {
                if (characteristics.TryGetValue(level, out var learnedSet) && learnedSet.SampleCount > 5)
                {
                    return learnedSet.GetBestMorphs();
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get which morphs most strongly correlate with a feature.
        /// Returns morph indices sorted by correlation strength.
        /// </summary>
        public List<int> GetCorrelatedMorphs(string feature, int topN = 10)
        {
            if (!_featureCorrelations.TryGetValue(feature, out var correlations))
                return new List<int>();
            
            return correlations.GetTopCorrelatedMorphs(topN);
        }
        
        /// <summary>
        /// Combine best morphs from all features into a single morph array.
        /// This is the KEY function - merge best-per-feature into final face.
        /// </summary>
        public float[] CreateOptimalMorphsFromFeatures(int morphCount = 62)
        {
            var result = new float[morphCount];
            var confidence = new float[morphCount];  // Track how confident we are in each morph
            
            foreach (var feature in FeatureMorphRanges.Keys)
            {
                var bestMorphs = GetBestMorphsForFeature(feature);
                
                foreach (var kv in bestMorphs)
                {
                    int idx = kv.Key;
                    float value = kv.Value;
                    
                    if (idx < morphCount)
                    {
                        // Weight by how many samples we have for this feature
                        float weight = 1f;
                        if (_featureCharacteristics.TryGetValue(feature, out var chars))
                        {
                            weight = Math.Min(1f, chars.Values.Sum(c => c.SampleCount) / 100f);
                        }
                        
                        result[idx] += value * weight;
                        confidence[idx] += weight;
                    }
                }
            }
            
            // Normalize by confidence
            for (int i = 0; i < morphCount; i++)
            {
                if (confidence[i] > 0)
                {
                    result[i] /= confidence[i];
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get a mutation suggestion for the worst feature.
        /// Returns specific morph indices and suggested deltas.
        /// </summary>
        public Dictionary<int, float> GetMutationForFeature(string feature, float currentScore)
        {
            var result = new Dictionary<int, float>();
            
            // Get correlations for this feature
            if (!_featureCorrelations.TryGetValue(feature, out var correlations))
                return result;
            
            // Get the morphs that most strongly correlate with improvements
            var topMorphs = correlations.GetTopCorrelatedMorphs(8);
            
            foreach (int morphIdx in topMorphs)
            {
                // Get the average delta that led to improvements
                float avgDelta = correlations.GetAverageImprovementDelta(morphIdx);
                if (Math.Abs(avgDelta) > 0.01f)
                {
                    result[morphIdx] = avgDelta;
                }
            }
            
            return result;
        }
        
        public string GetSummary()
        {
            int totalLearned = _featureCharacteristics.Values.Sum(c => c.Count);
            int totalCorrelations = _featureCorrelations.Values.Sum(c => c.TotalRecorded);
            
            return $"FeatureMorphLearning: {_totalExperiments} exp, {totalLearned} characteristics, {totalCorrelations} correlations";
        }
        
        
        public void Save()
        {
            try
            {
                using var writer = new BinaryWriter(File.Create(_savePath));
                
                // Header
                writer.Write("FML1");  // Magic + version
                writer.Write(_totalExperiments);
                
                // Save correlations
                writer.Write(_featureCorrelations.Count);
                foreach (var kv in _featureCorrelations)
                {
                    writer.Write(kv.Key);
                    kv.Value.Save(writer);
                }
                
                // Save characteristics
                writer.Write(_featureCharacteristics.Count);
                foreach (var featureKv in _featureCharacteristics)
                {
                    writer.Write(featureKv.Key);
                    writer.Write(featureKv.Value.Count);
                    foreach (var charKv in featureKv.Value)
                    {
                        writer.Write(charKv.Key);
                        charKv.Value.Save(writer);
                    }
                }
                
                SubModule.Log($"[FeatureMorphLearning] Saved: {GetSummary()}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"[FeatureMorphLearning] Save error: {ex.Message}");
            }
        }
        
        public void Load()
        {
            if (!File.Exists(_savePath)) return;
            
            try
            {
                using var reader = new BinaryReader(File.OpenRead(_savePath));
                
                string magic = reader.ReadString();
                if (!magic.StartsWith("FML")) return;
                
                _totalExperiments = reader.ReadInt32();
                
                // Load correlations
                int corrCount = reader.ReadInt32();
                for (int i = 0; i < corrCount; i++)
                {
                    string feature = reader.ReadString();
                    if (_featureCorrelations.TryGetValue(feature, out var corr))
                    {
                        corr.Load(reader);
                    }
                }
                
                // Load characteristics
                int charCount = reader.ReadInt32();
                for (int i = 0; i < charCount; i++)
                {
                    string feature = reader.ReadString();
                    int levelCount = reader.ReadInt32();
                    
                    if (!_featureCharacteristics.TryGetValue(feature, out var chars))
                    {
                        chars = new Dictionary<string, LearnedMorphSet>();
                        _featureCharacteristics[feature] = chars;
                    }
                    
                    for (int j = 0; j < levelCount; j++)
                    {
                        string level = reader.ReadString();
                        var learnedSet = new LearnedMorphSet(feature, level);
                        learnedSet.Load(reader);
                        chars[level] = learnedSet;
                    }
                }
                
                SubModule.Log($"[FeatureMorphLearning] Loaded: {GetSummary()}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"[FeatureMorphLearning] Load error: {ex.Message}");
            }
        }
        
    }
    
    /// <summary>
    /// Tracks correlations between morph changes and feature score changes
    /// </summary>
    internal class MorphCorrelations
    {
        private readonly string _feature;
        
        // Morph index -> (sum of score deltas when this morph increased, count)

        private Dictionary<int, (float sumDelta, int count)> _positiveCorrelations = new();
        private Dictionary<int, (float sumDelta, int count)> _negativeCorrelations = new();
        
        public int TotalRecorded { get; private set; }
        
        public MorphCorrelations(string feature)
        {
            _feature = feature;
        }
        

        public void RecordExperiment(float[] morphDeltas, float scoreDelta)
        {
            TotalRecorded++;
            
            for (int i = 0; i < morphDeltas.Length; i++)
            {
                float delta = morphDeltas[i];
                if (Math.Abs(delta) < 0.01f) continue;  // Skip tiny changes
                
                if (delta > 0)
                {
                    // Morph increased
                    if (!_positiveCorrelations.TryGetValue(i, out var pos))
                        pos = (0, 0);
                    _positiveCorrelations[i] = (pos.sumDelta + scoreDelta, pos.count + 1);
                }
                else
                {
                    // Morph decreased
                    if (!_negativeCorrelations.TryGetValue(i, out var neg))
                        neg = (0, 0);
                    _negativeCorrelations[i] = (neg.sumDelta + scoreDelta, neg.count + 1);
                }
            }
        }
        
        public List<int> GetTopCorrelatedMorphs(int topN)
        {
            var correlations = new Dictionary<int, float>();
            
            // Calculate correlation strength for each morph
            foreach (var kv in _positiveCorrelations)
            {
                if (kv.Value.count >= 3)  // Minimum samples
                {
                    float avgDelta = kv.Value.sumDelta / kv.Value.count;
                    correlations[kv.Key] = Math.Abs(avgDelta) * Math.Min(1f, kv.Value.count / 10f);
                }
            }
            
            foreach (var kv in _negativeCorrelations)
            {
                if (kv.Value.count >= 3)
                {
                    float avgDelta = kv.Value.sumDelta / kv.Value.count;
                    float strength = Math.Abs(avgDelta) * Math.Min(1f, kv.Value.count / 10f);
                    
                    if (correlations.TryGetValue(kv.Key, out float existing))
                        correlations[kv.Key] = Math.Max(existing, strength);
                    else
                        correlations[kv.Key] = strength;
                }
            }
            
            return correlations
                .OrderByDescending(kv => kv.Value)
                .Take(topN)
                .Select(kv => kv.Key)
                .ToList();
        }
        
        public float GetAverageImprovementDelta(int morphIdx)
        {
            float result = 0;
            int count = 0;
            
            // If increasing this morph improved score, suggest increase
            if (_positiveCorrelations.TryGetValue(morphIdx, out var pos) && pos.count >= 2)
            {
                float avgDelta = pos.sumDelta / pos.count;
                if (avgDelta > 0)  // Increase led to improvement
                {
                    result += avgDelta;
                    count++;
                }
            }
            
            // If decreasing this morph improved score, suggest decrease
            if (_negativeCorrelations.TryGetValue(morphIdx, out var neg) && neg.count >= 2)
            {
                float avgDelta = neg.sumDelta / neg.count;
                if (avgDelta > 0)  // Decrease led to improvement
                {
                    result -= avgDelta;  // Negative because we want to decrease
                    count++;
                }
            }
            
            return count > 0 ? result / count : 0;
        }
        
        public void Save(BinaryWriter writer)
        {
            writer.Write(TotalRecorded);
            
            writer.Write(_positiveCorrelations.Count);
            foreach (var kv in _positiveCorrelations)
            {
                writer.Write(kv.Key);
                writer.Write(kv.Value.sumDelta);
                writer.Write(kv.Value.count);
            }
            
            writer.Write(_negativeCorrelations.Count);
            foreach (var kv in _negativeCorrelations)
            {
                writer.Write(kv.Key);
                writer.Write(kv.Value.sumDelta);
                writer.Write(kv.Value.count);
            }
        }
        
        public void Load(BinaryReader reader)
        {
            TotalRecorded = reader.ReadInt32();
            
            int posCount = reader.ReadInt32();
            for (int i = 0; i < posCount; i++)
            {
                int idx = reader.ReadInt32();
                float sum = reader.ReadSingle();
                int count = reader.ReadInt32();
                _positiveCorrelations[idx] = (sum, count);
            }
            
            int negCount = reader.ReadInt32();
            for (int i = 0; i < negCount; i++)
            {
                int idx = reader.ReadInt32();
                float sum = reader.ReadSingle();
                int count = reader.ReadInt32();
                _negativeCorrelations[idx] = (sum, count);
            }
        }
    }
    
    /// <summary>
    /// Stores learned morph values for a specific feature characteristic
    /// </summary>
    internal class LearnedMorphSet
    {
        private readonly string _feature;
        private readonly string _level;
        
        // Morph index -> (sum of values, sum of weights, count)

        private Dictionary<int, (float sumValue, float sumWeight, int count)> _morphValues = new();
        
        public int SampleCount { get; private set; }
        
        public LearnedMorphSet(string feature, string level)
        {
            _feature = feature;
            _level = level;
        }
        

        public void RecordMorphValue(int morphIdx, float value, float scoreWeight)
        {
            SampleCount++;
            
            if (!_morphValues.TryGetValue(morphIdx, out var existing))
                existing = (0, 0, 0);
            
            _morphValues[morphIdx] = (
                existing.sumValue + value * scoreWeight,
                existing.sumWeight + scoreWeight,
                existing.count + 1
            );
        }
        
        public Dictionary<int, float> GetBestMorphs()
        {
            var result = new Dictionary<int, float>();
            
            foreach (var kv in _morphValues)
            {
                if (kv.Value.count >= 3 && kv.Value.sumWeight > 0)  // Minimum samples
                {
                    result[kv.Key] = kv.Value.sumValue / kv.Value.sumWeight;
                }
            }
            
            return result;
        }
        
        public void Save(BinaryWriter writer)
        {
            writer.Write(SampleCount);
            writer.Write(_morphValues.Count);
            
            foreach (var kv in _morphValues)
            {
                writer.Write(kv.Key);
                writer.Write(kv.Value.sumValue);
                writer.Write(kv.Value.sumWeight);
                writer.Write(kv.Value.count);
            }
        }
        
        public void Load(BinaryReader reader)
        {
            SampleCount = reader.ReadInt32();
            int count = reader.ReadInt32();
            
            for (int i = 0; i < count; i++)
            {
                int idx = reader.ReadInt32();
                float sumValue = reader.ReadSingle();
                float sumWeight = reader.ReadSingle();
                int cnt = reader.ReadInt32();
                _morphValues[idx] = (sumValue, sumWeight, cnt);
            }
        }
    }
}
