using System;
using System.Collections.Generic;
using FaceLearner.ML.Modules.Core;

namespace FaceLearner.ML.Modules.FaceParsing
{
    /// <summary>
    /// Semantic segmentation labels for CelebAMask-HQ / BiSeNet
    /// </summary>
    public enum FaceRegion
    {
        Background = 0,
        Skin = 1,
        LeftBrow = 2,
        RightBrow = 3,
        LeftEye = 4,
        RightEye = 5,
        EyeGlasses = 6,
        LeftEar = 7,
        RightEar = 8,
        Earring = 9,
        Nose = 10,
        Mouth = 11,  // Inner mouth
        UpperLip = 12,
        LowerLip = 13,
        Neck = 14,
        Necklace = 15,
        Cloth = 16,
        Hair = 17,
        Hat = 18
    }
    
    /// <summary>
    /// Bounding box and statistics for a single face region
    /// </summary>
    public class RegionBounds
    {
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public int PixelCount { get; set; }
        
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
        public float CenterX => (MinX + MaxX) / 2f;
        public float CenterY => (MinY + MaxY) / 2f;
        
        /// <summary>Is this a valid region with actual pixels?</summary>
        public bool IsValid => PixelCount > 0 && Width > 0 && Height > 0;
        
        /// <summary>
        /// Get normalized coordinates (0-1 range)
        /// </summary>
        public void GetNormalized(int imageWidth, int imageHeight, out float normX, out float normY, out float normW, out float normH)
        {
            normX = imageWidth > 0 ? MinX / (float)imageWidth : 0;
            normY = imageHeight > 0 ? MinY / (float)imageHeight : 0;
            normW = imageWidth > 0 ? Width / (float)imageWidth : 0;
            normH = imageHeight > 0 ? Height / (float)imageHeight : 0;
        }
    }
    
    /// <summary>
    /// Statistics for a single face region (legacy compatibility)
    /// </summary>
    public class RegionStats
    {
        /// <summary>Number of pixels in this region</summary>
        public int PixelCount { get; set; }
        
        /// <summary>Percentage of face area (0-1)</summary>
        public float AreaRatio { get; set; }
        
        /// <summary>Bounding box: left</summary>
        public int Left { get; set; }
        
        /// <summary>Bounding box: top</summary>
        public int Top { get; set; }
        
        /// <summary>Bounding box: right</summary>
        public int Right { get; set; }
        
        /// <summary>Bounding box: bottom</summary>
        public int Bottom { get; set; }
        
        /// <summary>Center X position (normalized 0-1)</summary>
        public float CenterX { get; set; }
        
        /// <summary>Center Y position (normalized 0-1)</summary>
        public float CenterY { get; set; }
        
        /// <summary>Width relative to face</summary>
        public float Width => (Right - Left) / (float)Math.Max(1, Right);
        
        /// <summary>Height relative to face</summary>
        public float Height => (Bottom - Top) / (float)Math.Max(1, Bottom);
        
        /// <summary>Create from RegionBounds</summary>
        public static RegionStats FromBounds(RegionBounds bounds, int imageWidth, int imageHeight)
        {
            return new RegionStats
            {
                PixelCount = bounds.PixelCount,
                Left = bounds.MinX,
                Top = bounds.MinY,
                Right = bounds.MaxX,
                Bottom = bounds.MaxY,
                CenterX = bounds.CenterX / imageWidth,
                CenterY = bounds.CenterY / imageHeight,
                AreaRatio = bounds.PixelCount / (float)(imageWidth * imageHeight)
            };
        }
    }
    
    /// <summary>
    /// Facial hair analysis (beard, mustache, etc.)
    /// </summary>
    public class FacialHairResult
    {
        /// <summary>Has any facial hair</summary>
        public bool HasFacialHair { get; set; }
        
        /// <summary>Beard coverage (0-1)</summary>
        public float BeardCoverage { get; set; }
        
        /// <summary>Mustache coverage (0-1)</summary>
        public float MustacheCoverage { get; set; }
        
        /// <summary>Stubble vs full beard (0=clean, 0.5=stubble, 1=full)</summary>
        public float Fullness { get; set; }
        
        /// <summary>Detection confidence</summary>
        public float Confidence { get; set; }
        
        public bool IsReliable => Confidence >= 0.6f;
    }
    
    /// <summary>
    /// Complete face parsing result.
    /// Implements IDetectionResult for module compatibility.
    /// </summary>
    public class FaceParsingResult : IDetectionResult
    {
        /// <summary>Raw segmentation mask [width, height] = region label</summary>
        public byte[,] Mask { get; set; }
        
        /// <summary>Image width</summary>
        public int Width { get; set; }
        
        /// <summary>Image height</summary>
        public int Height { get; set; }
        
        /// <summary>Per-region bounding boxes</summary>
        public Dictionary<FaceRegion, RegionBounds> RegionBounds { get; set; }
        
        /// <summary>Per-region pixel counts</summary>
        public Dictionary<FaceRegion, int> RegionPixelCounts { get; set; }
        
        /// <summary>Per-region statistics (legacy compatibility)</summary>
        public Dictionary<FaceRegion, RegionStats> Regions { get; set; }
        
        /// <summary>Facial hair analysis</summary>
        public FacialHairResult FacialHair { get; set; }
        
        /// <summary>Total skin area as percentage of face bounding box</summary>
        public float SkinCoverage { get; set; }
        
        #region IDetectionResult
        
        /// <summary>Overall parsing confidence</summary>
        public float Confidence { get; set; }
        
        /// <summary>Is reliable if we found key face regions</summary>
        public bool IsReliable => Confidence >= ConfidenceThresholds.Acceptable;
        
        /// <summary>Which detector produced this result</summary>
        public string Source { get; set; }
        
        #endregion
        
        /// <summary>Processing time in milliseconds</summary>
        public long ProcessingTimeMs { get; set; }
        
        public bool IsValid => Mask != null && RegionBounds != null && RegionBounds.Count > 0;
        
        public FaceParsingResult()
        {
            RegionBounds = new Dictionary<FaceRegion, RegionBounds>();
            RegionPixelCounts = new Dictionary<FaceRegion, int>();
            Regions = new Dictionary<FaceRegion, RegionStats>();
            FacialHair = new FacialHairResult();
        }
        
        /// <summary>
        /// Get the region at a specific pixel
        /// </summary>
        public FaceRegion GetRegionAt(int x, int y)
        {
            if (Mask == null || x < 0 || y < 0 || 
                x >= Mask.GetLength(0) || y >= Mask.GetLength(1))
                return FaceRegion.Background;
            
            return (FaceRegion)Mask[x, y];
        }
        
        /// <summary>
        /// Check if a specific region exists in the parsing
        /// </summary>
        public bool HasRegion(FaceRegion region)
        {
            if (RegionPixelCounts != null && RegionPixelCounts.ContainsKey(region))
            {
                return RegionPixelCounts[region] > 0;
            }
            if (RegionBounds != null && RegionBounds.ContainsKey(region))
            {
                return RegionBounds[region].PixelCount > 0;
            }
            return false;
        }
        
        /// <summary>
        /// Get pixel count for a region
        /// </summary>
        public int GetPixelCount(FaceRegion region)
        {
            if (RegionPixelCounts != null && RegionPixelCounts.ContainsKey(region))
            {
                return RegionPixelCounts[region];
            }
            if (RegionBounds != null && RegionBounds.ContainsKey(region))
            {
                return RegionBounds[region].PixelCount;
            }
            return 0;
        }
        
        /// <summary>
        /// Get stats for a region, or null if not found
        /// </summary>
        public RegionStats GetRegionStats(FaceRegion region)
        {
            // Try Regions dict first
            if (Regions != null && Regions.ContainsKey(region))
            {
                return Regions[region];
            }
            
            // Fall back to building from RegionBounds
            if (RegionBounds != null && RegionBounds.ContainsKey(region))
            {
                return RegionStats.FromBounds(RegionBounds[region], Width, Height);
            }
            
            return null;
        }
        
        /// <summary>
        /// Get bounds for a region
        /// </summary>
        public RegionBounds GetBounds(FaceRegion region)
        {
            if (RegionBounds != null && RegionBounds.ContainsKey(region))
            {
                return RegionBounds[region];
            }
            return null;
        }
        
        /// <summary>
        /// Get combined bounds for upper and lower lip
        /// </summary>
        public RegionBounds GetCombinedLipBounds()
        {
            var upper = GetBounds(FaceRegion.UpperLip);
            var lower = GetBounds(FaceRegion.LowerLip);
            
            if (upper == null && lower == null) return null;
            if (upper == null) return lower;
            if (lower == null) return upper;
            
            return new RegionBounds
            {
                MinX = Math.Min(upper.MinX, lower.MinX),
                MinY = Math.Min(upper.MinY, lower.MinY),
                MaxX = Math.Max(upper.MaxX, lower.MaxX),
                MaxY = Math.Max(upper.MaxY, lower.MaxY),
                PixelCount = upper.PixelCount + lower.PixelCount
            };
        }
        
        /// <summary>
        /// Build Regions dictionary from RegionBounds (for legacy compatibility)
        /// </summary>
        public void BuildRegionStats()
        {
            if (Regions == null)
                Regions = new Dictionary<FaceRegion, RegionStats>();
            
            if (RegionBounds == null) return;
            
            foreach (var kvp in RegionBounds)
            {
                Regions[kvp.Key] = RegionStats.FromBounds(kvp.Value, Width, Height);
            }
        }
    }
}
