using System;
using FaceLearner.ML.Modules.Core;

namespace FaceLearner.ML.Modules.Demographics.Age
{
    /// <summary>
    /// Age detection result
    /// </summary>
    public class AgeResult : IDetectionResult
    {
        /// <summary>Estimated age in years</summary>
        public int EstimatedAge { get; set; }
        
        /// <summary>Minimum likely age</summary>
        public int MinAge { get; set; }
        
        /// <summary>Maximum likely age</summary>
        public int MaxAge { get; set; }
        
        /// <summary>Age category</summary>
        public AgeCategory Category { get; set; }
        
        /// <summary>Is person likely a minor (under 18)?</summary>
        public bool IsMinor { get; set; }
        
        /// <summary>Is person elderly (60+)?</summary>
        public bool IsElderly { get; set; }
        
        public float Confidence { get; set; }
        public bool IsReliable => Confidence >= ConfidenceThresholds.Acceptable;
        public string Source { get; set; }
    }
    
    /// <summary>
    /// Age categories for quick classification
    /// </summary>
    public enum AgeCategory
    {
        Child,      // 0-12
        Teen,       // 13-17
        YoungAdult, // 18-29
        Adult,      // 30-44
        MiddleAge,  // 45-59
        Senior      // 60+
    }
    
    /// <summary>
    /// Age estimation from facial features.
    /// Uses wrinkle patterns, skin texture, and facial proportions.
    /// </summary>
    public class AgeModule
    {
        // Age ranges for categories
        private const int CHILD_MAX = 12;
        private const int TEEN_MAX = 17;
        private const int YOUNG_ADULT_MAX = 29;
        private const int ADULT_MAX = 44;
        private const int MIDDLE_AGE_MAX = 59;
        
        public AgeModule()
        {
        }
        
        /// <summary>
        /// Estimate age from facial landmarks
        /// Note: Landmark-based age is approximate - AI models are more accurate
        /// </summary>
        public AgeResult EstimateFromLandmarks(float[] landmarks)
        {
            var result = new AgeResult
            {
                Source = "AgeModule.Landmarks"
            };
            
            if (landmarks == null || landmarks.Length < 136)
            {
                result.EstimatedAge = 30;  // Default
                result.Confidence = 0.1f;
                return result;
            }
            
            // Landmark-based age estimation is weak
            // We can only detect major age groups
            
            // Face proportions change with age:
            // - Children have larger eyes relative to face
            // - Face elongates with age
            // - Jaw becomes more prominent with age
            
            bool isMediaPipe = landmarks.Length >= 936;
            
            float ageScore;
            if (isMediaPipe)
            {
                ageScore = EstimateFromMediaPipe(landmarks);
            }
            else
            {
                ageScore = EstimateFromDlib(landmarks);
            }
            
            // Convert score (0-1) to age estimate
            // 0 = child (8), 0.5 = adult (35), 1.0 = elderly (70)
            int estimatedAge = (int)(8 + ageScore * 62);
            
            result.EstimatedAge = estimatedAge;
            result.MinAge = Math.Max(1, estimatedAge - 10);
            result.MaxAge = Math.Min(100, estimatedAge + 10);
            result.IsMinor = estimatedAge < 18;
            result.IsElderly = estimatedAge >= 60;
            result.Category = GetCategory(estimatedAge);
            result.Confidence = 0.3f;  // Landmark-based is low confidence
            
            return result;
        }
        
        /// <summary>
        /// Create result from external model prediction
        /// </summary>
        public AgeResult FromModelPrediction(int predictedAge, float confidence, string modelName = "FairFace")
        {
            return new AgeResult
            {
                EstimatedAge = predictedAge,
                MinAge = Math.Max(1, predictedAge - 5),
                MaxAge = Math.Min(100, predictedAge + 5),
                IsMinor = predictedAge < 18,
                IsElderly = predictedAge >= 60,
                Category = GetCategory(predictedAge),
                Confidence = confidence,
                Source = $"AgeModule.{modelName}"
            };
        }
        
        private AgeCategory GetCategory(int age)
        {
            if (age <= CHILD_MAX) return AgeCategory.Child;
            if (age <= TEEN_MAX) return AgeCategory.Teen;
            if (age <= YOUNG_ADULT_MAX) return AgeCategory.YoungAdult;
            if (age <= ADULT_MAX) return AgeCategory.Adult;
            if (age <= MIDDLE_AGE_MAX) return AgeCategory.MiddleAge;
            return AgeCategory.Senior;
        }
        
        private float EstimateFromDlib(float[] landmarks)
        {
            // Use facial proportions for rough age estimate
            
            // Eye size relative to face (larger = younger)
            float leftEyeWidth = Math.Abs(landmarks[39 * 2] - landmarks[36 * 2]);
            float rightEyeWidth = Math.Abs(landmarks[45 * 2] - landmarks[42 * 2]);
            float avgEyeWidth = (leftEyeWidth + rightEyeWidth) / 2;
            
            float faceWidth = Math.Abs(landmarks[16 * 2] - landmarks[0 * 2]);
            float eyeRatio = avgEyeWidth / Math.Max(0.01f, faceWidth);
            
            // Children have eye ratio ~0.27, adults ~0.22
            float eyeScore = 1f - ((eyeRatio - 0.20f) / 0.10f);
            eyeScore = Math.Max(0f, Math.Min(1f, eyeScore));
            
            // Face elongation (longer = older/more developed)
            float faceHeight = Math.Abs(landmarks[8 * 2 + 1] - landmarks[27 * 2 + 1]);
            float faceAspect = faceHeight / Math.Max(0.01f, faceWidth);
            
            // Children have rounder faces (~1.0), adults more elongated (~1.3)
            float aspectScore = (faceAspect - 0.9f) / 0.5f;
            aspectScore = Math.Max(0f, Math.Min(1f, aspectScore));
            
            // Combine (rough estimate)
            return (eyeScore * 0.6f + aspectScore * 0.4f);
        }
        
        private float EstimateFromMediaPipe(float[] landmarks)
        {
            // Similar logic for MediaPipe 468 landmarks
            
            // Eye size
            float leftEyeWidth = Math.Abs(landmarks[133 * 2] - landmarks[33 * 2]);
            float rightEyeWidth = Math.Abs(landmarks[263 * 2] - landmarks[362 * 2]);
            float avgEyeWidth = (leftEyeWidth + rightEyeWidth) / 2;
            
            float faceWidth = Math.Abs(landmarks[454 * 2] - landmarks[234 * 2]);
            float eyeRatio = avgEyeWidth / Math.Max(0.01f, faceWidth);
            
            float eyeScore = 1f - ((eyeRatio - 0.18f) / 0.08f);
            eyeScore = Math.Max(0f, Math.Min(1f, eyeScore));
            
            // Face aspect
            float faceHeight = Math.Abs(landmarks[152 * 2 + 1] - landmarks[10 * 2 + 1]);
            float faceAspect = faceHeight / Math.Max(0.01f, faceWidth);
            
            float aspectScore = (faceAspect - 0.9f) / 0.5f;
            aspectScore = Math.Max(0f, Math.Min(1f, aspectScore));
            
            return (eyeScore * 0.6f + aspectScore * 0.4f);
        }
    }
}
