using System;
using System.Collections.Generic;
using System.Drawing;

namespace FaceLearner.ML.Modules.Core
{
    #region Base Interfaces
    
    /// <summary>
    /// Base interface for all detection results.
    /// Every module output must have a confidence score.
    /// </summary>
    public interface IDetectionResult
    {
        /// <summary>
        /// Calibrated confidence (0-1). 
        /// 0 = no information, 1 = absolutely certain.
        /// Note: This is CALIBRATED, not raw softmax probability!
        /// </summary>
        float Confidence { get; }
        
        /// <summary>
        /// Is the result reliable enough to use?
        /// Typically Confidence >= 0.35
        /// </summary>
        bool IsReliable { get; }
        
        /// <summary>
        /// Which detector/method produced this result
        /// </summary>
        string Source { get; }
    }
    
    /// <summary>
    /// Base interface for all detectors.
    /// A detector takes an image and produces a typed result.
    /// </summary>
    public interface IDetector<TResult> : IDisposable where TResult : IDetectionResult
    {
        /// <summary>
        /// Unique name of this detector
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// How much we trust this detector (0-1).
        /// Used for weighted voting when combining multiple detectors.
        /// </summary>
        float ReliabilityWeight { get; }
        
        /// <summary>
        /// Is the detector ready to use?
        /// </summary>
        bool IsLoaded { get; }
        
        /// <summary>
        /// Load/initialize the detector
        /// </summary>
        bool Load(string modelPath);
        
        /// <summary>
        /// Detect from bitmap
        /// </summary>
        TResult Detect(Bitmap image);
    }
    
    /// <summary>
    /// Interface for modules that orchestrate multiple detectors.
    /// A module combines multiple detector signals into one reliable result.
    /// </summary>
    public interface IModule<TResult> : IDisposable where TResult : IDetectionResult
    {
        /// <summary>
        /// Module name
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Is the module ready?
        /// </summary>
        bool IsReady { get; }
        
        /// <summary>
        /// Initialize the module with model paths
        /// </summary>
        bool Initialize(string basePath);
        
        /// <summary>
        /// Analyze image and return result
        /// </summary>
        TResult Analyze(Bitmap image);
    }
    
    #endregion
    
    #region Confidence System
    
    /// <summary>
    /// Centralized confidence thresholds.
    /// All modules use the same thresholds for consistency.
    /// </summary>
    public static class ConfidenceThresholds
    {
        /// <summary>Highly reliable result</summary>
        public const float High = 0.70f;
        
        /// <summary>Reliable enough to use</summary>
        public const float Reliable = 0.55f;
        
        /// <summary>Acceptable with caution</summary>
        public const float Acceptable = 0.35f;
        
        /// <summary>Uncertain, use fallbacks</summary>
        public const float Uncertain = 0.20f;
        
        /// <summary>Essentially no signal</summary>
        public const float NoSignal = 0.10f;
    }
    
    /// <summary>
    /// Calibrates raw probabilities to meaningful confidence scores.
    /// 
    /// Problem: Raw softmax gives 0.57 for Female, 0.43 for Male.
    ///          That's essentially a coin flip, NOT "57% confident Female"!
    /// 
    /// Solution: Calibrate so that 0.50 → 0.00 (no info), 0.90 → 0.80 (confident)
    /// </summary>
    public static class ConfidenceCalibrator
    {
        /// <summary>
        /// Convert raw softmax probability to calibrated confidence.
        /// </summary>
        /// <param name="probability">Raw probability (0.5-1.0 for winning class)</param>
        /// <returns>Calibrated confidence (0-1)</returns>
        public static float FromProbability(float probability)
        {
            // Clamp to valid range
            probability = Math.Max(0.5f, Math.Min(1.0f, probability));
            
            // Distance from 0.5 (uncertain)
            float deviation = probability - 0.5f;  // 0 to 0.5
            float normalized = deviation * 2f;     // 0 to 1
            
            // Non-linear: small deviations are less meaningful
            // 0.57 → 0.14 (not confident!)
            // 0.70 → 0.40 (somewhat confident)
            // 0.90 → 0.80 (confident)
            // 0.99 → 0.98 (very confident)
            return (float)Math.Pow(normalized, 0.7);
        }
        
        /// <summary>
        /// Combine multiple detection results using weighted voting.
        /// </summary>
        public static (T result, float confidence) CombineVotes<T>(
            IEnumerable<(T value, float confidence, float weight)> votes,
            Func<T, T, bool> areEqual)
        {
            var voteList = new List<(T value, float confidence, float weight)>(votes);
            
            if (voteList.Count == 0)
                return (default, 0f);
            
            if (voteList.Count == 1)
                return (voteList[0].value, voteList[0].confidence * voteList[0].weight);
            
            // Group by value and sum weighted confidences
            var groups = new Dictionary<int, (T value, float totalScore, int count)>();
            int groupId = 0;
            
            foreach (var vote in voteList)
            {
                bool found = false;
                foreach (var kvp in groups)
                {
                    if (areEqual(kvp.Value.value, vote.value))
                    {
                        var current = groups[kvp.Key];
                        groups[kvp.Key] = (current.value, 
                            current.totalScore + vote.confidence * vote.weight,
                            current.count + 1);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    groups[groupId++] = (vote.value, vote.confidence * vote.weight, 1);
                }
            }
            
            // Find winner
            T winner = default;
            float winnerScore = 0;
            float totalScore = 0;
            
            foreach (var group in groups.Values)
            {
                totalScore += group.totalScore;
                if (group.totalScore > winnerScore)
                {
                    winnerScore = group.totalScore;
                    winner = group.value;
                }
            }
            
            // Confidence is how much winner dominates
            float confidence = totalScore > 0 ? winnerScore / totalScore : 0;
            
            // Reduce confidence if there was disagreement
            if (groups.Count > 1)
            {
                confidence *= 0.8f;
            }
            
            return (winner, Math.Min(1f, confidence));
        }
    }
    
    #endregion
    
    #region Common Enums
    
    public enum Gender
    {
        Male,
        Female,
        Uncertain
    }
    
    public enum FaceShape
    {
        Oval,
        Round,
        Square,
        Heart,
        Oblong,
        Diamond,
        Unknown
    }
    
    /// <summary>
    /// Face features for per-feature scoring
    /// </summary>
    public enum FaceFeature
    {
        FaceShape,
        Eyes,
        Nose,
        Mouth,
        Jaw,
        Eyebrows,
        Forehead,
        Cheeks
    }
    
    #endregion
    
    #region High-Level Interfaces
    
    /// <summary>
    /// Main face analysis interface.
    /// Combines landmark detection, face parsing, proportions, and demographics.
    /// </summary>
    public interface IFaceAnalyzer : IDisposable
    {
        /// <summary>Is the analyzer ready?</summary>
        bool IsReady { get; }
        
        /// <summary>Compare raw landmarks</summary>
        float CompareLandmarks(float[] target, float[] current);
    }
    
    #endregion
}
