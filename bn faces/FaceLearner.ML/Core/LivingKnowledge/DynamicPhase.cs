using System;
using System.Collections.Generic;

namespace FaceLearner.Core.LivingKnowledge
{
        /// <summary>
    /// A dynamically defined learning phase - can be created, modified, or deleted at runtime
    /// </summary>
    [Serializable]
    public class DynamicPhase
    {
        
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        
        
        // When to enter this phase
        public float MinScoreToEnter { get; set; }
        public float MaxScoreToEnter { get; set; } = 1f;
        public bool EnterOnPlateau { get; set; }
        public bool EnterOnRapidProgress { get; set; }
        
        // Phase behavior parameters
        public float StepSize { get; set; } = 0.1f;
        public float MinSigma { get; set; } = 0.1f;  // Minimum CMA-ES sigma for this phase
        public int MinMorphChanges { get; set; } = 1;
        public int MaxMorphChanges { get; set; } = 5;
        public float TreeUsageProbability { get; set; } = 0.5f;
        public float RevertToBestProbability { get; set; } = 0.1f;
        
        // Exit conditions
        public float ExitOnScoreAbove { get; set; } = 1f;
        public int MaxIterationsInPhase { get; set; } = 200;
        public bool ExitOnPlateau { get; set; } = true;
        public int PlateauWindowSize { get; set; } = 30;
        public float PlateauThreshold { get; set; } = 0.005f;
        
        // Suggested next phases (by Id)
        public List<string> PreferredNextPhases { get; set; } = new List<string>();
        
        // Performance tracking
        public int TimesEntered { get; set; }
        public float TotalScoreGainInPhase { get; set; }
        public float TotalIterationsInPhase { get; set; }
        public int SuccessfulExits { get; set; }  // Exited with score improvement
        public int FailedExits { get; set; }      // Exited without improvement
        
        // Calculated metrics
        public float AverageScoreGain => TimesEntered > 0 ? TotalScoreGainInPhase / TimesEntered : 0;
        public float AverageIterations => TimesEntered > 0 ? TotalIterationsInPhase / TimesEntered : 0;
        public float SuccessRate => (TimesEntered > 0) ? (float)SuccessfulExits / TimesEntered : 0.5f;
        public float Effectiveness => SuccessRate * 0.5f + Math.Min(1f, AverageScoreGain * 10f) * 0.5f;
        
        // Is this a system-created phase or user-evolved?
        public bool IsSystemPhase { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Generation { get; set; } = 0;  // Evolution generation
        
        /// <summary>
        /// Create default exploration phase
        /// </summary>
        public static DynamicPhase CreateExploration()
        {
            return new DynamicPhase
            {
                Id = "exploration",
                Name = "Exploration",
                Description = "Aggressive search to find good regions",
                MinScoreToEnter = 0f,
                MaxScoreToEnter = 0.5f,  // Extended to overlap with Refinement
                StepSize = 0.25f,
                MinSigma = 0.15f,  // Large minimum for exploration
                MinMorphChanges = 4,
                MaxMorphChanges = 10,
                TreeUsageProbability = 0.3f,
                RevertToBestProbability = 0.05f,
                ExitOnScoreAbove = 0.4f,
                MaxIterationsInPhase = 120,
                ExitOnPlateau = true,
                PreferredNextPhases = new List<string> { "refinement", "plateau_escape" },
                IsSystemPhase = true
            };
        }
        
        /// <summary>
        /// Create default refinement phase
        /// </summary>
        public static DynamicPhase CreateRefinement()
        {
            return new DynamicPhase
            {
                Id = "refinement",
                Name = "Refinement",
                Description = "Medium steps to home in on good regions",
                MinScoreToEnter = 0.25f,
                MaxScoreToEnter = 0.75f,  // Extended to meet Convergence
                StepSize = 0.12f,  // Slightly larger
                MinSigma = 0.08f,  // Medium minimum for refinement
                MinMorphChanges = 2,
                MaxMorphChanges = 6,
                TreeUsageProbability = 0.5f,
                RevertToBestProbability = 0.1f,
                ExitOnScoreAbove = 0.7f,
                MaxIterationsInPhase = 150,
                ExitOnPlateau = true,
                PreferredNextPhases = new List<string> { "convergence", "plateau_escape" },
                IsSystemPhase = true
            };
        }
        
        /// <summary>
        /// Create default convergence phase
        /// </summary>
        public static DynamicPhase CreateConvergence()
        {
            return new DynamicPhase
            {
                Id = "convergence",
                Name = "Convergence",
                Description = "Fine-tuning to maximize score",
                MinScoreToEnter = 0.75f,  // Only for high scores!
                MaxScoreToEnter = 1f,
                StepSize = 0.08f,  // Slightly bigger steps
                MinSigma = 0.05f,  // Small minimum for fine-tuning
                MinMorphChanges = 1,
                MaxMorphChanges = 4,
                TreeUsageProbability = 0.7f,
                RevertToBestProbability = 0.15f,
                ExitOnScoreAbove = 0.95f,
                MaxIterationsInPhase = 100,
                ExitOnPlateau = true,
                PlateauWindowSize = 40,
                PreferredNextPhases = new List<string> { "plateau_escape" },
                IsSystemPhase = true
            };
        }
        
        /// <summary>
        /// Create plateau escape phase
        /// </summary>
        public static DynamicPhase CreatePlateauEscape()
        {
            return new DynamicPhase
            {
                Id = "plateau_escape",
                Name = "Plateau Escape",
                Description = "Aggressive moves to escape local optima",
                MinScoreToEnter = 0f,
                MaxScoreToEnter = 1f,
                EnterOnPlateau = true,
                StepSize = 0.4f,
                MinSigma = 0.25f,  // Large minimum to force big changes
                MinMorphChanges = 5,
                MaxMorphChanges = 12,
                TreeUsageProbability = 0.2f,
                RevertToBestProbability = 0.3f,
                MaxIterationsInPhase = 60,
                ExitOnPlateau = false,  // Don't exit on plateau - we're trying to escape!
                PreferredNextPhases = new List<string> { "refinement", "exploration" },
                IsSystemPhase = true
            };
        }
        
        /// <summary>
        /// Create a mutated copy of this phase (for evolution)
        /// </summary>
        public DynamicPhase Mutate(Random random, float mutationStrength = 0.2f)
        {
            var mutated = new DynamicPhase
            {
                Id = $"{Id}_gen{Generation + 1}_{random.Next(1000)}",
                Name = $"{Name} v{Generation + 1}",
                Description = $"Evolved from {Name}",
                IsSystemPhase = false,
                Generation = Generation + 1,
                CreatedAt = DateTime.Now,
                PreferredNextPhases = new List<string>(PreferredNextPhases)
            };
            
            // Mutate numeric parameters
            mutated.MinScoreToEnter = MutateValue(MinScoreToEnter, 0f, 1f, mutationStrength, random);
            mutated.MaxScoreToEnter = MutateValue(MaxScoreToEnter, mutated.MinScoreToEnter, 1f, mutationStrength, random);
            mutated.StepSize = MutateValue(StepSize, 0.01f, 0.5f, mutationStrength, random);
            mutated.MinSigma = MutateValue(MinSigma, 0.03f, 0.35f, mutationStrength, random);  // Evolve MinSigma
            mutated.MinMorphChanges = (int)MutateValue(MinMorphChanges, 1, 10, mutationStrength, random);
            mutated.MaxMorphChanges = (int)MutateValue(MaxMorphChanges, mutated.MinMorphChanges, 15, mutationStrength, random);
            mutated.TreeUsageProbability = MutateValue(TreeUsageProbability, 0f, 1f, mutationStrength, random);
            mutated.RevertToBestProbability = MutateValue(RevertToBestProbability, 0f, 0.5f, mutationStrength, random);
            mutated.ExitOnScoreAbove = MutateValue(ExitOnScoreAbove, mutated.MinScoreToEnter, 1f, mutationStrength, random);
            mutated.MaxIterationsInPhase = (int)MutateValue(MaxIterationsInPhase, 20, 500, mutationStrength, random);
            mutated.PlateauWindowSize = (int)MutateValue(PlateauWindowSize, 10, 100, mutationStrength, random);
            mutated.PlateauThreshold = MutateValue(PlateauThreshold, 0.001f, 0.02f, mutationStrength, random);
            
            // Randomly flip boolean flags
            mutated.EnterOnPlateau = random.NextDouble() < 0.1 ? !EnterOnPlateau : EnterOnPlateau;
            mutated.ExitOnPlateau = random.NextDouble() < 0.1 ? !ExitOnPlateau : ExitOnPlateau;
            
            return mutated;
        }
        
        private float MutateValue(float value, float min, float max, float strength, Random random)
        {
            float range = max - min;
            float mutation = (float)(random.NextDouble() - 0.5) * 2 * range * strength;
            return Math.Max(min, Math.Min(max, value + mutation));
        }
        
        /// <summary>
        /// Crossover with another phase (for evolution)
        /// </summary>
        public DynamicPhase CrossoverWith(DynamicPhase other, Random random)
        {
            var child = new DynamicPhase
            {
                Id = $"cross_{Id}_{other.Id}_{random.Next(1000)}",
                Name = $"Hybrid ({Name}/{other.Name})",
                Description = $"Crossover of {Name} and {other.Name}",
                IsSystemPhase = false,
                Generation = Math.Max(Generation, other.Generation) + 1,
                CreatedAt = DateTime.Now
            };
            
            // Randomly pick from either parent
            child.MinScoreToEnter = random.NextDouble() < 0.5 ? MinScoreToEnter : other.MinScoreToEnter;
            child.MaxScoreToEnter = random.NextDouble() < 0.5 ? MaxScoreToEnter : other.MaxScoreToEnter;
            child.StepSize = random.NextDouble() < 0.5 ? StepSize : other.StepSize;
            child.MinSigma = random.NextDouble() < 0.5 ? MinSigma : other.MinSigma;  // Include MinSigma
            child.MinMorphChanges = random.NextDouble() < 0.5 ? MinMorphChanges : other.MinMorphChanges;
            child.MaxMorphChanges = random.NextDouble() < 0.5 ? MaxMorphChanges : other.MaxMorphChanges;
            child.TreeUsageProbability = random.NextDouble() < 0.5 ? TreeUsageProbability : other.TreeUsageProbability;
            child.RevertToBestProbability = random.NextDouble() < 0.5 ? RevertToBestProbability : other.RevertToBestProbability;
            child.ExitOnScoreAbove = random.NextDouble() < 0.5 ? ExitOnScoreAbove : other.ExitOnScoreAbove;
            child.MaxIterationsInPhase = random.NextDouble() < 0.5 ? MaxIterationsInPhase : other.MaxIterationsInPhase;
            child.EnterOnPlateau = random.NextDouble() < 0.5 ? EnterOnPlateau : other.EnterOnPlateau;
            child.ExitOnPlateau = random.NextDouble() < 0.5 ? ExitOnPlateau : other.ExitOnPlateau;
            
            // Combine preferred next phases
            child.PreferredNextPhases = new List<string>(PreferredNextPhases);
            foreach (var p in other.PreferredNextPhases)
                if (!child.PreferredNextPhases.Contains(p))
                    child.PreferredNextPhases.Add(p);
            
            return child;
        }
        
        public override string ToString()
        {
            return $"{Name} [Step:{StepSize:F2} σ≥{MinSigma:F2} Morphs:{MinMorphChanges}-{MaxMorphChanges} Tree:{TreeUsageProbability:F1}] Eff:{Effectiveness:F2}";
        }
    }
}
