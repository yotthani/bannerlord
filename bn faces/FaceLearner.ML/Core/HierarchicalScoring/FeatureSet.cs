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
        public float EyeOpenness { get; set; }  // v3.0.38: Width/Height ratio — high=almond/monolid, low=round-open
        
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
                    return new[] { EyeWidth, EyeHeight, EyeDistance, EyeAngle, EyeVerticalPos, EyeOpenness };
                    
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
                EyeOpenness = this.EyeOpenness,
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
        // ═══════════════════════════════════════════════════════════════
        // FaceMesh 468 landmark indices
        // v3.0.27: FaceMesh 468 only — Dlib removed
        // ═══════════════════════════════════════════════════════════════

        // --- Jawline / Face Oval (36 points, vs Dlib's 17) ---
        // Full face oval from MediaPipe: forehead→right→chin→left→forehead
        // We use the jaw-relevant subset for contour measurements
        private static readonly int FM_JAW_RIGHT = 234;       // Right ear/jaw start (≈Dlib 0)
        private static readonly int FM_JAW_R1 = 93;            // (≈Dlib 1)
        private static readonly int FM_JAW_R2 = 132;           // (≈Dlib 2) Cheekbone level
        private static readonly int FM_JAW_R3 = 58;            // (≈Dlib 3) Mid jaw
        private static readonly int FM_JAW_R4 = 172;           // (≈Dlib 4) Lower jaw
        private static readonly int FM_JAW_R5 = 136;           // (≈Dlib 5)
        private static readonly int FM_JAW_R6 = 150;           // (≈Dlib 6) Near chin right
        private static readonly int FM_CHIN_R = 176;           // Right of chin
        private static readonly int FM_CHIN = 152;             // Chin center bottom (≈Dlib 8)
        private static readonly int FM_CHIN_L = 400;           // Left of chin
        private static readonly int FM_JAW_L6 = 378;           // Near chin left (≈Dlib 10)
        private static readonly int FM_JAW_L5 = 365;           // (≈Dlib 11)
        private static readonly int FM_JAW_L4 = 397;           // (≈Dlib 12) Lower jaw
        private static readonly int FM_JAW_L3 = 288;           // (≈Dlib 13) Mid jaw
        private static readonly int FM_JAW_L2 = 361;           // (≈Dlib 14) Cheekbone level
        private static readonly int FM_JAW_L1 = 323;           // (≈Dlib 15)
        private static readonly int FM_JAW_LEFT = 454;         // Left ear/jaw end (≈Dlib 16)

        // --- Forehead (FaceMesh EXCLUSIVE — Dlib has NONE!) ---
        private static readonly int FM_FOREHEAD_TOP = 10;      // Top center (hairline)
        private static readonly int FM_FOREHEAD_MID = 151;     // Mid forehead
        private static readonly int FM_FOREHEAD_R = 67;        // Right forehead
        private static readonly int FM_FOREHEAD_L = 297;       // Left forehead
        private static readonly int FM_TEMPLE_R = 54;          // Right temple
        private static readonly int FM_TEMPLE_L = 284;         // Left temple

        // --- Eyebrows ---
        // Right eyebrow (10 points vs Dlib's 5)
        private static readonly int FM_RBROW_OUTER = 70;       // (≈Dlib 17)
        private static readonly int FM_RBROW_MID2 = 63;        // (≈Dlib 18)
        private static readonly int FM_RBROW_MID = 105;        // (≈Dlib 19) — brow peak
        private static readonly int FM_RBROW_MID1 = 66;        // (≈Dlib 20)
        private static readonly int FM_RBROW_INNER = 107;      // (≈Dlib 21)
        // Left eyebrow
        private static readonly int FM_LBROW_INNER = 336;      // (≈Dlib 22)
        private static readonly int FM_LBROW_MID1 = 296;       // (≈Dlib 23)
        private static readonly int FM_LBROW_MID = 334;        // (≈Dlib 24) — brow peak
        private static readonly int FM_LBROW_MID2 = 293;       // (≈Dlib 25)
        private static readonly int FM_LBROW_OUTER = 300;      // (≈Dlib 26)

        // --- Nose ---
        private static readonly int FM_NOSE_TOP = 168;         // Between brows / glabella (≈Dlib 27)
        private static readonly int FM_NOSE_BRIDGE_UP = 6;     // Upper bridge (≈Dlib 28)
        private static readonly int FM_NOSE_BRIDGE_MID = 197;  // Mid bridge (≈Dlib 29)
        private static readonly int FM_NOSE_BRIDGE_LOW = 195;  // Lower bridge (≈Dlib 30)
        private static readonly int FM_NOSE_TIP = 1;           // Actual nose tip (FaceMesh EXCLUSIVE!)
        private static readonly int FM_NOSTRIL_R = 98;         // Right nostril (≈Dlib 31)
        private static readonly int FM_NOSE_R = 97;            // Right nose bottom (≈Dlib 32)
        private static readonly int FM_NOSE_BOTTOM = 2;        // Nose bottom center (≈Dlib 33)
        private static readonly int FM_NOSE_L = 326;           // Left nose bottom (≈Dlib 34)
        private static readonly int FM_NOSTRIL_L = 327;        // Left nostril (≈Dlib 35)

        // --- Eyes ---
        // Right eye (16 points vs Dlib's 6)
        private static readonly int FM_REYE_OUTER = 33;        // (≈Dlib 36)
        private static readonly int FM_REYE_UPPER_OUT = 160;   // (≈Dlib 37)
        private static readonly int FM_REYE_UPPER_IN = 158;    // (≈Dlib 38)
        private static readonly int FM_REYE_INNER = 133;       // (≈Dlib 39)
        private static readonly int FM_REYE_LOWER_IN = 153;    // (≈Dlib 40)
        private static readonly int FM_REYE_LOWER_OUT = 144;   // (≈Dlib 41)
        private static readonly int FM_REYE_UPPER_MID = 159;   // Additional: upper eyelid center
        private static readonly int FM_REYE_LOWER_MID = 145;   // Additional: lower eyelid center
        // Left eye
        private static readonly int FM_LEYE_OUTER = 362;       // (≈Dlib 42) — NOTE: this is actually outer corner
        private static readonly int FM_LEYE_UPPER_OUT = 385;   // (≈Dlib 43)
        private static readonly int FM_LEYE_UPPER_IN = 387;    // (≈Dlib 44)
        private static readonly int FM_LEYE_INNER = 263;       // (≈Dlib 45)
        private static readonly int FM_LEYE_LOWER_IN = 373;    // (≈Dlib 46)
        private static readonly int FM_LEYE_LOWER_OUT = 380;   // (≈Dlib 47)
        private static readonly int FM_LEYE_UPPER_MID = 386;   // Additional: upper eyelid center
        private static readonly int FM_LEYE_LOWER_MID = 374;   // Additional: lower eyelid center

        // --- Mouth / Lips ---
        // Outer lips
        private static readonly int FM_MOUTH_R = 61;           // Right corner (≈Dlib 48)
        private static readonly int FM_ULIP_R = 40;            // Upper lip right (≈Dlib 49)
        private static readonly int FM_ULIP_MR = 37;           // Upper lip mid-right (≈Dlib 50)
        private static readonly int FM_ULIP_TOP = 0;           // Upper lip center top (≈Dlib 51)
        private static readonly int FM_ULIP_ML = 267;          // Upper lip mid-left (≈Dlib 52)
        private static readonly int FM_ULIP_L = 270;           // Upper lip left (≈Dlib 53)
        private static readonly int FM_MOUTH_L = 291;          // Left corner (≈Dlib 54)
        private static readonly int FM_LLIP_L = 321;           // Lower lip left (≈Dlib 55)
        private static readonly int FM_LLIP_ML = 314;          // Lower lip mid-left (≈Dlib 56)
        private static readonly int FM_LLIP_BOTTOM = 17;       // Lower lip center bottom (≈Dlib 57)
        private static readonly int FM_LLIP_MR = 84;           // Lower lip mid-right (≈Dlib 58)
        private static readonly int FM_LLIP_R = 181;           // Lower lip right (≈Dlib 59)
        // Inner lips
        private static readonly int FM_INNER_R = 78;           // (≈Dlib 60)
        private static readonly int FM_INNER_UR = 82;          // (≈Dlib 61)
        private static readonly int FM_INNER_TOP = 13;         // Inner upper center (≈Dlib 62)
        private static readonly int FM_INNER_UL = 312;         // (≈Dlib 63)
        private static readonly int FM_INNER_L = 308;          // (≈Dlib 64)
        private static readonly int FM_INNER_LL = 317;         // (≈Dlib 65)
        private static readonly int FM_INNER_BOTTOM = 14;      // Inner lower center (≈Dlib 66)
        private static readonly int FM_INNER_LR = 87;          // (≈Dlib 67)

        // --- Cheek surface landmarks ---
        private static readonly int FM_CHEEK_R_UPPER = 50;     // Right upper cheek
        private static readonly int FM_CHEEK_L_UPPER = 280;    // Left upper cheek
        private static readonly int FM_CHEEK_R_MID = 116;      // Right mid cheek
        private static readonly int FM_CHEEK_L_MID = 345;      // Left mid cheek

        /// <summary>
        /// Extract features from FaceMesh 468 landmarks (936 floats).
        /// v3.0.27: FaceMesh 468 only — Dlib removed.
        /// </summary>
        public FeatureSet Extract(float[] landmarks)
        {
            if (landmarks == null || landmarks.Length < 936)
                return new FeatureSet();

            // v3.0.27: FaceMesh 468 only — Dlib removed
            float[] fm = landmarks;

            var features = new FeatureSet();

            // Helper functions — now index into FaceMesh 468 space
            float X(int fmIdx) => fm[fmIdx * 2];
            float Y(int fmIdx) => fm[fmIdx * 2 + 1];
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

            // ═══════════════════════════════════════════════════════════════
            // EBENE 1: GESICHTSFORM
            // ═══════════════════════════════════════════════════════════════

            // v3.0.27: Use FaceMesh native indices for reference measurements
            float faceWidthRaw = Dist(FM_JAW_RIGHT, FM_JAW_LEFT);    // Jaw endpoints (was Dlib 0,16)
            float faceHeightRaw = Dist(FM_CHIN, FM_NOSE_TOP);        // Chin to nose bridge (was Dlib 8,27)
            // v3.0.27: FaceMesh 468 only — Dlib removed. Use forehead landmark for stable fullHeight.
            float fullHeightRaw = Math.Abs(Y(FM_CHIN) - Y(FM_FOREHEAD_MID));

            // Face dimensions — normalize with Offset+Scale to spread variation
            features.FaceWidth = Clamp((faceWidthRaw - 0.35f) / 0.30f, 0f, 1f);
            features.FaceHeight = Clamp((faceHeightRaw - 0.20f) / 0.30f, 0f, 1f);
            float rawRatio = faceWidthRaw / (fullHeightRaw + 0.001f);
            features.FaceRatio = Clamp((rawRatio - 0.60f) / 0.50f, 0f, 1f);

            // v3.0.27: Contour profile using FaceMesh native jaw landmarks
            // These are direct measurements, no longer going through lossy Dlib conversion
            float maxWidth = faceWidthRaw + 0.001f;
            features.ContourUpperJaw = Clamp((Dist(FM_JAW_R1, FM_JAW_L1) / maxWidth - 0.90f) / 0.10f, 0f, 1f);
            features.ContourCheekbone = Clamp((Dist(FM_JAW_R2, FM_JAW_L2) / maxWidth - 0.85f) / 0.12f, 0f, 1f);
            features.ContourMidJaw = Clamp((Dist(FM_JAW_R3, FM_JAW_L3) / maxWidth - 0.75f) / 0.18f, 0f, 1f);
            features.ContourLowerJaw = Clamp((Dist(FM_JAW_R4, FM_JAW_L4) / maxWidth - 0.60f) / 0.25f, 0f, 1f);
            features.ContourNearChin = Clamp((Dist(FM_JAW_R5, FM_JAW_L5) / maxWidth - 0.45f) / 0.30f, 0f, 1f);
            features.ContourChinArea = Clamp(Dist(FM_JAW_R6, FM_JAW_L6) / maxWidth, 0f, 1f);

            // Face shape classification
            float prelimJawCurvature = Clamp(Math.Abs(Angle(FM_JAW_R4, FM_CHIN, FM_JAW_L4)) / 180f, 0f, 1f);
            // v3.0.27: ChinPointedness using FM_CHIN_R (176) and FM_CHIN_L (400) — these are
            // DIFFERENT from Dlib's 6,10 which were mapped to 150,176 (too close together → 8-12° angles).
            // FaceMesh chin area landmarks are wider spread → better angle discrimination.
            float prelimChinAngle = Math.Abs(Angle(FM_CHIN_R, FM_CHIN, FM_CHIN_L));
            // v3.0.28: Recalibrated — observed range ~100-160°
            float prelimChinPointedness = Clamp((160f - prelimChinAngle) / 60f, 0f, 1f);
            float prelimCheekWidth = Clamp(Dist(FM_JAW_R2, FM_JAW_L2) / faceWidthRaw, 0f, 1f);
            float prelimJawTaper = Dist(FM_JAW_R5, FM_JAW_L5) / (Dist(FM_JAW_R1, FM_JAW_L1) + 0.001f);
            features.FaceShape = ClassifyFaceShape(features.FaceRatio, prelimJawTaper,
                prelimJawCurvature, prelimChinPointedness, prelimCheekWidth);

            features.Confidence[SubPhase.FaceWidth] = 0.9f;
            features.Confidence[SubPhase.FaceHeight] = 0.9f;
            features.Confidence[SubPhase.FaceShape] = 0.85f;  // Higher with FaceMesh's better contour

            // ═══════════════════════════════════════════════════════════════
            // EBENE 2: STRUKTUR
            // ═══════════════════════════════════════════════════════════════

            // Forehead — v3.0.27: FaceMesh 468 only — Dlib removed
            // ACTUAL forehead measurements from FaceMesh forehead landmarks
            float foreheadH = Math.Abs(Y(FM_FOREHEAD_MID) - Y(FM_NOSE_TOP));
            features.ForeheadHeight = Clamp(foreheadH / (fullHeightRaw + 0.001f), 0f, 1f);
            // Temple-to-temple width = actual forehead width
            // v3.0.34: ForeheadWidth was near-saturated (~0.89-0.94 in all faces).
            // Apply offset+scale to spread the actual variation range.
            float foreheadW = Dist(FM_TEMPLE_R, FM_TEMPLE_L);
            float foreheadWidthRatio = foreheadW / (faceWidthRaw + 0.001f);
            features.ForeheadWidth = Clamp((foreheadWidthRatio - 0.75f) / 0.25f, 0f, 1f);
            // ForeheadSlope: gradient from hairline to brow (using Y positions)
            float hairlineY = Y(FM_FOREHEAD_TOP);
            float browY = (Y(FM_RBROW_MID) + Y(FM_LBROW_MID)) / 2f;
            float foreheadCenter = Y(FM_FOREHEAD_MID);
            // Slope = how much forehead curves (flat = 0.5, receding = higher, bulging = lower)
            float slopeBias = (foreheadCenter - (hairlineY + browY) / 2f) / (fullHeightRaw + 0.001f);
            features.ForeheadSlope = Clamp(0.5f + slopeBias * 10f, 0f, 1f);
            features.Confidence[SubPhase.Forehead] = 0.9f;

            // Jaw — v3.0.29: DECOUPLED from chin landmarks!
            // Previously JawAngle and JawCurvature used FM_CHIN (152) which gets moved by chin morphs
            // (51,52,53). This caused the optimizer to reject correct jaw improvements because chin
            // position changes made the jaw score look wrong. Now jaw features use only jaw-area
            // landmarks (R3/R4/R5, L3/L4/L5) that aren't affected by chin morph changes.
            features.JawWidth = Clamp(Dist(FM_JAW_R3, FM_JAW_L3) / faceWidthRaw, 0f, 1f);

            // JawAngle: angle of jaw from upper to lower jaw points (NO chin involvement!)
            // Uses the jaw curvature from ear→mid-jaw→lower-jaw, independent of chin position
            // IMPORTANT: L and R use DIFFERENT offsets! The Angle() function uses atan2(cross,dot)
            // which gives signed angles. Mirrored landmarks (R1/R3/R5 vs L1/L3/L5) produce
            // fundamentally different raw values: L maps to ~0.80-0.99, R maps to ~0.05-0.40.
            // v3.0.34: Widened L range (0.65-1.0 → 0-1, was 0.80-1.0) to prevent round-face saturation.
            // R range widened proportionally (0.0-0.40 → 0-1, was 0.0-0.20).
            float rawJawAngleL = (Angle(FM_JAW_R1, FM_JAW_R3, FM_JAW_R5) + 180f) / 360f;
            features.JawAngleLeft = Clamp((rawJawAngleL - 0.65f) / 0.35f, 0f, 1f);
            float rawJawAngleR = (Angle(FM_JAW_L1, FM_JAW_L3, FM_JAW_L5) + 180f) / 360f;
            features.JawAngleRight = Clamp((rawJawAngleR - 0.0f) / 0.40f, 0f, 1f);

            features.JawTaper = Clamp(Dist(FM_JAW_R5, FM_JAW_L5) / (Dist(FM_JAW_R1, FM_JAW_L1) + 0.001f), 0f, 1f);

            // JawCurvature: angle at mid-jaw between upper and lower jaw points
            // v3.0.29: Was Angle(FM_JAW_R4, FM_CHIN, FM_JAW_L4) — CHIN was the vertex!
            // Now uses FM_JAW_R3 as vertex (mid-jaw), measuring jaw curvature without chin.
            float jawAngle = Math.Abs(Angle(FM_JAW_R4, FM_JAW_R3, FM_JAW_L4));
            features.JawCurvature = Clamp(jawAngle / 180f, 0f, 1f);
            features.Confidence[SubPhase.Jaw] = 0.85f;

            // Chin — v3.0.27: Better chin landmarks from FaceMesh
            // FM_CHIN_R (176) and FM_CHIN_L (400) are actual chin-area points, wider than Dlib 7/9
            features.ChinWidth = Clamp(Dist(FM_CHIN_R, FM_CHIN_L) / faceWidthRaw, 0f, 1f);
            features.ChinHeight = Clamp(Dist(FM_CHIN, FM_LLIP_BOTTOM) / faceHeightRaw, 0f, 1f);

            // ChinPointedness: angle at chin center between chin-side landmarks
            // v3.0.28: FM_CHIN_R (176) / FM_CHIN_L (400) are widely spread → obtuse angles
            // Observed range: ~100-160°. Map: 100°→1.0 (pointed chin), 160°→0.0 (round chin)
            float chinAngle = Math.Abs(Angle(FM_CHIN_R, FM_CHIN, FM_CHIN_L));
            features.ChinPointedness = Clamp((160f - chinAngle) / 60f, 0f, 1f);

            // ChinDrop: how far chin extends below jaw line
            // v3.0.29: Use FM_CHIN_R/FM_CHIN_L as baseline instead of FM_JAW_R4/L4.
            // Previously used jaw landmarks that get moved by jaw morphs, creating cross-contamination.
            // Now uses chin-adjacent landmarks for a self-contained chin measurement.
            float chinSideY = (Y(FM_CHIN_R) + Y(FM_CHIN_L)) / 2f;
            float chinDrop = (Y(FM_CHIN) - chinSideY) / faceHeightRaw;
            features.ChinDrop = Clamp((chinDrop + 0.05f) / 0.15f, 0f, 1f);
            features.Confidence[SubPhase.Chin] = 0.9f;  // Better with FaceMesh chin landmarks

            // Cheeks — v3.0.27: FaceMesh 468 only — Dlib removed
            // Use actual cheek surface landmarks
            float cheekCenterY = (Y(FM_CHEEK_R_MID) + Y(FM_CHEEK_L_MID)) / 2f;
            features.CheekHeight = Clamp((Y(FM_CHIN) - cheekCenterY) / (faceHeightRaw * 1.3f), 0f, 1f);
            features.CheekWidth = Clamp((Dist(FM_JAW_R2, FM_JAW_L2) / faceWidthRaw - 0.85f) / 0.12f, 0f, 1f);
            // CheekProminence: distance from cheek surface to nose center line
            float cheekToNoseR = Dist(FM_CHEEK_R_MID, FM_NOSE_BRIDGE_LOW);
            float cheekToNoseL = Dist(FM_CHEEK_L_MID, FM_NOSE_BRIDGE_LOW);
            features.CheekProminence = Clamp((cheekToNoseR + cheekToNoseL) / 2f / faceWidthRaw * 2f, 0f, 1f);
            features.Confidence[SubPhase.Cheeks] = 0.85f;

            // ═══════════════════════════════════════════════════════════════
            // EBENE 3: GROSSE FEATURES
            // ═══════════════════════════════════════════════════════════════

            // Nose — v3.0.27: Using native FaceMesh nose landmarks
            // NoseWidth: nostril-to-nostril distance
            float noseWidthRatio = Dist(FM_NOSTRIL_R, FM_NOSTRIL_L) / faceWidthRaw;
            features.NoseWidth = Clamp((noseWidthRatio - 0.15f) / 0.35f, 0f, 1f);
            // NoseLength: bridge top to bottom center
            float noseLenRatio = Dist(FM_NOSE_TOP, FM_NOSE_BOTTOM) / faceHeightRaw;
            features.NoseLength = Clamp((noseLenRatio - 0.30f) / 0.30f, 0f, 1f);
            // NoseBridge: top to lower bridge
            float noseBridgeRatio = Dist(FM_NOSE_TOP, FM_NOSE_BRIDGE_LOW) / faceHeightRaw;
            features.NoseBridge = Clamp((noseBridgeRatio - 0.15f) / 0.25f, 0f, 1f);
            // NoseTip: ratio of tip protrusion to nose length
            float noseTipRatio = Dist(FM_NOSE_BRIDGE_LOW, FM_NOSE_BOTTOM) / (Dist(FM_NOSE_TOP, FM_NOSE_BOTTOM) + 0.001f);
            features.NoseTip = Clamp((noseTipRatio - 0.20f) / 0.50f, 0f, 1f);
            // NostrilWidth: nostril flare ratio (horizontal nostril span vs bridge width)
            float nostrilSpan = Math.Abs(X(FM_NOSTRIL_R) - X(FM_NOSTRIL_L));
            float bridgeTopSpan = Math.Abs(X(FM_NOSE_TOP) - X(FM_NOSE_BRIDGE_LOW)) + 0.001f;
            features.NostrilWidth = Clamp(nostrilSpan / (bridgeTopSpan + faceWidthRaw) * 2.5f, 0f, 1f);
            features.Confidence[SubPhase.Nose] = 0.9f;

            // Eyes — v3.0.27: FaceMesh has more eye landmarks for better measurements
            // Use more points for eye width/height to get stable averages
            float rEyeW = Dist(FM_REYE_OUTER, FM_REYE_INNER);
            float lEyeW = Dist(FM_LEYE_OUTER, FM_LEYE_INNER);
            // v3.0.27: FaceMesh 468 only — use mid-eyelid points for more accurate height
            float rEyeH = Dist(FM_REYE_UPPER_MID, FM_REYE_LOWER_MID);
            float lEyeH = Dist(FM_LEYE_UPPER_MID, FM_LEYE_LOWER_MID);

            float eyeToFaceRatio = (rEyeW + lEyeW) / 2f / faceWidthRaw;
            features.EyeWidth = Clamp((eyeToFaceRatio - 0.16f) / 0.20f, 0f, 1f);
            float eyeHeightRatio = (rEyeH + lEyeH) / 2f / faceWidthRaw;
            features.EyeHeight = Clamp((eyeHeightRatio - 0.01f) / 0.14f, 0f, 1f);
            // EyeDistance: inner corner to inner corner, normalized to face width
            // v3.0.34: Was ALWAYS saturated at 1.0/1.0 (both photo and render).
            // Observed ratio range is actually 0.28-0.50+. Widen offset+scale significantly
            // so we get meaningful variation instead of free points.
            float eyeDistRatio = Dist(FM_REYE_INNER, FM_LEYE_INNER) / faceWidthRaw;
            features.EyeDistance = Clamp((eyeDistRatio - 0.22f) / 0.35f, 0f, 1f);
            // EyeAngle: Canthal tilt
            float leftTilt = (float)Math.Atan2(Y(FM_REYE_OUTER) - Y(FM_REYE_INNER), X(FM_REYE_INNER) - X(FM_REYE_OUTER));
            float rightTilt = (float)Math.Atan2(Y(FM_LEYE_OUTER) - Y(FM_LEYE_INNER), X(FM_LEYE_INNER) - X(FM_LEYE_OUTER));
            float avgTiltDeg = (leftTilt + rightTilt) / 2f * 180f / (float)Math.PI;
            features.EyeAngle = Clamp((avgTiltDeg + 15f) / 30f, 0f, 1f);
            // EyeVerticalPos: relative to face height
            float eyeCenterY = (Y(FM_REYE_OUTER) + Y(FM_LEYE_INNER)) / 2f;
            features.EyeVerticalPos = Clamp((Y(FM_CHIN) - eyeCenterY) / (faceHeightRaw * 1.5f), 0f, 1f);
            // v3.0.38: EyeOpenness — aspect ratio Width/Height.
            // Almond/monolid eyes: wide but not tall → high ratio (3-6)
            // Round/open eyes: wide and tall → low ratio (2-3)
            // This distinguishes monolid (eye_mono_lid morph) from closed (eye_closure morph).
            // Without this, the optimizer sees "low EyeHeight" and cranks up eye_closure
            // instead of using eye_mono_lid for Asian-style eyes.
            float avgEyeW = (rEyeW + lEyeW) / 2f;
            float avgEyeH = (rEyeH + lEyeH) / 2f;
            float eyeAspect = avgEyeW / (avgEyeH + 0.001f);
            features.EyeOpenness = Clamp((eyeAspect - 2.0f) / 4.0f, 0f, 1f);  // 2.0=round(0.0), 6.0=narrow(1.0)
            features.Confidence[SubPhase.Eyes] = 0.9f;

            // Mouth — v3.0.27: Using native FaceMesh lip landmarks
            float mouthWidthRatio = Dist(FM_MOUTH_R, FM_MOUTH_L) / faceWidthRaw;
            features.MouthWidth = Clamp((mouthWidthRatio - 0.25f) / 0.30f, 0f, 1f);
            float mouthHeightRatio = Dist(FM_ULIP_TOP, FM_LLIP_BOTTOM) / faceWidthRaw;
            // v3.0.36: Cap MouthHeight when mouth is open (smiling/laughing).
            // Open mouth inflates MouthHeight to 1.0 (ratio>0.21), but Bannerlord renders
            // always have closed mouth → MouthHeight=0.65-0.70 max → Match=0.20 ◄◄ BAD.
            // Detect open mouth: if gap between inner lip top and bottom > 30% of outer lip height,
            // cap the ratio to closed-mouth maximum (~0.15).
            float innerGap = Dist(FM_INNER_TOP, FM_INNER_BOTTOM);
            float outerGap = Dist(FM_ULIP_TOP, FM_LLIP_BOTTOM) + 0.001f;
            float openRatio = innerGap / outerGap;
            if (openRatio > 0.30f)  // Mouth is open (smiling, laughing, talking)
            {
                // Cap to closed-mouth equivalent — use lip thickness only, not gap
                mouthHeightRatio = Math.Min(mouthHeightRatio, 0.15f);
            }
            features.MouthHeight = Clamp((mouthHeightRatio - 0.03f) / 0.18f, 0f, 1f);
            // Lip thickness: ratio of upper/lower lip to total mouth height (self-referencing)
            float mouthH = Dist(FM_ULIP_TOP, FM_LLIP_BOTTOM) + 0.001f;
            float upperLipRatio = Dist(FM_ULIP_TOP, FM_INNER_TOP) / mouthH;
            features.UpperLipThickness = Clamp((upperLipRatio - 0.15f) / 0.60f, 0f, 1f);
            float lowerLipRatio = Dist(FM_LLIP_BOTTOM, FM_INNER_BOTTOM) / mouthH;
            features.LowerLipThickness = Clamp((lowerLipRatio - 0.15f) / 0.60f, 0f, 1f);
            // MouthVerticalPos relative to face
            features.MouthVerticalPos = Clamp((Y(FM_CHIN) - Y(FM_ULIP_TOP)) / faceHeightRaw, 0f, 1f);
            features.Confidence[SubPhase.Mouth] = 0.85f;

            // ═══════════════════════════════════════════════════════════════
            // EBENE 4: FEINE DETAILS
            // ═══════════════════════════════════════════════════════════════

            // Eyebrows
            // v3.0.34: BrowHeight was ALWAYS saturated at 1.0 in photos because
            // fullHeightRaw (chin-to-brow) is small, making browEyeGap/fullHeightRaw too large.
            // Fix: use faceHeightRaw (chin-to-nose-bridge, same fix as EyeVertPos v3.0.24)
            // and widen the offset+scale range.
            float browEyeGap = Math.Abs(Y(FM_RBROW_MID) - Y(FM_REYE_OUTER));
            // v3.0.36: Still saturating at 1.0 for some faces (1473033823: BrowHeight=1.0→0.78).
            // Widened scale from 0.20 to 0.30 — high brows (ratio 0.34) now map to 1.0 instead of 0.24.
            features.EyebrowHeight = Clamp((browEyeGap / faceHeightRaw - 0.04f) / 0.30f, 0f, 1f);
            // EyebrowArch: peak of brow vs ends
            float rBrowPeakDrop = Math.Abs(Y(FM_RBROW_MID) - (Y(FM_RBROW_OUTER) + Y(FM_RBROW_INNER)) / 2f);
            features.EyebrowArch = Clamp(rBrowPeakDrop / faceHeightRaw * 20f, 0f, 1f);
            // EyebrowThickness: brow landmark vertical spread
            float browSpreadL = Math.Abs(Y(FM_RBROW_OUTER) - Y(FM_RBROW_MID1));
            float browSpreadR = Math.Abs(Y(FM_LBROW_OUTER) - Y(FM_LBROW_MID1));
            features.EyebrowThickness = Clamp((browSpreadL + browSpreadR) / 2f / faceHeightRaw * 5f, 0f, 1f);
            // EyebrowAngle: slope from inner to outer
            float leftBrowSlope = (Y(FM_RBROW_OUTER) - Y(FM_RBROW_INNER)) / (X(FM_RBROW_INNER) - X(FM_RBROW_OUTER) + 0.001f);
            float rightBrowSlope = (Y(FM_LBROW_OUTER) - Y(FM_LBROW_INNER)) / (X(FM_LBROW_OUTER) - X(FM_LBROW_INNER) + 0.001f);
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
