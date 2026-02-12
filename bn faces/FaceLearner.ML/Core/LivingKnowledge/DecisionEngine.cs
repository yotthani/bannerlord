using System;
using FaceLearner.Core.LivingKnowledge;

namespace FaceLearner.ML.Core.LivingKnowledge
{
    /// <summary>
    /// Actions the learning system can take
    /// </summary>
    public enum LearningAction
    {
        Mutate,
        RevertAndMutate,
        NextTarget,
        Stop
    }
    
    /// <summary>
    /// Decision made by the DecisionEngine
    /// </summary>
    public struct LearningDecision
    {
        public LearningAction Action;
        public string Reason;
    }
    
    /// <summary>
    /// Makes decisions about learning flow: mutate, revert, next target.
    /// Extracted from LearningOrchestrator for Single Responsibility.
    /// </summary>
    public class DecisionEngine
    {
        // Configuration
        private const int ABSOLUTE_MAX_ITERATIONS = 400;
        private const int MIN_ITERATIONS_FOR_ALL_PHASES = 200;
        private const int MIN_ITERATIONS = 100;
        
        // Thresholds
        private const float EXCELLENT_SCORE = 0.92f;
        private const float VERY_GOOD_SCORE = 0.85f;
        private const float GOOD_SCORE = 0.75f;
        private const float MEDIUM_SCORE = 0.70f;
        private const float LOW_SCORE = 0.60f;
        private const float BAD_SCORE = 0.50f;
        private const float VERY_BAD_SCORE = 0.35f;
        
        // Properties for diagnostics

        public string LastDecisionReason { get; private set; }
        
        /// <summary>
        /// Decide what to do next based on current state
        /// </summary>
        public LearningDecision Decide(
            float currentScore,
            float bestScore,
            int iterationsOnTarget,
            int consecutiveNonImprovements,
            int newPeakCount,
            bool isRapidProgress,
            HierarchicalPhaseSystem.HierarchicalPhase currentPhase,
            HierarchicalPhaseSystem.SubPhase currentSubPhase)
        {
            // === Check for target completion ===
            var targetDecision = CheckTargetCompletion(
                bestScore, 
                iterationsOnTarget, 
                consecutiveNonImprovements, 
                newPeakCount,
                isRapidProgress);
            
            if (targetDecision.HasValue)
            {
                return targetDecision.Value;
            }
            
            // === Check for revert ===
            float revertThreshold = GetRevertThreshold(currentPhase, currentSubPhase);
            bool shouldRevert = currentScore < bestScore - revertThreshold && 
                                consecutiveNonImprovements > 3 &&
                                iterationsOnTarget > 10;
            
            if (shouldRevert)
            {
                LastDecisionReason = $"Score dropped {bestScore - currentScore:F3} below best";
                return new LearningDecision
                {
                    Action = LearningAction.RevertAndMutate,
                    Reason = LastDecisionReason
                };
            }
            
            // === Default: Mutate ===
            LastDecisionReason = "Continue learning";
            return new LearningDecision
            {
                Action = LearningAction.Mutate,
                Reason = LastDecisionReason
            };
        }
        
        /// <summary>
        /// Get revert threshold based on current phase
        /// </summary>

        private float GetRevertThreshold(
            HierarchicalPhaseSystem.HierarchicalPhase phase,
            HierarchicalPhaseSystem.SubPhase subPhase)
        {
            // Allow MORE exploration during Coarse and Broad phases
            if (phase == HierarchicalPhaseSystem.HierarchicalPhase.Coarse)
                return 0.10f;  // 10% tolerance in Coarse
            
            if (subPhase == HierarchicalPhaseSystem.SubPhase.Broad)
                return 0.06f;  // 6% tolerance in Broad
            
            if (subPhase == HierarchicalPhaseSystem.SubPhase.PlateauEscape)
                return 0.08f;  // 8% tolerance during escape
            
            return 0.03f;  // Default: 3% tolerance
        }
        
        /// <summary>
        /// Check if we should move to next target
        /// </summary>
        private LearningDecision? CheckTargetCompletion(
            float bestScore,
            int iterationsOnTarget,
            int consecutiveNonImprovements,
            int newPeakCount,
            bool isRapidProgress)
        {
            // Absolute maximum reached
            if (iterationsOnTarget >= ABSOLUTE_MAX_ITERATIONS)
            {
                LastDecisionReason = "Absolute max iterations reached";
                return CreateNextTargetDecision();
            }
            
            // Excellent score achieved (after minimum iterations)
            if (bestScore > EXCELLENT_SCORE && iterationsOnTarget > MIN_ITERATIONS)
            {
                LastDecisionReason = $"Excellent score {bestScore:F3} achieved";
                return CreateNextTargetDecision();
            }
            
            // Very good score and all phases had time
            if (bestScore > VERY_GOOD_SCORE && iterationsOnTarget > 150)
            {
                LastDecisionReason = $"Very good score {bestScore:F3} after 150 iterations";
                return CreateNextTargetDecision();
            }
            
            // Good score and all phases complete
            if (bestScore > GOOD_SCORE && iterationsOnTarget > MIN_ITERATIONS_FOR_ALL_PHASES)
            {
                LastDecisionReason = $"Good score {bestScore:F3} after all phases";
                return CreateNextTargetDecision();
            }
            
            // Early success: high score and stable
            if (bestScore > 0.82f && consecutiveNonImprovements > 30 && iterationsOnTarget > 120)
            {
                LastDecisionReason = $"Early success: {bestScore:F3} stable for {consecutiveNonImprovements}";
                return CreateNextTargetDecision();
            }
            
            // No peaks = hopeless
            if (newPeakCount == 0 && iterationsOnTarget > 80)
            {
                LastDecisionReason = $"No peaks after {iterationsOnTarget} iterations";
                return CreateNextTargetDecision();
            }
            
            // Only 1 peak and stuck
            if (newPeakCount == 1 && iterationsOnTarget > 100 && consecutiveNonImprovements > 40)
            {
                LastDecisionReason = $"Only 1 peak, stuck for {consecutiveNonImprovements}";
                return CreateNextTargetDecision();
            }
            
            // Minimum iterations not reached - continue
            if (iterationsOnTarget < MIN_ITERATIONS)
            {
                return null;
            }
            
            // Making progress - continue
            if (isRapidProgress && iterationsOnTarget < 180)
            {
                return null;
            }
            
            // Long stagnation
            if (consecutiveNonImprovements > 60)
            {
                LastDecisionReason = $"Stagnant for {consecutiveNonImprovements} iterations";
                return CreateNextTargetDecision();
            }
            
            // Score-based max iterations
            if (bestScore < VERY_BAD_SCORE && iterationsOnTarget > 100)
            {
                LastDecisionReason = $"Very bad score {bestScore:F3} - aborting";
                return CreateNextTargetDecision();
            }
            
            if (bestScore < BAD_SCORE && iterationsOnTarget > 150)
            {
                LastDecisionReason = $"Bad score {bestScore:F3} after 150 iterations";
                return CreateNextTargetDecision();
            }
            
            if (bestScore < LOW_SCORE && iterationsOnTarget > 180)
            {
                LastDecisionReason = $"Low score {bestScore:F3} after 180 iterations";
                return CreateNextTargetDecision();
            }
            
            if (bestScore < MEDIUM_SCORE && iterationsOnTarget > 220)
            {
                LastDecisionReason = $"Medium score {bestScore:F3} after 220 iterations";
                return CreateNextTargetDecision();
            }
            
            // Continue learning
            return null;
        }
        
        private LearningDecision CreateNextTargetDecision()
        {
            return new LearningDecision
            {
                Action = LearningAction.NextTarget,
                Reason = LastDecisionReason
            };
        }
    }
}
