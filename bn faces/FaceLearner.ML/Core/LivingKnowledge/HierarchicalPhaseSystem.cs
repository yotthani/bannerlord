using System;
using System.Collections.Generic;
using System.Linq;
using FaceLearner.ML.Modules;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Hierarchical Phase System - structured learning from coarse to fine.
    /// 
    /// Instead of chaotic "mutate everything, exploit random feature", this system:
    /// 1. COARSE: Find rough similarity (all morphs)
    /// 2. PROPORTIONS: Lock in face shape (Face + Jaw only)
    /// 3. FEATURES: Refine main features (Eyes, Nose, Mouth)
    /// 4. DETAILS: Polish weak areas (Brows + worst feature)
    /// 5. POLISH: Final global fine-tuning
    /// 
    /// Each phase FREEZES non-active morphs to prevent regression.
    /// 
    /// Within each main phase, SUBPHASES are dynamic:
    /// - Broad: Large sigma exploration
    /// - Refinement: Medium sigma after finding good region
    /// - PlateauEscape: Temporary sigma boost when stuck
    /// - FineTune: Small sigma for final adjustments
    /// </summary>
    public class HierarchicalPhaseSystem
    {
        // Main Phase definitions
        public enum HierarchicalPhase
        {
            Coarse,       // All morphs, big sigma - find rough match
            Proportions,  // Face + Jaw only - establish structure
            Features,     // Eyes + Nose + Mouth - main recognition features
            Details,      // Brows + weakest feature - fine details
            Polish        // All morphs, tiny sigma - final harmony
        }
        
        // Sub-phase definitions (dynamic within each main phase)
        public enum SubPhase
        {
            Broad,          // Wide exploration with large sigma
            Refinement,     // Focused improvement with medium sigma
            PlateauEscape,  // Break out of local minima with temporary sigma boost
            FineTune        // Final adjustments with small sigma
        }
        
        // Morph index ranges per feature (Bannerlord)

        public static readonly Dictionary<string, (int Start, int End)> MorphRanges = new Dictionary<string, (int, int)>
        {
            { "Eyes", (0, 15) },
            { "Nose", (16, 27) },
            { "Mouth", (28, 38) },
            { "Face", (39, 50) },
            { "Jaw", (51, 60) },
            { "Brows", (61, 68) }
        };
        
        // Phase configuration
        public class PhaseConfig
        {
            public HierarchicalPhase Phase { get; set; }

            public string Name { get; set; }
            public int MinIterations { get; set; }      // Minimum iterations before can transition
            public int MaxIterations { get; set; }      // Force transition after this
            public float MinSigma { get; set; }         // CMA-ES minimum step size
            public float MaxSigma { get; set; }         // CMA-ES maximum step size
            public float TransitionScoreThreshold { get; set; }  // Overall score to allow early transition
            public List<string> ActiveFeatures { get; set; }     // Which features' morphs are active
            public string Description { get; set; }
            
            /// <summary>
            /// Get all morph indices that are active in this phase
            /// </summary>
            public HashSet<int> GetActiveMorphIndices()
            {
                var indices = new HashSet<int>();
                
                // "All" means all morphs active
                if (ActiveFeatures.Contains("All"))
                {
                    for (int i = 0; i < 70; i++) // Assume max 70 morphs
                        indices.Add(i);
                    return indices;
                }
                
                foreach (var feature in ActiveFeatures)
                {
                    if (MorphRanges.TryGetValue(feature, out var range))
                    {
                        for (int i = range.Start; i <= range.End; i++)
                            indices.Add(i);
                    }
                }
                
                return indices;
            }
        }
        
        // Phase configurations
        // NOTE: TransitionScoreThreshold is NO LONGER used for main phase transitions!
        // Phases transition based on:
        //   1. MaxIterations reached (timeout)
        //   2. Plateau detected (no progress)
        // This ensures EVERY phase is properly explored regardless of initial score.
        // Total iterations for all phases: ~175 (fits within 200 target limit)

        private static readonly List<PhaseConfig> PhaseConfigs = new List<PhaseConfig>
        {
            new PhaseConfig
            {
                Phase = HierarchicalPhase.Coarse,
                Name = "1. Coarse Search",
                MinIterations = 20,     // Must explore for at least 20 iterations
                MaxIterations = 40,     // Up to 40 iterations (was 60)
                MinSigma = 0.5f,
                MaxSigma = 1.0f,
                TransitionScoreThreshold = 0.55f,  // NOT USED for main transition
                ActiveFeatures = new List<string> { "All" },
                Description = "Find rough similarity - all morphs active, big steps"
            },
            new PhaseConfig
            {
                Phase = HierarchicalPhase.Proportions,
                Name = "2. Proportions",
                MinIterations = 20,     // Must work on proportions for 20 iterations
                MaxIterations = 35,     // Was 50
                MinSigma = 0.3f,
                MaxSigma = 0.6f,
                TransitionScoreThreshold = 0.62f,  // NOT USED for main transition
                ActiveFeatures = new List<string> { "Face", "Jaw" },
                Description = "Lock in face shape - only Face + Jaw morphs"
            },
            new PhaseConfig
            {
                Phase = HierarchicalPhase.Features,
                Name = "3. Main Features",
                MinIterations = 25,     // Must work on features for 25 iterations
                MaxIterations = 40,     // Was 60
                MinSigma = 0.2f,
                MaxSigma = 0.5f,
                TransitionScoreThreshold = 0.70f,  // NOT USED for main transition
                ActiveFeatures = new List<string> { "Eyes", "Nose", "Mouth" },
                Description = "Refine main features - Eyes, Nose, Mouth"
            },
            new PhaseConfig
            {
                Phase = HierarchicalPhase.Details,
                Name = "4. Details",
                MinIterations = 15,     // Must polish details for 15 iterations
                MaxIterations = 30,     // Was 40
                MinSigma = 0.15f,
                MaxSigma = 0.35f,
                TransitionScoreThreshold = 0.78f,  // NOT USED for main transition
                ActiveFeatures = new List<string> { "Brows" }, // + weakest feature added dynamically
                Description = "Polish details - Brows + weakest feature"
            },
            new PhaseConfig
            {
                Phase = HierarchicalPhase.Polish,
                Name = "5. Polish",
                MinIterations = 15,     // Final polish for 15 iterations
                MaxIterations = 30,     // Was 40
                MinSigma = 0.08f,
                MaxSigma = 0.20f,
                TransitionScoreThreshold = 1.0f,  // Never early exit from Polish
                ActiveFeatures = new List<string> { "All" },
                Description = "Final global fine-tuning - all morphs, tiny steps"
            }
        };
        
        // Current state
        private HierarchicalPhase _currentPhase = HierarchicalPhase.Coarse;
        private SubPhase _currentSubPhase = SubPhase.Broad;

        private int _phaseIterations = 0;
        private int _subPhaseIterations = 0;
        private float _phaseStartScore = 0;
        private float _subPhaseStartScore = 0;
        private float[] _frozenMorphs;  // Morphs frozen at phase transition
        private HashSet<int> _activeMorphIndices;
        
        // Plateau detection for subphase transitions
        private Queue<float> _recentScores = new Queue<float>();
        private const int PLATEAU_WINDOW = 8;

        private int _plateauCounter = 0;
        
        // Feature scores for dynamic adjustment
        private Dictionary<string, float> _currentFeatureScores = new Dictionary<string, float>();
        
        // Statistics
        public int TotalIterations { get; private set; } = 0;
        public HierarchicalPhase CurrentPhase => _currentPhase;
        public SubPhase CurrentSubPhase => _currentSubPhase;

        public int PhaseIterations => _phaseIterations;
        public int SubPhaseIterations => _subPhaseIterations;
        public PhaseConfig CurrentConfig => PhaseConfigs[(int)_currentPhase];
        public HashSet<int> ActiveMorphIndices => _activeMorphIndices;
        
        public HierarchicalPhaseSystem()
        {
            Reset();
        }
        
        /// <summary>
        /// Reset to initial state for new target
        /// </summary>

        public void Reset()
        {
            _currentPhase = HierarchicalPhase.Coarse;
            _currentSubPhase = SubPhase.Broad;
            _phaseIterations = 0;
            _subPhaseIterations = 0;
            _phaseStartScore = 0;
            _subPhaseStartScore = 0;
            _frozenMorphs = null;
            _currentFeatureScores.Clear();
            _recentScores.Clear();
            _plateauCounter = 0;
            TotalIterations = 0;
            UpdateActiveMorphs();
        }
        
        /// <summary>
        /// Update active morph indices based on current phase
        /// </summary>
        private void UpdateActiveMorphs()
        {
            var config = CurrentConfig;
            _activeMorphIndices = config.GetActiveMorphIndices();
            
            // For Details phase, dynamically add weakest feature
            if (_currentPhase == HierarchicalPhase.Details && _currentFeatureScores.Count > 0)
            {
                string weakest = GetWeakestFeature();
                if (!string.IsNullOrEmpty(weakest) && MorphRanges.TryGetValue(weakest, out var range))
                {
                    for (int i = range.Start; i <= range.End; i++)
                        _activeMorphIndices.Add(i);
                    SubModule.Log($"    [HPS] Details phase: Added {weakest} (weakest) to active morphs");
                }
            }
        }
        
        /// <summary>
        /// Get the weakest feature by score
        /// </summary>
        private string GetWeakestFeature()
        {
            if (_currentFeatureScores.Count == 0) return null;
            
            // Exclude Brows since it's always in Details
            var candidates = _currentFeatureScores
                .Where(kv => kv.Key != "Brows")
                .OrderBy(kv => kv.Value)
                .FirstOrDefault();
            
            return candidates.Key;
        }
        
        /// <summary>
        /// Called each iteration - returns whether MAIN phase changed
        /// Also manages dynamic SubPhase transitions internally
        /// </summary>
        public bool Iterate(float currentScore, Dictionary<string, float> featureScores)
        {
            TotalIterations++;
            _phaseIterations++;
            _subPhaseIterations++;
            
            // Update feature scores
            if (featureScores != null)
            {
                _currentFeatureScores = new Dictionary<string, float>(featureScores);
            }
            
            // Track scores for plateau detection
            _recentScores.Enqueue(currentScore);
            while (_recentScores.Count > PLATEAU_WINDOW)
                _recentScores.Dequeue();
            
            // Detect plateau (score not improving)
            UpdatePlateauDetection(currentScore);
            
            // Check for SubPhase transition (internal, doesn't return true)
            CheckSubPhaseTransition(currentScore);
            
            // Check for MAIN phase transition
            var config = CurrentConfig;
            bool shouldTransition = false;
            string reason = "";
            
            // Max iterations reached - force transition
            if (_phaseIterations >= config.MaxIterations)
            {
                shouldTransition = true;
                reason = "max iterations";
            }
            // PLATEAU DETECTION: No progress after minimum iterations
            // This is the PRIMARY transition trigger - NOT score thresholds!
            else if (_phaseIterations >= config.MinIterations && _plateauCounter >= 8)
            {
                shouldTransition = true;
                reason = $"plateau detected (no progress for {_plateauCounter} checks)";
            }
            // For proportion/feature phases: Check if relevant features are good enough
            // BUT only after a significant number of iterations (not just MinIterations)
            else if (_phaseIterations >= config.MinIterations + 10 && ShouldTransitionByFeatureScores())
            {
                shouldTransition = true;
                reason = "feature scores sufficient";
            }
            
            if (shouldTransition && _currentPhase != HierarchicalPhase.Polish)
            {
                return TransitionToNextPhase(currentScore, reason);
            }
            
            return false;
        }
        
        /// <summary>
        /// Update plateau detection based on recent scores
        /// </summary>
        private void UpdatePlateauDetection(float currentScore)
        {
            if (_recentScores.Count >= PLATEAU_WINDOW)
            {
                float maxRecent = _recentScores.Max();
                float minRecent = _recentScores.Min();
                
                // If score range is very small, we're on a plateau
                if (maxRecent - minRecent < 0.015f)
                {
                    _plateauCounter++;
                }
                else
                {
                    _plateauCounter = Math.Max(0, _plateauCounter - 2);  // Decay faster
                }
            }
        }
        
        /// <summary>
        /// Check and perform SubPhase transitions based on progress
        /// </summary>
        private void CheckSubPhaseTransition(float currentScore)
        {
            SubPhase newSubPhase = _currentSubPhase;
            string reason = "";
            
            float avgFocusScore = GetAverageFocusScore();
            var config = CurrentConfig;
            
            switch (_currentSubPhase)
            {
                case SubPhase.Broad:
                    // Transition to Refinement when we've found a decent region
                    if (avgFocusScore > 0.40f && _subPhaseIterations > 8)
                    {
                        newSubPhase = SubPhase.Refinement;
                        reason = $"focus score {avgFocusScore:F2} > 0.40";
                    }
                    else if (_subPhaseIterations > 18)
                    {
                        newSubPhase = SubPhase.Refinement;
                        reason = "broad timeout";
                    }
                    break;
                    
                case SubPhase.Refinement:
                    // Transition to PlateauEscape if stuck
                    if (_plateauCounter >= 6)
                    {
                        newSubPhase = SubPhase.PlateauEscape;
                        reason = "plateau detected";
                    }
                    // Transition to FineTune after enough refinement iterations
                    // Not based on score threshold - just time spent refining
                    else if (_subPhaseIterations >= 15)
                    {
                        newSubPhase = SubPhase.FineTune;
                        reason = $"refinement complete ({_subPhaseIterations} iter)";
                    }
                    break;
                    
                case SubPhase.PlateauEscape:
                    // Short burst (5-8 iter), then back to Refinement
                    if (_subPhaseIterations >= 6)
                    {
                        newSubPhase = SubPhase.Refinement;
                        reason = "escape complete";
                        _plateauCounter = 0;  // Reset plateau counter
                    }
                    break;
                    
                case SubPhase.FineTune:
                    // Can go back to PlateauEscape if stuck again
                    if (_plateauCounter >= 8)
                    {
                        newSubPhase = SubPhase.PlateauEscape;
                        reason = "stuck in fine-tune";
                    }
                    // Stay in FineTune until main phase advances
                    break;
            }
            
            // Apply transition if changed
            if (newSubPhase != _currentSubPhase)
            {
                SubModule.Log($"    ─── SubPhase: {_currentSubPhase} → {newSubPhase} ({reason}) ───");
                _currentSubPhase = newSubPhase;
                _subPhaseIterations = 0;
                _subPhaseStartScore = currentScore;
            }
        }
        
        /// <summary>
        /// Get average score for features active in current phase
        /// </summary>
        private float GetAverageFocusScore()
        {
            if (_currentFeatureScores.Count == 0) return 0;
            
            var config = CurrentConfig;
            var focusFeatures = config.ActiveFeatures.Contains("All") 
                ? new[] { "Face", "Eyes", "Nose", "Mouth", "Jaw", "Brows" }
                : config.ActiveFeatures.ToArray();
            
            var scores = focusFeatures
                .Where(f => _currentFeatureScores.ContainsKey(f))
                .Select(f => _currentFeatureScores[f])
                .ToList();
            
            return scores.Count > 0 ? scores.Average() : 0;
        }
        
        /// <summary>
        /// Check if relevant features for current phase are good enough
        /// </summary>
        private bool ShouldTransitionByFeatureScores()
        {
            if (_currentFeatureScores.Count == 0) return false;
            
            var config = CurrentConfig;
            float threshold = 0.5f;  // Feature score threshold
            
            switch (_currentPhase)
            {
                case HierarchicalPhase.Proportions:
                    // Check Face and Jaw scores
                    float faceScore = GetScoreOrDefault("Face");
                    float jawScore = GetScoreOrDefault("Jaw");
                    return faceScore >= threshold && jawScore >= threshold;
                    
                case HierarchicalPhase.Features:
                    // Check Eyes, Nose, Mouth scores
                    float eyesScore = GetScoreOrDefault("Eyes");
                    float noseScore = GetScoreOrDefault("Nose");
                    float mouthScore = GetScoreOrDefault("Mouth");
                    return eyesScore >= threshold && noseScore >= threshold && mouthScore >= threshold;
                    
                case HierarchicalPhase.Details:
                    // Check Brows + weakest
                    float browsScore = GetScoreOrDefault("Brows");
                    string weakest = GetWeakestFeature();
                    float weakestScore = weakest != null ? GetScoreOrDefault(weakest) : 1f;
                    return browsScore >= threshold && weakestScore >= threshold;
            }
            
            return false;
        }
        
        /// <summary>
        /// Helper to get feature score with default (for .NET Framework compatibility)
        /// </summary>
        private float GetScoreOrDefault(string feature, float defaultValue = 0f)
        {
            if (_currentFeatureScores.TryGetValue(feature, out float score))
                return score;
            return defaultValue;
        }
        
        /// <summary>
        /// Transition to next phase
        /// </summary>
        private bool TransitionToNextPhase(float currentScore, string reason)
        {
            var oldPhase = _currentPhase;
            var oldConfig = CurrentConfig;
            
            // Move to next phase
            int nextIndex = (int)_currentPhase + 1;
            if (nextIndex >= PhaseConfigs.Count)
            {
                // Already at Polish - stay there
                return false;
            }
            
            _currentPhase = (HierarchicalPhase)nextIndex;
            _phaseIterations = 0;
            _phaseStartScore = currentScore;
            
            // Reset SubPhase to Broad for new main phase
            _currentSubPhase = SubPhase.Broad;
            _subPhaseIterations = 0;
            _subPhaseStartScore = currentScore;
            _plateauCounter = 0;
            _recentScores.Clear();
            
            // Update active morphs for new phase
            UpdateActiveMorphs();
            
            var newConfig = CurrentConfig;
            SubModule.Log($"  [HPS] Phase transition: {oldConfig.Name} → {newConfig.Name} ({reason})");
            SubModule.Log($"    Active morphs: {_activeMorphIndices.Count} | σ range: [{newConfig.MinSigma:F2}, {newConfig.MaxSigma:F2}]");
            
            return true;
        }
        
        /// <summary>
        /// Check if a morph index is active in current phase
        /// </summary>
        public bool IsMorphActive(int morphIndex)
        {
            return _activeMorphIndices?.Contains(morphIndex) ?? true;
        }
        
        /// <summary>
        /// Get sigma range for current phase
        /// </summary>
        public (float min, float max) GetSigmaRange()
        {
            var config = CurrentConfig;
            return (config.MinSigma, config.MaxSigma);
        }
        
        /// <summary>
        /// Get recommended sigma based on main phase AND sub-phase
        /// </summary>

        public float GetRecommendedSigma()
        {
            var config = CurrentConfig;
            
            // Base sigma from main phase config
            float baseSigma = config.MaxSigma;
            
            // Modify based on sub-phase
            float sigma = _currentSubPhase switch
            {
                SubPhase.Broad => baseSigma,                           // Full exploration
                SubPhase.Refinement => baseSigma * 0.65f,             // Medium - focus on improvement
                SubPhase.PlateauEscape => baseSigma * 1.3f,           // Boost to escape local minima
                SubPhase.FineTune => config.MinSigma * 1.5f,          // Close to minimum for fine adjustments
                _ => baseSigma
            };
            
            // Clamp to phase range
            sigma = Math.Max(config.MinSigma, Math.Min(config.MaxSigma * 1.3f, sigma));
            
            return sigma;
        }
        
        /// <summary>
        /// Get status string for logging (includes SubPhase)
        /// </summary>
        public string GetStatus()
        {
            var config = CurrentConfig;
            float sigma = GetRecommendedSigma();
            return $"[{config.Name}/{_currentSubPhase}] iter={_phaseIterations}/{config.MaxIterations} active={_activeMorphIndices.Count} σ={sigma:F2}";
        }
        
        /// <summary>
        /// Get detailed status for UI
        /// </summary>
        public string GetDetailedStatus()
        {
            var config = CurrentConfig;
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"Phase: {config.Name}");
            sb.AppendLine($"  {config.Description}");
            sb.AppendLine($"  Iteration: {_phaseIterations}/{config.MaxIterations}");
            sb.AppendLine($"  Active features: {string.Join(", ", config.ActiveFeatures)}");
            sb.AppendLine($"  Active morphs: {_activeMorphIndices.Count}");
            sb.AppendLine($"  Sigma: {config.MinSigma:F2} - {config.MaxSigma:F2}");
            
            return sb.ToString();
        }
    }
}
