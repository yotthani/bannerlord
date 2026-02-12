using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Manages dynamic phases - creates, evolves, and selects phases
    /// </summary>
    public class PhaseManager
    {
        private Dictionary<string, DynamicPhase> _phases = new Dictionary<string, DynamicPhase>();
        private string _savePath;
        private Random _random = new Random();
        

        public int TotalPhases => _phases.Count;
        public int EvolvedPhases => _phases.Values.Count(p => !p.IsSystemPhase);
        public int MaxGeneration => _phases.Values.Max(p => p.Generation);
        
        public PhaseManager(string savePath)
        {
            _savePath = savePath;
            InitializeDefaultPhases();
        }
        

        private void InitializeDefaultPhases()
        {
            AddPhase(DynamicPhase.CreateExploration());
            AddPhase(DynamicPhase.CreateRefinement());
            AddPhase(DynamicPhase.CreateConvergence());
            AddPhase(DynamicPhase.CreatePlateauEscape());
            
            // Add some specialized phases
            AddPhase(CreateMicroAdjust());
            AddPhase(CreateBroadSearch());
        }
        
        private DynamicPhase CreateMicroAdjust()
        {
            return new DynamicPhase
            {
                Id = "micro_adjust",
                Name = "Micro Adjust",
                Description = "Very fine adjustments for high scores",
                MinScoreToEnter = 0.7f,
                StepSize = 0.015f,
                MinMorphChanges = 1,
                MaxMorphChanges = 2,
                TreeUsageProbability = 0.9f,
                RevertToBestProbability = 0.2f,
                ExitOnScoreAbove = 0.98f,
                MaxIterationsInPhase = 100,
                PreferredNextPhases = new List<string> { "convergence" },
                IsSystemPhase = true
            };
        }
        
        private DynamicPhase CreateBroadSearch()
        {
            return new DynamicPhase
            {
                Id = "broad_search",
                Name = "Broad Search",
                Description = "Wide exploration when stuck at low scores",
                MinScoreToEnter = 0f,
                MaxScoreToEnter = 0.25f,
                EnterOnPlateau = true,
                StepSize = 0.45f,
                MinSigma = 0.25f,  // Large minimum for broad search
                MinMorphChanges = 6,
                MaxMorphChanges = 15,
                TreeUsageProbability = 0.1f,
                RevertToBestProbability = 0.4f,
                MaxIterationsInPhase = 80,
                PreferredNextPhases = new List<string> { "exploration", "refinement" },
                IsSystemPhase = true
            };
        }
        

        public void AddPhase(DynamicPhase phase)
        {
            _phases[phase.Id] = phase;
        }
        
        public DynamicPhase GetPhase(string id)
        {
            return _phases.TryGetValue(id, out var phase) ? phase : null;
        }
        

        public IEnumerable<DynamicPhase> GetAllPhases() => _phases.Values;
        
        /// <summary>
        /// Select best phase for current situation
        /// </summary>
        public DynamicPhase SelectPhase(float currentScore, float bestScore, bool isOnPlateau, 
            bool isRapidProgress, string currentPhaseId)
        {
            var candidates = _phases.Values
                .Where(p => currentScore >= p.MinScoreToEnter && currentScore <= p.MaxScoreToEnter)
                .Where(p => !isOnPlateau || p.EnterOnPlateau || !p.ExitOnPlateau)
                .ToList();
            
            if (candidates.Count == 0)
                candidates = _phases.Values.ToList();  // Fallback to any phase
            
            // Score each candidate
            var scored = candidates.Select(p => new
            {
                Phase = p,
                Score = ScorePhaseForSituation(p, currentScore, bestScore, isOnPlateau, isRapidProgress, currentPhaseId)
            })
            .OrderByDescending(x => x.Score)
            .ToList();
            
            // Usually pick best, sometimes explore
            if (_random.NextDouble() < 0.1 && scored.Count > 1)
            {
                // Exploration: pick from top 3
                int idx = _random.Next(Math.Min(3, scored.Count));
                return scored[idx].Phase;
            }
            
            return scored.First().Phase;
        }
        

        private float ScorePhaseForSituation(DynamicPhase phase, float currentScore, float bestScore,
            bool isOnPlateau, bool isRapidProgress, string currentPhaseId)
        {
            float score = 0;
            
            // Historical effectiveness
            score += phase.Effectiveness * 2f;
            
            // Plateau handling
            if (isOnPlateau && phase.EnterOnPlateau)
                score += 0.5f;
            if (isOnPlateau && phase.StepSize > 0.2f)
                score += 0.3f;  // Prefer aggressive phases when stuck
            
            // Rapid progress - maintain similar approach
            if (isRapidProgress && phase.Id == currentPhaseId)
                score += 0.4f;
            
            // Score range appropriateness
            float rangeFit = 1f - Math.Abs(currentScore - (phase.MinScoreToEnter + phase.MaxScoreToEnter) / 2);
            score += rangeFit * 0.3f;
            
            // Prefer phases we haven't used much (exploration bonus)
            if (phase.TimesEntered < 10)
                score += 0.2f;
            
            // Slight preference for system phases (proven)
            if (phase.IsSystemPhase)
                score += 0.1f;
            
            // Bonus for evolved phases that perform well
            if (!phase.IsSystemPhase && phase.Effectiveness > 0.6f)
                score += 0.3f;
            
            return score;
        }
        
        /// <summary>
        /// Evolve new phases based on performance
        /// </summary>
        public List<DynamicPhase> EvolvePhases(int count = 2)
        {
            var newPhases = new List<DynamicPhase>();
            
            // Get best performing phases
            var bestPhases = _phases.Values
                .Where(p => p.TimesEntered >= 5)
                .OrderByDescending(p => p.Effectiveness)
                .Take(5)
                .ToList();
            
            if (bestPhases.Count < 2)
                bestPhases = _phases.Values.Take(5).ToList();
            
            for (int i = 0; i < count; i++)
            {
                DynamicPhase newPhase;
                
                if (_random.NextDouble() < 0.6 && bestPhases.Count >= 2)
                {
                    // Crossover between two good phases
                    var parent1 = bestPhases[_random.Next(bestPhases.Count)];
                    var parent2 = bestPhases[_random.Next(bestPhases.Count)];
                    newPhase = parent1.CrossoverWith(parent2, _random);
                }
                else
                {
                    // Mutate a good phase
                    var parent = bestPhases[_random.Next(bestPhases.Count)];
                    float mutationStrength = 0.15f + (float)_random.NextDouble() * 0.2f;
                    newPhase = parent.Mutate(_random, mutationStrength);
                }
                
                AddPhase(newPhase);
                newPhases.Add(newPhase);
                SubModule.Log($"PhaseManager: Evolved '{newPhase.Name}' from gen {newPhase.Generation - 1}");
            }
            
            // Prune poorly performing evolved phases
            PruneWeakPhases();
            
            return newPhases;
        }
        
        private void PruneWeakPhases()
        {
            // Only prune evolved phases with enough data
            var toPrune = _phases.Values
                .Where(p => !p.IsSystemPhase)
                .Where(p => p.TimesEntered >= 20)
                .Where(p => p.Effectiveness < 0.2f)
                .Take(3)  // Don't prune too many at once
                .ToList();
            
            foreach (var phase in toPrune)
            {
                _phases.Remove(phase.Id);
                SubModule.Log($"PhaseManager: Pruned weak phase '{phase.Name}' (eff: {phase.Effectiveness:F2})");
            }
        }
        
        /// <summary>
        /// Update phase statistics after use
        /// </summary>
        public void RecordPhaseResult(string phaseId, float scoreGain, int iterations, bool wasSuccessful)
        {
            if (_phases.TryGetValue(phaseId, out var phase))
            {
                phase.TimesEntered++;
                phase.TotalScoreGainInPhase += scoreGain;
                phase.TotalIterationsInPhase += iterations;
                
                if (wasSuccessful)
                    phase.SuccessfulExits++;
                else
                    phase.FailedExits++;
            }
        }
        
        /// <summary>
        /// Adapt phase parameters based on performance
        /// </summary>
        public void AdaptPhaseParameters()
        {
            foreach (var phase in _phases.Values.Where(p => p.TimesEntered >= 10))
            {
                // If phase has low success rate, make it more aggressive
                if (phase.SuccessRate < 0.3f)
                {
                    phase.StepSize = Math.Min(0.5f, phase.StepSize * 1.1f);
                    phase.MaxMorphChanges = Math.Min(15, phase.MaxMorphChanges + 1);
                }
                // If phase has high success rate but low average gain, make it more precise
                else if (phase.SuccessRate > 0.6f && phase.AverageScoreGain < 0.01f)
                {
                    phase.StepSize = Math.Max(0.01f, phase.StepSize * 0.9f);
                }
                
                // Adjust max iterations based on average
                if (phase.AverageIterations > 0)
                {
                    float optimalIterations = phase.AverageIterations * 1.2f;  // 20% buffer
                    phase.MaxIterationsInPhase = (int)Math.Max(30, Math.Min(500, optimalIterations));
                }
            }
        }
        
        public bool Load()
        {
            if (!File.Exists(_savePath)) return false;
            
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(_savePath)))
                {
                    int version = reader.ReadInt32();
                    if (version < 1) return false;
                    
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        var phase = new DynamicPhase
                        {
                            Id = reader.ReadString(),
                            Name = reader.ReadString(),
                            Description = reader.ReadString(),
                            MinScoreToEnter = reader.ReadSingle(),
                            MaxScoreToEnter = reader.ReadSingle(),
                            EnterOnPlateau = reader.ReadBoolean(),
                            EnterOnRapidProgress = reader.ReadBoolean(),
                            StepSize = reader.ReadSingle(),
                            MinMorphChanges = reader.ReadInt32(),
                            MaxMorphChanges = reader.ReadInt32(),
                            TreeUsageProbability = reader.ReadSingle(),
                            RevertToBestProbability = reader.ReadSingle(),
                            ExitOnScoreAbove = reader.ReadSingle(),
                            MaxIterationsInPhase = reader.ReadInt32(),
                            ExitOnPlateau = reader.ReadBoolean(),
                            PlateauWindowSize = reader.ReadInt32(),
                            PlateauThreshold = reader.ReadSingle(),
                            TimesEntered = reader.ReadInt32(),
                            TotalScoreGainInPhase = reader.ReadSingle(),
                            TotalIterationsInPhase = reader.ReadSingle(),
                            SuccessfulExits = reader.ReadInt32(),
                            FailedExits = reader.ReadInt32(),
                            IsSystemPhase = reader.ReadBoolean(),
                            Generation = reader.ReadInt32()
                        };
                        
                        int nextCount = reader.ReadInt32();
                        phase.PreferredNextPhases = new List<string>();
                        for (int j = 0; j < nextCount; j++)
                            phase.PreferredNextPhases.Add(reader.ReadString());
                        
                        _phases[phase.Id] = phase;
                    }
                }
                
                SubModule.Log($"PhaseManager loaded: {TotalPhases} phases ({EvolvedPhases} evolved)");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"PhaseManager load error: {ex.Message}");
                return false;
            }
        }
        
        public void Save()
        {
            try
            {
                SubModule.Log($"PhaseManager: Saving to {_savePath}");
                string dir = Path.GetDirectoryName(_savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                using (var writer = new BinaryWriter(File.Create(_savePath)))
                {
                    writer.Write(1);  // Version
                    writer.Write(_phases.Count);
                    
                    foreach (var phase in _phases.Values)
                    {
                        writer.Write(phase.Id);
                        writer.Write(phase.Name);
                        writer.Write(phase.Description ?? "");
                        writer.Write(phase.MinScoreToEnter);
                        writer.Write(phase.MaxScoreToEnter);
                        writer.Write(phase.EnterOnPlateau);
                        writer.Write(phase.EnterOnRapidProgress);
                        writer.Write(phase.StepSize);
                        writer.Write(phase.MinMorphChanges);
                        writer.Write(phase.MaxMorphChanges);
                        writer.Write(phase.TreeUsageProbability);
                        writer.Write(phase.RevertToBestProbability);
                        writer.Write(phase.ExitOnScoreAbove);
                        writer.Write(phase.MaxIterationsInPhase);
                        writer.Write(phase.ExitOnPlateau);
                        writer.Write(phase.PlateauWindowSize);
                        writer.Write(phase.PlateauThreshold);
                        writer.Write(phase.TimesEntered);
                        writer.Write(phase.TotalScoreGainInPhase);
                        writer.Write(phase.TotalIterationsInPhase);
                        writer.Write(phase.SuccessfulExits);
                        writer.Write(phase.FailedExits);
                        writer.Write(phase.IsSystemPhase);
                        writer.Write(phase.Generation);
                        
                        writer.Write(phase.PreferredNextPhases?.Count ?? 0);
                        if (phase.PreferredNextPhases != null)
                            foreach (var next in phase.PreferredNextPhases)
                                writer.Write(next);
                    }
                }
                SubModule.Log($"PhaseManager: Saved {_phases.Count} phases");
            }
            catch (Exception ex)
            {
                SubModule.Log($"PhaseManager save error: {ex.Message}");
            }
        }
        
        public string GetSummary()
        {
            var best = _phases.Values.OrderByDescending(p => p.Effectiveness).FirstOrDefault();
            return $"Phases:{TotalPhases} (Evolved:{EvolvedPhases}) MaxGen:{MaxGeneration} Best:{best?.Name ?? "none"}";
        }
    }
}
