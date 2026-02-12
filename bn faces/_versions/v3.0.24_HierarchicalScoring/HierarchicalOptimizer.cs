using System;
using System.Collections.Generic;
using System.Linq;
using FaceLearner.Core;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Hierarchical morph optimizer.
    /// 
    /// Key differences from old system:
    /// 1. Only optimizes 5-8 morphs at a time (focused)
    /// 2. Uses sub-phase-specific scoring
    /// 3. Locks morphs after optimization (prevents regression)
    /// 4. Clear phase transitions with different strategies
    /// </summary>
    public class HierarchicalOptimizer
    {
        private readonly HierarchicalPhaseController _phaseController;
        private readonly HierarchicalScorer _scorer;
        private readonly MorphLockManager _lockManager;
        private readonly FeatureExtractor _featureExtractor;
        private readonly Random _random = new Random();
        
        // Current state
        private float[] _currentMorphs;
        private float[] _bestMorphs;
        private float _bestScore;
        
        // Target
        private FeatureSet _targetFeatures;
        private float[] _targetLandmarks;
        
        // Statistics
        public int TotalIterations { get; private set; }
        public int TargetsProcessed { get; private set; }
        
        public HierarchicalOptimizer()
        {
            _lockManager = new MorphLockManager();
            _phaseController = new HierarchicalPhaseController { LockManager = _lockManager };
            _scorer = new HierarchicalScorer();
            _featureExtractor = new FeatureExtractor();
            
            // Subscribe to phase events
            _phaseController.OnPhaseChanged += OnPhaseChanged;
            _phaseController.OnSubPhaseComplete += OnSubPhaseComplete;
            _phaseController.OnMainPhaseComplete += OnMainPhaseComplete;
        }
        
        /// <summary>Set race for generation (null for learning)</summary>
        public void SetRace(string raceId)
        {
            _scorer.SetRace(raceId);
        }
        
        /// <summary>Initialize with starting morphs</summary>
        public void Initialize(float[] startingMorphs)
        {
            _currentMorphs = (float[])startingMorphs.Clone();
            _bestMorphs = (float[])startingMorphs.Clone();
            _bestScore = 0f;
            TotalIterations = 0;
            
            _phaseController.Reset();
        }
        
        /// <summary>Set target from landmarks</summary>
        public void SetTarget(float[] targetLandmarks)
        {
            _targetLandmarks = targetLandmarks;
            _targetFeatures = _featureExtractor.Extract(targetLandmarks);
            _bestScore = 0f;
            TargetsProcessed++;
            
            SubModule.Log($"[HierOpt] New target set, features extracted");
        }
        
        /// <summary>
        /// Run one optimization iteration.
        /// Returns the score achieved.
        /// </summary>
        public OptimizationResult Iterate(float[] renderedLandmarks)
        {
            if (_targetFeatures == null || renderedLandmarks == null)
            {
                return new OptimizationResult { Action = PhaseAction.Abort };
            }
            
            TotalIterations++;
            
            // Extract current features
            var currentFeatures = _featureExtractor.Extract(renderedLandmarks);
            
            // Get sub-phase-specific score
            float subPhaseScore = _scorer.CalculateSubPhaseScore(
                _phaseController.CurrentSubPhase, 
                _targetFeatures, 
                currentFeatures);
            
            // Also get hierarchical total for logging
            var hierarchicalScore = _scorer.Calculate(_targetFeatures, currentFeatures);
            
            // Report to phase controller
            var action = _phaseController.ReportScore(subPhaseScore, _currentMorphs);
            
            // Track best
            if (hierarchicalScore.Total > _bestScore)
            {
                _bestScore = hierarchicalScore.Total;
                _bestMorphs = (float[])_currentMorphs.Clone();
            }
            
            // Generate next morphs if continuing
            float[] nextMorphs = null;
            if (action == PhaseAction.Continue)
            {
                nextMorphs = GenerateNextMorphs(subPhaseScore);
            }
            
            return new OptimizationResult
            {
                Action = action,
                SubPhaseScore = subPhaseScore,
                TotalScore = hierarchicalScore.Total,
                HierarchicalScore = hierarchicalScore,
                NextMorphs = nextMorphs,
                CurrentPhase = _phaseController.GetStatus()
            };
        }
        
        /// <summary>Generate next morph values to try</summary>
        private float[] GenerateNextMorphs(float currentScore)
        {
            var settings = _phaseController.GetCurrentSettings();
            var activeMorphs = _phaseController.GetActiveMorphs();
            
            var nextMorphs = (float[])_currentMorphs.Clone();
            
            // Only vary active morphs for current sub-phase
            foreach (int morphIdx in activeMorphs)
            {
                if (morphIdx >= nextMorphs.Length) continue;
                
                // Get allowed range (may be limited by locks)
                var (lockMin, lockMax) = _lockManager.GetAllowedRange(morphIdx);
                
                // Get official Bannerlord range for this morph
                var (blMin, blMax, blRange) = MorphGroups.GetMorphRange(morphIdx);
                
                // Generate RANGE-PROPORTIONAL variation
                // Sigma is now a percentage of the morph's natural range
                // e.g., sigma=0.8 means ±80% of the morph's range
                float variation = (float)(_random.NextDouble() * 2 - 1) * settings.Sigma * blRange;
                float newValue = _currentMorphs[morphIdx] + variation;
                
                // Clamp to lock range first, then to Bannerlord range
                newValue = Math.Max(lockMin, Math.Min(lockMax, newValue));
                newValue = Math.Max(blMin - 0.5f, Math.Min(blMax + 0.5f, newValue));  // Allow slight overshoot
                
                nextMorphs[morphIdx] = newValue;
            }
            
            // Update current morphs
            _currentMorphs = nextMorphs;
            
            return nextMorphs;
        }
        
        /// <summary>Get current best morphs</summary>
        public float[] GetBestMorphs() => (float[])_bestMorphs.Clone();
        
        /// <summary>Get current morphs</summary>
        public float[] GetCurrentMorphs() => (float[])_currentMorphs.Clone();
        
        /// <summary>Get best score achieved</summary>
        public float GetBestScore() => _bestScore;
        
        /// <summary>Get phase status</summary>
        public string GetStatus() => _phaseController.GetStatus();
        
        /// <summary>Get detailed status</summary>
        public string GetDetailedStatus() => _phaseController.GetDetailedStatus();
        
        /// <summary>Get hierarchical score breakdown</summary>
        public HierarchicalScore GetScoreBreakdown(float[] renderedLandmarks)
        {
            var currentFeatures = _featureExtractor.Extract(renderedLandmarks);
            return _scorer.Calculate(_targetFeatures, currentFeatures);
        }
        
        // Event handlers
        private void OnPhaseChanged(MainPhase main, SubPhase sub, OptPhase opt)
        {
            SubModule.Log($"[HierOpt] Phase: {main}/{sub}/{opt}");
        }
        
        private void OnSubPhaseComplete(SubPhase sub, float score, float[] bestMorphs)
        {
            SubModule.Log($"[HierOpt] SubPhase {sub} complete with score {score:F3}");
            
            // IMPORTANT: Reset current morphs to the best from this sub-phase!
            // This ensures the next phase starts from the best position, not the last iteration.
            if (bestMorphs != null)
            {
                _currentMorphs = (float[])bestMorphs.Clone();
                SubModule.Log($"[HierOpt] Reset currentMorphs to subPhase best");
            }
        }
        
        private void OnMainPhaseComplete(MainPhase main, float score)
        {
            SubModule.Log($"[HierOpt] MainPhase {main} complete with score {score:F3}");
        }
    }
    
    /// <summary>Result of one optimization iteration</summary>
    public class OptimizationResult
    {
        /// <summary>What to do next</summary>
        public PhaseAction Action { get; set; }
        
        /// <summary>Score for current sub-phase</summary>
        public float SubPhaseScore { get; set; }
        
        /// <summary>Total hierarchical score</summary>
        public float TotalScore { get; set; }
        
        /// <summary>Full score breakdown</summary>
        public HierarchicalScore HierarchicalScore { get; set; }
        
        /// <summary>Next morphs to render (null if complete)</summary>
        public float[] NextMorphs { get; set; }
        
        /// <summary>Current phase description</summary>
        public string CurrentPhase { get; set; }
    }
}
