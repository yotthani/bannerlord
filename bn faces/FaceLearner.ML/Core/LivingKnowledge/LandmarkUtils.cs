using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Static utility methods for landmark calculations.
    /// Extracted from KnowledgeTree for Clean Code compliance (Single Responsibility).
    /// </summary>
    public static class LandmarkUtils
    {
        // === GENDER SCALING CONSTANTS (Bannerlord specific) ===
        // These values represent the bone scale used by Bannerlord for different genders
        // Male characters are rendered larger (1.05), females smaller (0.97)
        // This MUST be compensated when comparing photo landmarks to rendered landmarks!
        public const float MALE_SCALE = 1.05f;
        public const float FEMALE_SCALE = 0.97f;
        public const float NEUTRAL_SCALE = 1.0f;
        
        // OPTIMIZATION: Pre-computed landmark importance weights for 468-point MediaPipe
        // Higher weight = more important for face matching
        // Nose tip, chin, jaw contour: HIGH (distinctive)
        // Forehead edges, ears: LOW (often occluded)
        private static float[] _landmarkWeights468 = null;
        
        /// <summary>
        /// Apply gender-based scaling correction to landmarks.
        /// Call this on RENDERED landmarks to normalize them before comparison with photo landmarks.
        /// This compensates for Bannerlord's gender-based bone scaling.
        /// </summary>
        /// <param name="landmarks">Rendered landmarks (936 floats for 468 points)</param>
        /// <param name="isFemale">Gender of the rendered character</param>
        /// <returns>Normalized landmarks (as if scale was 1.0)</returns>
        public static float[] NormalizeGenderScale(float[] landmarks, bool isFemale)
        {
            if (landmarks == null) return null;
            
            float scale = isFemale ? FEMALE_SCALE : MALE_SCALE;
            float invScale = 1.0f / scale;
            
            var normalized = new float[landmarks.Length];
            for (int i = 0; i < landmarks.Length; i++)
            {
                normalized[i] = landmarks[i] * invScale;
            }
            
            return normalized;
        }
        
        /// <summary>
        /// Apply gender-based scaling to photo landmarks.
        /// Call this on PHOTO landmarks before comparing with rendered landmarks.
        /// </summary>
        /// <param name="landmarks">Photo landmarks</param>
        /// <param name="isFemale">Target gender for the Bannerlord character</param>
        /// <returns>Scaled landmarks matching expected engine scale</returns>
        public static float[] ApplyGenderScale(float[] landmarks, bool isFemale)
        {
            if (landmarks == null) return null;
            
            float scale = isFemale ? FEMALE_SCALE : MALE_SCALE;
            
            var scaled = new float[landmarks.Length];
            for (int i = 0; i < landmarks.Length; i++)
            {
                scaled[i] = landmarks[i] * scale;
            }
            
            return scaled;
        }
        
        /// <summary>
        /// Calculate match score with gender scale compensation.
        /// This should be used instead of CalculateMatchScore when comparing photo vs rendered.
        /// </summary>
        public static float CalculateMatchScoreGenderAware(float[] photoLandmarks, float[] renderedLandmarks, 
            float[] targetRatios, bool isFemale)
        {
            if (photoLandmarks == null || renderedLandmarks == null) return 0;
            
            // Normalize rendered landmarks to neutral scale
            var normalizedRendered = NormalizeGenderScale(renderedLandmarks, isFemale);
            
            // Now compare
            return targetRatios != null
                ? CalculateMatchScoreOptimized(normalizedRendered, photoLandmarks, targetRatios)
                : CalculateMatchScore(normalizedRendered, photoLandmarks);
        }
        

        private static float[] GetLandmarkWeights(int numPoints)
        {
            if (_landmarkWeights468 == null)
            {
                _landmarkWeights468 = new float[468];
                for (int i = 0; i < 468; i++)
                {
                    // Default weight
                    _landmarkWeights468[i] = 1.0f;

                    // MediaPipe regions (approximate)
                    // Nose (1, 2, 4, 5, 6, 19, 94, 98, 168, 195-199): HIGH
                    if (i <= 6 || (i >= 94 && i <= 98) || i == 168 || (i >= 195 && i <= 199))
                        _landmarkWeights468[i] = 1.5f;

                    // Jaw contour (172-215, 397-454): HIGH
                    if ((i >= 172 && i <= 215) || (i >= 397 && i <= 454))
                        _landmarkWeights468[i] = 1.4f;

                    // Eyes (33-133, 263-362): MEDIUM-HIGH
                    if ((i >= 33 && i <= 133) || (i >= 263 && i <= 362))
                        _landmarkWeights468[i] = 1.2f;

                    // Mouth (61-146, 291-324): MEDIUM
                    if ((i >= 61 && i <= 146) || (i >= 291 && i <= 324))
                        _landmarkWeights468[i] = 1.1f;

                    // Forehead top (10, 151, 9, 8, 107, 336): LOW (often covered by hair)
                    if (i == 10 || i == 151 || i == 9 || i == 8 || i == 107 || i == 336)
                        _landmarkWeights468[i] = 0.6f;
                }
            }
            return _landmarkWeights468;
        }
        
        /// <summary>
        /// Calculate Euclidean distance between two landmark arrays.
        /// </summary>

        public static float CalculateLandmarkDistance(float[] a, float[] b)
        {
            if (a == null || b == null) return float.MaxValue;
            
            int len = Math.Min(a.Length, b.Length);
            if (len == 0) return float.MaxValue;
            
            float sumSq = 0;
            for (int i = 0; i < len; i++)
            {
                float diff = a[i] - b[i];
                sumSq += diff * diff;
            }
            return (float)Math.Sqrt(sumSq / len);
        }
        
        /// <summary>
        /// Calculate WEIGHTED Euclidean distance - important landmarks count more.
        /// </summary>

        public static float CalculateLandmarkDistanceWeighted(float[] a, float[] b)
        {
            if (a == null || b == null) return float.MaxValue;
            
            int len = Math.Min(a.Length, b.Length);
            if (len == 0) return float.MaxValue;
            
            int numPoints = len / 2;
            var weights = GetLandmarkWeights(numPoints);
            
            float sumSq = 0;
            float totalWeight = 0;
            
            for (int i = 0; i < numPoints && i < weights.Length; i++)
            {
                float w = weights[i];
                int idx = i * 2;
                if (idx + 1 < len)
                {
                    float dx = a[idx] - b[idx];
                    float dy = a[idx + 1] - b[idx + 1];
                    sumSq += (dx * dx + dy * dy) * w;
                    totalWeight += w;
                }
            }
            
            return totalWeight > 0 ? (float)Math.Sqrt(sumSq / totalWeight) : float.MaxValue;
        }
        
        /// <summary>
        /// Calculate comprehensive face shape ratios from FaceMesh 468 landmarks.
        /// Returns 41 ratios covering all facial features for accurate shape matching.
        /// </summary>

        public static float[] CalculateFaceShapeRatios(float[] landmarks)
        {
            if (landmarks == null || landmarks.Length < 468 * 2)
                return new float[41];

            var ratios = new float[41];

            try
            {
                CalculateRatiosMediaPipe(landmarks, ratios);
            }
            catch
            {
                // Return zeros on any error
            }

            return ratios;
        }
        
        /// <summary>
        /// Detect if the face is smiling based on FaceMesh 468 landmarks.
        /// Returns a smile score 0-1 where >0.5 indicates significant smile.
        /// </summary>

        public static SmileInfo DetectSmile(float[] landmarks)
        {
            if (landmarks == null || landmarks.Length < 468 * 2)
                return new SmileInfo();

            return DetectSmileMediaPipe(landmarks);
        }

        private static SmileInfo DetectSmileMediaPipe(float[] lm)
        {
            // MediaPipe 468 landmarks:
            // 61, 291 = mouth corners (left, right)
            // 0 = upper lip center
            // 17 = lower lip center
            // 13 = upper lip inner
            // 14 = lower lip inner
            
            float X(int i) => lm[i * 2];
            float Y(int i) => lm[i * 2 + 1];
            float Dist(int a, int b) => (float)Math.Sqrt(Math.Pow(X(a) - X(b), 2) + Math.Pow(Y(a) - Y(b), 2));
            
            var result = new SmileInfo();
            
            try
            {
                float mouthWidth = Dist(61, 291);
                float mouthHeight = Dist(0, 17);
                float mouthAspect = mouthWidth / (mouthHeight + 0.001f);
                
                result.MouthAspect = mouthAspect;
                
                float cornerAvgY = (Y(61) + Y(291)) / 2f;
                float centerY = (Y(0) + Y(17)) / 2f;
                float cornerLift = centerY - cornerAvgY;
                float cornerLiftNorm = cornerLift / (mouthHeight + 0.001f);
                result.CornerLift = cornerLiftNorm;
                
                float innerMouthHeight = Dist(13, 14);
                float opennessRatio = innerMouthHeight / (mouthHeight + 0.001f);
                result.MouthOpenness = opennessRatio;
                
                float smileScore = 0f;
                if (mouthAspect > 3.0f)
                    smileScore += Math.Min((mouthAspect - 3.0f) / 3.0f, 1.0f) * 0.4f;
                if (cornerLiftNorm > 0.05f)
                    smileScore += Math.Min(cornerLiftNorm / 0.3f, 1.0f) * 0.4f;
                if (opennessRatio > 0.3f)
                    smileScore += Math.Min((opennessRatio - 0.3f) / 0.5f, 1.0f) * 0.2f;
                
                result.SmileScore = Math.Min(smileScore, 1.0f);
                result.IsSmiling = result.SmileScore > 0.3f;
                result.IsBigSmile = result.SmileScore > 0.6f;
            }
            catch { }
            
            return result;
        }
        
        /// <summary>
        /// Calculate 41 ratios from MediaPipe 468 landmarks
        /// </summary>

        private static void CalculateRatiosMediaPipe(float[] lm, float[] ratios)
        {
            // Helper to get point
            float X(int i) => lm[i * 2];
            float Y(int i) => lm[i * 2 + 1];
            float Dist(int a, int b) => (float)Math.Sqrt(Math.Pow(X(a) - X(b), 2) + Math.Pow(Y(a) - Y(b), 2));
            
            // === FACE DIMENSIONS ===
            float faceWidth = Dist(234, 454);          // Ear to ear
            float faceHeight = Dist(152, 10);          // Top to chin
            
            if (faceWidth < 0.01f) return;
            
            // [0-4] Overall face proportions
            ratios[0] = 1.0f;                                    // Face width baseline
            ratios[1] = faceHeight / faceWidth;                  // Face height ratio
            ratios[2] = Dist(10, 152) / faceWidth;               // Full height ratio
            ratios[3] = Dist(234, 152) / faceWidth;              // Left face length
            ratios[4] = Dist(454, 152) / faceWidth;              // Right face length
            
            // === JAW SHAPE ===
            // [5-10] Jaw contour (using silhouette points)
            ratios[5] = Dist(234, 132) / faceWidth;
            ratios[6] = Dist(132, 147) / faceWidth;
            ratios[7] = Dist(147, 187) / faceWidth;
            ratios[8] = Dist(187, 207) / faceWidth;
            ratios[9] = Dist(207, 152) / faceWidth;
            ratios[10] = Dist(152, 427) / faceWidth;
            
            // [11-14] Jaw angles
            ratios[11] = (X(147) - X(152)) / faceWidth;          // Jaw taper left
            ratios[12] = (X(376) - X(152)) / faceWidth;          // Jaw taper right
            ratios[13] = (Y(147) - Y(152)) / faceHeight;         // Jaw drop left
            ratios[14] = (Y(376) - Y(152)) / faceHeight;         // Jaw drop right
            
            // [15-17] Chin shape
            ratios[15] = Dist(172, 152) / faceWidth;             // Chin width left
            ratios[16] = Dist(397, 152) / faceWidth;             // Chin width right
            ratios[17] = Dist(175, 396) / faceWidth;             // Chin bottom width
            
            // === EYES ===
            // [18-23] Eye dimensions
            float leftEyeW = Dist(33, 133);
            float rightEyeW = Dist(362, 263);
            float leftEyeH = Dist(159, 145);
            float rightEyeH = Dist(386, 374);
            ratios[18] = leftEyeW / faceWidth;
            ratios[19] = rightEyeW / faceWidth;
            ratios[20] = leftEyeH / (leftEyeW + 0.001f);
            ratios[21] = rightEyeH / (rightEyeW + 0.001f);
            ratios[22] = Dist(133, 362) / faceWidth;             // Eye separation
            ratios[23] = Dist(33, 263) / faceWidth;              // Eye span
            
            // [24-25] Eye position
            float eyeCenterY = (Y(159) + Y(386)) / 2f;
            ratios[24] = (eyeCenterY - Y(10)) / faceHeight;
            ratios[25] = ((X(133) + X(33)) / 2f - X(234)) / faceWidth;
            
            // === EYEBROWS ===
            // [26-29] Brow shape
            ratios[26] = Dist(70, 107) / faceWidth;              // Left brow width
            ratios[27] = Dist(336, 300) / faceWidth;             // Right brow width
            ratios[28] = (Y(105) - eyeCenterY) / faceHeight;
            ratios[29] = (Y(334) - eyeCenterY) / faceHeight;
            
            // === NOSE ===
            // [30-33] Nose dimensions
            // FIX v2.7.3: Corrected landmark indices for nose width
            // v2.7.2 WRONG: Dist(48, 278) = 0.01-0.02 (philtrum area, too close!)
            // v2.7.3 CORRECT: Dist(64, 294) = alar base (outer nostril edges)
            ratios[30] = Dist(64, 294) / faceWidth;                 // Nose width (alar base)
            ratios[31] = Dist(168, 1) / faceHeight;                 // Nose bridge length
            ratios[32] = Dist(129, 358) / faceWidth;                // Nose tip width (wings)
            ratios[33] = (Y(1) - Y(168)) / faceHeight;              // Nose length
            
            // === MOUTH ===
            // [34-38] Mouth dimensions
            float mouthW = Dist(61, 291);
            float mouthH = Dist(0, 17);
            ratios[34] = mouthW / faceWidth;
            ratios[35] = mouthH / (mouthW + 0.001f);
            ratios[36] = Dist(39, 269) / (mouthW + 0.001f);      // Upper lip width
            ratios[37] = Dist(181, 405) / (mouthW + 0.001f);     // Lower lip width
            ratios[38] = Dist(0, 13) / (mouthH + 0.001f);        // Upper lip thickness
            
            // === FACE THIRDS ===
            // [39-40] Facial proportions
            float browY = (Y(105) + Y(334)) / 2f;
            float noseBottomY = Y(2);
            float chinY = Y(152);
            float fullH = Math.Abs(chinY - Y(10));
            ratios[39] = Math.Abs(noseBottomY - browY) / (fullH + 0.001f);
            ratios[40] = Math.Abs(chinY - noseBottomY) / (fullH + 0.001f);
        }
        
        /// <summary>
        /// Get weights for each of the 41 ratios.
        /// Higher weights = more important for face shape matching.
        /// </summary>

        public static float[] GetRatioWeights()
        {
            // v2.9.2: More balanced weights - don't over-weight any single feature
            return new float[]
            {
                // [0-4] Overall face - moderate importance
                0.8f,   // [0] Face aspect ratio baseline
                1.5f,   // [1] Face height ratio
                1.2f,   // [2] Full height ratio
                1.0f,   // [3] Left jaw length
                1.0f,   // [4] Right jaw length
                
                // [5-10] Jaw contour - HIGH importance (round vs sharp)
                1.5f, 1.8f, 2.0f, 2.2f, 2.5f, 2.5f,
                
                // [11-14] Jaw angles - VERY HIGH (V-shape vs U-shape)
                3.0f, 3.0f, 2.5f, 2.5f,
                
                // [15-17] Chin shape - HIGH
                2.5f, 2.5f, 3.0f,
                
                // [18-25] Eyes - moderate
                1.0f, 1.0f, 0.8f, 0.8f, 1.2f, 1.0f, 0.6f, 0.5f,
                
                // [26-29] Eyebrows - lower
                0.6f, 0.6f, 0.7f, 0.7f,
                
                // [30-33] Nose - moderate (not as distinctive as thought)
                1.2f, 1.0f, 1.0f, 1.2f,
                
                // [34-38] Mouth - moderate  
                1.0f, 0.8f, 0.6f, 0.6f, 0.7f,
                
                // [39-40] Face thirds - moderate
                1.0f, 1.2f
            };
        }
        
        /// <summary>
        /// Calculate shape distance between two faces using all 41 ratios with weights.
        /// </summary>

        public static float CalculateShapeDistance(float[] landmarksA, float[] landmarksB)
        {
            var ratiosA = CalculateFaceShapeRatios(landmarksA);
            var ratiosB = CalculateFaceShapeRatios(landmarksB);
            var weights = GetRatioWeights();
            
            float sumSq = 0;
            float totalWeight = 0;
            for (int i = 0; i < ratiosA.Length && i < weights.Length; i++)
            {
                float diff = ratiosA[i] - ratiosB[i];
                sumSq += diff * diff * weights[i];
                totalWeight += weights[i];
            }
            return totalWeight > 0 ? (float)Math.Sqrt(sumSq / totalWeight) : 0f;
        }
        
        /// <summary>
        /// OPTIMIZED: Calculate shape distance using pre-computed target ratios.
        /// </summary>

        public static float CalculateShapeDistanceOptimized(float[] currentLandmarks, float[] targetRatios)
        {
            var currentRatios = CalculateFaceShapeRatios(currentLandmarks);
            var weights = GetRatioWeights();
            
            float sumSq = 0;
            float totalWeight = 0;
            for (int i = 0; i < currentRatios.Length && i < weights.Length && i < targetRatios.Length; i++)
            {
                float diff = currentRatios[i] - targetRatios[i];
                sumSq += diff * diff * weights[i];
                totalWeight += weights[i];
            }
            return totalWeight > 0 ? (float)Math.Sqrt(sumSq / totalWeight) : 0f;
        }
        
        /// <summary>
        /// OPTIMIZED: Calculate match score using pre-computed target ratios.
        /// </summary>

        public static float CalculateMatchScoreOptimized(float[] current, float[] target, float[] targetRatios, bool useWeightedLandmarks = true)
        {
            if (current == null || target == null) return 0;
            
            // Use weighted distance for better accuracy on distinctive features
            float landmarkDist = useWeightedLandmarks 
                ? CalculateLandmarkDistanceWeighted(current, target)
                : CalculateLandmarkDistance(current, target);
            float shapeDist = CalculateShapeDistanceOptimized(current, targetRatios);
            
            // Convert distances to scores (0-1)
            // Sharp decay for shape differences
            float landmarkScore = (float)Math.Exp(-landmarkDist * 10.0);
            float shapeScore = (float)Math.Exp(-shapeDist * 12.0);  // Even sharper for 41 ratios
            
            // v2.9.2: Balanced 50/50 weighting
            // Ratios capture PROPORTIONS but lose absolute SIZE
            // Landmarks capture actual POSITIONS including size
            // Both are needed for good visual similarity
            return landmarkScore * 0.50f + shapeScore * 0.50f;
        }
        
        /// <summary>
        /// Extend landmarks with shape ratios for better matching.
        /// </summary>

        public static float[] ExtendLandmarksWithRatios(float[] landmarks)
        {
            var ratios = CalculateFaceShapeRatios(landmarks);
            
            // Create extended array: original landmarks + ratios (weighted)
            var extended = new float[landmarks.Length + ratios.Length];
            Array.Copy(landmarks, extended, landmarks.Length);
            
            // Add ratios with weight (they're more important for face type)
            for (int i = 0; i < ratios.Length; i++)
            {
                extended[landmarks.Length + i] = ratios[i] * 2.0f;  // Weight ratios more
            }
            
            return extended;
        }
        
        /// <summary>
        /// Calculate match score between current and target landmarks.
        /// Returns 0-1 where 1 is perfect match.
        /// Uses all 41 shape ratios for accurate face type matching.
        /// </summary>

        public static float CalculateMatchScore(float[] current, float[] target)
        {
            if (current == null || target == null) return 0;
            
            float landmarkDist = CalculateLandmarkDistance(current, target);
            float shapeDist = CalculateShapeDistance(current, target);
            
            // Convert distances to scores (0-1)
            // Sharp decay for 41-ratio shape matching
            float landmarkScore = (float)Math.Exp(-landmarkDist * 10.0);
            float shapeScore = (float)Math.Exp(-shapeDist * 12.0);
            
            // v2.9.2: Balanced 50/50 weighting
            return landmarkScore * 0.50f + shapeScore * 0.50f;
        }
        
        /// <summary>
        /// Calculate detailed score breakdown.
        /// Uses all 41 shape ratios.
        /// </summary>

        public static (float total, float landmarkScore, float shapeScore) CalculateDetailedScore(
            float[] current, float[] target)
        {
            if (current == null || target == null) return (0, 0, 0);
            
            float landmarkDist = CalculateLandmarkDistance(current, target);
            float shapeDist = CalculateShapeDistance(current, target);
            
            // Sharp decay for 41-ratio shape matching
            float landmarkScore = (float)Math.Exp(-landmarkDist * 10.0);
            float shapeScore = (float)Math.Exp(-shapeDist * 12.0);
            float total = landmarkScore * 0.50f + shapeScore * 0.50f;  // v2.9.2: Balanced
            
            return (total, landmarkScore, shapeScore);
        }
        
        /// <summary>
        /// Get detailed distance metrics between landmark arrays.
        /// </summary>

        public static (float rmse, float maxDiff, int worstIdx) GetDetailedDistance(float[] a, float[] b)
        {
            if (a == null || b == null) return (float.MaxValue, float.MaxValue, -1);
            
            int len = Math.Min(a.Length, b.Length);
            if (len == 0) return (float.MaxValue, float.MaxValue, -1);
            
            float sumSq = 0;
            float maxDiff = 0;
            int worstIdx = 0;
            
            for (int i = 0; i < len; i++)
            {
                float diff = Math.Abs(a[i] - b[i]);
                sumSq += diff * diff;
                if (diff > maxDiff)
                {
                    maxDiff = diff;
                    worstIdx = i;
                }
            }
            
            float rmse = (float)Math.Sqrt(sumSq / len);
            return (rmse, maxDiff, worstIdx);
        }
        
        /// <summary>
        /// Get landmark region name for debugging.
        /// </summary>

        public static string GetLandmarkRegion(int index)
        {
            // MediaPipe face mesh regions (approximate)
            int pointIndex = index / 2;  // Convert coordinate index to point index
            
            if (pointIndex <= 10) return "Forehead";
            if (pointIndex >= 33 && pointIndex <= 133) return "LeftEye";
            if (pointIndex >= 263 && pointIndex <= 362) return "RightEye";
            if (pointIndex >= 0 && pointIndex <= 4) return "Nose";
            if (pointIndex >= 61 && pointIndex <= 146) return "Mouth";
            if (pointIndex >= 172 && pointIndex <= 234) return "LeftJaw";
            if (pointIndex >= 397 && pointIndex <= 454) return "RightJaw";
            
            return "Face";
        }
    }
    
    /// <summary>
    /// Information about smile detection
    /// </summary>
    public class SmileInfo
    {

        public float SmileScore { get; set; } = 0f;
        public bool IsSmiling { get; set; } = false;
        public bool IsBigSmile { get; set; } = false;
        
        public float MouthAspect { get; set; } = 0f;
        public float CornerLift { get; set; } = 0f;
        public float MouthOpenness { get; set; } = 0f;
        
        /// <summary>
        /// Get weight multiplier for mouth-related features.
        /// Returns 1.0 for neutral, 0.5 for smile, 0.2 for big smile.
        /// </summary>
        public float GetMouthFeatureWeight()
        {
            if (IsBigSmile) return 0.2f;
            if (IsSmiling) return 0.5f;
            return 1.0f;
        }
        
        /// <summary>
        /// Get weight multiplier for face shape features (affected by smile).
        /// Returns 1.0 for neutral, 0.7 for smile, 0.5 for big smile.
        /// </summary>
        public float GetFaceShapeWeight()
        {
            if (IsBigSmile) return 0.5f;
            if (IsSmiling) return 0.7f;
            return 1.0f;
        }
        
        public override string ToString()
        {
            if (IsBigSmile) return $"BigSmile({SmileScore:F2})";
            if (IsSmiling) return $"Smile({SmileScore:F2})";
            return "Neutral";
        }
    }
}
