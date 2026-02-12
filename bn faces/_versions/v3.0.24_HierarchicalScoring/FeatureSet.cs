using System;
using System.Collections.Generic;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Extracted facial features from landmarks.
    /// Each feature is a normalized value (0-1) representing a measurement.
    /// This is what we compare between target and current face.
    /// </summary>
    public class FeatureSet
    {
        // ═══════════════════════════════════════════════════════════════
        // EBENE 1: GESICHTSFORM
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Face width (0=narrow, 1=wide)</summary>
        public float FaceWidth { get; set; }

        /// <summary>Face height (0=short, 1=tall)</summary>
        public float FaceHeight { get; set; }

        /// <summary>Width/Height ratio (0.7=long, 1.0=round)</summary>
        public float FaceRatio { get; set; }

        /// <summary>Face shape classification</summary>
        public FaceShapeType FaceShape { get; set; }

        // v3.0.23: Contour profile — widths at different heights along landmarks 0-16,
        // normalized to the widest point (0-16). This captures the actual face outline
        // instead of guessing from individual measurements.
        // Each value is 0-1 where 1.0 = as wide as the widest point.
        /// <summary>Width at upper jaw level (landmarks 1-15), relative to max width</summary>
        public float ContourUpperJaw { get; set; }
        /// <summary>Width at cheekbone level (landmarks 2-14), relative to max width</summary>
        public float ContourCheekbone { get; set; }
        /// <summary>Width at mid jaw level (landmarks 3-13), relative to max width</summary>
        public float ContourMidJaw { get; set; }
        /// <summary>Width at lower jaw level (landmarks 4-12), relative to max width</summary>
        public float ContourLowerJaw { get; set; }
        /// <summary>Width near chin (landmarks 5-11), relative to max width</summary>
        public float ContourNearChin { get; set; }
        /// <summary>Width at chin area (landmarks 6-10), relative to max width</summary>
        public float ContourChinArea { get; set; }
        
        // ═══════════════════════════════════════════════════════════════
        // EBENE 2: STRUKTUR
        // ═══════════════════════════════════════════════════════════════
        
        // Forehead
        public float ForeheadHeight { get; set; }
        public float ForeheadWidth { get; set; }
        public float ForeheadSlope { get; set; }
        
        // Jaw
        public float JawWidth { get; set; }
        public float JawAngleLeft { get; set; }
        public float JawAngleRight { get; set; }
        public float JawTaper { get; set; }  // How much jaw narrows toward chin
        public float JawCurvature { get; set; }  // 0=angular jaw, 1=round jaw
        
        // Chin
        public float ChinWidth { get; set; }
        public float ChinHeight { get; set; }
        public float ChinPointedness { get; set; }  // 0=round, 1=pointed (from angle, not width!)
        public float ChinDrop { get; set; }  // How far chin extends below jaw line
        
        // Cheeks
        public float CheekHeight { get; set; }
        public float CheekWidth { get; set; }
        public float CheekProminence { get; set; }
        
        // ═══════════════════════════════════════════════════════════════
        // EBENE 3: GROSSE FEATURES
        // ═══════════════════════════════════════════════════════════════
        
        // Nose
        public float NoseWidth { get; set; }
        public float NoseLength { get; set; }
        public float NoseBridge { get; set; }
        public float NoseTip { get; set; }
        public float NostrilWidth { get; set; }
        
        // Eyes
        public float EyeWidth { get; set; }
        public float EyeHeight { get; set; }
        public float EyeDistance { get; set; }
        public float EyeAngle { get; set; }
        public float EyeVerticalPos { get; set; }
        
        // Mouth
        public float MouthWidth { get; set; }
        public float MouthHeight { get; set; }
        public float UpperLipThickness { get; set; }
        public float LowerLipThickness { get; set; }
        public float MouthVerticalPos { get; set; }
        
        // ═══════════════════════════════════════════════════════════════
        // EBENE 4: FEINE DETAILS
        // ═══════════════════════════════════════════════════════════════
        
        // Eyebrows
        public float EyebrowHeight { get; set; }
        public float EyebrowArch { get; set; }
        public float EyebrowThickness { get; set; }
        public float EyebrowAngle { get; set; }
        
        // ═══════════════════════════════════════════════════════════════
        // CONFIDENCE
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Confidence per sub-phase (how reliable is the measurement)</summary>
        public Dictionary<SubPhase, float> Confidence { get; set; } = new Dictionary<SubPhase, float>();
        
        // ═══════════════════════════════════════════════════════════════
        // METHODS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Get all features for a specific sub-phase</summary>
        public float[] GetFeaturesForPhase(SubPhase phase)
        {
            switch (phase)
            {
                case SubPhase.FaceWidth:
                    return new[] { FaceWidth };
                    
                case SubPhase.FaceHeight:
                    return new[] { FaceHeight };
                    
                case SubPhase.FaceShape:
                    // v3.0.23: Contour profile replaces guessed shape enum.
                    // 6 width measurements along landmarks 0-16 directly capture the face outline.
                    // FaceRatio provides the overall proportion context.
                    return new[] { FaceRatio, ContourUpperJaw, ContourCheekbone, ContourMidJaw,
                                   ContourLowerJaw, ContourNearChin, ContourChinArea };
                    
                case SubPhase.Forehead:
                    // NOTE: ForeheadSlope removed - it's a constant 0.5 (would need 3D data)
                    return new[] { ForeheadHeight, ForeheadWidth };
                    
                case SubPhase.Jaw:
                    // JawCurvature is KEY for round vs angular jaw!
                    return new[] { JawWidth, JawAngleLeft, JawAngleRight, JawTaper, JawCurvature };
                    
                case SubPhase.Chin:
                    // ChinPointedness from angle is KEY for round vs pointed chin!
                    return new[] { ChinWidth, ChinHeight, ChinPointedness, ChinDrop };
                    
                case SubPhase.Cheeks:
                    // NOTE: CheekProminence removed - it's a constant 0.5 (would need 3D data)
                    return new[] { CheekHeight, CheekWidth };
                    
                case SubPhase.Nose:
                    return new[] { NoseWidth, NoseLength, NoseBridge, NoseTip, NostrilWidth };
                    
                case SubPhase.Eyes:
                    return new[] { EyeWidth, EyeHeight, EyeDistance, EyeAngle, EyeVerticalPos };
                    
                case SubPhase.Mouth:
                    return new[] { MouthWidth, MouthHeight, UpperLipThickness, LowerLipThickness, MouthVerticalPos };
                    
                case SubPhase.Eyebrows:
                    return new[] { EyebrowHeight, EyebrowArch, EyebrowThickness, EyebrowAngle };
                    
                default:
                    return Array.Empty<float>();
            }
        }
        
        /// <summary>Clone this feature set</summary>
        public FeatureSet Clone()
        {
            return new FeatureSet
            {
                // Ebene 1
                FaceWidth = this.FaceWidth,
                FaceHeight = this.FaceHeight,
                FaceRatio = this.FaceRatio,
                FaceShape = this.FaceShape,
                ContourUpperJaw = this.ContourUpperJaw,
                ContourCheekbone = this.ContourCheekbone,
                ContourMidJaw = this.ContourMidJaw,
                ContourLowerJaw = this.ContourLowerJaw,
                ContourNearChin = this.ContourNearChin,
                ContourChinArea = this.ContourChinArea,
                
                // Ebene 2
                ForeheadHeight = this.ForeheadHeight,
                ForeheadWidth = this.ForeheadWidth,
                ForeheadSlope = this.ForeheadSlope,
                JawWidth = this.JawWidth,
                JawAngleLeft = this.JawAngleLeft,
                JawAngleRight = this.JawAngleRight,
                JawTaper = this.JawTaper,
                JawCurvature = this.JawCurvature,
                ChinWidth = this.ChinWidth,
                ChinHeight = this.ChinHeight,
                ChinPointedness = this.ChinPointedness,
                ChinDrop = this.ChinDrop,
                CheekHeight = this.CheekHeight,
                CheekWidth = this.CheekWidth,
                CheekProminence = this.CheekProminence,
                
                // Ebene 3
                NoseWidth = this.NoseWidth,
                NoseLength = this.NoseLength,
                NoseBridge = this.NoseBridge,
                NoseTip = this.NoseTip,
                NostrilWidth = this.NostrilWidth,
                EyeWidth = this.EyeWidth,
                EyeHeight = this.EyeHeight,
                EyeDistance = this.EyeDistance,
                EyeAngle = this.EyeAngle,
                EyeVerticalPos = this.EyeVerticalPos,
                MouthWidth = this.MouthWidth,
                MouthHeight = this.MouthHeight,
                UpperLipThickness = this.UpperLipThickness,
                LowerLipThickness = this.LowerLipThickness,
                MouthVerticalPos = this.MouthVerticalPos,
                
                // Ebene 4
                EyebrowHeight = this.EyebrowHeight,
                EyebrowArch = this.EyebrowArch,
                EyebrowThickness = this.EyebrowThickness,
                EyebrowAngle = this.EyebrowAngle,
                
                Confidence = new Dictionary<SubPhase, float>(this.Confidence)
            };
        }
    }
    
    public enum FaceShapeType
    {
        Unknown = 0,
        Round = 1,
        Oval = 2,
        Square = 3,
        Heart = 4,
        Oblong = 5,
        Diamond = 6
    }
    
    /// <summary>
    /// Extracts FeatureSet from landmarks
    /// </summary>
    public class FeatureExtractor
    {
        /// <summary>
        /// Extract features from Dlib 68 landmarks (136 floats)
        /// </summary>
        public FeatureSet Extract(float[] landmarks)
        {
            if (landmarks == null || landmarks.Length < 136)
                return new FeatureSet();
            
            var features = new FeatureSet();
            
            // Helper functions
            float X(int i) => landmarks[i * 2];
            float Y(int i) => landmarks[i * 2 + 1];
            float Dist(int a, int b) => (float)Math.Sqrt(
                Math.Pow(X(a) - X(b), 2) + Math.Pow(Y(a) - Y(b), 2));
            float Angle(int a, int center, int b)
            {
                float ax = X(a) - X(center);
                float ay = Y(a) - Y(center);
                float bx = X(b) - X(center);
                float by = Y(b) - Y(center);
                float dot = ax * bx + ay * by;
                float cross = ax * by - ay * bx;
                return (float)Math.Atan2(cross, dot) * 180f / (float)Math.PI;
            }
            
            // DEBUG: Log landmark count to verify format
            // FaceMesh=468*3=1404, Dlib68=68*2=136
            bool isFaceMesh = landmarks.Length > 400;
            
            // === EBENE 1: GESICHTSFORM ===
            
            float faceWidthRaw = Dist(0, 16);      // Jaw endpoints
            float faceHeightRaw = Dist(8, 27);     // Chin to nose bridge
            float fullHeightRaw = Math.Abs(Y(8) - Y(19));  // Chin to brow
            
            // FaceWidth/FaceHeight: The * 2f normalization works because Dlib landmarks
            // are already normalized to the detected face bounding box (0-1 range).
            // The scale is consistent between photo and render as long as both use
            // the same landmark detector. FaceRatio provides the scale-invariant shape info.
            features.FaceWidth = Clamp(faceWidthRaw * 2f, 0f, 1f);
            features.FaceHeight = Clamp(faceHeightRaw * 2f, 0f, 1f);
            features.FaceRatio = faceWidthRaw / (fullHeightRaw + 0.001f);

            // v3.0.23: Contour profile — measure face width at 6 different heights
            // using landmark pairs along the jawline (0-16).
            // Normalized to the widest point (Dist(0,16)) so the profile is scale-invariant.
            // This captures the actual face outline shape instead of guessing from individual values.
            float maxWidth = faceWidthRaw + 0.001f;  // Dist(0,16), avoid division by zero
            features.ContourUpperJaw = Clamp(Dist(1, 15) / maxWidth, 0f, 1f);
            features.ContourCheekbone = Clamp(Dist(2, 14) / maxWidth, 0f, 1f);
            features.ContourMidJaw = Clamp(Dist(3, 13) / maxWidth, 0f, 1f);
            features.ContourLowerJaw = Clamp(Dist(4, 12) / maxWidth, 0f, 1f);
            features.ContourNearChin = Clamp(Dist(5, 11) / maxWidth, 0f, 1f);
            features.ContourChinArea = Clamp(Dist(6, 10) / maxWidth, 0f, 1f);

            // Classify face shape using 5 measurements for better discrimination
            // (jawCurvature, chinPointedness, cheekWidth computed below but needed here;
            //  compute preliminary values now, final features set in their own sections)
            float prelimJawCurvature = Clamp(Math.Abs(Angle(4, 8, 12)) / 180f, 0f, 1f);
            float prelimChinAngle = Math.Abs(Angle(6, 8, 10));
            float prelimChinPointedness = Clamp((140f - prelimChinAngle) / 80f, 0f, 1f);
            float prelimCheekWidth = Clamp(Dist(2, 14) / faceWidthRaw, 0f, 1f);
            float prelimJawTaper = Dist(5, 11) / (Dist(1, 15) + 0.001f);
            features.FaceShape = ClassifyFaceShape(features.FaceRatio, prelimJawTaper,
                prelimJawCurvature, prelimChinPointedness, prelimCheekWidth);
            
            features.Confidence[SubPhase.FaceWidth] = 0.9f;
            features.Confidence[SubPhase.FaceHeight] = 0.9f;
            features.Confidence[SubPhase.FaceShape] = 0.8f;
            
            // === EBENE 2: STRUKTUR ===
            
            // Forehead (estimated from brow landmarks)
            features.ForeheadHeight = Clamp(Math.Abs(Y(27) - Y(19)) / faceHeightRaw, 0f, 1f);
            features.ForeheadWidth = Clamp(Dist(17, 26) / faceWidthRaw, 0f, 1f);
            // ForeheadSlope: estimate from brow curvature instead of hardcoding
            float browMidY = Y(19);
            float browLeftY = Y(17);
            float browRightY = Y(26);
            features.ForeheadSlope = Clamp(0.5f + (browMidY - (browLeftY + browRightY) / 2f) * 5f, 0f, 1f);
            features.Confidence[SubPhase.Forehead] = 0.7f;
            
            // Jaw
            features.JawWidth = Clamp(Dist(3, 13) / faceWidthRaw, 0f, 1f);
            features.JawAngleLeft = Clamp((Angle(1, 4, 8) + 180f) / 360f, 0f, 1f);
            features.JawAngleRight = Clamp((Angle(15, 12, 8) + 180f) / 360f, 0f, 1f);
            features.JawTaper = Clamp(Dist(5, 11) / (Dist(1, 15) + 0.001f), 0f, 1f);
            
            // NEW: JawCurvature - measures how curved/angular the jaw line is
            // Points 4-8-12 form the lower jaw. Sharp angle = angular, gentle curve = round
            float jawAngle = Math.Abs(Angle(4, 8, 12));
            features.JawCurvature = Clamp(jawAngle / 180f, 0f, 1f);  // 0=sharp angle, 1=flat/round
            features.Confidence[SubPhase.Jaw] = 0.85f;
            
            // Chin - IMPROVED: Use actual chin shape, not just width
            features.ChinWidth = Clamp(Dist(7, 9) / faceWidthRaw, 0f, 1f);
            features.ChinHeight = Clamp(Dist(8, 57) / faceHeightRaw, 0f, 1f);
            
            // ChinPointedness: Measure angle at chin point (landmarks 6, 8, 10)
            // Sharp angle = pointed chin, wide angle = round chin
            float chinAngle = Math.Abs(Angle(6, 8, 10));
            // v3.0.24: Widened range from /80 to /120 to prevent saturation on pointed chins
            // Sharp angle (~40°) = pointed (1.0), Wide angle (~160°) = round (0.0)
            features.ChinPointedness = Clamp((160f - chinAngle) / 120f, 0f, 1f);
            
            // Also measure the "drop" - how far chin extends below jaw line
            float jawLineY = (Y(4) + Y(12)) / 2f;
            float chinDrop = (Y(8) - jawLineY) / faceHeightRaw;
            features.ChinDrop = Clamp(chinDrop * 3f, 0f, 1f);  // How much chin extends down
            features.Confidence[SubPhase.Chin] = 0.85f;
            
            // Cheeks (estimated) - use actual cheek bone distance
            // v3.0.24: Use faceHeightRaw instead of fullHeightRaw (brow Y varies between photo/render)
            float cheekCenterY = (Y(2) + Y(14)) / 2f;
            features.CheekHeight = Clamp((Y(8) - cheekCenterY) / (faceHeightRaw * 1.3f), 0f, 1f);
            features.CheekWidth = Clamp(Dist(2, 14) / faceWidthRaw, 0f, 1f);
            // CheekProminence: estimate from relative position
            float cheekOutward = (Dist(2, 30) + Dist(14, 30)) / 2f;  // Cheek to nose tip
            features.CheekProminence = Clamp(cheekOutward / faceWidthRaw * 2f, 0f, 1f);
            features.Confidence[SubPhase.Cheeks] = 0.7f;
            
            // === EBENE 3: GROSSE FEATURES ===
            
            // Nose — v3.0.24: Recalibrated multipliers so 5th-95th percentile maps to ~0.15-0.85
            features.NoseWidth = Clamp(Dist(31, 35) / faceWidthRaw * 2.0f, 0f, 1f);
            features.NoseLength = Clamp(Dist(27, 33) / faceHeightRaw * 2.0f, 0f, 1f);
            features.NoseBridge = Clamp(Dist(27, 30) / faceHeightRaw * 3.0f, 0f, 1f);
            features.NoseTip = Clamp(Dist(30, 33) / faceHeightRaw * 5.0f, 0f, 1f);
            // v3.0.24: NostrilWidth was DUPLICATE of NoseWidth! New formula: nostril flare ratio
            float nostrilSpan = Math.Abs(X(31) - X(35));  // Nostril-to-nostril horizontal
            float bridgeTopSpan = Math.Abs(X(27) - X(30)) + 0.001f;  // Upper bridge width
            features.NostrilWidth = Clamp(nostrilSpan / (bridgeTopSpan + faceWidthRaw) * 2.5f, 0f, 1f);
            features.Confidence[SubPhase.Nose] = 0.9f;
            
            // Eyes
            float leftEyeW = Dist(36, 39);
            float rightEyeW = Dist(42, 45);
            float leftEyeH = Dist(37, 41);
            float rightEyeH = Dist(43, 47);
            
            // v3.0.24: Recalibrated multipliers for real variation
            features.EyeWidth = Clamp((leftEyeW + rightEyeW) / 2f / faceWidthRaw * 4.5f, 0f, 1f);
            features.EyeHeight = Clamp((leftEyeH + rightEyeH) / 2f / faceHeightRaw * 10.0f, 0f, 1f);
            features.EyeDistance = Clamp(Dist(39, 42) / faceWidthRaw * 3f, 0f, 1f);
            // v3.0.24: EyeAngle was broken (Angle at point 39 always ~30° → saturated at 1.0)
            // New: Canthal tilt = slope of the line from inner to outer eye corner
            float leftTilt = (float)Math.Atan2(Y(36) - Y(39), X(39) - X(36));
            float rightTilt = (float)Math.Atan2(Y(42) - Y(45), X(45) - X(42));
            float avgTiltDeg = (leftTilt + rightTilt) / 2f * 180f / (float)Math.PI;
            features.EyeAngle = Clamp((avgTiltDeg + 15f) / 30f, 0f, 1f);
            // v3.0.24: EyeVertPos was ALWAYS 1.0 for renders because fullHeightRaw differs
            // between photo/render bounding boxes. Use faceHeightRaw (chin-to-nose-bridge) instead.
            float eyeCenterY = (Y(36) + Y(45)) / 2f;
            features.EyeVerticalPos = Clamp((Y(8) - eyeCenterY) / (faceHeightRaw * 1.5f), 0f, 1f);
            features.Confidence[SubPhase.Eyes] = 0.9f;
            
            // Mouth
            // v3.0.24: Recalibrated multipliers for real variation
            features.MouthWidth = Clamp(Dist(48, 54) / faceWidthRaw * 1.8f, 0f, 1f);
            features.MouthHeight = Clamp(Dist(51, 57) / faceHeightRaw * 5.0f, 0f, 1f);
            features.UpperLipThickness = Clamp(Dist(51, 62) / faceHeightRaw * 10.0f, 0f, 1f);
            features.LowerLipThickness = Clamp(Dist(57, 66) / faceHeightRaw * 10.0f, 0f, 1f);
            // v3.0.24: Use faceHeightRaw instead of fullHeightRaw (brow Y varies between photo/render)
            features.MouthVerticalPos = Clamp((Y(8) - Y(51)) / faceHeightRaw, 0f, 1f);
            features.Confidence[SubPhase.Mouth] = 0.85f;
            
            // === EBENE 4: FEINE DETAILS ===
            
            // Eyebrows
            features.EyebrowHeight = Clamp(Math.Abs(Y(19) - Y(36)) / faceHeightRaw * 5f, 0f, 1f);
            features.EyebrowArch = Clamp(Math.Abs(Y(19) - (Y(17) + Y(21)) / 2f) / faceHeightRaw * 20f, 0f, 1f);
            // EyebrowThickness: estimate from brow landmark spread
            float browSpreadL = Math.Abs(Y(17) - Y(20));
            float browSpreadR = Math.Abs(Y(22) - Y(25));
            // v3.0.23: Normalize by faceHeightRaw to make scale-invariant (was absolute pixel distance)
            features.EyebrowThickness = Clamp((browSpreadL + browSpreadR) / 2f / faceHeightRaw * 5f, 0f, 1f);
            // v3.0.24: BrowAngle was broken (Angle at point 19 always near-collinear → always 0.0)
            // New: Brow slope from inner to outer corner. Positive = arched up, negative = sloping down
            float leftBrowSlope = (Y(17) - Y(21)) / (X(21) - X(17) + 0.001f);
            float rightBrowSlope = (Y(26) - Y(22)) / (X(22) - X(26) + 0.001f);
            float avgBrowSlope = (leftBrowSlope + rightBrowSlope) / 2f;
            features.EyebrowAngle = Clamp(avgBrowSlope * 1.5f + 0.5f, 0f, 1f);
            features.Confidence[SubPhase.Eyebrows] = 0.7f;
            
            return features;
        }
        
        /// <summary>
        /// Classify face shape using 5 measurements instead of 2.
        /// Uses a scoring approach: each shape type has an ideal profile,
        /// and we pick the best-matching one. This avoids fragile threshold cascades.
        /// </summary>
        private FaceShapeType ClassifyFaceShape(float ratio, float jawTaper,
            float jawCurvature, float chinPointedness, float cheekWidth)
        {
            // Each shape defined by ideal values for:
            // [faceRatio, jawTaper, jawCurvature, chinPointedness, cheekWidth]
            // Scoring = sum of (1 - |actual - ideal|) * weight for each measurement

            float bestScore = float.MinValue;
            FaceShapeType bestShape = FaceShapeType.Oval;

            // Round: wide face, wide jaw, curved jaw, round chin, wide cheeks
            float s = ShapeScore(ratio, 0.97f, 1.5f) + ShapeScore(jawTaper, 0.85f, 1.0f) +
                      ShapeScore(jawCurvature, 0.8f, 1.2f) + ShapeScore(chinPointedness, 0.2f, 1.3f) +
                      ShapeScore(cheekWidth, 0.9f, 0.8f);
            if (s > bestScore) { bestScore = s; bestShape = FaceShapeType.Round; }

            // Oval: medium ratio, moderate taper, gentle curves, slightly pointed chin
            s = ShapeScore(ratio, 0.82f, 1.5f) + ShapeScore(jawTaper, 0.70f, 1.0f) +
                ShapeScore(jawCurvature, 0.65f, 1.0f) + ShapeScore(chinPointedness, 0.45f, 1.0f) +
                ShapeScore(cheekWidth, 0.80f, 0.8f);
            if (s > bestScore) { bestScore = s; bestShape = FaceShapeType.Oval; }

            // Square: wide face, wide jaw, angular jaw, blunt chin, wide cheeks
            s = ShapeScore(ratio, 0.90f, 1.5f) + ShapeScore(jawTaper, 0.85f, 1.2f) +
                ShapeScore(jawCurvature, 0.35f, 1.3f) + ShapeScore(chinPointedness, 0.25f, 1.2f) +
                ShapeScore(cheekWidth, 0.85f, 0.8f);
            if (s > bestScore) { bestScore = s; bestShape = FaceShapeType.Square; }

            // Heart: wide forehead/cheeks, narrow jaw, moderate curve, pointed chin
            s = ShapeScore(ratio, 0.87f, 1.2f) + ShapeScore(jawTaper, 0.55f, 1.5f) +
                ShapeScore(jawCurvature, 0.6f, 0.8f) + ShapeScore(chinPointedness, 0.7f, 1.5f) +
                ShapeScore(cheekWidth, 0.90f, 1.0f);
            if (s > bestScore) { bestScore = s; bestShape = FaceShapeType.Heart; }

            // Oblong: tall face, moderate jaw, gentle curves, moderate chin
            s = ShapeScore(ratio, 0.70f, 1.8f) + ShapeScore(jawTaper, 0.72f, 0.8f) +
                ShapeScore(jawCurvature, 0.6f, 0.8f) + ShapeScore(chinPointedness, 0.4f, 0.8f) +
                ShapeScore(cheekWidth, 0.75f, 0.8f);
            if (s > bestScore) { bestScore = s; bestShape = FaceShapeType.Oblong; }

            // Diamond: narrow forehead, wide cheeks, narrow jaw, angular, pointed chin
            s = ShapeScore(ratio, 0.80f, 1.0f) + ShapeScore(jawTaper, 0.58f, 1.3f) +
                ShapeScore(jawCurvature, 0.45f, 1.0f) + ShapeScore(chinPointedness, 0.6f, 1.2f) +
                ShapeScore(cheekWidth, 0.95f, 1.2f);
            if (s > bestScore) { bestShape = FaceShapeType.Diamond; }

            return bestShape;
        }

        /// <summary>
        /// Score how well an actual value matches an ideal, with configurable weight.
        /// Returns weight * (1 - |actual - ideal|), clamped to [0, weight].
        /// </summary>
        private float ShapeScore(float actual, float ideal, float weight)
        {
            return weight * Math.Max(0f, 1f - Math.Abs(actual - ideal));
        }
        
        private float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
