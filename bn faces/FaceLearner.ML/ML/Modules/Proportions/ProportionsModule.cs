using System;
using System.Drawing;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.FaceParsing;

namespace FaceLearner.ML.Modules.Proportions
{
    /// <summary>
    /// Proportions Module - Analyzes facial proportions using landmarks + face parsing.
    /// 
    /// This is a KEY module for accurate face matching:
    /// - Combines MediaPipe landmarks with BiSeNet face parsing
    /// - Provides per-feature measurements (eyes, nose, mouth, etc.)
    /// - Each measurement has its own confidence score
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
            SubModule.Log($"Proportions: Initialized (6 analyzers ready)");
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
        /// Full analysis using pre-computed landmarks and face parsing
        /// </summary>
        public ProportionsResult Analyze(float[] landmarks, FaceParsingResult parsing)
        {
            if (landmarks == null || landmarks.Length < 136)
            {
                return CreateEmptyResult("Invalid landmarks");
            }
            
            try
            {
                // Convert 468-point to 68-point if needed (for compatibility)
                // FaceMesh: 1404 (468*3) or 936 (468*2) values
                // Dlib: 136 (68*2) values
                float[] lm68 = (landmarks.Length >= 936 || landmarks.Length >= 1404)
                    ? LandmarkConverter.ConvertFaceMeshTo68(landmarks) 
                    : landmarks;
                
                // Analyze each feature
                var faceGeometry = _faceGeometry.Analyze(lm68, parsing);
                var eyes = _eyeAnalyzer.Analyze(lm68, parsing);
                var nose = _noseAnalyzer.Analyze(lm68, parsing);
                var mouth = _mouthAnalyzer.Analyze(lm68, parsing);
                var jaw = _jawAnalyzer.Analyze(lm68, parsing);
                var eyebrows = _eyebrowAnalyzer.Analyze(lm68, parsing);
                
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
    
    #region Landmark Converter
    
    /// <summary>
    /// Converts between landmark formats
    /// </summary>
    internal static class LandmarkConverter
    {
        /// <summary>
        /// Convert 468-point FaceMesh to 68-point dlib format.
        /// Handles both 2D (936 values) and 3D (1404 values) landmark formats.
        /// </summary>
        public static float[] ConvertFaceMeshTo68(float[] faceMesh)
        {
            if (faceMesh == null) return null;
            
            // Determine if 2D or 3D format
            bool is3D = faceMesh.Length >= 1404;  // 468 * 3
            bool is2D = faceMesh.Length >= 936;   // 468 * 2
            
            if (!is2D && !is3D)
            {
                return faceMesh;  // Return as-is if not FaceMesh format
            }
            
            int stride = is3D ? 3 : 2;  // xyz vs xy
            
            // FaceMesh index to dlib 68-point mapping
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
            
            float[] result = new float[136];  // 68 * 2 (xy only)
            for (int i = 0; i < 68 && i < mapping.Length; i++)
            {
                int fmIdx = mapping[i];
                int srcIdx = fmIdx * stride;
                
                if (srcIdx + 1 < faceMesh.Length)
                {
                    result[i * 2] = faceMesh[srcIdx];         // x
                    result[i * 2 + 1] = faceMesh[srcIdx + 1]; // y
                    // z is discarded
                }
            }
            return result;
        }
    }
    
    #endregion
    
    #region Feature Analyzers
    
    /// <summary>
    /// Base class for feature analyzers
    /// </summary>
    internal abstract class FeatureAnalyzer<TResult>
    {
        protected float GetX(float[] lm, int idx) => lm[idx * 2];
        protected float GetY(float[] lm, int idx) => lm[idx * 2 + 1];
        
        protected float Distance(float[] lm, int a, int b)
        {
            float dx = GetX(lm, a) - GetX(lm, b);
            float dy = GetY(lm, a) - GetY(lm, b);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
        
        protected float Angle(float[] lm, int a, int b, int c)
        {
            float ax = GetX(lm, a) - GetX(lm, b);
            float ay = GetY(lm, a) - GetY(lm, b);
            float cx = GetX(lm, c) - GetX(lm, b);
            float cy = GetY(lm, c) - GetY(lm, b);
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
    /// Analyzes overall face geometry
    /// </summary>
    internal class FaceGeometryAnalyzer : FeatureAnalyzer<FaceGeometryResult>
    {
        public override FaceGeometryResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            // Use landmarks for basic measurements
            float faceWidth = Distance(lm, 0, 16);   // Jaw endpoints
            float faceHeight = Distance(lm, 8, 27);  // Chin to brow
            
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
            FaceShape shape = ClassifyFaceShape(ratio, lm);
            
            // Jaw angle (at jaw corners)
            float jawAngleLeft = Angle(lm, 1, 4, 8);
            float jawAngleRight = Angle(lm, 15, 12, 8);
            float avgJawAngle = (jawAngleLeft + jawAngleRight) / 2f;
            
            // Symmetry
            float leftWidth = Distance(lm, 8, 0);
            float rightWidth = Distance(lm, 8, 16);
            float symmetry = 1f - Math.Abs(leftWidth - rightWidth) / Math.Max(leftWidth, rightWidth);
            
            // Forehead and chin ratios
            float foreheadHeight = Distance(lm, 27, 19);  // Brow center to hairline estimate
            float chinHeight = Distance(lm, 33, 8);       // Nose bottom to chin
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
        
        private FaceShape ClassifyFaceShape(float ratio, float[] lm)
        {
            // Simple classification based on ratio and jaw
            float jawTaper = Distance(lm, 5, 11) / (Distance(lm, 1, 15) + 0.001f);
            
            if (ratio > 0.95f) return FaceShape.Round;
            if (ratio > 0.85f && jawTaper < 0.7f) return FaceShape.Heart;
            if (ratio > 0.85f) return FaceShape.Square;
            if (ratio < 0.75f) return FaceShape.Oblong;
            if (jawTaper < 0.75f) return FaceShape.Diamond;
            return FaceShape.Oval;
        }
    }
    
    /// <summary>
    /// Analyzes eye region
    /// </summary>
    internal class EyeAnalyzer : FeatureAnalyzer<EyeAnalysisResult>
    {
        public override EyeAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            float faceWidth = Distance(lm, 0, 16);
            float faceHeight = Distance(lm, 8, 27);
            
            // Landmark-based measurements
            float leftEyeWidth = Distance(lm, 36, 39);
            float rightEyeWidth = Distance(lm, 42, 45);
            float avgEyeWidth = (leftEyeWidth + rightEyeWidth) / 2f;
            
            float leftEyeHeight = Distance(lm, 37, 41);
            float rightEyeHeight = Distance(lm, 43, 47);
            float avgEyeHeight = (leftEyeHeight + rightEyeHeight) / 2f;
            
            float innerSpacing = Distance(lm, 39, 42);
            float outerSpacing = Distance(lm, 36, 45);
            
            // Eye tilt
            float leftTilt = GetY(lm, 36) - GetY(lm, 39);
            float rightTilt = GetY(lm, 42) - GetY(lm, 45);
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
            float eyeCenterY = (GetY(lm, 39) + GetY(lm, 42)) / 2f;
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
    /// Analyzes nose region
    /// </summary>
    internal class NoseAnalyzer : FeatureAnalyzer<NoseAnalysisResult>
    {
        public override NoseAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            float faceWidth = Distance(lm, 0, 16);
            float faceHeight = Distance(lm, 8, 27);
            
            // Landmark-based
            float noseLength = Distance(lm, 27, 30);
            float noseWidth = Distance(lm, 31, 35);
            float bridgeWidth = Math.Abs(GetX(lm, 31) - GetX(lm, 35)) * 0.5f; // Estimate
            float tipWidth = noseWidth;
            
            // Tip angle (based on nose tip position relative to nose base)
            float tipY = GetY(lm, 30);
            float baseY = (GetY(lm, 31) + GetY(lm, 35)) / 2f;
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
    /// Analyzes mouth and lips
    /// </summary>
    internal class MouthAnalyzer : FeatureAnalyzer<MouthAnalysisResult>
    {
        public override MouthAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            float faceWidth = Distance(lm, 0, 16);
            float faceHeight = Distance(lm, 8, 27);
            
            // Landmark-based
            float mouthWidth = Distance(lm, 48, 54);
            float mouthHeight = Distance(lm, 51, 57);
            float upperLipHeight = Distance(lm, 51, 62);
            float lowerLipHeight = Distance(lm, 57, 66);
            
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
            float leftCornerY = GetY(lm, 48);
            float rightCornerY = GetY(lm, 54);
            float centerY = GetY(lm, 51);
            float curve = ((leftCornerY + rightCornerY) / 2f - centerY) * 10f;
            
            // Cupid's bow
            float cupidBow = Math.Abs(GetY(lm, 50) - GetY(lm, 52)) / (faceHeight + 0.001f);
            
            // Vertical position
            float mouthCenterY = (GetY(lm, 51) + GetY(lm, 57)) / 2f;
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
    /// Analyzes jaw and chin
    /// </summary>
    internal class JawAnalyzer : FeatureAnalyzer<JawAnalysisResult>
    {
        public override JawAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            float faceWidth = Distance(lm, 0, 16);
            
            // Jaw measurements
            float jawWidthTop = Distance(lm, 1, 15);
            float jawWidthMid = Distance(lm, 3, 13);
            float jawWidthLow = Distance(lm, 5, 11);
            float chinWidth = Distance(lm, 7, 9);
            
            // Jaw taper
            float taper = jawWidthLow / (jawWidthTop + 0.001f);
            
            // Jaw angle
            float jawAngleLeft = Angle(lm, 1, 4, 8);
            float jawAngleRight = Angle(lm, 15, 12, 8);
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
    /// Analyzes eyebrows
    /// </summary>
    internal class EyebrowAnalyzer : FeatureAnalyzer<EyebrowAnalysisResult>
    {
        public override EyebrowAnalysisResult Analyze(float[] lm, FaceParsingResult parsing)
        {
            float faceWidth = Distance(lm, 0, 16);
            float faceHeight = Distance(lm, 8, 27);
            
            // Landmark-based
            float leftLength = Distance(lm, 17, 21);
            float rightLength = Distance(lm, 22, 26);
            float avgLength = (leftLength + rightLength) / 2f;
            
            // Brow spacing
            float spacing = Distance(lm, 21, 22);
            
            // Arch (peak point relative to ends)
            float leftPeakY = GetY(lm, 19);
            float leftStartY = GetY(lm, 17);
            float leftEndY = GetY(lm, 21);
            float leftArch = leftPeakY - (leftStartY + leftEndY) / 2f;
            
            float rightPeakY = GetY(lm, 24);
            float rightStartY = GetY(lm, 22);
            float rightEndY = GetY(lm, 26);
            float rightArch = rightPeakY - (rightStartY + rightEndY) / 2f;
            
            float avgArch = (leftArch + rightArch) / 2f;
            
            // Arch position (where along the brow is the peak)
            float leftArchPos = (GetX(lm, 19) - GetX(lm, 17)) / (leftLength + 0.001f);
            float rightArchPos = (GetX(lm, 26) - GetX(lm, 24)) / (rightLength + 0.001f);
            float avgArchPos = (leftArchPos + rightArchPos) / 2f;
            
            // Tilt
            float leftTilt = GetY(lm, 21) - GetY(lm, 17);
            float rightTilt = GetY(lm, 22) - GetY(lm, 26);
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
