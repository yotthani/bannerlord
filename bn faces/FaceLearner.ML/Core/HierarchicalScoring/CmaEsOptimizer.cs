using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// CMA-ES (Covariance Matrix Adaptation Evolution Strategy) optimizer.
    /// v3.0.29b: Fixed elitism, base morph tracking, and candidate cycling.
    ///
    /// Key fixes over v3.0.29:
    /// - ELITISM: Best-ever candidate is always included in next generation (slot 0)
    /// - BASE MORPHS: Updated after each generation to track best for inactive morphs
    /// - NO STALE CANDIDATES: GetNextCandidate returns mean (current best estimate) as fallback
    /// - MEAN INJECTION: If best-ever is better than generation best, inject it into mean update
    ///
    /// Diagonal CMA-ES for our use case:
    /// - Diagonal covariance approximation (full matrix too expensive for 62D)
    /// - Population size adapts to dimensionality of active morph subset (3-12)
    /// - Integrates with hierarchical phase system (each SubPhase gets its own state)
    /// </summary>
    public class CmaEsOptimizer
    {
        #region State

        // Problem dimensionality (number of active morphs in current SubPhase)
        private int _dim;

        // Active morph indices (maps local CMA-ES dimension to global morph index)
        private int[] _activeMorphIndices;

        // Distribution parameters
        private double[] _mean;       // Mean of the search distribution
        private double _sigma;        // Overall step size
        private double[] _diagC;      // Diagonal covariance (variance per dimension)
        private double[] _pc;         // Evolution path for covariance
        private double[] _ps;         // Evolution path for step-size

        // Population
        private int _lambda;          // Population size (includes 1 elite slot)
        private int _mu;              // Number of parents (best individuals to keep)
        private double[] _weights;    // Recombination weights
        private double _muEff;        // Effective number of parents

        // Learning rates
        private double _cc;           // Learning rate for covariance path
        private double _cs;           // Learning rate for step-size path
        private double _c1;           // Learning rate for rank-1 update
        private double _cmu;          // Learning rate for rank-mu update
        private double _damps;        // Damping for step-size
        private double _chiN;         // Expected length of N(0,I) vector

        // Current generation
        private double[][] _population;  // Population of candidate solutions (local coords)
        private double[] _fitness;       // Fitness of each candidate
        private int _currentCandidate;   // Which candidate we're evaluating NEXT
        private int _generation;

        // Global morph state
        private float[] _baseMorphs;     // Full 62-morph array — updated to best after each generation
        private float _bestFitness;
        private float[] _bestMorphs;     // Best full 62-morph array ever seen
        private double[] _bestLocal;     // Best local (active-only) coords ever seen

        // Morph constraints
        private float[] _morphMin;
        private float[] _morphMax;

        private readonly Random _rng = new Random();

        #endregion

        #region Constants

        private const int NUM_MORPHS = 62;
        private const int MIN_POPULATION = 8;   // Raised from 6 — need enough for elitism + diversity
        private const int MAX_POPULATION = 16;

        #endregion

        #region Public API

        /// <summary>
        /// Initialize CMA-ES for a new SubPhase.
        /// </summary>
        public void Initialize(int[] activeMorphIndices, float[] currentMorphs, float initialSigma,
            float[] morphMin = null, float[] morphMax = null)
        {
            _activeMorphIndices = activeMorphIndices;
            _dim = activeMorphIndices.Length;

            // Store constraints
            _morphMin = morphMin ?? new float[NUM_MORPHS];
            _morphMax = morphMax ?? Enumerable.Repeat(1.0f, NUM_MORPHS).ToArray();

            // Store base morphs (we only modify active indices)
            _baseMorphs = (float[])currentMorphs.Clone();
            _bestMorphs = (float[])currentMorphs.Clone();
            _bestFitness = float.MinValue;

            // Initialize mean from current morph values
            _mean = new double[_dim];
            _bestLocal = new double[_dim];
            for (int i = 0; i < _dim; i++)
            {
                _mean[i] = currentMorphs[activeMorphIndices[i]];
                _bestLocal[i] = _mean[i];
            }

            // Step size — scale by average range of active morphs
            double avgRange = 0;
            for (int i = 0; i < _dim; i++)
            {
                var (_, _, range) = MorphGroups.GetMorphRange(activeMorphIndices[i]);
                avgRange += range;
            }
            avgRange /= _dim;
            _sigma = initialSigma * avgRange;

            // Population size: 4 + floor(3 * ln(n)), clamped. +1 for elite slot.
            _lambda = Math.Max(MIN_POPULATION, Math.Min(MAX_POPULATION, 5 + (int)(3 * Math.Log(_dim))));
            _mu = _lambda / 2;

            // Recombination weights (log-linear)
            _weights = new double[_mu];
            double weightSum = 0;
            for (int i = 0; i < _mu; i++)
            {
                _weights[i] = Math.Log(_mu + 0.5) - Math.Log(i + 1);
                weightSum += _weights[i];
            }
            for (int i = 0; i < _mu; i++)
                _weights[i] /= weightSum;

            _muEff = 1.0 / _weights.Sum(w => w * w);

            // Learning rates (Hansen's defaults)
            _cc = (4.0 + _muEff / _dim) / (_dim + 4.0 + 2.0 * _muEff / _dim);
            _cs = (_muEff + 2.0) / (_dim + _muEff + 5.0);
            _c1 = 2.0 / ((_dim + 1.3) * (_dim + 1.3) + _muEff);
            _cmu = Math.Min(1.0 - _c1, 2.0 * (_muEff - 2.0 + 1.0 / _muEff) / ((_dim + 2.0) * (_dim + 2.0) + _muEff));
            _damps = 1.0 + 2.0 * Math.Max(0, Math.Sqrt((_muEff - 1.0) / (_dim + 1.0)) - 1.0) + _cs;
            _chiN = Math.Sqrt(_dim) * (1.0 - 1.0 / (4.0 * _dim) + 1.0 / (21.0 * _dim * _dim));

            // Initialize covariance (diagonal) and paths
            _diagC = new double[_dim];
            _pc = new double[_dim];
            _ps = new double[_dim];

            for (int i = 0; i < _dim; i++)
            {
                var (_, _, range) = MorphGroups.GetMorphRange(activeMorphIndices[i]);
                double normalizedRange = range / avgRange;
                _diagC[i] = normalizedRange * normalizedRange;
                _pc[i] = 0;
                _ps[i] = 0;
            }

            // Allocate population
            _population = new double[_lambda][];
            _fitness = new double[_lambda];
            for (int i = 0; i < _lambda; i++)
                _population[i] = new double[_dim];

            _currentCandidate = 0;
            _generation = 0;

            // Generate first population
            GeneratePopulation();
        }

        /// <summary>
        /// Get the next candidate morph array to evaluate.
        /// Returns a full 62-morph array with only active morphs modified.
        /// </summary>
        public float[] GetNextCandidate()
        {
            if (_population == null)
                return _baseMorphs;

            // Safety: if somehow past population end, return the mean (best estimate)
            if (_currentCandidate >= _lambda)
                return BuildFullMorphArray(_mean);

            return BuildFullMorphArray(_population[_currentCandidate]);
        }

        /// <summary>
        /// Report the fitness (score) for the current candidate.
        /// When all candidates are evaluated, triggers a generation update.
        /// Returns true if a new generation was started.
        /// </summary>
        public bool ReportFitness(float fitness, float[] evaluatedMorphs)
        {
            if (_population == null || _currentCandidate >= _lambda) return false;

            _fitness[_currentCandidate] = fitness;

            // Track global best
            if (fitness > _bestFitness)
            {
                _bestFitness = fitness;
                _bestMorphs = (float[])evaluatedMorphs.Clone();
                // Also track in local coordinates
                for (int i = 0; i < _dim; i++)
                    _bestLocal[i] = _population[_currentCandidate][i];
            }

            _currentCandidate++;

            // All candidates evaluated? Update distribution and generate new population
            if (_currentCandidate >= _lambda)
            {
                UpdateDistribution();

                // Update base morphs to best — so inactive morphs reflect best state
                _baseMorphs = (float[])_bestMorphs.Clone();

                GeneratePopulation();
                _generation++;
                return true;
            }

            return false;
        }

        /// <summary>Current generation number</summary>
        public int Generation => _generation;

        /// <summary>Current candidate index within generation</summary>
        public int CurrentCandidateIndex => _currentCandidate;

        /// <summary>Population size</summary>
        public int PopulationSize => _lambda;

        /// <summary>Best fitness seen so far</summary>
        public float BestFitness => _bestFitness;

        /// <summary>Best morph array seen so far (full 62)</summary>
        public float[] BestMorphs => _bestMorphs;

        /// <summary>Current step size (for monitoring)</summary>
        public double Sigma => _sigma;

        /// <summary>Whether the optimizer has converged (sigma very small)</summary>
        public bool HasConverged => _sigma < 0.001;

        /// <summary>
        /// Update base morphs without reinitializing.
        /// Used when restoring global best at SubPhase transitions.
        /// </summary>
        public void UpdateBaseMorphs(float[] newBase)
        {
            if (newBase == null) return;
            _baseMorphs = (float[])newBase.Clone();

            // Update mean to reflect new base for active morphs
            for (int i = 0; i < _dim; i++)
            {
                _mean[i] = newBase[_activeMorphIndices[i]];
                _bestLocal[i] = _mean[i];
            }
            _bestFitness = float.MinValue;  // Reset — new starting point

            // Regenerate population around new mean
            GeneratePopulation();
        }

        #endregion

        #region Internal: Build morph array

        /// <summary>Build a full 62-morph array from local (active-only) coordinates</summary>
        private float[] BuildFullMorphArray(double[] localCoords)
        {
            float[] morphs = (float[])_baseMorphs.Clone();
            for (int i = 0; i < _dim; i++)
            {
                int globalIdx = _activeMorphIndices[i];
                float val = (float)localCoords[i];
                val = Math.Max(_morphMin[globalIdx], Math.Min(_morphMax[globalIdx], val));
                morphs[globalIdx] = val;
            }
            return morphs;
        }

        #endregion

        #region Internal: Generate + Update

        /// <summary>
        /// Sample a new population from the current distribution N(mean, sigma² * C).
        /// Slot 0 is always the ELITE (best-ever local coordinates) for monotonic improvement.
        /// </summary>
        private void GeneratePopulation()
        {
            _currentCandidate = 0;

            // Slot 0: ELITE — inject best-ever candidate to guarantee no regression
            if (_bestFitness > float.MinValue)
            {
                for (int i = 0; i < _dim; i++)
                    _population[0][i] = _bestLocal[i];
            }
            else
            {
                // First generation: slot 0 is the mean (starting point)
                for (int i = 0; i < _dim; i++)
                    _population[0][i] = _mean[i];
            }

            // Slots 1..lambda-1: random samples from N(mean, sigma² * C)
            for (int k = 1; k < _lambda; k++)
            {
                for (int i = 0; i < _dim; i++)
                {
                    double z = SampleGaussian();
                    double stdDev = Math.Sqrt(Math.Max(_diagC[i], 1e-20));
                    _population[k][i] = _mean[i] + _sigma * stdDev * z;

                    // Clamp to morph bounds
                    int globalIdx = _activeMorphIndices[i];
                    _population[k][i] = Math.Max(_morphMin[globalIdx],
                        Math.Min(_morphMax[globalIdx], _population[k][i]));
                }
            }
        }

        /// <summary>
        /// CMA-ES distribution update after evaluating all candidates.
        /// </summary>
        private void UpdateDistribution()
        {
            // Sort population by fitness (descending — we maximize)
            int[] indices = Enumerable.Range(0, _lambda).ToArray();
            Array.Sort(indices, (a, b) => _fitness[b].CompareTo(_fitness[a]));

            // === 1. UPDATE MEAN ===
            double[] oldMean = (double[])_mean.Clone();
            for (int i = 0; i < _dim; i++)
            {
                _mean[i] = 0;
                for (int k = 0; k < _mu; k++)
                {
                    _mean[i] += _weights[k] * _population[indices[k]][i];
                }
            }

            // === 2. UPDATE EVOLUTION PATHS ===
            double[] meanDiff = new double[_dim];
            for (int i = 0; i < _dim; i++)
            {
                double stdDev = Math.Sqrt(Math.Max(_diagC[i], 1e-20));
                meanDiff[i] = (_mean[i] - oldMean[i]) / (_sigma * stdDev + 1e-20);
            }

            // Cumulation for step-size control
            double psNorm = 0;
            for (int i = 0; i < _dim; i++)
            {
                _ps[i] = (1 - _cs) * _ps[i] + Math.Sqrt(_cs * (2 - _cs) * _muEff) * meanDiff[i];
                psNorm += _ps[i] * _ps[i];
            }
            psNorm = Math.Sqrt(psNorm);

            // Cumulation for covariance path (with hsig correction)
            double hsig = psNorm / Math.Sqrt(1 - Math.Pow(1 - _cs, 2 * (_generation + 1))) / _chiN
                          < 1.4 + 2.0 / (_dim + 1) ? 1.0 : 0.0;

            for (int i = 0; i < _dim; i++)
            {
                double stdDev = Math.Sqrt(Math.Max(_diagC[i], 1e-20));
                double meanStep = (_mean[i] - oldMean[i]) / (_sigma + 1e-20);
                _pc[i] = (1 - _cc) * _pc[i]
                         + hsig * Math.Sqrt(_cc * (2 - _cc) * _muEff) * meanStep / (stdDev + 1e-20);
            }

            // === 3. UPDATE DIAGONAL COVARIANCE ===
            for (int i = 0; i < _dim; i++)
            {
                double stdDev = Math.Sqrt(Math.Max(_diagC[i], 1e-20));

                // Rank-1 update
                double rank1 = _pc[i] * _pc[i];

                // Rank-mu update
                double rankMu = 0;
                for (int k = 0; k < _mu; k++)
                {
                    double step = (_population[indices[k]][i] - oldMean[i]) / (_sigma * stdDev + 1e-20);
                    rankMu += _weights[k] * step * step;
                }

                // Combined update
                _diagC[i] = (1 - _c1 - _cmu) * _diagC[i]
                           + _c1 * (rank1 + (1 - hsig) * _cc * (2 - _cc) * _diagC[i])
                           + _cmu * rankMu * _diagC[i];

                // Safety bounds
                _diagC[i] = Math.Max(_diagC[i], 1e-20);
                _diagC[i] = Math.Min(_diagC[i], 1e6);
            }

            // === 4. UPDATE STEP SIZE (sigma) ===
            _sigma *= Math.Exp((_cs / _damps) * (psNorm / _chiN - 1));
            _sigma = Math.Max(_sigma, 1e-10);
            _sigma = Math.Min(_sigma, 2.0);
        }

        /// <summary>Sample from standard normal distribution using Box-Muller</summary>
        private double SampleGaussian()
        {
            double u1 = _rng.NextDouble();
            double u2 = _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1 + 1e-10)) * Math.Cos(2.0 * Math.PI * u2);
        }

        #endregion
    }
}
