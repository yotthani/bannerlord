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
    ///
    /// v3.0.27: FaceMesh 468 only — Dlib removed.
    /// </summary>
    public static class LandmarkGenderEstimator
    {
        // FaceMesh landmark indices (matching FeatureSet.cs definitions)

        // Jawline
        private const int FM_JAW_RIGHT = 234;   // Right jaw start
        private const int FM_JAW_R3 = 58;        // Right jaw mid-upper
        private const int FM_JAW_R4 = 172;       // Right jaw mid
        private const int FM_JAW_R5 = 136;       // Right jaw lower
        private const int FM_CHIN_R = 176;       // Right chin
        private const int FM_CHIN = 152;         // Chin center
        private const int FM_CHIN_L = 400;       // Left chin
        private const int FM_JAW_L5 = 365;       // Left jaw lower
        private const int FM_JAW_L4 = 397;       // Left jaw mid
        private const int FM_JAW_L3 = 288;       // Left jaw mid-upper
        private const int FM_JAW_LEFT = 454;     // Left jaw start

        // Eyebrows
        private const int FM_RBROW_OUTER = 70;   // Right brow outer
        private const int FM_RBROW_MID = 105;    // Right brow middle
        private const int FM_RBROW_INNER = 107;  // Right brow inner
        private const int FM_LBROW_INNER = 336;  // Left brow inner
        private const int FM_LBROW_OUTER = 300;  // Left brow outer

        // Nose
        private const int FM_NOSE_TOP = 168;         // Nose bridge top
        private const int FM_NOSE_BRIDGE_LOW = 195;  // Nose bridge bottom
        private const int FM_NOSTRIL_R = 98;          // Right nostril
        private const int FM_NOSTRIL_L = 327;         // Left nostril

        // Right Eye
        private const int FM_REYE_OUTER = 33;        // Outer corner
        private const int FM_REYE_UPPER_OUT = 160;   // Upper lid outer
        private const int FM_REYE_INNER = 133;       // Inner corner
        private const int FM_REYE_LOWER_OUT = 144;   // Lower lid outer

        // Left Eye
        private const int FM_LEYE_OUTER = 362;       // Outer corner
        private const int FM_LEYE_UPPER_OUT = 385;   // Upper lid outer
        private const int FM_LEYE_INNER = 263;       // Inner corner
        private const int FM_LEYE_LOWER_OUT = 380;   // Lower lid outer

        // Mouth
        private const int FM_MOUTH_R = 61;       // Right corner
        private const int FM_ULIP_TOP = 0;       // Upper lip top center
        private const int FM_MOUTH_L = 291;      // Left corner
        private const int FM_LLIP_BOTTOM = 17;   // Lower lip bottom center
        private const int FM_INNER_TOP = 13;     // Inner lip top
        private const int FM_INNER_BOTTOM = 14;  // Inner lip bottom

        /// <summary>
        /// Estimate gender from facial landmarks (FaceMesh 468 only).
        /// Requires 936+ floats (468 points x 2 coordinates).
        /// Returns: (isFemale, confidence 0-1, featureScores for debugging)
        /// </summary>
        public static (bool isFemale, float confidence, Dictionary<string, float> scores)
            EstimateGender(float[] landmarks, bool beardDetected = false)
        {
            var scores = new Dictionary<string, float>();

            if (landmarks == null || landmarks.Length < 936)
                return (false, 0f, scores);

            float[] fm = landmarks;

            // Helper functions — now using FaceMesh indices directly
            float GetX(int fmIdx) => fm[fmIdx * 2];
            float GetY(int fmIdx) => fm[fmIdx * 2 + 1];
            float Dist(int a, int b) => (float)Math.Sqrt(
                Math.Pow(GetX(a) - GetX(b), 2) + Math.Pow(GetY(a) - GetY(b), 2));

            // === MEASUREMENTS ===

            // Face dimensions
            float faceWidthTop = Dist(FM_JAW_RIGHT, FM_JAW_LEFT);   // Temple to temple (jaw line ends)
            float faceWidthMid = Dist(FM_JAW_R3, FM_JAW_L3);       // Mid-jaw
            float faceWidthLow = Dist(FM_JAW_R5, FM_JAW_L5);       // Lower jaw
            float faceHeight = Dist(FM_CHIN, FM_NOSE_TOP);          // Chin to nose bridge
            float jawWidth = Dist(FM_JAW_R4, FM_JAW_L4);            // Jaw at widest
            float chinWidth = Dist(FM_CHIN_R, FM_CHIN_L);           // Chin point area

            // Eyes
            float leftEyeWidth = Dist(FM_REYE_OUTER, FM_REYE_INNER);
            float rightEyeWidth = Dist(FM_LEYE_OUTER, FM_LEYE_INNER);
            float leftEyeHeight = Dist(FM_REYE_UPPER_OUT, FM_REYE_LOWER_OUT);
            float rightEyeHeight = Dist(FM_LEYE_UPPER_OUT, FM_LEYE_LOWER_OUT);
            float eyeDistance = Dist(FM_REYE_INNER, FM_LEYE_INNER); // Inner eye corners
            float avgEyeWidth = (leftEyeWidth + rightEyeWidth) / 2f;
            float avgEyeHeight = (leftEyeHeight + rightEyeHeight) / 2f;

            // Nose
            float noseWidth = Dist(FM_NOSTRIL_R, FM_NOSTRIL_L);
            float noseLength = Dist(FM_NOSE_TOP, FM_NOSE_BRIDGE_LOW);
            float noseBridgeWidth = Math.Abs(GetX(FM_NOSTRIL_R) - GetX(FM_NOSTRIL_L));

            // Mouth/Lips
            float mouthWidth = Dist(FM_MOUTH_R, FM_MOUTH_L);
            float upperLipHeight = Dist(FM_ULIP_TOP, FM_INNER_TOP);
            float lowerLipHeight = Dist(FM_LLIP_BOTTOM, FM_INNER_BOTTOM);
            float mouthHeight = Dist(FM_ULIP_TOP, FM_LLIP_BOTTOM);
            float lipFullness = upperLipHeight + lowerLipHeight;

            // Brow
            float leftBrowLength = Dist(FM_RBROW_OUTER, FM_RBROW_INNER);
            float rightBrowLength = Dist(FM_LBROW_INNER, FM_LBROW_OUTER);
            float browThickness = Math.Abs(GetY(FM_RBROW_MID) - GetY(FM_REYE_UPPER_OUT));  // Brow to eye distance

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
    }
}
