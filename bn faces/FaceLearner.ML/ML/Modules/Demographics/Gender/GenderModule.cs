using System;
using System.Drawing;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.FaceParsing;

// Alias to avoid namespace conflict (Gender folder vs Gender enum)
using GenderEnum = FaceLearner.ML.Modules.Core.Gender;

namespace FaceLearner.ML.Modules.Demographics.Gender
{
    /// <summary>
    /// Detailed gender detection result with confidence breakdown
    /// </summary>
    public class GenderResult : IDetectionResult
    {
        /// <summary>Detected gender</summary>
        public GenderEnum DetectedGender { get; set; }
        
        /// <summary>Is the person likely female?</summary>
        public bool IsFemale { get; set; }
        
        /// <summary>Combined detection confidence</summary>
        public float Confidence { get; set; }
        
        /// <summary>Landmark-based gender score (0=male, 1=female)</summary>
        public float LandmarkScore { get; set; }
        
        /// <summary>AI model gender score (0=male, 1=female)</summary>
        public float ModelScore { get; set; }
        
        /// <summary>Facial hair detection result</summary>
        public bool HasFacialHair { get; set; }
        
        /// <summary>Facial hair coverage (if detected)</summary>
        public float FacialHairCoverage { get; set; }
        
        public bool IsReliable => Confidence >= ConfidenceThresholds.High;
        public string Source { get; set; }
        
        /// <summary>
        /// Detailed breakdown for debugging
        /// </summary>
        public GenderBreakdown Breakdown { get; set; }
    }
    
    /// <summary>
    /// Detailed scores used in gender detection
    /// </summary>
    public class GenderBreakdown
    {
        // Landmark-based features
        public float JawWidthScore { get; set; }      // Wider = more male
        public float BrowProminence { get; set; }     // More prominent = male
        public float EyeDistance { get; set; }        // Wider relative = female
        public float ChinPointedness { get; set; }    // Pointier = female
        public float NoseWidth { get; set; }          // Wider = male
        public float LipFullness { get; set; }        // Fuller = female
        public float FaceAspectRatio { get; set; }    // Longer = male
        
        // AI model scores (if available)
        public float FairFaceScore { get; set; }
        public float FairFaceConfidence { get; set; }
        public float InsightFaceScore { get; set; }
        public float InsightFaceConfidence { get; set; }
    }
    
    /// <summary>
    /// Gender detection using multiple signals:
    /// 1. Facial landmarks (jaw width, brow prominence, etc.)
    /// 2. Face parsing (beard detection)
    /// 3. AI models (FairFace, InsightFace)
    /// </summary>
    public class GenderModule
    {
        // Calibrated thresholds for landmark-based detection
        private const float JAW_WIDTH_MALE_THRESHOLD = 0.85f;
        private const float BROW_PROMINENCE_MALE_THRESHOLD = 0.15f;
        private const float CHIN_POINTEDNESS_FEMALE_THRESHOLD = 0.6f;
        
        // Weight for combining signals
        private const float LANDMARK_WEIGHT = 0.3f;
        private const float MODEL_WEIGHT = 0.5f;
        private const float BEARD_WEIGHT = 0.2f;  // Beard = definitely male
        
        public GenderModule()
        {
        }
        
        /// <summary>
        /// Detect gender from landmarks and optional face parsing
        /// </summary>
        public GenderResult Detect(float[] landmarks, FaceParsingResult parsing = null)
        {
            var result = new GenderResult
            {
                Source = "GenderModule",
                Breakdown = new GenderBreakdown()
            };
            
            // 1. Analyze landmarks
            float landmarkScore = AnalyzeLandmarks(landmarks, result.Breakdown);
            result.LandmarkScore = landmarkScore;
            
            // 2. Check for facial hair (strong male indicator)
            bool hasBeard = false;
            float beardCoverage = 0f;
            
            if (parsing?.FacialHair != null)
            {
                hasBeard = parsing.FacialHair.HasFacialHair && 
                          parsing.FacialHair.BeardCoverage > 0.05f;
                beardCoverage = parsing.FacialHair.BeardCoverage;
            }
            
            result.HasFacialHair = hasBeard;
            result.FacialHairCoverage = beardCoverage;
            
            // 3. Combine signals
            float combinedScore;
            float confidence;
            
            if (hasBeard && beardCoverage > 0.1f)
            {
                // Beard detected = male with high confidence
                combinedScore = 0f;  // 0 = male
                confidence = Math.Min(0.95f, 0.7f + beardCoverage);
            }
            else
            {
                // Use landmark score
                combinedScore = landmarkScore;
                
                // Confidence based on how extreme the score is
                float deviation = Math.Abs(combinedScore - 0.5f) * 2;  // 0-1
                confidence = 0.4f + deviation * 0.4f;  // 0.4-0.8 range
            }
            
            // Final decision
            result.IsFemale = combinedScore > 0.5f;
            result.DetectedGender = result.IsFemale ? GenderEnum.Female : GenderEnum.Male;
            result.Confidence = confidence;
            
            return result;
        }
        
        /// <summary>
        /// Detect gender including AI model scores
        /// </summary>
        public GenderResult DetectWithModel(
            float[] landmarks, 
            FaceParsingResult parsing,
            bool modelIsFemale,
            float modelConfidence,
            string modelSource = "FairFace")
        {
            var result = Detect(landmarks, parsing);
            
            // Incorporate model score
            result.ModelScore = modelIsFemale ? 1f : 0f;
            
            if (modelSource == "FairFace")
            {
                result.Breakdown.FairFaceScore = result.ModelScore;
                result.Breakdown.FairFaceConfidence = modelConfidence;
            }
            else
            {
                result.Breakdown.InsightFaceScore = result.ModelScore;
                result.Breakdown.InsightFaceConfidence = modelConfidence;
            }
            
            // Recombine with model
            if (result.HasFacialHair && result.FacialHairCoverage > 0.1f)
            {
                // Beard still overrides
                return result;
            }
            
            // Weighted combination
            float landmarkWeight = LANDMARK_WEIGHT;
            float modelWeight = MODEL_WEIGHT * modelConfidence;  // Scale by model confidence
            float totalWeight = landmarkWeight + modelWeight;
            
            float combinedScore = (result.LandmarkScore * landmarkWeight + 
                                  result.ModelScore * modelWeight) / totalWeight;
            
            result.IsFemale = combinedScore > 0.5f;
            result.DetectedGender = result.IsFemale ? GenderEnum.Female : GenderEnum.Male;
            
            // Confidence from agreement
            bool agreement = (result.LandmarkScore > 0.5f) == modelIsFemale;
            result.Confidence = agreement 
                ? Math.Min(0.95f, (result.Confidence + modelConfidence) / 2 + 0.1f)
                : Math.Max(0.3f, modelConfidence - 0.2f);
            
            return result;
        }
        
        /// <summary>
        /// Analyze landmarks for gender indicators
        /// Returns 0=male, 1=female
        /// </summary>
        private float AnalyzeLandmarks(float[] landmarks, GenderBreakdown breakdown)
        {
            if (landmarks == null || landmarks.Length < 136)
            {
                return 0.5f;  // Unknown
            }
            
            // MediaPipe uses 468 landmarks × 2 coords = 936 values
            // dlib uses 68 landmarks × 2 coords = 136 values
            // We'll support both
            
            bool isMediaPipe = landmarks.Length >= 936;
            
            float score = 0.5f;  // Start neutral
            int factors = 0;
            
            if (isMediaPipe)
            {
                // MediaPipe landmark analysis
                score = AnalyzeMediaPipeLandmarks(landmarks, breakdown);
            }
            else
            {
                // dlib 68-point landmark analysis
                score = AnalyzeDlibLandmarks(landmarks, breakdown);
            }
            
            return Math.Max(0f, Math.Min(1f, score));
        }
        
        /// <summary>
        /// Analyze 68-point dlib landmarks
        /// </summary>
        private float AnalyzeDlibLandmarks(float[] landmarks, GenderBreakdown breakdown)
        {
            // Key indices for dlib 68 landmarks:
            // 0-16: Jaw contour
            // 17-21: Left eyebrow
            // 22-26: Right eyebrow
            // 27-35: Nose
            // 36-41: Left eye
            // 42-47: Right eye
            // 48-67: Mouth
            
            float score = 0.5f;
            int factors = 0;
            
            // Jaw width (points 0 and 16)
            float jawWidth = Math.Abs(landmarks[16 * 2] - landmarks[0 * 2]);
            float faceHeight = Math.Abs(landmarks[8 * 2 + 1] - landmarks[27 * 2 + 1]);
            float jawRatio = jawWidth / Math.Max(0.01f, faceHeight);
            
            // Wide jaw = more male (typical male: 0.85+, female: 0.75-)
            float jawScore = jawRatio > JAW_WIDTH_MALE_THRESHOLD ? 0.3f : 0.7f;
            breakdown.JawWidthScore = jawRatio;
            score += (jawScore - 0.5f) * 0.3f;
            factors++;
            
            // Chin pointedness (point 8 relative to points 6,10)
            float chinX = landmarks[8 * 2];
            float chinY = landmarks[8 * 2 + 1];
            float leftJaw = landmarks[6 * 2 + 1];
            float rightJaw = landmarks[10 * 2 + 1];
            float jawLineY = (leftJaw + rightJaw) / 2;
            float chinExtension = chinY - jawLineY;
            
            // More pointed chin = more female
            float chinPointScore = chinExtension > 0.05f ? 0.65f : 0.35f;
            breakdown.ChinPointedness = chinExtension;
            score += (chinPointScore - 0.5f) * 0.2f;
            factors++;
            
            // Eye distance relative to face width
            float leftEyeX = (landmarks[36 * 2] + landmarks[39 * 2]) / 2;
            float rightEyeX = (landmarks[42 * 2] + landmarks[45 * 2]) / 2;
            float eyeDistance = Math.Abs(rightEyeX - leftEyeX);
            float eyeRatio = eyeDistance / Math.Max(0.01f, jawWidth);
            
            // Wider eye spacing relative to face = more female
            float eyeScore = eyeRatio > 0.38f ? 0.6f : 0.4f;
            breakdown.EyeDistance = eyeRatio;
            score += (eyeScore - 0.5f) * 0.15f;
            factors++;
            
            // Nose width (points 31-35)
            float noseWidth = Math.Abs(landmarks[35 * 2] - landmarks[31 * 2]);
            float noseRatio = noseWidth / Math.Max(0.01f, jawWidth);
            
            // Wider nose = more male
            float noseScore = noseRatio > 0.25f ? 0.35f : 0.65f;
            breakdown.NoseWidth = noseRatio;
            score += (noseScore - 0.5f) * 0.15f;
            factors++;
            
            // Lip fullness (mouth height)
            float upperLipTop = landmarks[51 * 2 + 1];
            float upperLipBot = landmarks[62 * 2 + 1];
            float lowerLipTop = landmarks[66 * 2 + 1];
            float lowerLipBot = landmarks[57 * 2 + 1];
            float lipHeight = Math.Abs(lowerLipBot - upperLipTop);
            float lipRatio = lipHeight / Math.Max(0.01f, faceHeight);
            
            // Fuller lips = more female
            float lipScore = lipRatio > 0.12f ? 0.6f : 0.4f;
            breakdown.LipFullness = lipRatio;
            score += (lipScore - 0.5f) * 0.1f;
            factors++;
            
            // Face aspect ratio
            float faceAspect = faceHeight / Math.Max(0.01f, jawWidth);
            breakdown.FaceAspectRatio = faceAspect;
            
            // Longer face = more male
            float aspectScore = faceAspect > 1.3f ? 0.4f : 0.6f;
            score += (aspectScore - 0.5f) * 0.1f;
            factors++;
            
            return score;
        }
        
        /// <summary>
        /// Analyze MediaPipe 468 landmarks
        /// </summary>
        private float AnalyzeMediaPipeLandmarks(float[] landmarks, GenderBreakdown breakdown)
        {
            // MediaPipe face mesh key points:
            // 10: Forehead center
            // 152: Chin
            // 234: Right ear
            // 454: Left ear
            // 33, 133: Left eye corners
            // 362, 263: Right eye corners
            // 1, 4: Nose tip/bridge
            // 61, 291: Mouth corners
            
            float score = 0.5f;
            
            // Face width (ear to ear approximation)
            float faceWidth = Math.Abs(landmarks[454 * 2] - landmarks[234 * 2]);
            
            // Face height (forehead to chin)
            float faceHeight = Math.Abs(landmarks[152 * 2 + 1] - landmarks[10 * 2 + 1]);
            
            // Jaw width at jawline
            float jawWidth = Math.Abs(landmarks[172 * 2] - landmarks[397 * 2]);
            float jawRatio = jawWidth / Math.Max(0.01f, faceWidth);
            
            breakdown.JawWidthScore = jawRatio;
            float jawScore = jawRatio > 0.85f ? 0.35f : 0.65f;
            score += (jawScore - 0.5f) * 0.3f;
            
            // Eye spacing
            float leftEyeX = landmarks[33 * 2];
            float rightEyeX = landmarks[263 * 2];
            float eyeDistance = Math.Abs(rightEyeX - leftEyeX);
            float eyeRatio = eyeDistance / Math.Max(0.01f, faceWidth);
            
            breakdown.EyeDistance = eyeRatio;
            float eyeScore = eyeRatio > 0.35f ? 0.6f : 0.4f;
            score += (eyeScore - 0.5f) * 0.2f;
            
            // Nose width
            float noseWidth = Math.Abs(landmarks[129 * 2] - landmarks[358 * 2]);
            float noseRatio = noseWidth / Math.Max(0.01f, faceWidth);
            
            breakdown.NoseWidth = noseRatio;
            float noseScore = noseRatio > 0.22f ? 0.35f : 0.65f;
            score += (noseScore - 0.5f) * 0.2f;
            
            // Face aspect ratio
            float aspect = faceHeight / Math.Max(0.01f, faceWidth);
            breakdown.FaceAspectRatio = aspect;
            float aspectScore = aspect > 1.25f ? 0.4f : 0.6f;
            score += (aspectScore - 0.5f) * 0.15f;
            
            // Mouth width
            float mouthWidth = Math.Abs(landmarks[291 * 2] - landmarks[61 * 2]);
            float mouthRatio = mouthWidth / Math.Max(0.01f, faceWidth);
            
            // Wider mouth relative to face = more male
            float mouthScore = mouthRatio > 0.38f ? 0.4f : 0.6f;
            score += (mouthScore - 0.5f) * 0.15f;
            
            return Math.Max(0f, Math.Min(1f, score));
        }
    }
}
