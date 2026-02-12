using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FaceLearner.Core
{
    /// <summary>
    /// Lightweight, READ-ONLY knowledge access for the Core mod.
    /// This allows face generation using pre-trained knowledge without
    /// requiring the heavy ML addon.
    /// 
    /// For TRAINING/LEARNING, the ML addon's HierarchicalKnowledge is needed.
    /// </summary>
    public class KnowledgeReader
    {
        private string _knowledgePath;
        private bool _isLoaded;
        
        // Simplified knowledge structure (loaded from file)
        private Dictionary<string, float[]> _featureBaseMorphs = new Dictionary<string, float[]>();
        private int _numMorphs = 62;
        
        // Simple feature presets for when no knowledge file exists
        private static readonly Dictionary<string, float[]> DefaultPresets = new Dictionary<string, float[]>
        {
            // These are basic starting points - real values come from trained knowledge
            { "Gender:Male", CreateBaseMorphs(new[] { (0, 0.1f), (1, -0.05f), (6, 0.1f) }) },
            { "Gender:Female", CreateBaseMorphs(new[] { (0, -0.1f), (1, 0.05f), (6, -0.1f) }) },
            { "AgeGroup:Young", CreateBaseMorphs(new[] { (3, -0.1f), (8, 0.05f) }) },
            { "AgeGroup:Middle", CreateBaseMorphs(new[] { (3, 0.0f), (8, 0.0f) }) },
            { "AgeGroup:Mature", CreateBaseMorphs(new[] { (3, 0.15f), (8, -0.05f) }) },
        };
        
        public bool IsLoaded => _isLoaded;
        public int FeatureCount => _featureBaseMorphs.Count;
        
        public KnowledgeReader(string knowledgePath)
        {
            _knowledgePath = knowledgePath;
        }
        
        /// <summary>
        /// Load knowledge from file (if exists)
        /// </summary>
        public bool Load()
        {
            _featureBaseMorphs.Clear();
            
            if (!File.Exists(_knowledgePath))
            {
                SubModule.Log("[KnowledgeReader] No knowledge file found, using defaults");
                _featureBaseMorphs = new Dictionary<string, float[]>(DefaultPresets);
                _isLoaded = true;
                return true;
            }
            
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(_knowledgePath)))
                {
                    string header = reader.ReadString();
                    
                    // Check for V4 format (has shared knowledge)
                    if (header == "HKNOW04")
                    {
                        LoadV4Format(reader);
                    }
                    else if (header == "HKNOW03" || header == "HKNOW02" || header == "HKNOW01")
                    {
                        // Older formats - just use defaults
                        SubModule.Log($"[KnowledgeReader] Old format {header}, using defaults + tree");
                        _featureBaseMorphs = new Dictionary<string, float[]>(DefaultPresets);
                        // Could parse tree for additional knowledge
                    }
                    else
                    {
                        SubModule.Log($"[KnowledgeReader] Unknown format: {header}");
                        _featureBaseMorphs = new Dictionary<string, float[]>(DefaultPresets);
                    }
                }
                
                _isLoaded = true;
                SubModule.Log($"[KnowledgeReader] Loaded {_featureBaseMorphs.Count} feature entries");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"[KnowledgeReader] Load error: {ex.Message}");
                _featureBaseMorphs = new Dictionary<string, float[]>(DefaultPresets);
                _isLoaded = true;
                return false;
            }
        }
        
        private void LoadV4Format(BinaryReader reader)
        {
            // Skip experiments count
            reader.ReadInt32();
            
            // Skip tree (we only want shared knowledge for lightweight reading)
            SkipNode(reader);
            
            // Skip feature problem branches
            int branchCount = reader.ReadInt32();
            for (int i = 0; i < branchCount; i++)
            {
                reader.ReadString();  // key
                SkipNode(reader);
            }
            
            // Load shared feature knowledge (this is what we want!)
            int sharedCount = reader.ReadInt32();
            for (int i = 0; i < sharedCount; i++)
            {
                string featureKey = reader.ReadString();
                int learnCount = reader.ReadInt32();
                float confidence = reader.ReadSingle();
                
                // Read base morphs
                int morphCount = reader.ReadInt32();
                var morphs = new float[_numMorphs];
                for (int j = 0; j < morphCount; j++)
                {
                    int key = reader.ReadInt32();
                    float value = reader.ReadSingle();
                    if (key >= 0 && key < _numMorphs)
                        morphs[key] = value;
                }
                
                // Skip learned contexts
                int contextCount = reader.ReadInt32();
                for (int j = 0; j < contextCount; j++)
                    reader.ReadString();
                
                // Only store if confident enough
                if (confidence > 0.2f)
                {
                    _featureBaseMorphs[featureKey] = morphs;
                }
            }
        }
        
        private void SkipNode(BinaryReader reader)
        {
            // Skip node data (simplified - matches HierarchicalKnowledge format)
            reader.ReadString();  // Path
            reader.ReadInt32();   // Feature
            reader.ReadString();  // Value
            reader.ReadInt32();   // UseCount
            reader.ReadInt32();   // SuccessCount
            reader.ReadSingle();  // ConfidenceScore
            
            // Skip MorphDeltas
            int deltaCount = reader.ReadInt32();
            for (int i = 0; i < deltaCount; i++)
            {
                reader.ReadInt32();
                reader.ReadSingle();
            }
            
            // V3+ metrics
            int varCount = reader.ReadInt32();
            for (int i = 0; i < varCount; i++)
            {
                reader.ReadInt32();
                reader.ReadSingle();
            }
            
            reader.ReadSingle();  // OutcomeVariance
            reader.ReadSingle();  // Health
            reader.ReadInt32();   // SplitCount
            reader.ReadInt32();   // MergeCount
            reader.ReadInt64();   // LastUsed
            reader.ReadInt64();   // LastRefinement
            
            int outcomeCount = reader.ReadInt32();
            for (int i = 0; i < outcomeCount; i++)
                reader.ReadSingle();
            
            // Skip children recursively
            int childCount = reader.ReadInt32();
            for (int i = 0; i < childCount; i++)
                SkipNode(reader);
        }
        
        /// <summary>
        /// Get starting morphs for a set of features
        /// </summary>
        public float[] GetStartingMorphs(Dictionary<string, string> features)
        {
            var morphs = new float[_numMorphs];
            
            foreach (var feature in features)
            {
                string key = $"{feature.Key}:{feature.Value}";
                if (_featureBaseMorphs.TryGetValue(key, out var baseMorphs))
                {
                    for (int i = 0; i < _numMorphs && i < baseMorphs.Length; i++)
                    {
                        morphs[i] += baseMorphs[i];
                    }
                }
            }
            
            // Clamp to valid range
            for (int i = 0; i < morphs.Length; i++)
            {
                morphs[i] = Math.Max(-1f, Math.Min(1f, morphs[i]));
            }
            
            return morphs;
        }
        
        /// <summary>
        /// Get morphs for simple generation (gender + age only)
        /// </summary>
        public float[] GetSimpleMorphs(bool isFemale, float age)
        {
            var features = new Dictionary<string, string>
            {
                { "Gender", isFemale ? "Female" : "Male" },
                { "AgeGroup", age < 0.33f ? "Young" : (age < 0.66f ? "Middle" : "Mature") }
            };
            
            return GetStartingMorphs(features);
        }
        
        public string GetSummary()
        {
            return $"KnowledgeReader: {_featureBaseMorphs.Count} features loaded";
        }
        
        // Helper to create sparse morph arrays
        private static float[] CreateBaseMorphs((int index, float value)[] values)
        {
            var morphs = new float[62];
            foreach (var (index, value) in values)
            {
                if (index >= 0 && index < 62)
                    morphs[index] = value;
            }
            return morphs;
        }
    }
}
