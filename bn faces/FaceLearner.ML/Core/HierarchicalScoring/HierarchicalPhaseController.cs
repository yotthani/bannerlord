using System;
using System.Collections.Generic;
using System.Linq;
using FaceLearner.Core;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Controls the 3-level hierarchical phase system.
    /// 
    /// Level 1 (Main Phase): Foundation → Structure → MajorFeatures → FineDetails
    /// Level 2 (Sub Phase):  e.g., Foundation has FaceWidth, FaceHeight, FaceShape
    /// Level 3 (Opt Phase):  Exploration → Refinement → (PlateauEscape) → LockIn
    /// 
    /// Key principle: Small, focused optimization problems.
    /// Instead of optimizing 70 morphs at once, we optimize 5-8 at a time.
    /// </summary>
    public class HierarchicalPhaseController
    {
        // Current position in hierarchy
        public MainPhase CurrentMainPhase { get; private set; } = MainPhase.Foundation;
        public SubPhase CurrentSubPhase { get; private set; } = SubPhase.FaceWidth;
        public OptPhase CurrentOptPhase { get; private set; } = OptPhase.Exploration;
        
        // Phase-specific settings
        private readonly Dictionary<OptPhase, OptPhaseSettings> _optSettings;
        
        // State tracking
        private int _iterationsInOptPhase;
        private int _iterationsWithoutImprovement;
        private float _bestScoreInPhase;
        private float _bestScoreInSubPhase;
        private float[] _bestMorphsInSubPhase;
        
        // Callbacks
        public event Action<MainPhase, SubPhase, OptPhase> OnPhaseChanged;
        public event Action<SubPhase, float, float[]> OnSubPhaseComplete;  // Added bestMorphs!
        public event Action<MainPhase, float> OnMainPhaseComplete;
        
        // Lock manager reference (set externally)
        public MorphLockManager LockManager { get; set; }
        
        // Score thresholds
        // v3.0.36: Raised LockIn from 0.80 to 0.90. With wider expected ranges (v3.0.36),
        // SubPhase scores inflate — features that used to score 0.75 now score 0.90+.
        // The old 0.80 threshold caused premature lock-in after just 47 iterations instead of 150+.
        // 0.90 ensures the optimizer keeps refining even when ranges are forgiving.
        public float LockInThreshold { get; set; } = 0.90f;
        public float MinAcceptableScore { get; set; } = 0.50f;   // Minimum before moving on
        public float PlateauThreshold { get; set; } = 0.01f;     // Score change considered "no improvement"
        
        public HierarchicalPhaseController()
        {
            // v3.0.29: Iteration budgets raised for CMA-ES population-based optimization.
            // CMA-ES evaluates a population of 6-10 candidates per generation, so each
            // "generation" needs 6-10 iterations. Old budgets (15/30) gave only 2-5 generations
            // which isn't enough for the covariance matrix to learn meaningful correlations.
            // New budgets give 4-8 generations per phase, enough for CMA-ES to converge.
            //
            // Sigma values are initial step sizes (fraction of morph range).
            // CMA-ES adapts sigma automatically, so these are just starting points.
            _optSettings = new Dictionary<OptPhase, OptPhaseSettings>
            {
                { OptPhase.Exploration, new OptPhaseSettings {
                    Sigma = 0.40f,  // ±40% of range - broad search
                    MaxIterations = 40,   // ~5-6 CMA-ES generations
                    // v3.0.36: Raised from 0.50 to 0.65. With wider ranges, 0.50 is trivial.
                    TargetScore = 0.65f,
                    Description = "Broad search, find direction"
                }},
                { OptPhase.Refinement, new OptPhaseSettings {
                    Sigma = 0.15f,  // ±15% of range - fine-tune
                    MaxIterations = 50,   // ~6-8 CMA-ES generations
                    // v3.0.36: Raised from 0.75 to 0.85. With wider ranges, 0.75 is reached
                    // almost immediately → premature lock → only 47 iterations total.
                    // 0.85 forces the optimizer to keep refining fine details.
                    TargetScore = 0.85f,
                    Description = "Fine-tune found direction"
                }},
                { OptPhase.PlateauEscape, new OptPhaseSettings {
                    Sigma = 0.70f,  // ±70% of range - aggressive jumps!
                    MaxIterations = 24,   // ~3-4 CMA-ES generations
                    TargetScore = 0.60f,
                    Description = "Large jumps to escape local minimum"
                }},
                { OptPhase.LockIn, new OptPhaseSettings {
                    Sigma = 0.06f,  // ±6% of range - final polish
                    MaxIterations = 12,   // ~2 CMA-ES generations
                    // v3.0.36: Raised from 0.85 to 0.93 to match inflated scores from wider ranges.
                    TargetScore = 0.93f,
                    Description = "Final polish before locking"
                }}
            };
            
            Reset();
        }
        
        /// <summary>Reset to beginning</summary>
        public void Reset()
        {
            CurrentMainPhase = MainPhase.Foundation;
            CurrentSubPhase = SubPhase.FaceWidth;
            CurrentOptPhase = OptPhase.Exploration;
            
            _iterationsInOptPhase = 0;
            _iterationsWithoutImprovement = 0;
            _bestScoreInPhase = 0f;
            _bestScoreInSubPhase = 0f;
            _bestMorphsInSubPhase = null;
            
            LockManager?.ClearAll();
        }
        
        /// <summary>Get current settings for optimization</summary>
        public OptPhaseSettings GetCurrentSettings()
        {
            return _optSettings[CurrentOptPhase];
        }
        
        /// <summary>Get morphs that should be active for current sub-phase</summary>
        public int[] GetActiveMorphs()
        {
            return MorphGroups.GetMorphsForPhase(CurrentSubPhase);
        }
        
        /// <summary>
        /// Report a score from the optimizer.
        /// Returns true if we should continue, false if phase is complete.
        /// </summary>
        public PhaseAction ReportScore(float score, float[] currentMorphs)
        {
            _iterationsInOptPhase++;
            
            // Track best score in sub-phase
            if (score > _bestScoreInSubPhase)
            {
                _bestScoreInSubPhase = score;
                _bestMorphsInSubPhase = (float[])currentMorphs.Clone();
                _iterationsWithoutImprovement = 0;
            }
            else if (score < _bestScoreInSubPhase - PlateauThreshold)
            {
                _iterationsWithoutImprovement++;
            }
            else
            {
                _iterationsWithoutImprovement++;
            }
            
            // Track best overall
            if (score > _bestScoreInPhase)
            {
                _bestScoreInPhase = score;
            }
            
            var settings = GetCurrentSettings();
            
            // Check transitions
            return EvaluateTransition(score, settings);
        }
        
        /// <summary>
        /// Evaluate if we should transition to next phase
        /// </summary>
        private PhaseAction EvaluateTransition(float score, OptPhaseSettings settings)
        {
            // === CHECK FOR LOCK-IN ===
            if (score >= LockInThreshold && CurrentOptPhase != OptPhase.LockIn)
            {
                // Score is good enough - transition to lock-in
                TransitionToOptPhase(OptPhase.LockIn);
                return PhaseAction.Continue;
            }
            
            if (CurrentOptPhase == OptPhase.LockIn)
            {
                if (_iterationsInOptPhase >= settings.MaxIterations || score >= settings.TargetScore)
                {
                    // Lock-in complete - lock morphs and move to next sub-phase
                    LockCurrentSubPhase();
                    return TransitionToNextSubPhase();
                }
                return PhaseAction.Continue;
            }
            
            // === CHECK FOR PLATEAU ===
            // v3.0.29: Raised plateau thresholds for CMA-ES. Within a CMA-ES generation,
            // individual candidates may score worse than the best, which shouldn't count as "no improvement".
            // We need at least 2 full generations without improvement before declaring plateau.
            int plateauThreshold = CurrentOptPhase == OptPhase.Exploration ? 18 : 25;
            if (_iterationsWithoutImprovement >= plateauThreshold)
            {
                if (CurrentOptPhase == OptPhase.PlateauEscape)
                {
                    // Already tried escape, accept current score and move on
                    if (_bestScoreInSubPhase >= MinAcceptableScore)
                    {
                        LockCurrentSubPhase();
                        return TransitionToNextSubPhase();
                    }
                    else
                    {
                        // Score too low even after escape - skip with penalty
                        return TransitionToNextSubPhase();
                    }
                }
                else
                {
                    // Try plateau escape
                    TransitionToOptPhase(OptPhase.PlateauEscape);
                    return PhaseAction.Continue;
                }
            }
            
            // === CHECK FOR TARGET REACHED ===
            if (score >= settings.TargetScore)
            {
                if (CurrentOptPhase == OptPhase.Exploration)
                {
                    TransitionToOptPhase(OptPhase.Refinement);
                    return PhaseAction.Continue;
                }
                else if (CurrentOptPhase == OptPhase.Refinement)
                {
                    if (score >= LockInThreshold)
                    {
                        TransitionToOptPhase(OptPhase.LockIn);
                    }
                    else
                    {
                        // Good enough, lock and move on
                        LockCurrentSubPhase();
                        return TransitionToNextSubPhase();
                    }
                    return PhaseAction.Continue;
                }
            }
            
            // === CHECK FOR MAX ITERATIONS ===
            if (_iterationsInOptPhase >= settings.MaxIterations)
            {
                if (CurrentOptPhase == OptPhase.Exploration)
                {
                    // Move to refinement even if target not reached
                    TransitionToOptPhase(OptPhase.Refinement);
                    return PhaseAction.Continue;
                }
                else if (CurrentOptPhase == OptPhase.Refinement)
                {
                    if (_bestScoreInSubPhase >= MinAcceptableScore)
                    {
                        LockCurrentSubPhase();
                        return TransitionToNextSubPhase();
                    }
                    else
                    {
                        // Not good enough - try plateau escape
                        TransitionToOptPhase(OptPhase.PlateauEscape);
                        return PhaseAction.Continue;
                    }
                }
            }
            
            return PhaseAction.Continue;
        }
        
        /// <summary>Transition to a new opt phase</summary>
        private void TransitionToOptPhase(OptPhase newPhase)
        {
            if (newPhase == CurrentOptPhase) return;
            
            SubModule.Log($"  [Phase] {CurrentSubPhase}/{CurrentOptPhase} → {CurrentSubPhase}/{newPhase}");
            CurrentOptPhase = newPhase;
            _iterationsInOptPhase = 0;
            _iterationsWithoutImprovement = 0;
            
            OnPhaseChanged?.Invoke(CurrentMainPhase, CurrentSubPhase, CurrentOptPhase);
        }
        
        /// <summary>Lock morphs for current sub-phase</summary>
        private void LockCurrentSubPhase()
        {
            if (LockManager != null && _bestMorphsInSubPhase != null)
            {
                // Tighter lock for good scores, looser for mediocre
                float variation = _bestScoreInSubPhase >= 0.80f ? 0.08f : 0.12f;
                LockManager.LockPhase(CurrentSubPhase, _bestMorphsInSubPhase, variation);
                
                SubModule.Log($"  [Lock] {CurrentSubPhase} locked at score {_bestScoreInSubPhase:F3} (±{variation:F2})");
            }
            
            OnSubPhaseComplete?.Invoke(CurrentSubPhase, _bestScoreInSubPhase, _bestMorphsInSubPhase);
        }
        
        /// <summary>Transition to next sub-phase or main phase</summary>
        private PhaseAction TransitionToNextSubPhase()
        {
            var subPhases = MorphGroups.GetSubPhases(CurrentMainPhase);
            int currentIndex = Array.IndexOf(subPhases, CurrentSubPhase);
            
            if (currentIndex < subPhases.Length - 1)
            {
                // Next sub-phase within same main phase
                CurrentSubPhase = subPhases[currentIndex + 1];
                CurrentOptPhase = OptPhase.Exploration;
                _iterationsInOptPhase = 0;
                _iterationsWithoutImprovement = 0;
                _bestScoreInSubPhase = 0f;
                _bestMorphsInSubPhase = null;
                
                SubModule.Log($"  [Phase] → {CurrentMainPhase}/{CurrentSubPhase}");
                OnPhaseChanged?.Invoke(CurrentMainPhase, CurrentSubPhase, CurrentOptPhase);
                return PhaseAction.Continue;
            }
            else
            {
                // Main phase complete - move to next
                return TransitionToNextMainPhase();
            }
        }
        
        /// <summary>Transition to next main phase</summary>
        private PhaseAction TransitionToNextMainPhase()
        {
            OnMainPhaseComplete?.Invoke(CurrentMainPhase, _bestScoreInPhase);
            
            var mainPhases = (MainPhase[])Enum.GetValues(typeof(MainPhase));
            int currentIndex = Array.IndexOf(mainPhases, CurrentMainPhase);
            
            if (currentIndex < mainPhases.Length - 1)
            {
                // Next main phase
                CurrentMainPhase = mainPhases[currentIndex + 1];
                var subPhases = MorphGroups.GetSubPhases(CurrentMainPhase);
                CurrentSubPhase = subPhases[0];
                CurrentOptPhase = OptPhase.Exploration;
                
                _iterationsInOptPhase = 0;
                _iterationsWithoutImprovement = 0;
                _bestScoreInPhase = 0f;
                _bestScoreInSubPhase = 0f;
                _bestMorphsInSubPhase = null;
                
                SubModule.Log($"  [Phase] === {CurrentMainPhase} ===");
                OnPhaseChanged?.Invoke(CurrentMainPhase, CurrentSubPhase, CurrentOptPhase);
                return PhaseAction.Continue;
            }
            else
            {
                // All phases complete!
                SubModule.Log($"  [Phase] ALL COMPLETE");
                return PhaseAction.Complete;
            }
        }
        
        /// <summary>Get status string</summary>
        public string GetStatus()
        {
            return $"{CurrentMainPhase}/{CurrentSubPhase}/{CurrentOptPhase} " +
                   $"(iter={_iterationsInOptPhase}, best={_bestScoreInSubPhase:F3})";
        }
        
        /// <summary>Get detailed status</summary>
        public string GetDetailedStatus()
        {
            var settings = GetCurrentSettings();
            return $"Phase: {CurrentMainPhase}/{CurrentSubPhase}/{CurrentOptPhase}\n" +
                   $"  Iterations: {_iterationsInOptPhase}/{settings.MaxIterations}\n" +
                   $"  No improvement for: {_iterationsWithoutImprovement}\n" +
                   $"  Best in sub-phase: {_bestScoreInSubPhase:F3}\n" +
                   $"  Best overall: {_bestScoreInPhase:F3}\n" +
                   $"  Active morphs: {string.Join(",", GetActiveMorphs())}\n" +
                   $"  {LockManager?.GetStatus() ?? "No lock manager"}";
        }
    }
    
    /// <summary>Settings for an optimization phase</summary>
    public class OptPhaseSettings
    {
        /// <summary>Sigma for variation (how much to change morphs)</summary>
        public float Sigma { get; set; }
        
        /// <summary>Maximum iterations before moving on</summary>
        public int MaxIterations { get; set; }
        
        /// <summary>Target score to aim for</summary>
        public float TargetScore { get; set; }
        
        /// <summary>Human-readable description</summary>
        public string Description { get; set; }
    }
    
    /// <summary>Action to take after reporting a score</summary>
    public enum PhaseAction
    {
        /// <summary>Continue optimizing</summary>
        Continue,
        
        /// <summary>All phases complete</summary>
        Complete,
        
        /// <summary>Abort (error or user cancel)</summary>
        Abort
    }
}
