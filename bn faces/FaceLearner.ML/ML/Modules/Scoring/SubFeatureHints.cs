using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FaceLearner.ML.Modules.Core;

namespace FaceLearner.ML.Modules.Scoring
{
    /// <summary>
    /// Quality level for sub-features
    /// </summary>
    public enum SubFeatureQuality
    {
        Bad,    // < 0.4
        Poor,   // 0.4 - 0.55
        Okay,   // 0.55 - 0.7
        Good,   // 0.7 - 0.85
        Great   // > 0.85
    }
    
    /// <summary>
    /// Detailed sub-feature analysis for a single facial feature.
    /// Helps morpher understand WHAT specifically needs improvement.
    /// </summary>
    public class SubFeatureAnalysis
    {
        public FaceFeature Feature { get; set; }
        public float OverallScore { get; set; }
        public Dictionary<string, float> SubScores { get; set; } = new Dictionary<string, float>();
        public Dictionary<string, SubFeatureQuality> SubQualities { get; set; } = new Dictionary<string, SubFeatureQuality>();
        
        /// <summary>
        /// Get worst sub-features that need improvement
        /// </summary>
        public IEnumerable<string> GetWorstSubFeatures(int count = 2)
        {
            return SubScores
                .OrderBy(kv => kv.Value)
                .Take(count)
                .Select(kv => kv.Key);
        }
        
        /// <summary>
        /// Get sub-features below a threshold
        /// </summary>
        public IEnumerable<string> GetProblemSubFeatures(float threshold = 0.5f)
        {
            return SubScores
                .Where(kv => kv.Value < threshold)
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key);
        }
        
        /// <summary>
        /// Format as hint string: "Face:0.25 (width:bad, height:bad, roundness:okay)"
        /// </summary>
        public string ToHintString()
        {
            var sb = new StringBuilder();
            sb.Append($"{Feature}:{OverallScore:F2}");
            
            if (SubQualities.Count > 0)
            {
                var hints = SubQualities
                    .OrderBy(kv => kv.Value)  // Worst first
                    .Take(4)  // Max 4 sub-features
                    .Select(kv => $"{kv.Key}:{kv.Value.ToString().ToLower()}");
                
                sb.Append($" ({string.Join(", ", hints)})");
            }
            
            return sb.ToString();
        }
        
        public override string ToString() => ToHintString();
    }
    
    /// <summary>
    /// Complete sub-feature analysis for all facial features.
    /// Provides detailed hints for the morpher.
    /// </summary>
    public class SubFeatureHints
    {
        public SubFeatureAnalysis FaceShape { get; set; }
        public SubFeatureAnalysis Eyes { get; set; }
        public SubFeatureAnalysis Nose { get; set; }
        public SubFeatureAnalysis Mouth { get; set; }
        public SubFeatureAnalysis Jaw { get; set; }
        public SubFeatureAnalysis Eyebrows { get; set; }
        
        /// <summary>
        /// Get all analyses
        /// </summary>
        public IEnumerable<SubFeatureAnalysis> All
        {
            get
            {
                if (FaceShape != null) yield return FaceShape;
                if (Eyes != null) yield return Eyes;
                if (Nose != null) yield return Nose;
                if (Mouth != null) yield return Mouth;
                if (Jaw != null) yield return Jaw;
                if (Eyebrows != null) yield return Eyebrows;
            }
        }
        
        /// <summary>
        /// Get features that need the most work, with their problem sub-features
        /// </summary>
        public Dictionary<FaceFeature, List<string>> GetMutationPriorities(float threshold = 0.55f)
        {
            var result = new Dictionary<FaceFeature, List<string>>();
            
            foreach (var analysis in All)
            {
                if (analysis.OverallScore < threshold)
                {
                    var problems = analysis.GetProblemSubFeatures(threshold).ToList();
                    if (problems.Count > 0)
                    {
                        result[analysis.Feature] = problems;
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Format as multi-line hint string
        /// </summary>
        public string ToHintString()
        {
            var lines = All
                .OrderBy(a => a.OverallScore)  // Worst first
                .Select(a => a.ToHintString());
            
            return string.Join("\n", lines);
        }
        
        /// <summary>
        /// Format as compact single-line (for logging)
        /// </summary>
        public string ToCompactString()
        {
            var parts = All
                .OrderBy(a => a.OverallScore)
                .Select(a => 
                {
                    var worst = a.GetWorstSubFeatures(2).ToList();
                    if (worst.Count > 0)
                        return $"{a.Feature}:{a.OverallScore:F2}({string.Join(",", worst)})";
                    return $"{a.Feature}:{a.OverallScore:F2}";
                });
            
            return string.Join(" | ", parts);
        }
        
        public override string ToString() => ToCompactString();
        
        /// <summary>
        /// Convert score to quality level
        /// </summary>
        public static SubFeatureQuality ScoreToQuality(float score)
        {
            if (score < 0.4f) return SubFeatureQuality.Bad;
            if (score < 0.55f) return SubFeatureQuality.Poor;
            if (score < 0.7f) return SubFeatureQuality.Okay;
            if (score < 0.85f) return SubFeatureQuality.Good;
            return SubFeatureQuality.Great;
        }
    }
}
