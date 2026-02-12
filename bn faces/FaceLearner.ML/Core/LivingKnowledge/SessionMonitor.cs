using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Real-time session monitor that actively watches progress and intervenes
    /// OPTIMIZED: Analysis only runs every ANALYSIS_INTERVAL iterations
    /// </summary>
    public class SessionMonitor
    {
        // OPTIMIZATION: Only analyze trends every N iterations
        private const int ANALYSIS_INTERVAL = 10;

        private int _iterationsSinceAnalysis = 0;
        
        // Score history for trend analysis
        private Queue<float> _shortTermScores = new Queue<float>();   // Last 10
        private Queue<float> _mediumTermScores = new Queue<float>();  // Last 50
        private Queue<float> _longTermScores = new Queue<float>();    // Last 200
        
        // OPTIMIZATION: Running stats to avoid recalculation

        private float _shortTermSum = 0;
        private float _mediumTermSum = 0;
        private float _longTermSum = 0;
        
        // Timing - simplified
        private DateTime _lastTimingCheck;

        private int _iterationsSinceTimingCheck;
        private float _averageIterationTime;
        
        // Alerts and interventions
        public event Action<string, InterventionType> OnIntervention;
        
        // Current analysis

        public float ShortTermTrend { get; private set; }   // Positive = improving
        public float MediumTermTrend { get; private set; }
        public float LongTermTrend { get; private set; }
        public float Volatility { get; private set; }        // How much score jumps around
        public bool IsStagnant { get; private set; }
        public bool IsRegressing { get; private set; }
        public bool IsRapidProgress { get; private set; }
        public float ProgressRate { get; private set; }      // Score gain per iteration
        
        // Session stats
        public int TotalIterations { get; private set; }
        public float SessionBestScore { get; private set; }
        public float SessionStartScore { get; private set; }
        public int InterventionCount { get; private set; }
        public DateTime SessionStart { get; private set; }
        
        // Thresholds (these adapt over time)

        private float _stagnationThreshold = 0.0005f;  // Lower threshold - don't cry wolf
        private float _regressionThreshold = -0.01f;
        private float _rapidProgressThreshold = 0.02f;
        private int _stagnationWindow = 100;  // Need more data before declaring stagnant
        
        public SessionMonitor()
        {
            SessionStart = DateTime.Now;
            _lastTimingCheck = DateTime.Now;
        }
        
        /// <summary>
        /// Record a new score observation - OPTIMIZED
        /// </summary>

        public void RecordScore(float score)
        {
            TotalIterations++;
            _iterationsSinceAnalysis++;
            _iterationsSinceTimingCheck++;
            
            // OPTIMIZED: Only check timing every 50 iterations
            if (_iterationsSinceTimingCheck >= 50)
            {
                var now = DateTime.Now;
                var elapsed = (now - _lastTimingCheck).TotalMilliseconds;
                _averageIterationTime = (float)(elapsed / _iterationsSinceTimingCheck);
                _lastTimingCheck = now;
                _iterationsSinceTimingCheck = 0;
            }
            
            // Record first score
            if (TotalIterations == 1)
                SessionStartScore = score;
            
            // Update best
            if (score > SessionBestScore)
                SessionBestScore = score;
            
            // Add to history queues with running sums
            _shortTermScores.Enqueue(score);
            _shortTermSum += score;
            if (_shortTermScores.Count > 10) 
                _shortTermSum -= _shortTermScores.Dequeue();
            
            _mediumTermScores.Enqueue(score);
            _mediumTermSum += score;
            if (_mediumTermScores.Count > 50) 
                _mediumTermSum -= _mediumTermScores.Dequeue();
            
            _longTermScores.Enqueue(score);
            _longTermSum += score;
            if (_longTermScores.Count > 200) 
                _longTermSum -= _longTermScores.Dequeue();
            
            // OPTIMIZED: Only analyze every N iterations
            if (_iterationsSinceAnalysis >= ANALYSIS_INTERVAL)
            {
                _iterationsSinceAnalysis = 0;
                AnalyzeTrends();
                CheckInterventions();
            }
        }
        
        private void AnalyzeTrends()
        {
            // OPTIMIZED: Use running sums where possible, minimize ToArray calls
            
            // Calculate trends only if we have enough data
            if (_shortTermScores.Count >= 5)
                ShortTermTrend = CalculateTrendOptimized(_shortTermScores);
            
            if (_mediumTermScores.Count >= 20)
                MediumTermTrend = CalculateTrendOptimized(_mediumTermScores);
            
            if (_longTermScores.Count >= 50)
                LongTermTrend = CalculateTrendOptimized(_longTermScores);
            
            // Calculate volatility using running mean
            if (_mediumTermScores.Count >= 10)
            {
                float mean = _mediumTermSum / _mediumTermScores.Count;
                float sumSquares = 0;
                foreach (var s in _mediumTermScores)
                    sumSquares += (s - mean) * (s - mean);
                Volatility = (float)Math.Sqrt(sumSquares / _mediumTermScores.Count);
            }
            
            // Progress rate - simple estimate
            if (_longTermScores.Count >= 10)
            {
                // Use first and last without ToArray
                float first = 0, last = 0;
                int i = 0;
                foreach (var s in _longTermScores)
                {
                    if (i == 0) first = s;
                    last = s;
                    i++;
                }
                ProgressRate = (last - first) / _longTermScores.Count;
            }
            
            // Status flags
            IsStagnant = _mediumTermScores.Count >= _stagnationWindow && 
                        Math.Abs(MediumTermTrend) < _stagnationThreshold;
            
            IsRegressing = ShortTermTrend < _regressionThreshold && 
                          MediumTermTrend < _regressionThreshold * 0.5f;
            
            IsRapidProgress = ShortTermTrend > _rapidProgressThreshold;
        }
        
        /// <summary>
        /// Calculate trend without array allocation
        /// </summary>
        private float CalculateTrendOptimized(Queue<float> scores)
        {
            if (scores.Count < 2) return 0;
            
            // Simple linear regression slope
            int n = scores.Count;
            float sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int x = 0;
            
            foreach (var y in scores)
            {
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
                x++;
            }
            
            float denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 0.0001f) return 0;
            
            return (n * sumXY - sumX * sumY) / denom;
        }
        
        private int _lastInterventionIter = 0;
        private const int INTERVENTION_COOLDOWN = 50;  // Minimum iterations between interventions
        

        private void CheckInterventions()
        {
            // Only check after enough data
            if (TotalIterations < 50) return;
            
            // Cooldown - don't spam interventions
            if (TotalIterations - _lastInterventionIter < INTERVENTION_COOLDOWN) return;
            
            // Severe stagnation - stuck for a long time with tiny range
            if (IsStagnant && _mediumTermScores.Count >= 80)
            {
                // OPTIMIZED: Single pass min/max
                float min = float.MaxValue, max = float.MinValue;
                foreach (var s in _mediumTermScores)
                {
                    if (s < min) min = s;
                    if (s > max) max = s;
                }
                if ((max - min) < 0.005f)
                {
                    TriggerIntervention("Severe stagnation detected - need radical change", 
                        InterventionType.ForcePhaseChange);
                    return;  // Only one intervention per check
                }
            }
            
            // Regression - getting significantly worse
            if (IsRegressing && ShortTermTrend < -0.02f)
            {
                TriggerIntervention("Score regression detected - reverting strategy",
                    InterventionType.RevertToBest);
                return;
            }
            
            // NOTE: Removed IncreaseStepSize intervention - our adaptive step already handles this
            // NOTE: Removed ReduceStepSize intervention - our adaptive step already handles this
            // NOTE: Removed MaintainStrategy intervention - not useful
        }
        
        private void TriggerIntervention(string reason, InterventionType type)
        {
            InterventionCount++;
            _lastInterventionIter = TotalIterations;
            OnIntervention?.Invoke(reason, type);
        }
        
        /// <summary>
        /// Get recommended phase adjustments based on current analysis
        /// </summary>
        public PhaseAdjustment GetPhaseAdjustment()
        {
            var adj = new PhaseAdjustment();
            
            // Stagnant - need more exploration
            if (IsStagnant)
            {
                adj.StepSizeMultiplier = 1.5f;
                adj.MorphCountMultiplier = 1.3f;
                adj.TreeUsageMultiplier = 0.7f;  // Less tree, more random
                // DON'T suggest phase change immediately - let normal phase logic handle it
                // Only suggest after REALLY long stagnation
                if (TotalIterations > 200 && _mediumTermScores.Count >= 150)
                {
                    adj.SuggestPhaseChange = true;
                    adj.SuggestedPhaseType = "aggressive";
                }
            }
            // Regressing - be more careful
            else if (IsRegressing)
            {
                adj.StepSizeMultiplier = 0.5f;
                adj.MorphCountMultiplier = 0.7f;
                adj.TreeUsageMultiplier = 1.5f;  // More tree guidance
                adj.ForceRevertToBest = true;
            }
            // High volatility - stabilize
            else if (Volatility > 0.03f)
            {
                adj.StepSizeMultiplier = 0.7f;
                adj.MorphCountMultiplier = 0.8f;
            }
            // Rapid progress - continue
            else if (IsRapidProgress)
            {
                adj.StepSizeMultiplier = 1.1f;  // Slight increase
                adj.MaintainCurrentStrategy = true;
            }
            // Steady progress - fine tune
            else if (MediumTermTrend > 0.001f)
            {
                adj.StepSizeMultiplier = 0.9f;  // Slightly reduce for precision
            }
            
            return adj;
        }
        
        /// <summary>
        /// Should we evolve new phases based on current performance?
        /// </summary>

        public bool ShouldEvolvePhases()
        {
            // Evolve if session is long and performance is suboptimal
            if (TotalIterations > 500 && ProgressRate < 0.0005f)
                return true;
            
            // Evolve if we've had many interventions
            if (InterventionCount > 10)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Get suggested parameters for a new evolved phase
        /// </summary>
        public DynamicPhase SuggestNewPhase(Random random)
        {
            var phase = new DynamicPhase
            {
                Id = $"evolved_{TotalIterations}_{random.Next(1000)}",
                Name = $"Evolved Phase {DateTime.Now:HHmm}",
                Description = "Created by SessionMonitor based on performance analysis",
                IsSystemPhase = false,
                Generation = 1,
                CreatedAt = DateTime.Now
            };
            
            // Base parameters on what seems to be working
            if (IsStagnant)
            {
                // Need more aggressive exploration
                phase.StepSize = 0.35f + (float)random.NextDouble() * 0.15f;
                phase.MinMorphChanges = 4;
                phase.MaxMorphChanges = 10;
                phase.TreeUsageProbability = 0.2f;
                phase.EnterOnPlateau = true;
            }
            else if (Volatility > 0.03f)
            {
                // Need more stability
                phase.StepSize = 0.05f + (float)random.NextDouble() * 0.05f;
                phase.MinMorphChanges = 1;
                phase.MaxMorphChanges = 3;
                phase.TreeUsageProbability = 0.8f;
            }
            else
            {
                // Balanced approach
                phase.StepSize = 0.1f + (float)random.NextDouble() * 0.1f;
                phase.MinMorphChanges = 2;
                phase.MaxMorphChanges = 5;
                phase.TreeUsageProbability = 0.5f;
            }
            
            return phase;
        }
        

        public string GetStatusReport()
        {
            return $"Trend:[S:{ShortTermTrend:+0.000;-0.000} M:{MediumTermTrend:+0.000;-0.000}] " +
                   $"Vol:{Volatility:F3} Rate:{ProgressRate:F4}/iter " +
                   $"Best:{SessionBestScore:F3} Interventions:{InterventionCount}";
        }
    }
    
    public enum InterventionType
    {
        ForcePhaseChange,
        RevertToBest,
        ReduceStepSize,
        IncreaseStepSize,
        MaintainStrategy,
        EvolveNewPhase
    }
    
    public class PhaseAdjustment
    {

        public float StepSizeMultiplier { get; set; } = 1f;
        public float MorphCountMultiplier { get; set; } = 1f;
        public float TreeUsageMultiplier { get; set; } = 1f;
        public bool SuggestPhaseChange { get; set; }
        public string SuggestedPhaseType { get; set; }
        public bool ForceRevertToBest { get; set; }
        public bool MaintainCurrentStrategy { get; set; }
    }
}
