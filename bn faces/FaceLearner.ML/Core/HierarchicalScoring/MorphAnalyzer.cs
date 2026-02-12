using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Analyzes which Bannerlord morphs affect which facial features.
    /// 
    /// This is used to build/validate the MorphGroups mapping.
    /// Run this once to discover the morph→feature relationships.
    /// 
    /// Method:
    /// 1. Start with neutral face
    /// 2. Vary one morph at a time
    /// 3. Measure which landmarks change
    /// 4. Map landmarks to features
    /// 5. Output morph→feature mapping
    /// </summary>
    public class MorphAnalyzer
    {
        // Landmark groups (Dlib 68)
        private static readonly Dictionary<string, int[]> LandmarkGroups = new Dictionary<string, int[]>
        {
            { "Jaw", Enumerable.Range(0, 17).ToArray() },           // 0-16
            { "LeftBrow", Enumerable.Range(17, 5).ToArray() },      // 17-21
            { "RightBrow", Enumerable.Range(22, 5).ToArray() },     // 22-26
            { "NoseBridge", Enumerable.Range(27, 4).ToArray() },    // 27-30
            { "NoseBottom", Enumerable.Range(31, 5).ToArray() },    // 31-35
            { "LeftEye", Enumerable.Range(36, 6).ToArray() },       // 36-41
            { "RightEye", Enumerable.Range(42, 6).ToArray() },      // 42-47
            { "OuterLip", Enumerable.Range(48, 12).ToArray() },     // 48-59
            { "InnerLip", Enumerable.Range(60, 8).ToArray() },      // 60-67
        };
        
        // Feature categories derived from landmark groups
        private static readonly Dictionary<SubPhase, string[]> PhaseToLandmarkGroups = new Dictionary<SubPhase, string[]>
        {
            { SubPhase.FaceWidth, new[] { "Jaw" } },
            { SubPhase.FaceHeight, new[] { "Jaw", "NoseBridge" } },
            { SubPhase.FaceShape, new[] { "Jaw" } },
            { SubPhase.Forehead, new[] { "LeftBrow", "RightBrow" } },
            { SubPhase.Jaw, new[] { "Jaw" } },
            { SubPhase.Chin, new[] { "Jaw" } },  // Lower part of jaw
            { SubPhase.Cheeks, new[] { "Jaw" } },  // Middle part of jaw
            { SubPhase.Nose, new[] { "NoseBridge", "NoseBottom" } },
            { SubPhase.Eyes, new[] { "LeftEye", "RightEye" } },
            { SubPhase.Mouth, new[] { "OuterLip", "InnerLip" } },
            { SubPhase.Eyebrows, new[] { "LeftBrow", "RightBrow" } },
        };
        
        // Analysis results
        private readonly Dictionary<int, MorphEffect> _morphEffects = new Dictionary<int, MorphEffect>();
        
        /// <summary>Threshold for considering a landmark "affected"</summary>
        public float ChangeThreshold { get; set; } = 0.005f;
        
        /// <summary>
        /// Analyze a single morph by comparing baseline and modified landmarks.
        /// Call this repeatedly for each morph to build the complete mapping.
        /// </summary>
        public void AnalyzeMorph(int morphIndex, float[] baselineLandmarks, float[] modifiedLandmarks)
        {
            if (baselineLandmarks == null || modifiedLandmarks == null)
                return;
            
            if (baselineLandmarks.Length != modifiedLandmarks.Length)
                return;
            
            var effect = new MorphEffect { MorphIndex = morphIndex };
            
            // Calculate change for each landmark
            int numLandmarks = baselineLandmarks.Length / 2;
            for (int i = 0; i < numLandmarks; i++)
            {
                float dx = modifiedLandmarks[i * 2] - baselineLandmarks[i * 2];
                float dy = modifiedLandmarks[i * 2 + 1] - baselineLandmarks[i * 2 + 1];
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                
                if (dist > ChangeThreshold)
                {
                    effect.AffectedLandmarks[i] = dist;
                }
            }
            
            // Determine which landmark groups are affected
            foreach (var group in LandmarkGroups)
            {
                float groupEffect = 0f;
                foreach (int lm in group.Value)
                {
                    if (effect.AffectedLandmarks.TryGetValue(lm, out float e))
                        groupEffect += e;
                }
                
                if (groupEffect > ChangeThreshold)
                {
                    effect.AffectedGroups[group.Key] = groupEffect;
                }
            }
            
            // Determine which phases this morph belongs to
            foreach (var phaseMapping in PhaseToLandmarkGroups)
            {
                float phaseEffect = 0f;
                foreach (string groupName in phaseMapping.Value)
                {
                    if (effect.AffectedGroups.TryGetValue(groupName, out float e))
                        phaseEffect += e;
                }
                
                if (phaseEffect > ChangeThreshold)
                {
                    effect.AffectedPhases[phaseMapping.Key] = phaseEffect;
                }
            }
            
            // Determine primary phase (strongest effect)
            if (effect.AffectedPhases.Count > 0)
            {
                effect.PrimaryPhase = effect.AffectedPhases
                    .OrderByDescending(kv => kv.Value)
                    .First().Key;
            }
            
            _morphEffects[morphIndex] = effect;
        }
        
        /// <summary>Get the analysis result for a morph</summary>
        public MorphEffect GetMorphEffect(int morphIndex)
        {
            _morphEffects.TryGetValue(morphIndex, out var effect);
            return effect;
        }
        
        /// <summary>Get all morphs that affect a specific phase</summary>
        public int[] GetMorphsForPhase(SubPhase phase, float minEffect = 0.01f)
        {
            return _morphEffects
                .Where(kv => kv.Value.AffectedPhases.TryGetValue(phase, out float e) && e >= minEffect)
                .OrderByDescending(kv => kv.Value.AffectedPhases[phase])
                .Select(kv => kv.Key)
                .ToArray();
        }
        
        /// <summary>Generate code for MorphGroups class based on analysis</summary>
        public string GenerateMorphGroupsCode()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// Auto-generated morph groups based on analysis");
            sb.AppendLine("// Run MorphAnalyzer to regenerate this mapping");
            sb.AppendLine();
            
            foreach (SubPhase phase in Enum.GetValues(typeof(SubPhase)))
            {
                var morphs = GetMorphsForPhase(phase);
                if (morphs.Length > 0)
                {
                    sb.AppendLine($"public static readonly int[] {phase} = {{ {string.Join(", ", morphs)} }};");
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>Get summary of all analyzed morphs</summary>
        public string GetSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Morph Analysis Summary ({_morphEffects.Count} morphs analyzed) ===");
            sb.AppendLine();
            
            // Group by primary phase
            var byPhase = _morphEffects.Values
                .Where(e => e.PrimaryPhase.HasValue)
                .GroupBy(e => e.PrimaryPhase.Value)
                .OrderBy(g => g.Key);
            
            foreach (var group in byPhase)
            {
                sb.AppendLine($"{group.Key}: {string.Join(", ", group.Select(e => e.MorphIndex))}");
            }
            
            // Unmapped morphs
            var unmapped = _morphEffects.Values
                .Where(e => !e.PrimaryPhase.HasValue)
                .Select(e => e.MorphIndex)
                .ToArray();
            
            if (unmapped.Length > 0)
            {
                sb.AppendLine($"Unmapped: {string.Join(", ", unmapped)}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>Clear all analysis data</summary>
        public void Clear() => _morphEffects.Clear();
    }
    
    /// <summary>Effect of a single morph on facial features</summary>
    public class MorphEffect
    {
        public int MorphIndex { get; set; }
        
        /// <summary>Affected landmarks (index → amount of change)</summary>
        public Dictionary<int, float> AffectedLandmarks { get; set; } = new Dictionary<int, float>();
        
        /// <summary>Affected landmark groups (name → total effect)</summary>
        public Dictionary<string, float> AffectedGroups { get; set; } = new Dictionary<string, float>();
        
        /// <summary>Affected phases (phase → total effect)</summary>
        public Dictionary<SubPhase, float> AffectedPhases { get; set; } = new Dictionary<SubPhase, float>();
        
        /// <summary>Primary phase (strongest effect)</summary>
        public SubPhase? PrimaryPhase { get; set; }
        
        public override string ToString()
        {
            string phases = AffectedPhases.Count > 0 
                ? string.Join(", ", AffectedPhases.Select(kv => $"{kv.Key}:{kv.Value:F3}"))
                : "none";
            return $"Morph[{MorphIndex}] → {phases}";
        }
    }
}
