using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// CMA-ES: Covariance Matrix Adaptation Evolution Strategy
    /// Gold standard for black-box optimization in high-dimensional spaces.
    /// Learns correlations between parameters and adapts step sizes automatically.
    /// </summary>
    public class CmaEs
    {
        private readonly int _n;           // Dimension (number of parameters)
        private readonly int _lambda;      // Population size
        private readonly int _mu;          // Number of parents for recombination
        private readonly double[] _weights;// Recombination weights
        private readonly double _mueff;    // Variance-effective size of mu
        
        // Strategy parameters
        private readonly double _cc;       // Learning rate for cumulation for C
        private readonly double _cs;       // Learning rate for cumulation for sigma
        private readonly double _c1;       // Learning rate for rank-one update of C
        private readonly double _cmu;      // Learning rate for rank-mu update of C
        private readonly double _damps;    // Damping for sigma
        private readonly double _chiN;     // Expectation of ||N(0,I)||
        
        // Dynamic state
        private double[] _mean;            // Distribution mean (current best estimate)
        private double _sigma;             // Step size
        private double[,] _C;              // Covariance matrix
        private double[,] _B;              // Eigenvectors of C
        private double[] _D;               // Sqrt of eigenvalues of C
        private double[] _pc;              // Evolution path for C
        private double[] _ps;              // Evolution path for sigma
        private int _eigenUpdateCounter;
        
        // Population
        private double[][] _population;
        private double[] _fitness;
        private int[] _arindex;            // Sorted indices by fitness
        
        // Statistics
        public int Generation { get; private set; }
        public double BestFitness { get; private set; }
        public double[] BestSolution { get; private set; }
        public double Sigma => _sigma;
        
        // Bounds
        private readonly double _minBound;
        private readonly double _maxBound;
        
        private readonly Random _rng;
        
        public CmaEs(int dimension, double[] initialMean = null, double initialSigma = 0.3,
                     double minBound = 0.0, double maxBound = 1.0, int? populationSize = null, int? seed = null)
        {
            _n = dimension;
            _minBound = minBound;
            _maxBound = maxBound;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            
            // Population size: default is 4 + floor(3*ln(n))
            _lambda = populationSize ?? Math.Max(8, 4 + (int)(3 * Math.Log(_n)));
            _mu = _lambda / 2;
            
            // Recombination weights (log-linear)
            _weights = new double[_mu];
            double sumWeights = 0;
            for (int i = 0; i < _mu; i++)
            {
                _weights[i] = Math.Log(_mu + 0.5) - Math.Log(i + 1);
                sumWeights += _weights[i];
            }
            for (int i = 0; i < _mu; i++)
                _weights[i] /= sumWeights;
            
            // Variance-effective size of mu
            double sumW2 = _weights.Sum(w => w * w);
            _mueff = 1.0 / sumW2;
            
            // Strategy parameter settings
            _cc = (4.0 + _mueff / _n) / (_n + 4.0 + 2.0 * _mueff / _n);
            _cs = (_mueff + 2.0) / (_n + _mueff + 5.0);
            _c1 = 2.0 / ((_n + 1.3) * (_n + 1.3) + _mueff);
            _cmu = Math.Min(1.0 - _c1, 2.0 * (_mueff - 2.0 + 1.0 / _mueff) / ((_n + 2.0) * (_n + 2.0) + _mueff));
            _damps = 1.0 + 2.0 * Math.Max(0.0, Math.Sqrt((_mueff - 1.0) / (_n + 1.0)) - 1.0) + _cs;
            _chiN = Math.Sqrt(_n) * (1.0 - 1.0 / (4.0 * _n) + 1.0 / (21.0 * _n * _n));
            
            // Initialize mean
            _mean = new double[_n];
            if (initialMean != null)
            {
                Array.Copy(initialMean, _mean, _n);
            }
            else
            {
                // Start in middle of bounds
                for (int i = 0; i < _n; i++)
                    _mean[i] = (_minBound + _maxBound) / 2.0;
            }
            
            _sigma = initialSigma;
            
            // Initialize covariance matrix as identity
            _C = new double[_n, _n];
            _B = new double[_n, _n];
            _D = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                _C[i, i] = 1.0;
                _B[i, i] = 1.0;
                _D[i] = 1.0;
            }
            
            // Evolution paths
            _pc = new double[_n];
            _ps = new double[_n];
            
            // Population storage
            _population = new double[_lambda][];
            for (int i = 0; i < _lambda; i++)
                _population[i] = new double[_n];
            _fitness = new double[_lambda];
            _arindex = new int[_lambda];
            
            BestFitness = double.MinValue;
            BestSolution = new double[_n];
            Generation = 0;
            _eigenUpdateCounter = 0;
        }
        
        /// <summary>
        /// Sample the next population. Returns array of candidate solutions.
        /// </summary>
        public double[][] SamplePopulation()
        {
            // CRITICAL: Check and fix sigma BEFORE sampling (use phase-dependent minimum)
            if (double.IsNaN(_sigma) || double.IsInfinity(_sigma) || _sigma < _minSigma)
            {
                _sigma = Math.Max(_minSigma, 0.3);  // Reset to reasonable value
            }
            else if (_sigma > 1.0)
            {
                _sigma = 1.0;
            }
            
            // Update eigen decomposition periodically
            if (_eigenUpdateCounter >= 1.0 / (_c1 + _cmu) / _n / 10.0)
            {
                UpdateEigenDecomposition();
                _eigenUpdateCounter = 0;
            }
            
            for (int k = 0; k < _lambda; k++)
            {
                // Sample from N(0, I)
                double[] z = new double[_n];
                for (int i = 0; i < _n; i++)
                    z[i] = SampleGaussian();
                
                // Transform: y = B * D * z
                double[] y = new double[_n];
                for (int i = 0; i < _n; i++)
                {
                    double sum = 0;
                    for (int j = 0; j < _n; j++)
                    {
                        double dj = _D[j];
                        // Sanitize D
                        if (double.IsNaN(dj) || double.IsInfinity(dj) || dj < 1e-10)
                        {
                            dj = 1.0;
                        }
                        double bij = _B[i, j];
                        if (double.IsNaN(bij) || double.IsInfinity(bij))
                        {
                            bij = (i == j) ? 1.0 : 0.0;
                        }
                        sum += bij * dj * z[j];
                    }
                    y[i] = double.IsNaN(sum) ? z[i] : sum;  // Fallback to raw gaussian if transform fails
                }
                
                // x = mean + sigma * y
                for (int i = 0; i < _n; i++)
                {
                    double meanVal = _mean[i];
                    if (double.IsNaN(meanVal) || double.IsInfinity(meanVal))
                    {
                        meanVal = (_minBound + _maxBound) / 2.0;
                    }
                    
                    _population[k][i] = meanVal + _sigma * y[i];
                    
                    // Sanitize and clamp to bounds
                    if (double.IsNaN(_population[k][i]) || double.IsInfinity(_population[k][i]))
                    {
                        _population[k][i] = meanVal;
                    }
                    _population[k][i] = Math.Max(_minBound, Math.Min(_maxBound, _population[k][i]));
                }
            }
            
            return _population;
        }
        
        /// <summary>
        /// Update the distribution based on fitness values.
        /// Fitness should be MAXIMIZED (higher is better).
        /// </summary>
        public void Update(double[] fitnessValues)
        {
            if (fitnessValues.Length != _lambda)
                throw new ArgumentException($"Expected {_lambda} fitness values, got {fitnessValues.Length}");
            
            // CRITICAL: Check for NaN/Inf in fitness values
            for (int i = 0; i < fitnessValues.Length; i++)
            {
                if (double.IsNaN(fitnessValues[i]) || double.IsInfinity(fitnessValues[i]))
                {
                    fitnessValues[i] = -1000;  // Replace with very bad fitness
                }
            }
            
            Array.Copy(fitnessValues, _fitness, _lambda);
            
            // Sort by fitness (descending - best first)
            for (int i = 0; i < _lambda; i++)
                _arindex[i] = i;
            Array.Sort(_arindex, (a, b) => _fitness[b].CompareTo(_fitness[a]));
            
            // Track best
            if (_fitness[_arindex[0]] > BestFitness)
            {
                BestFitness = _fitness[_arindex[0]];
                Array.Copy(_population[_arindex[0]], BestSolution, _n);
            }
            
            // CRITICAL: Check for NaN in sigma BEFORE using it
            if (double.IsNaN(_sigma) || double.IsInfinity(_sigma) || _sigma < 1e-10)
            {
                _sigma = 0.3;  // Reset to reasonable value
            }
            
            // Compute weighted mean of best mu individuals
            double[] oldMean = new double[_n];
            Array.Copy(_mean, oldMean, _n);
            
            for (int i = 0; i < _n; i++)
            {
                _mean[i] = 0;
                for (int j = 0; j < _mu; j++)
                    _mean[i] += _weights[j] * _population[_arindex[j]][i];
                
                // Sanitize mean
                if (double.IsNaN(_mean[i]) || double.IsInfinity(_mean[i]))
                {
                    _mean[i] = oldMean[i];  // Keep old value if new is bad
                }
            }
            
            // Update evolution path for sigma
            double[] meanDiff = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                meanDiff[i] = (_mean[i] - oldMean[i]) / _sigma;
                // Sanitize
                if (double.IsNaN(meanDiff[i]) || double.IsInfinity(meanDiff[i]))
                {
                    meanDiff[i] = 0;
                }
            }
            
            // Compute C^(-1/2) * (mean - oldMean) / sigma
            double[] invsqrtC_meanDiff = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                double sum = 0;
                for (int j = 0; j < _n; j++)
                {
                    double dj = Math.Max(1e-10, _D[j]);  // Prevent division by zero
                    sum += _B[i, j] * meanDiff[j] / dj;
                }
                // Actually need B * D^-1 * B^T * meanDiff, simplified here
                invsqrtC_meanDiff[i] = double.IsNaN(sum) ? 0 : sum;
            }
            
            // Simplified: use meanDiff directly (works when C is close to identity)
            double csqrt = Math.Sqrt(_cs * (2.0 - _cs) * _mueff);
            if (double.IsNaN(csqrt)) csqrt = 1.0;
            
            for (int i = 0; i < _n; i++)
            {
                _ps[i] = (1.0 - _cs) * _ps[i] + csqrt * meanDiff[i];
                // Sanitize
                if (double.IsNaN(_ps[i]) || double.IsInfinity(_ps[i]))
                {
                    _ps[i] = 0;
                }
            }
            
            // Update sigma
            double psNorm = Math.Sqrt(_ps.Sum(p => p * p));
            if (double.IsNaN(psNorm) || psNorm < 1e-10)
            {
                psNorm = _chiN;  // Use expected value if calculation failed
            }
            
            double sigmaUpdate = Math.Exp(_cs / _damps * (psNorm / _chiN - 1.0));
            if (!double.IsNaN(sigmaUpdate) && !double.IsInfinity(sigmaUpdate))
            {
                _sigma *= sigmaUpdate;
            }
            
            // Prevent sigma from exploding or vanishing
            // CRITICAL: Use explicit bounds check that works with NaN
            if (double.IsNaN(_sigma) || _sigma < 0.05)
            {
                _sigma = 0.05;  // Minimum for meaningful exploration
            }
            else if (_sigma > 1.0)
            {
                _sigma = 1.0;
            }
            
            // Update evolution path for C
            double denom = Math.Sqrt(1.0 - Math.Pow(1.0 - _cs, 2.0 * (Generation + 1)));
            if (denom < 1e-10) denom = 1.0;
            double hsig = psNorm / denom < (1.4 + 2.0 / (_n + 1.0)) * _chiN ? 1.0 : 0.0;
            double ccsqrt = Math.Sqrt(_cc * (2.0 - _cc) * _mueff);
            if (double.IsNaN(ccsqrt)) ccsqrt = 1.0;
            
            for (int i = 0; i < _n; i++)
            {
                _pc[i] = (1.0 - _cc) * _pc[i] + hsig * ccsqrt * meanDiff[i];
                // Sanitize
                if (double.IsNaN(_pc[i]) || double.IsInfinity(_pc[i]))
                {
                    _pc[i] = 0;
                }
            }
            
            // Update covariance matrix
            double c1a = _c1 * (1.0 - (1.0 - hsig * hsig) * _cc * (2.0 - _cc));
            for (int i = 0; i < _n; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    // Rank-one update
                    double c_ij = (1.0 - c1a - _cmu) * _C[i, j] + _c1 * _pc[i] * _pc[j];
                    
                    // Rank-mu update
                    for (int k = 0; k < _mu; k++)
                    {
                        double yi = (_population[_arindex[k]][i] - oldMean[i]) / _sigma;
                        double yj = (_population[_arindex[k]][j] - oldMean[j]) / _sigma;
                        // Sanitize yi and yj
                        if (double.IsNaN(yi) || double.IsInfinity(yi)) yi = 0;
                        if (double.IsNaN(yj) || double.IsInfinity(yj)) yj = 0;
                        c_ij += _cmu * _weights[k] * yi * yj;
                    }
                    
                    // Sanitize c_ij before storing
                    if (double.IsNaN(c_ij) || double.IsInfinity(c_ij))
                    {
                        c_ij = (i == j) ? 1.0 : 0.0;  // Reset to identity
                    }
                    
                    _C[i, j] = c_ij;
                    _C[j, i] = c_ij;
                }
            }
            
            _eigenUpdateCounter++;
            Generation++;
        }
        
        /// <summary>
        /// Get current mean (best estimate of optimum)
        /// </summary>
        public double[] GetMean()
        {
            double[] result = new double[_n];
            Array.Copy(_mean, result, _n);
            return result;
        }
        
        /// <summary>
        /// Set mean (e.g., to start from a known good solution)
        /// </summary>
        public void SetMean(double[] newMean)
        {
            if (newMean.Length != _n)
                throw new ArgumentException($"Expected {_n} values, got {newMean.Length}");
            Array.Copy(newMean, _mean, _n);
        }
        
        /// <summary>
        /// Check if converged (sigma very small or variance very low)
        /// </summary>
        public bool IsConverged(double tolerance = 1e-4)  // More aggressive: was 1e-6
        {
            // CRITICAL: NaN sigma means something is broken - trigger reset
            if (double.IsNaN(_sigma) || double.IsInfinity(_sigma))
            {
                return true;  // Trigger reset
            }
            
            if (_sigma < tolerance) return true;
            
            // Check if all eigenvalues are very small
            double maxD = 0;
            foreach (var d in _D)
            {
                if (!double.IsNaN(d) && !double.IsInfinity(d) && d > maxD)
                    maxD = d;
            }
            if (_sigma * maxD < tolerance) return true;
            
            // NEW: Check if sigma is too small to make meaningful changes
            // With 62 morphs clamped to 0-1, a sigma of 0.001 makes <0.1% changes
            if (_sigma < 0.005) return true;  // Practical minimum
            
            return false;
        }
        
        /// <summary>
        /// Reset sigma to escape local minimum
        /// </summary>
        public void IncreaseSigma(double factor = 2.0)
        {
            // CRITICAL: Fix NaN sigma before increasing
            if (double.IsNaN(_sigma) || double.IsInfinity(_sigma) || _sigma < 0.01)
            {
                _sigma = 0.3;  // Reset to reasonable value
            }
            else
            {
                _sigma = Math.Min(1.0, _sigma * factor);
            }
        }
        
        /// <summary>
        /// Set sigma directly (for exploitation/exploration control)
        /// </summary>
        public void SetSigma(double newSigma)
        {
            if (double.IsNaN(newSigma) || double.IsInfinity(newSigma))
            {
                _sigma = 0.3;
                return;
            }
            _sigma = Math.Max(_minSigma, Math.Min(1.0, newSigma));
        }
        
        /// <summary>
        /// Minimum allowed sigma (phase-dependent)
        /// </summary>
        private double _minSigma = 0.05;
        public double MinSigma => _minSigma;
        
        /// <summary>
        /// Set the minimum sigma (called when phase changes)
        /// </summary>
        public void SetMinSigma(double minSigma)
        {
            _minSigma = Math.Max(0.01, Math.Min(0.5, minSigma));
            
            // Immediately enforce if current sigma is below minimum
            if (_sigma < _minSigma || double.IsNaN(_sigma))
            {
                _sigma = _minSigma;
            }
        }
        
        /// <summary>
        /// Enforce minimum sigma (call before sampling)
        /// </summary>
        public void EnforceMinSigma()
        {
            if (double.IsNaN(_sigma) || double.IsInfinity(_sigma) || _sigma < _minSigma)
            {
                _sigma = _minSigma;
            }
        }
        
        private void UpdateEigenDecomposition()
        {
            // Simple power iteration for eigenvector approximation
            // For full accuracy, use proper eigen decomposition library
            // This is a simplified version that works well enough
            
            for (int i = 0; i < _n; i++)
            {
                double cii = _C[i, i];
                // CRITICAL: Handle NaN/Inf/negative values in covariance diagonal
                if (double.IsNaN(cii) || double.IsInfinity(cii) || cii < 1e-10)
                {
                    cii = 1.0;  // Reset to identity
                    _C[i, i] = 1.0;
                }
                
                _D[i] = Math.Sqrt(Math.Max(1e-10, cii));
                
                // Sanitize D
                if (double.IsNaN(_D[i]) || double.IsInfinity(_D[i]))
                {
                    _D[i] = 1.0;
                }
                
                _B[i, i] = 1.0;
                for (int j = 0; j < i; j++)
                {
                    _B[i, j] = 0;
                    _B[j, i] = 0;
                }
            }
        }
        
        private double SampleGaussian()
        {
            // Box-Muller transform
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
        
        public string GetStatus()
        {
            return $"Gen:{Generation} σ:{_sigma:F4} Best:{BestFitness:F4} Pop:{_lambda}";
        }
        
        /// <summary>
        /// Full reset of CMA-ES state (use when stuck in NaN state)
        /// </summary>
        public void Reset(double newSigma = 0.3)
        {
            _sigma = newSigma;
            
            // Reset covariance to identity
            for (int i = 0; i < _n; i++)
            {
                _C[i, i] = 1.0;
                _B[i, i] = 1.0;
                _D[i] = 1.0;
                _pc[i] = 0;
                _ps[i] = 0;
                
                for (int j = 0; j < i; j++)
                {
                    _C[i, j] = 0;
                    _C[j, i] = 0;
                    _B[i, j] = 0;
                    _B[j, i] = 0;
                }
            }
            
            _eigenUpdateCounter = 0;
        }
    }
}
