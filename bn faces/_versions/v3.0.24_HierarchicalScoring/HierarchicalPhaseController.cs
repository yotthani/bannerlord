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
        public float LockInThreshold { get; set; } = 0.80f;      // Score needed for lock-in
        public float MinAcceptableScore { get; set; } = 0.50f;   // Minimum before moving on
        public float PlateauThreshold { get; set; } = 0.01f;     // Score change considered "no improvement"
        
        public HierarchicalPhaseController()
        {
            // Sigma values are now PERCENTAGES of the morph's natural range!
            // e.g., Sigma=0.5 means variation of ±50% of the morph's range
            _optSettings = new Dictionary<OptPhase, OptPhaseSettings>
            {
                { OptPhase.Exploration, new OptPhaseSettings {
                    Sigma = 0.50f,  // ±50% of range - broad search
                    MaxIterations = 15,
                    TargetScore = 0.50f,
                    Description = "Broad search, find direction"
                }},
                { OptPhase.Refinement, new OptPhaseSettings {
                    Sigma = 0.20f,  // ±20% of range - fine-tune
                    MaxIterations = 30,
                    TargetScore = 0.75f,
                    Description = "Fine-tune found direction"
                }},
                { OptPhase.PlateauEscape, new OptPhaseSettings {
                    Sigma = 0.80f,  // ±80% of range - aggressive jumps!
                    MaxIterations = 10,
                    TargetScore = 0.60f,
                    Description = "Large jumps to escape local minimum"
                }},
                { OptPhase.LockIn, new OptPhaseSettings {
                    Sigma = 0.08f,  // ±8% of range - final polish
                    MaxIterations = 5,
                    TargetScore = 0.85f,
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
            int plateauThreshold = CurrentOptPhase == OptPhase.Exploration ? 8 : 15;
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
