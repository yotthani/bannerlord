using System;
using System.Drawing;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.FaceParsing;

namespace FaceLearner.ML.Modules.Proportions
{
    /// <summary>
    /// Proportions Module - Analyzes facial proportions using landmarks + face parsing.
    /// v3.0.27: Native FaceMesh 468 support - no more Dlib conversion.
    ///
    /// This is a KEY module for accurate face matching:
    /// - Combines MediaPipe landmarks with BiSeNet face parsing
    /// - Provides per-feature measurements (eyes, nose, mouth, etc.)
    /// - Each measurement has its own confidence score
    /// - Works directly in FaceMesh 468 index space
    ///
    /// Usage:
    ///   var result = proportionsModule.Analyze(bitmap, landmarks, parsingResult);
    ///   float noseWidth = result.Nose.Width;  // 0-1 relative to face
    /// </summary>
    public class ProportionsModule : IModule<ProportionsResult>
    {
        // Sub-analyzers
        private readonly FaceGeometryAnalyzer _faceGeometry;
        private readonly EyeAnalyzer _eyeAnalyzer;
        private readonly NoseAnalyzer _noseAnalyzer;
        private readonly MouthAnalyzer _mouthAnalyzer;
        private readonly JawAnalyzer _jawAnalyzer;
        private readonly EyebrowAnalyzer _eyebrowAnalyzer;

        private bool _isReady;

        public ProportionsModule()
        {
            _faceGeometry = new FaceGeometryAnalyzer();
            _eyeAnalyzer = new EyeAnalyzer();
            _noseAnalyzer = new NoseAnalyzer();
            _mouthAnalyzer = new MouthAnalyzer();
            _jawAnalyzer = new JawAnalyzer();
            _eyebrowAnalyzer = new EyebrowAnalyzer();
        }

        #region IModule Implementation

        public string Name => "Proportions";
        public bool IsReady => _isReady;

        public bool Initialize(string basePath)
        {
            // Proportions module doesn't need model files
            // It uses landmarks + parsing from other modules
            _isReady = true;
            SubModule.Log($"Proportions: Initialized (6 analyzers ready, FaceMesh 468 native)");
            return true;
        }

        /// <summary>
        /// Simple analyze from bitmap - requires landmarks to be detected first
        /// </summary>
        public ProportionsResult Analyze(Bitmap image)
        {
            // This version can't work without landmarks
            // Use the overload with landmarks instead
            return CreateEmptyResult("Use Analyze(landmarks, parsing) instead");
        }

        /// <summary>
        /// Full analysis using pre-computed landmarks and face parsing.
        /// Requires FaceMesh 468 landmarks (936 or 1404 values).
        /// </summary>
        public ProportionsResult Analyze(float[] landmarks, FaceParsingResult parsing)
        {
            if (landmarks == null || landmarks.Length < 936)
            {
                return CreateEmptyResult("Invalid landmarks");
            }

            try
            {
                // Pass landmarks directly to analyzers - they handle format detection internally
                var faceGeometry = _faceGeometry.Analyze(landmarks, parsing);
                var eyes = _eyeAnalyzer.Analyze(landmarks, parsing);
                var nose = _noseAnalyzer.Analyze(landmarks, parsing);
                var mouth = _mouthAnalyzer.Analyze(landmarks, parsing);
                var jaw = _jawAnalyzer.Analyze(landmarks, parsing);
                var eyebrows = _eyebrowAnalyzer.Analyze(landmarks, parsing);

                // Calculate overall confidence
                float overallConf = CalculateOverallConfidence(
                    faceGeometry, eyes, nose, mouth, jaw, eyebrows);

                return new ProportionsResult
                {
                    FaceGeometry = faceGeometry,
                    Eyes = eyes,
                    Nose = nose,
                    Mouth = mouth,
                    Jaw = jaw,
                    Eyebrows = eyebrows,
                    Confidence = overallConf,
                    Source = parsing != null && parsing.IsReliable
                        ? "Landmarks+Parsing"
                        : "Landmarks"
                };
            }
            catch (Exception ex)
            {
                SubModule.Log($"Proportions: Analysis failed - {ex.Message}");
                return CreateEmptyResult(ex.Message);
            }
        }

        public void Dispose()
        {
            _isReady = false;
        }

        #endregion

        #region Private

        private float CalculateOverallConfidence(
            FaceGeometryResult face,
            EyeAnalysisResult eyes,
            NoseAnalysisResult nose,
            MouthAnalysisResult mouth,
            JawAnalysisResult jaw,
            EyebrowAnalysisResult brows)
        {
            // Weighted average based on visual importance
            float total = 0;
            float weightSum = 0;

            void Add(float conf, float weight)
            {
                total += conf * weight;
                weightSum += weight;
            }

            Add(face?.Confidence ?? 0, 1.0f);   // Face shape important
            Add(eyes?.Confidence ?? 0, 1.2f);   // Eyes most important
            Add(nose?.Confidence ?? 0, 0.9f);
            Add(mouth?.Confidence ?? 0, 1.0f);
            Add(jaw?.Confidence ?? 0, 0.8f);
            Add(brows?.Confidence ?? 0, 0.6f);

            return weightSum > 0 ? total / weightSum : 0;
        }

        private ProportionsResult CreateEmptyResult(string reason)
        {
            return new ProportionsResult
            {
                FaceGeometry = null,
                Eyes = null,
                Nose = null,
                Mouth = null,
                Jaw = null,
                Eyebrows = null,
                Confidence = 0,
                Source = $"{Name} ({reason})"
            };
        }

        #endregion
    }

    #region Feature Analyzers

    /// <summary>
    /// Base class for feature analyzers.
    /// Works natively in FaceMesh 468 index space (936 or 1404 values).
    /// </summary>
    internal abstract class FeatureAnalyzer<TResult>
    {
        /// <summary>
        /// The working landmark array in FaceMesh 468 index space (xy interleaved, 936 values).
        /// Populated by EnsureFaceMesh() at the start of each Analyze call.
        /// </summary>
        private float[] _fm;

        /// <summary>
        /// Converts landmarks to FaceMesh 468 xy format, storing in _fm.
        /// - FaceMesh 3D (1404 values): extract xy, drop z
        /// - FaceMesh 2D (936 values): use directly
        /// </summary>
        protected void EnsureFaceMesh(float[] landmarks)
        {
            if (landmarks.Length >= 1404)
            {
                // FaceMesh 3D (468*3) -> extract xy only
                _fm = new float[936];
                for (int i = 0; i < 468; i++)
                {
                    _fm[i * 2] = landmarks[i * 3];
                    _fm[i * 2 + 1] = landmarks[i * 3 + 1];
                }
            }
            else if (landmarks.Length >= 936)
            {
                // FaceMesh 2D (468*2) -> use directly
                _fm = landmarks;
            }
            else
            {
                SubModule.Log($"Proportions: Landmark array too small ({landmarks.Length} values, need >= 936 for FaceMesh 468)");
                _fm = new float[936];
            }
        }

        /// <summary>Get X coordinate for FaceMesh landmark index</summary>
        protected float GetX(int fmIdx) => _fm[fmIdx * 2];

        /// <summary>Get Y coordinate for FaceMesh landmark index</summary>
        protected float GetY(int fmIdx) => _fm[fmIdx * 2 + 1];

        /// <summary>Euclidean distance between two FaceMesh landmark indices</summary>
        protected float Distance(int a, int b)
        {
            float dx = GetX(a) - GetX(b);
            float dy = GetY(a) - GetY(b);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Angle at point b formed by points a-b-c (FaceMesh indices)</summary>
        protected float Angle(int a, int b, int c)
        {
            float ax = GetX(a) - GetX(b);
            float ay = GetY(a) - GetY(b);
            float cx = GetX(c) - GetX(b);
            float cy = GetY(c) - GetY(b);
            float dot = ax * cx + ay * cy;
            float magA = (float)Math.Sqrt(ax * ax + ay * ay);
            float magC = (float)Math.Sqrt(cx * cx + cy * cy);
            if (magA < 0.0001f || magC < 0.0001f) return 90f;
            float cos = Math.Max(-1f, Math.Min(1f, dot / (magA * magC)));
            return (float)(Math.Acos(cos) * 180 / Math.PI);
        }

        public abstract TResult Analyze(float[] landmarks, FaceParsingResult parsing);
    }

    /// <summary>
    /// Analyzes overall face geometry.
    /// FaceMesh indices used:
    ///   Jaw endpoints: 234 (Dlib 0), 454 (Dlib 16)
    ///   Chin: 152 (Dlib 8), Nose bridge top: 168 (Dlib 27)
    ///   Jaw corners: 93 (1), 172 (4), 323 (15), 288 (12)
    ///   Mid-jaw: 136 (5), 365 (11), 58 (3), 397 (13)
    ///   Brow center: 105 (19), Nose bottom center: 2 (33)
    /// </summary>
    internal class FaceGeometryAnalyzer : FeatureAnalyzer<FaceGeometryResult>
    {
        public override FaceGeometryResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            EnsureFaceMesh(lm);

            // Use landmarks for basic measurements
            float faceWidth = Distance(234, 454);    // Jaw endpoints (Dlib 0, 16)
            float faceHeight = Distance(152, 168);   // Chin to brow (Dlib 8, 27)

            // Use face parsing if available for more accurate bounds
            if (parsing != null && parsing.IsReliable && parsing.HasRegion(FaceRegion.Skin))
            {
                var skinBounds = parsing.GetBounds(FaceRegion.Skin);
                if (skinBounds != null)
                {
                    float normX, normY, normW, normH;
                    skinBounds.GetNormalized(parsing.Width, parsing.Height, out normX, out normY, out normW, out normH);

                    // Override with parsing dimensions (more accurate for width)
                    faceWidth = normW;
                    faceHeight = normH;
                }
            }

            float ratio = faceHeight > 0 ? faceWidth / faceHeight : 0.8f;

            // Determine face shape
            FaceShape shape = ClassifyFaceShape(ratio);

            // Jaw angle (at jaw corners)
            float jawAngleLeft = Angle(93, 172, 152);    // Dlib 1, 4, 8
            float jawAngleRight = Angle(323, 288, 152);  // Dlib 15, 12, 8
            float avgJawAngle = (jawAngleLeft + jawAngleRight) / 2f;

            // Symmetry
            float leftWidth = Distance(152, 234);    // Dlib 8, 0
            float rightWidth = Distance(152, 454);   // Dlib 8, 16
            float symmetry = 1f - Math.Abs(leftWidth - rightWidth) / Math.Max(leftWidth, rightWidth);

            // Forehead and chin ratios
            float foreheadHeight = Distance(168, 105);   // Brow top to brow center (Dlib 27, 19)
            float chinHeight = Distance(2, 152);         // Nose bottom to chin (Dlib 33, 8)
            float foreheadRatio = foreheadHeight / (faceHeight + 0.001f);
            float chinRatio = chinHeight / (faceHeight + 0.001f);

            float confidence = parsing != null && parsing.IsReliable ? 0.85f : 0.65f;

            return new FaceGeometryResult
            {
                WidthHeightRatio = ratio,
                Shape = shape,
                JawAngle = avgJawAngle,
                Symmetry = symmetry,
                ForeheadRatio = foreheadRatio,
                ChinRatio = chinRatio,
                Confidence = confidence
            };
        }

        private FaceShape ClassifyFaceShape(float ratio)
        {
            // Simple classification based on ratio and jaw
            float jawTaper = Distance(136, 365) / (Distance(93, 323) + 0.001f);  // Dlib 5,11 / 1,15

            if (ratio > 0.95f) return FaceShape.Round;
            if (ratio > 0.85f && jawTaper < 0.7f) return FaceShape.Heart;
            if (ratio > 0.85f) return FaceShape.Square;
            if (ratio < 0.75f) return FaceShape.Oblong;
            if (jawTaper < 0.75f) return FaceShape.Diamond;
            return FaceShape.Oval;
        }
    }

    /// <summary>
    /// Analyzes eye region.
    /// FaceMesh indices used:
    ///   Right eye: 33 (36), 160 (37), 158 (38), 133 (39), 153 (40), 144 (41)
    ///   Left eye:  362 (42), 385 (43), 387 (44), 263 (45), 373 (46), 380 (47)
    ///   Jaw endpoints: 234 (0), 454 (16) for face width
    ///   Chin: 152 (8), Nose bridge: 168 (27) for face height
    /// </summary>
    internal class EyeAnalyzer : FeatureAnalyzer<EyeAnalysisResult>
    {
        public override EyeAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            EnsureFaceMesh(lm);

            float faceWidth = Distance(234, 454);    // Dlib 0, 16
            float faceHeight = Distance(152, 168);   // Dlib 8, 27

            // Landmark-based measurements
            float leftEyeWidth = Distance(33, 133);    // Dlib 36, 39
            float rightEyeWidth = Distance(362, 263);  // Dlib 42, 45
            float avgEyeWidth = (leftEyeWidth + rightEyeWidth) / 2f;

            float leftEyeHeight = Distance(160, 144);   // Dlib 37, 41
            float rightEyeHeight = Distance(385, 380);  // Dlib 43, 47
            float avgEyeHeight = (leftEyeHeight + rightEyeHeight) / 2f;

            float innerSpacing = Distance(133, 362);  // Dlib 39, 42
            float outerSpacing = Distance(33, 263);    // Dlib 36, 45

            // Eye tilt
            float leftTilt = GetY(33) - GetY(133);     // Dlib 36, 39
            float rightTilt = GetY(362) - GetY(263);   // Dlib 42, 45
            float avgTilt = (leftTilt + rightTilt) / 2f;

            // Override with parsing if available
            if (parsing != null && parsing.IsReliable)
            {
                var leftBounds = parsing.GetBounds(FaceRegion.LeftEye);
                var rightBounds = parsing.GetBounds(FaceRegion.RightEye);

                if (leftBounds.IsValid && rightBounds.IsValid)
                {
                    // Use parsing for more accurate width
                    float parsedLeftWidth = (float)leftBounds.Width / parsing.Width;
                    float parsedRightWidth = (float)rightBounds.Width / parsing.Width;
                    avgEyeWidth = (parsedLeftWidth + parsedRightWidth) / 2f;
                }
            }

            // Normalize
            float sizeRatio = avgEyeWidth / (faceWidth + 0.001f);
            float openness = avgEyeHeight / (avgEyeWidth + 0.001f);
            float innerSpacingNorm = innerSpacing / (faceWidth + 0.001f);
            float outerSpacingNorm = outerSpacing / (faceWidth + 0.001f);

            // Symmetry
            float eyeSymmetry = 1f - Math.Abs(leftEyeWidth - rightEyeWidth) /
                                     (Math.Max(leftEyeWidth, rightEyeWidth) + 0.001f);

            // Vertical position
            float eyeCenterY = (GetY(133) + GetY(362)) / 2f;  // Dlib 39, 42
            float verticalPos = eyeCenterY / (faceHeight + 0.001f);

            float confidence = parsing != null && parsing.IsReliable ? 0.85f : 0.70f;

            return new EyeAnalysisResult
            {
                SizeRatio = sizeRatio,
                InnerSpacing = innerSpacingNorm,
                OuterSpacing = outerSpacingNorm,
                Openness = openness,
                Tilt = avgTilt,
                Symmetry = eyeSymmetry,
                VerticalPosition = verticalPos,
                Confidence = confidence
            };
        }
    }

    /// <summary>
    /// Analyzes nose region.
    /// FaceMesh indices used:
    ///   Nose bridge top: 168 (27), tip: 195 (30)
    ///   Nose bottom: 98 (31), 97 (32), 2 (33), 326 (34), 327 (35)
    ///   Jaw endpoints: 234 (0), 454 (16), Chin: 152 (8)
    /// </summary>
    internal class NoseAnalyzer : FeatureAnalyzer<NoseAnalysisResult>
    {
        public override NoseAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            EnsureFaceMesh(lm);

            float faceWidth = Distance(234, 454);    // Dlib 0, 16
            float faceHeight = Distance(152, 168);   // Dlib 8, 27

            // Landmark-based
            float noseLength = Distance(168, 195);    // Dlib 27, 30
            float noseWidth = Distance(98, 327);      // Dlib 31, 35
            float bridgeWidth = Math.Abs(GetX(98) - GetX(327)) * 0.5f;  // Estimate (Dlib 31, 35)
            float tipWidth = noseWidth;

            // Tip angle (based on nose tip position relative to nose base)
            float tipY = GetY(195);                         // Dlib 30
            float baseY = (GetY(98) + GetY(327)) / 2f;     // Dlib 31, 35
            float tipAngle = (baseY - tipY) * 100f;  // Positive = upturned

            // Override with parsing if available
            if (parsing != null && parsing.IsReliable && parsing.HasRegion(FaceRegion.Nose))
            {
                var noseBounds = parsing.GetBounds(FaceRegion.Nose);
                noseWidth = (float)noseBounds.Width / parsing.Width;
                noseLength = (float)noseBounds.Height / parsing.Height;
            }

            // Normalize
            float lengthNorm = noseLength / (faceHeight + 0.001f);
            float widthNorm = noseWidth / (faceWidth + 0.001f);
            float bridgeNorm = bridgeWidth / (faceWidth + 0.001f);
            float tipNorm = tipWidth / (faceWidth + 0.001f);

            float confidence = parsing != null && parsing.IsReliable ? 0.80f : 0.60f;

            return new NoseAnalysisResult
            {
                Length = lengthNorm,
                Width = widthNorm,
                BridgeWidth = bridgeNorm,
                TipWidth = tipNorm,
                TipAngle = tipAngle,
                NostrilFlare = widthNorm * 0.8f,  // Approximate
                BridgeCurve = 0.5f,  // Would need profile for this
                Confidence = confidence
            };
        }
    }

    /// <summary>
    /// Analyzes mouth and lips.
    /// FaceMesh indices used:
    ///   Outer mouth: 61 (48), 40 (49), 37 (50), 0 (51-ulip_top), 267 (52), 270 (53),
    ///                291 (54), 321 (55), 314 (56), 17 (57-llip_bottom), 84 (58), 181 (59)
    ///   Inner mouth: 78 (60), 82 (61), 13 (62-inner_top), 312 (63), 308 (64),
    ///                317 (65), 14 (66-inner_bottom), 87 (67)
    ///   Jaw endpoints: 234 (0), 454 (16), Chin: 152 (8), Nose bridge: 168 (27)
    /// </summary>
    internal class MouthAnalyzer : FeatureAnalyzer<MouthAnalysisResult>
    {
        public override MouthAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            EnsureFaceMesh(lm);

            float faceWidth = Distance(234, 454);    // Dlib 0, 16
            float faceHeight = Distance(152, 168);   // Dlib 8, 27

            // Landmark-based
            float mouthWidth = Distance(61, 291);          // Dlib 48, 54
            float mouthHeight = Distance(0, 17);           // Dlib 51 (ulip_top), 57 (llip_bottom)
            float upperLipHeight = Distance(0, 13);        // Dlib 51, 62 (inner_top)
            float lowerLipHeight = Distance(17, 14);       // Dlib 57, 66 (inner_bottom)

            // Override with parsing if available
            if (parsing != null && parsing.IsReliable)
            {
                var lipBounds = parsing.GetCombinedLipBounds();
                if (lipBounds.IsValid)
                {
                    mouthWidth = (float)lipBounds.Width / parsing.Width;
                    mouthHeight = (float)lipBounds.Height / parsing.Height;
                }

                var upperBounds = parsing.GetBounds(FaceRegion.UpperLip);
                var lowerBounds = parsing.GetBounds(FaceRegion.LowerLip);
                if (upperBounds.IsValid)
                    upperLipHeight = (float)upperBounds.Height / parsing.Height;
                if (lowerBounds.IsValid)
                    lowerLipHeight = (float)lowerBounds.Height / parsing.Height;
            }

            // Normalize
            float widthNorm = mouthWidth / (faceWidth + 0.001f);
            float heightNorm = mouthHeight / (faceHeight + 0.001f);
            float lipRatio = upperLipHeight / (lowerLipHeight + 0.001f);
            float fullness = (upperLipHeight + lowerLipHeight) / (faceHeight + 0.001f);

            // Mouth curve (corners relative to center)
            float leftCornerY = GetY(61);    // Dlib 48
            float rightCornerY = GetY(291);  // Dlib 54
            float centerY = GetY(0);         // Dlib 51 (ulip_top)
            float curve = ((leftCornerY + rightCornerY) / 2f - centerY) * 10f;

            // Cupid's bow
            float cupidBow = Math.Abs(GetY(37) - GetY(267)) / (faceHeight + 0.001f);  // Dlib 50, 52

            // Vertical position
            float mouthCenterY = (GetY(0) + GetY(17)) / 2f;  // Dlib 51, 57
            float verticalPos = mouthCenterY / (faceHeight + 0.001f);

            float confidence = parsing != null && parsing.IsReliable ? 0.85f : 0.65f;

            return new MouthAnalysisResult
            {
                Width = widthNorm,
                Height = heightNorm,
                UpperLipHeight = upperLipHeight / (faceHeight + 0.001f),
                LowerLipHeight = lowerLipHeight / (faceHeight + 0.001f),
                LipRatio = lipRatio,
                Fullness = fullness,
                CupidBowDepth = cupidBow,
                Curve = curve,
                VerticalPosition = verticalPos,
                Confidence = confidence
            };
        }
    }

    /// <summary>
    /// Analyzes jaw and chin.
    /// FaceMesh indices used:
    ///   Jaw: 234 (0), 93 (1), 132 (2), 58 (3), 172 (4), 136 (5), 150 (6),
    ///        176 (7), 152 (8-chin), 400 (9), 378 (10), 365 (11), 397 (12),
    ///        288 (13), 361 (14), 323 (15), 454 (16)
    /// </summary>
    internal class JawAnalyzer : FeatureAnalyzer<JawAnalysisResult>
    {
        public override JawAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            EnsureFaceMesh(lm);

            float faceWidth = Distance(234, 454);   // Dlib 0, 16

            // Jaw measurements
            float jawWidthTop = Distance(93, 323);   // Dlib 1, 15
            float jawWidthMid = Distance(58, 288);   // Dlib 3, 13
            float jawWidthLow = Distance(136, 365);  // Dlib 5, 11
            float chinWidth = Distance(176, 400);    // Dlib 7, 9

            // Jaw taper
            float taper = jawWidthLow / (jawWidthTop + 0.001f);

            // Jaw angle
            float jawAngleLeft = Angle(93, 172, 152);    // Dlib 1, 4, 8
            float jawAngleRight = Angle(323, 288, 152);  // Dlib 15, 12, 8
            float avgAngle = (jawAngleLeft + jawAngleRight) / 2f;

            // Chin pointedness
            float chinPointedness = 1f - (chinWidth / (jawWidthLow + 0.001f));

            // Symmetry
            float symmetry = 1f - Math.Abs(jawAngleLeft - jawAngleRight) /
                                  (Math.Max(jawAngleLeft, jawAngleRight) + 0.001f);

            // Normalize
            float widthNorm = jawWidthMid / (faceWidth + 0.001f);
            float chinNorm = chinWidth / (faceWidth + 0.001f);

            return new JawAnalysisResult
            {
                Width = widthNorm,
                Taper = taper,
                Angle = avgAngle,
                ChinWidth = chinNorm,
                ChinPointedness = chinPointedness,
                Symmetry = symmetry,
                Confidence = 0.70f
            };
        }
    }

    /// <summary>
    /// Analyzes eyebrows.
    /// FaceMesh indices used:
    ///   Right brow: 70 (17), 63 (18), 105 (19), 66 (20), 107 (21)
    ///   Left brow:  336 (22), 296 (23), 334 (24), 293 (25), 300 (26)
    ///   Jaw endpoints: 234 (0), 454 (16), Chin: 152 (8), Nose bridge: 168 (27)
    /// </summary>
    internal class EyebrowAnalyzer : FeatureAnalyzer<EyebrowAnalysisResult>
    {
        public override EyebrowAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            EnsureFaceMesh(lm);

            float faceWidth = Distance(234, 454);    // Dlib 0, 16
            float faceHeight = Distance(152, 168);   // Dlib 8, 27

            // Landmark-based
            float leftLength = Distance(70, 107);    // Dlib 17, 21
            float rightLength = Distance(336, 300);  // Dlib 22, 26
            float avgLength = (leftLength + rightLength) / 2f;

            // Brow spacing
            float spacing = Distance(107, 336);    // Dlib 21, 22

            // Arch (peak point relative to ends)
            float leftPeakY = GetY(105);     // Dlib 19
            float leftStartY = GetY(70);     // Dlib 17
            float leftEndY = GetY(107);      // Dlib 21
            float leftArch = leftPeakY - (leftStartY + leftEndY) / 2f;

            float rightPeakY = GetY(334);    // Dlib 24
            float rightStartY = GetY(336);   // Dlib 22
            float rightEndY = GetY(300);     // Dlib 26
            float rightArch = rightPeakY - (rightStartY + rightEndY) / 2f;

            float avgArch = (leftArch + rightArch) / 2f;

            // Arch position (where along the brow is the peak)
            float leftArchPos = (GetX(105) - GetX(70)) / (leftLength + 0.001f);    // Dlib 19, 17
            float rightArchPos = (GetX(300) - GetX(334)) / (rightLength + 0.001f);  // Dlib 26, 24
            float avgArchPos = (leftArchPos + rightArchPos) / 2f;

            // Tilt
            float leftTilt = GetY(107) - GetY(70);     // Dlib 21, 17
            float rightTilt = GetY(336) - GetY(300);   // Dlib 22, 26
            float avgTilt = (leftTilt + rightTilt) / 2f;

            // Symmetry
            float symmetry = 1f - Math.Abs(leftLength - rightLength) /
                                  (Math.Max(leftLength, rightLength) + 0.001f);

            // Override with parsing for thickness
            float thickness = 0.1f;  // Default
            if (parsing != null && parsing.IsReliable)
            {
                var leftBrow = parsing.GetBounds(FaceRegion.LeftBrow);
                var rightBrow = parsing.GetBounds(FaceRegion.RightBrow);
                if (leftBrow.IsValid && rightBrow.IsValid)
                {
                    thickness = ((float)leftBrow.Height + rightBrow.Height) /
                                (2f * parsing.Height);
                }
            }

            // Normalize
            float lengthNorm = avgLength / (faceWidth + 0.001f);
            float spacingNorm = spacing / (faceWidth + 0.001f);
            float archNorm = Math.Abs(avgArch) / (faceHeight + 0.001f);

            return new EyebrowAnalysisResult
            {
                Length = lengthNorm,
                Thickness = thickness,
                ArchHeight = archNorm,
                ArchPosition = avgArchPos,
                Spacing = spacingNorm,
                Tilt = avgTilt,
                Symmetry = symmetry,
                Confidence = parsing != null && parsing.IsReliable ? 0.75f : 0.55f
            };
        }
    }

    #endregion
}
