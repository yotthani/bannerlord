using System;
using System.Collections.Generic;
using FaceLearner.ML.Modules.Core;

namespace FaceLearner.ML.Modules.Proportions
{
    #region Feature-Specific Results
    
    /// <summary>
    /// Overall face geometry analysis
    /// </summary>
    public class FaceGeometryResult
    {
        /// <summary>Face width to height ratio (0.7=long, 1.0=square)</summary>
        public float WidthHeightRatio { get; set; }
        
        /// <summary>Detected face shape category</summary>
        public FaceShape Shape { get; set; }
        
        /// <summary>Jaw angle in degrees (smaller=sharper)</summary>
        public float JawAngle { get; set; }
        
        /// <summary>How symmetric is the face (0-1)</summary>
        public float Symmetry { get; set; }
        
        /// <summary>Forehead height relative to face</summary>
        public float ForeheadRatio { get; set; }
        
        /// <summary>Chin height relative to face</summary>
        public float ChinRatio { get; set; }
        
        public float Confidence { get; set; }
    }
    
    /// <summary>
    /// Eye region analysis
    /// </summary>
    public class EyeAnalysisResult
    {
        /// <summary>Eye width relative to face width</summary>
        public float SizeRatio { get; set; }
        
        /// <summary>Inner eye corner distance relative to face</summary>
        public float InnerSpacing { get; set; }
        
        /// <summary>Outer eye corner distance relative to face</summary>
        public float OuterSpacing { get; set; }
        
        /// <summary>Eye height/width ratio (larger=more open)</summary>
        public float Openness { get; set; }
        
        /// <summary>Eye tilt angle (positive=outer higher)</summary>
        public float Tilt { get; set; }
        
        /// <summary>Left/right eye symmetry (1=perfect)</summary>
        public float Symmetry { get; set; }
        
        /// <summary>Vertical position on face (0=top, 1=bottom)</summary>
        public float VerticalPosition { get; set; }
        
        public float Confidence { get; set; }
    }
    
    /// <summary>
    /// Nose analysis
    /// </summary>
    public class NoseAnalysisResult
    {
        /// <summary>Nose length relative to face height</summary>
        public float Length { get; set; }
        
        /// <summary>Nose width at widest point relative to face</summary>
        public float Width { get; set; }
        
        /// <summary>Bridge width (top of nose)</summary>
        public float BridgeWidth { get; set; }
        
        /// <summary>Tip width (bottom of nose)</summary>
        public float TipWidth { get; set; }
        
        /// <summary>Tip angle (-=droopy, 0=straight, +=upturned)</summary>
        public float TipAngle { get; set; }
        
        /// <summary>Nostril flare (width of nostrils)</summary>
        public float NostrilFlare { get; set; }
        
        /// <summary>Bridge curve (how curved is the nose profile)</summary>
        public float BridgeCurve { get; set; }
        
        public float Confidence { get; set; }
    }
    
    /// <summary>
    /// Mouth and lip analysis
    /// </summary>
    public class MouthAnalysisResult
    {
        /// <summary>Mouth width relative to face</summary>
        public float Width { get; set; }
        
        /// <summary>Mouth height (when closed)</summary>
        public float Height { get; set; }
        
        /// <summary>Upper lip height</summary>
        public float UpperLipHeight { get; set; }
        
        /// <summary>Lower lip height</summary>
        public float LowerLipHeight { get; set; }
        
        /// <summary>Upper/lower lip ratio</summary>
        public float LipRatio { get; set; }
        
        /// <summary>Total lip fullness relative to face</summary>
        public float Fullness { get; set; }
        
        /// <summary>Cupid's bow depth</summary>
        public float CupidBowDepth { get; set; }
        
        /// <summary>Mouth curve (negative=frown, positive=smile)</summary>
        public float Curve { get; set; }
        
        /// <summary>Vertical position on face</summary>
        public float VerticalPosition { get; set; }
        
        public float Confidence { get; set; }
    }
    
    /// <summary>
    /// Jaw and chin analysis
    /// </summary>
    public class JawAnalysisResult
    {
        /// <summary>Jaw width at widest point</summary>
        public float Width { get; set; }
        
        /// <summary>Jaw taper (how much it narrows to chin)</summary>
        public float Taper { get; set; }
        
        /// <summary>Jaw angle (sharper=more angular)</summary>
        public float Angle { get; set; }
        
        /// <summary>Chin width</summary>
        public float ChinWidth { get; set; }
        
        /// <summary>Chin pointedness (how pointed is the chin)</summary>
        public float ChinPointedness { get; set; }
        
        /// <summary>Left/right jaw symmetry</summary>
        public float Symmetry { get; set; }
        
        public float Confidence { get; set; }
    }
    
    /// <summary>
    /// Eyebrow analysis
    /// </summary>
    public class EyebrowAnalysisResult
    {
        /// <summary>Brow length relative to eye width</summary>
        public float Length { get; set; }
        
        /// <summary>Brow thickness</summary>
        public float Thickness { get; set; }
        
        /// <summary>Brow arch height</summary>
        public float ArchHeight { get; set; }
        
        /// <summary>Arch position (0=inner, 1=outer)</summary>
        public float ArchPosition { get; set; }
        
        /// <summary>Distance between brows</summary>
        public float Spacing { get; set; }
        
        /// <summary>Brow tilt angle</summary>
        public float Tilt { get; set; }
        
        /// <summary>Left/right symmetry</summary>
        public float Symmetry { get; set; }
        
        public float Confidence { get; set; }
    }
    
    #endregion
    
    #region Combined Result
    
    /// <summary>
    /// Complete facial proportions analysis.
    /// Contains measurements for all face features.
    /// </summary>
    public class ProportionsResult : IDetectionResult
    {
        /// <summary>Overall face geometry</summary>
        public FaceGeometryResult FaceGeometry { get; set; }
        
        /// <summary>Eye measurements</summary>
        public EyeAnalysisResult Eyes { get; set; }
        
        /// <summary>Nose measurements</summary>
        public NoseAnalysisResult Nose { get; set; }
        
        /// <summary>Mouth/lip measurements</summary>
        public MouthAnalysisResult Mouth { get; set; }
        
        /// <summary>Jaw/chin measurements</summary>
        public JawAnalysisResult Jaw { get; set; }
        
        /// <summary>Eyebrow measurements</summary>
        public EyebrowAnalysisResult Eyebrows { get; set; }
        
        #region IDetectionResult
        
        public float Confidence { get; set; }
        public bool IsReliable => Confidence >= ConfidenceThresholds.Acceptable;
        public string Source { get; set; }
        
        #endregion
        
        #region Convenience
        
        /// <summary>
        /// Get confidence for a specific feature
        /// </summary>
        public float GetFeatureConfidence(FaceFeature feature)
        {
            switch (feature)
            {
                case FaceFeature.FaceShape: return FaceGeometry?.Confidence ?? 0;
                case FaceFeature.Eyes: return Eyes?.Confidence ?? 0;
                case FaceFeature.Nose: return Nose?.Confidence ?? 0;
                case FaceFeature.Mouth: return Mouth?.Confidence ?? 0;
                case FaceFeature.Jaw: return Jaw?.Confidence ?? 0;
                case FaceFeature.Eyebrows: return Eyebrows?.Confidence ?? 0;
                default: return 0;
            }
        }
        
        /// <summary>
        /// Get all measurements as a flat dictionary (for ML/comparison)
        /// </summary>
        public Dictionary<string, float> ToFlatDictionary()
        {
            var dict = new Dictionary<string, float>();
            
            // Face geometry
            if (FaceGeometry != null)
            {
                dict["face_width_height_ratio"] = FaceGeometry.WidthHeightRatio;
                dict["face_jaw_angle"] = FaceGeometry.JawAngle;
                dict["face_symmetry"] = FaceGeometry.Symmetry;
                dict["face_forehead_ratio"] = FaceGeometry.ForeheadRatio;
                dict["face_chin_ratio"] = FaceGeometry.ChinRatio;
            }
            
            // Eyes
            if (Eyes != null)
            {
                dict["eye_size_ratio"] = Eyes.SizeRatio;
                dict["eye_inner_spacing"] = Eyes.InnerSpacing;
                dict["eye_outer_spacing"] = Eyes.OuterSpacing;
                dict["eye_openness"] = Eyes.Openness;
                dict["eye_tilt"] = Eyes.Tilt;
                dict["eye_symmetry"] = Eyes.Symmetry;
                dict["eye_vertical_position"] = Eyes.VerticalPosition;
            }
            
            // Nose
            if (Nose != null)
            {
                dict["nose_length"] = Nose.Length;
                dict["nose_width"] = Nose.Width;
                dict["nose_bridge_width"] = Nose.BridgeWidth;
                dict["nose_tip_width"] = Nose.TipWidth;
                dict["nose_tip_angle"] = Nose.TipAngle;
                dict["nose_nostril_flare"] = Nose.NostrilFlare;
                dict["nose_bridge_curve"] = Nose.BridgeCurve;
            }
            
            // Mouth
            if (Mouth != null)
            {
                dict["mouth_width"] = Mouth.Width;
                dict["mouth_height"] = Mouth.Height;
                dict["mouth_upper_lip"] = Mouth.UpperLipHeight;
                dict["mouth_lower_lip"] = Mouth.LowerLipHeight;
                dict["mouth_lip_ratio"] = Mouth.LipRatio;
                dict["mouth_fullness"] = Mouth.Fullness;
                dict["mouth_cupid_bow"] = Mouth.CupidBowDepth;
                dict["mouth_curve"] = Mouth.Curve;
                dict["mouth_vertical_position"] = Mouth.VerticalPosition;
            }
            
            // Jaw
            if (Jaw != null)
            {
                dict["jaw_width"] = Jaw.Width;
                dict["jaw_taper"] = Jaw.Taper;
                dict["jaw_angle"] = Jaw.Angle;
                dict["jaw_chin_width"] = Jaw.ChinWidth;
                dict["jaw_chin_pointedness"] = Jaw.ChinPointedness;
                dict["jaw_symmetry"] = Jaw.Symmetry;
            }
            
            // Eyebrows
            if (Eyebrows != null)
            {
                dict["brow_length"] = Eyebrows.Length;
                dict["brow_thickness"] = Eyebrows.Thickness;
                dict["brow_arch_height"] = Eyebrows.ArchHeight;
                dict["brow_arch_position"] = Eyebrows.ArchPosition;
                dict["brow_spacing"] = Eyebrows.Spacing;
                dict["brow_tilt"] = Eyebrows.Tilt;
                dict["brow_symmetry"] = Eyebrows.Symmetry;
            }
            
            return dict;
        }
        
        /// <summary>
        /// Convert to float array for ML (fixed order)
        /// </summary>
        public float[] ToArray()
        {
            var dict = ToFlatDictionary();
            var keys = new[]
            {
                // Face (5)
                "face_width_height_ratio", "face_jaw_angle", "face_symmetry", 
                "face_forehead_ratio", "face_chin_ratio",
                // Eyes (7)
                "eye_size_ratio", "eye_inner_spacing", "eye_outer_spacing",
                "eye_openness", "eye_tilt", "eye_symmetry", "eye_vertical_position",
                // Nose (7)
                "nose_length", "nose_width", "nose_bridge_width", "nose_tip_width",
                "nose_tip_angle", "nose_nostril_flare", "nose_bridge_curve",
                // Mouth (9)
                "mouth_width", "mouth_height", "mouth_upper_lip", "mouth_lower_lip",
                "mouth_lip_ratio", "mouth_fullness", "mouth_cupid_bow", 
                "mouth_curve", "mouth_vertical_position",
                // Jaw (6)
                "jaw_width", "jaw_taper", "jaw_angle", "jaw_chin_width",
                "jaw_chin_pointedness", "jaw_symmetry",
                // Brows (7)
                "brow_length", "brow_thickness", "brow_arch_height",
                "brow_arch_position", "brow_spacing", "brow_tilt", "brow_symmetry"
            };
            
            var result = new float[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                float val;
                dict.TryGetValue(keys[i], out val);
                result[i] = val;
            }
            return result;
        }
        
        #endregion
    }
    
    #endregion
}
