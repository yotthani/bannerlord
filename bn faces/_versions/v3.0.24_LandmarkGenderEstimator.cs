using System;
using System.Collections.Generic;

namespace FaceLearner.ML
{
    /// <summary>
    /// Gender estimation based purely on facial landmark proportions.
    /// Uses sexual dimorphism in facial structure:
    /// - Men: wider jaw, prominent brow, larger nose, squarer face
    /// - Women: rounder face, fuller lips, larger eyes relative to face
    /// 
    /// This is more reliable than AI models for edge cases and doesn't
    /// depend on names or metadata.
    /// </summary>
    public static class LandmarkGenderEstimator
    {
        /// <summary>
        /// Estimate gender from 68-point landmarks (or 468 converted to 68)
        /// Returns: (isFemale, confidence 0-1, featureScores for debugging)
        /// </summary>
        public static (bool isFemale, float confidence, Dictionary<string, float> scores)
            EstimateGender(float[] landmarks, bool beardDetected = false)
        {
            var scores = new Dictionary<string, float>();
            
            if (landmarks == null || landmarks.Length < 136)
                return (false, 0f, scores);
            
            // Convert 468-point to 68-point if needed
            float[] lm = landmarks;
            if (landmarks.Length >= 936)
            {
                lm = ConvertFaceMeshTo68(landmarks);
            }
            
            // Helper functions
            float GetX(int idx) => lm[idx * 2];
            float GetY(int idx) => lm[idx * 2 + 1];
            float Dist(int a, int b) => (float)Math.Sqrt(
                Math.Pow(GetX(a) - GetX(b), 2) + Math.Pow(GetY(a) - GetY(b), 2));
            
            // === MEASUREMENTS ===
            
            // Face dimensions
            float faceWidthTop = Dist(0, 16);      // Temple to temple (jaw line ends)
            float faceWidthMid = Dist(3, 13);      // Mid-jaw
            float faceWidthLow = Dist(5, 11);      // Lower jaw
            float faceHeight = Dist(8, 27);        // Chin to nose bridge
            float jawWidth = Dist(4, 12);          // Jaw at widest
            float chinWidth = Dist(7, 9);          // Chin point area
            
            // Eyes
            float leftEyeWidth = Dist(36, 39);
            float rightEyeWidth = Dist(42, 45);
            float leftEyeHeight = Dist(37, 41);
            float rightEyeHeight = Dist(43, 47);
            float eyeDistance = Dist(39, 42);      // Inner eye corners
            float avgEyeWidth = (leftEyeWidth + rightEyeWidth) / 2f;
            float avgEyeHeight = (leftEyeHeight + rightEyeHeight) / 2f;
            
            // Nose
            float noseWidth = Dist(31, 35);
            float noseLength = Dist(27, 30);
            float noseBridgeWidth = Math.Abs(GetX(31) - GetX(35));
            
            // Mouth/Lips
            float mouthWidth = Dist(48, 54);
            float upperLipHeight = Dist(51, 62);
            float lowerLipHeight = Dist(57, 66);
            float mouthHeight = Dist(51, 57);
            float lipFullness = upperLipHeight + lowerLipHeight;
            
            // Brow
            float leftBrowLength = Dist(17, 21);
            float rightBrowLength = Dist(22, 26);
            float browThickness = Math.Abs(GetY(19) - GetY(37));  // Brow to eye distance
            
            // === GENDER INDICATORS (positive = female, negative = male) ===
            // NOTE: Thresholds calibrated for diverse face types (Western, Asian, African)
            // v3.0.24: When beard is detected, jaw/chin/lip landmarks are occluded by the beard.
            // Reduce their weights drastically and rely on upper-face features instead.
            float beardPenalty = beardDetected ? 0.2f : 1.0f;  // 80% reduction for beard-occluded features

            float totalScore = 0;
            float totalWeight = 0;

            // 1. JAW RATIO: Men have wider, squarer jaws
            // Asian faces tend to have wider jaws regardless of gender, so reduce weight
            float jawRatio = jawWidth / (faceWidthTop + 0.001f);
            float jawScore = (0.82f - jawRatio) * 2.5f;  // Adjusted threshold
            scores["jawRatio"] = jawScore;
            float jawWeight = 1.5f * beardPenalty;  // v3.0.24: reduced when beard detected
            totalScore += jawScore * jawWeight;
            totalWeight += jawWeight;

            // 2. CHIN SHAPE: Men have wider, squarer chins
            float chinRatio = chinWidth / (jawWidth + 0.001f);
            float chinScore = (chinRatio - 0.22f) * 3f;  // Adjusted
            scores["chinShape"] = chinScore;
            float chinWeight = 1.5f * beardPenalty;  // v3.0.24: reduced when beard detected
            totalScore += chinScore * chinWeight;
            totalWeight += chinWeight;
            
            // 3. FACE ASPECT: Men have squarer faces (width/height closer to 1)
            float faceAspect = faceWidthTop / (faceHeight + 0.001f);
            float aspectScore = (0.72f - faceAspect) * 2.5f;  // Adjusted
            scores["faceAspect"] = aspectScore;
            totalScore += aspectScore * 1.2f;
            totalWeight += 1.2f;
            
            // 4. EYE SIZE: Women have larger eyes relative to face - STRONG INDICATOR
            float eyeSizeRatio = avgEyeWidth / (faceWidthTop + 0.001f);
            float eyeScore = (eyeSizeRatio - 0.11f) * 15f;  // Increased sensitivity
            scores["eyeSize"] = eyeScore;
            totalScore += eyeScore * 3.0f;  // High weight - eyes are key!
            totalWeight += 3.0f;
            
            // 5. EYE OPENNESS: Women tend to have more open eyes
            float eyeOpenness = avgEyeHeight / (avgEyeWidth + 0.001f);
            float openScore = (eyeOpenness - 0.32f) * 6f;
            scores["eyeOpenness"] = openScore;
            totalScore += openScore * 2.0f;
            totalWeight += 2.0f;
            
            // 6. LIP FULLNESS: Women have fuller lips - STRONG INDICATOR
            // v3.0.24: Beard occludes lips, making them appear fuller → false female signal
            float lipRatio = lipFullness / (faceHeight + 0.001f);
            float lipScore = (lipRatio - 0.06f) * 15f;  // Increased sensitivity
            scores["lipFullness"] = lipScore;
            float lipWeight = 3.5f * beardPenalty;  // v3.0.24: heavily reduced when beard detected
            totalScore += lipScore * lipWeight;
            totalWeight += lipWeight;
            
            // 7. NOSE SIZE: Men have larger noses relative to face
            float noseRatio = noseWidth / (faceWidthTop + 0.001f);
            float noseScore = (0.16f - noseRatio) * 6f;  // Adjusted
            scores["noseSize"] = noseScore;
            totalScore += noseScore * 1.5f;
            totalWeight += 1.5f;
            
            // 8. NOSE LENGTH: Men have longer noses
            float noseLengthRatio = noseLength / (faceHeight + 0.001f);
            float noseLengthScore = (0.26f - noseLengthRatio) * 4f;
            scores["noseLength"] = noseLengthScore;
            totalScore += noseLengthScore * 1.0f;
            totalWeight += 1.0f;
            
            // 9. BROW PROMINENCE: Men have thicker, more prominent brows
            float browRatio = browThickness / (faceHeight + 0.001f);
            float browScore = (0.12f - browRatio) * 8f;  // Less prominent = female
            scores["browProminence"] = browScore;
            totalScore += browScore * 2.0f;
            totalWeight += 2.0f;
            
            // 10. MOUTH WIDTH: Women tend to have wider mouths relative to jaw
            float mouthRatio = mouthWidth / (jawWidth + 0.001f);
            float mouthScore = (mouthRatio - 0.55f) * 3f;
            scores["mouthWidth"] = mouthScore;
            totalScore += mouthScore * 1.0f;
            totalWeight += 1.0f;
            
            // === FINAL CALCULATION ===
            
            float avgScore = totalScore / totalWeight;
            scores["avgScore"] = avgScore;
            
            // Convert to probability (sigmoid-like)
            float probability = 0.5f + (float)Math.Tanh(avgScore) * 0.5f;
            
            // Confidence: how far from 0.5 (uncertain)
            float confidence = Math.Abs(probability - 0.5f) * 2f;
            
            bool isFemale = probability > 0.5f;
            
            return (isFemale, confidence, scores);
        }
        
        /// <summary>
        /// Combine landmark-based estimation with AI model for best accuracy
        /// CRITICAL: When both have low confidence, return TRULY UNCERTAIN, not male
        /// SPECIAL: When landmarks give near-zero confidence, face might not fit our model
        /// HAIR BIAS FIX: FairFace often mistakes long-haired men for women
        /// </summary>
        public static (bool isFemale, float confidence) CombineEstimates(
            bool landmarkFemale, float landmarkConf,
            bool aiFemale, float aiConf,
            Dictionary<string, float> landmarkScores = null)
        {
            const float MIN_CONFIDENCE = 0.25f;  // Below this, don't trust
            const float VERY_LOW_LANDMARK = 0.10f;  // Below this, landmarks totally failed
            const float UNCERTAIN_CONF = 0.08f;  // Return this when truly uncertain
            
            // HAIR BIAS CORRECTION: FairFace often mistakes long-haired men for women
            // If FairFace says Female with HIGH confidence but landmarks are uncertain,
            // check if specific masculine features are present (strong jaw, etc.)
            if (aiFemale && aiConf > 0.70f && landmarkConf < 0.40f)
            {
                bool hasMaleFeatures = false;
                
                // Check raw landmark scores for male indicators
                if (landmarkScores != null)
                {
                    // Low jaw score = squarer/wider jaw = male indicator
                    if (landmarkScores.TryGetValue("jawRatio", out float jawScore) && jawScore < 0.20f)
                        hasMaleFeatures = true;
                    
                    // Low chin score = wider chin = male indicator  
                    if (landmarkScores.TryGetValue("chinShape", out float chinScore) && chinScore < 0.10f)
                        hasMaleFeatures = true;
                        
                    // Low brow score = prominent brows = male indicator
                    if (landmarkScores.TryGetValue("browProminence", out float browScore) && browScore < 0.0f)
                        hasMaleFeatures = true;
                }
                
                // Also suspicious if landmarks lean female but with very low confidence
                // This often means conflicting signals (some male, some female features)
                if (landmarkConf < 0.30f)
                    hasMaleFeatures = true;
                
                if (hasMaleFeatures)
                {
                    // FairFace says "definitely female" but we see male features
                    // This is the classic "long-haired male" confusion pattern
                    // Return uncertain/male instead of trusting FairFace
                    return (false, 0.30f);  // Male with low confidence (needs verification)
                }
            }
            
            // Check which estimates are usable
            bool landmarkUsable = landmarkConf >= MIN_CONFIDENCE;
            bool aiUsable = aiConf >= MIN_CONFIDENCE;
            
            // SPECIAL CHECK: If landmarks gave near-zero confidence, the face might be
            // atypical (Asian, unusual angle, etc.) - don't trust AI alone in this case!
            bool landmarkTotallyFailed = landmarkConf < VERY_LOW_LANDMARK;
            
            // Case 1: Both usable and agree - high confidence result
            if (landmarkUsable && aiUsable && landmarkFemale == aiFemale)
            {
                float combinedConf = Math.Min(0.95f, (landmarkConf + aiConf) / 2f + 0.15f);
                return (landmarkFemale, combinedConf);
            }
            
            // Case 2: Both usable but disagree - weighted vote, lower confidence
            if (landmarkUsable && aiUsable)
            {
                float landmarkWeight = 0.55f;
                float aiWeight = 0.45f;
                
                float landmarkVote = landmarkFemale ? landmarkConf * landmarkWeight : -landmarkConf * landmarkWeight;
                float aiVote = aiFemale ? aiConf * aiWeight : -aiConf * aiWeight;
                
                float totalVote = landmarkVote + aiVote;
                bool finalFemale = totalVote > 0;
                float finalConf = Math.Abs(totalVote) * 0.5f;
                
                return (finalFemale, finalConf);
            }
            
            // Case 3: Only landmarks usable
            if (landmarkUsable)
            {
                return (landmarkFemale, landmarkConf * 0.8f);
            }
            
            // Case 4: Only AI usable - BUT landmarks totally failed
            // This suggests an atypical face - AI alone is NOT reliable here!
            // Return uncertain and let caller use metadata or skip
            if (aiUsable && landmarkTotallyFailed)
            {
                // Landmark total failure = unusual face, AI probably wrong too
                // Return UNCERTAIN so caller uses fallbacks (metadata, skip, etc.)
                return (aiFemale, UNCERTAIN_CONF);  // Very low confidence = uncertain
            }
            
            // Case 5: Only AI usable, landmarks gave SOME signal
            if (aiUsable)
            {
                return (aiFemale, aiConf * 0.4f);  // Low trust
            }
            
            // Case 6: NEITHER is reliably usable - truly uncertain
            // Pick the slightly higher one but with VERY LOW confidence
            if (landmarkConf > aiConf)
            {
                return (landmarkFemale, UNCERTAIN_CONF);
            }
            else if (aiConf > landmarkConf)
            {
                return (aiFemale, UNCERTAIN_CONF);
            }
            else
            {
                // Exactly equal and both low - truly unknown, don't bias to male
                return (true, UNCERTAIN_CONF);
            }
        }
        
        /// <summary>
        /// Convert 468-point FaceMesh to 68-point dlib format
        /// </summary>
        private static float[] ConvertFaceMeshTo68(float[] faceMesh)
        {
            if (faceMesh == null || faceMesh.Length < 936) return faceMesh;
            
            // FaceMesh index to dlib 68-point mapping (approximate)
            int[] mapping = new int[]
            {
                // Jaw (0-16)
                234, 93, 132, 58, 172, 136, 150, 176, 152, 400, 378, 365, 397, 288, 361, 323, 454,
                // Right eyebrow (17-21)
                70, 63, 105, 66, 107,
                // Left eyebrow (22-26)
                336, 296, 334, 293, 300,
                // Nose bridge (27-30)
                168, 197, 5, 4,
                // Nose bottom (31-35)
                75, 97, 2, 326, 305,
                // Right eye (36-41)
                33, 160, 158, 133, 153, 144,
                // Left eye (42-47)
                362, 385, 387, 263, 373, 380,
                // Outer mouth (48-59)
                61, 39, 37, 0, 267, 269, 291, 405, 314, 17, 84, 181,
                // Inner mouth (60-67)
                78, 82, 13, 312, 308, 317, 14, 87
            };
            
            float[] result = new float[136];
            for (int i = 0; i < 68 && i < mapping.Length; i++)
            {
                int fmIdx = mapping[i];
                if (fmIdx * 2 + 1 < faceMesh.Length)
                {
                    result[i * 2] = faceMesh[fmIdx * 2];
                    result[i * 2 + 1] = faceMesh[fmIdx * 2 + 1];
                }
            }
            return result;
        }
    }
}
