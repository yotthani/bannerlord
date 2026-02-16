using System;
using System.Drawing;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.FaceParsing;
using FaceLearner.ML.Modules.Proportions;
using FaceLearner.ML.Modules.Scoring;
using FaceLearner.ML.Modules.Demographics;

// Alias to avoid namespace conflict
using GenderEnum = FaceLearner.ML.Modules.Core.Gender;

namespace FaceLearner.ML.Modules
{
    /// <summary>
    /// Combined face analysis result
    /// </summary>
    public class FaceAnalysisResult
    {
        /// <summary>Was a face detected?</summary>
        public bool FaceDetected { get; set; }
        
        /// <summary>Face bounding box in original image</summary>
        public Rectangle FaceRect { get; set; }
        
        /// <summary>468-point FaceMesh landmarks (936 floats)</summary>
        public float[] Landmarks { get; set; }
        
        /// <summary>Semantic face parsing result</summary>
        public FaceParsingResult Parsing { get; set; }
        
        /// <summary>Facial proportions analysis</summary>
        public ProportionsResult Proportions { get; set; }
        
        /// <summary>Detected gender</summary>
        public GenderEnum Gender { get; set; }
        
        /// <summary>Gender detection confidence (0-1)</summary>
        public float GenderConfidence { get; set; }
        
        /// <summary>Has visible facial hair (beard/mustache)?</summary>
        public bool HasFacialHair { get; set; }
        
        /// <summary>Estimated age in years</summary>
        public int Age { get; set; }
        
        /// <summary>Age estimation confidence (0-1)</summary>
        public float AgeConfidence { get; set; }
        
        /// <summary>Normalized skin tone (0=very dark, 1=very light)</summary>
        public float SkinTone { get; set; }
        
        /// <summary>Skin tone detection confidence (0-1)</summary>
        public float SkinToneConfidence { get; set; }
        
        /// <summary>Detected race/ethnicity (from FairFace)</summary>
        public string Race { get; set; }
        
        /// <summary>Race detection confidence (0-1)</summary>
        public float RaceConfidence { get; set; }
        
        /// <summary>Overall analysis confidence</summary>
        public float Confidence { get; set; }
        
        /// <summary>Processing time in milliseconds</summary>
        public long ProcessingTimeMs { get; set; }
        
        public bool IsReliable => Confidence >= ConfidenceThresholds.Acceptable;
    }
    
    /// <summary>
    /// Main face analysis API.
    /// Combines landmark detection, face parsing, proportions, and demographics.
    /// </summary>
    public class FaceAnalyzer : IFaceAnalyzer
    {
        private LandmarkDetector _landmarkDetector;
        private FaceParsingModule _parsingModule;
        private ProportionsModule _proportionsModule;
        private DemographicsModule _demographicsModule;
        private ScoringModule _scoringModule;
        
        private bool _isReady;
        private bool _useLightMode;  // Skip heavy analysis
        
        public bool IsReady => _isReady;
        
        public FaceAnalyzer(string basePath)
        {
            Initialize(basePath);
        }
        
        /// <summary>
        /// Create analyzer in light mode (skip face parsing for speed)
        /// </summary>
        public FaceAnalyzer(string basePath, bool lightMode)
        {
            _useLightMode = lightMode;
            Initialize(basePath);
        }
        
        private void Initialize(string basePath)
        {
            try
            {
                // Initialize landmark detector (uses FaceMesh 468)
                _landmarkDetector = new LandmarkDetector();
                if (!_landmarkDetector.Initialize(basePath))
                {
                    SubModule.Log("FaceAnalyzer: Landmark detector failed to initialize");
                }
                
                // Initialize face parsing (BiSeNet)
                _parsingModule = new FaceParsingModule();
                _parsingModule.Initialize(basePath);
                
                // Initialize proportions analyzer
                _proportionsModule = new ProportionsModule();
                
                // Initialize demographics
                _demographicsModule = new DemographicsModule();
                
                // Initialize scoring
                _scoringModule = new ScoringModule();
                
                _isReady = _landmarkDetector?.IsInitialized ?? false;
                
                if (_isReady)
                {
                    string parsingStatus = _parsingModule.IsReady ? "✓" : "✗";
                    SubModule.Log($"FaceAnalyzer: Ready (Parsing:{parsingStatus})");
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"FaceAnalyzer: Initialization failed - {ex.Message}");
                _isReady = false;
            }
        }
        
        /// <summary>
        /// Analyze a face image from file path
        /// </summary>
        public FaceAnalysisResult Analyze(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
            {
                return new FaceAnalysisResult { FaceDetected = false };
            }
            
            using (var bitmap = new Bitmap(imagePath))
            {
                return Analyze(bitmap);
            }
        }
        
        /// <summary>
        /// Analyze a face image from bitmap
        /// </summary>
        public FaceAnalysisResult Analyze(Bitmap image)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new FaceAnalysisResult();
            
            if (image == null || !_isReady)
            {
                return result;
            }
            
            try
            {
                // 1. Detect landmarks
                result.Landmarks = _landmarkDetector.DetectLandmarks(image);
                result.FaceDetected = result.Landmarks != null && result.Landmarks.Length > 0;
                
                if (!result.FaceDetected)
                {
                    return result;
                }
                
                // Estimate face rect from landmarks
                result.FaceRect = EstimateFaceRect(result.Landmarks, image.Width, image.Height);
                
                // 2. Face parsing (if available and not in light mode)
                if (!_useLightMode && _parsingModule != null && _parsingModule.IsReady)
                {
                    result.Parsing = _parsingModule.Analyze(image);
                }
                
                // 3. Analyze proportions
                result.Proportions = _proportionsModule.Analyze(result.Landmarks, result.Parsing);
                
                // 4. Demographics (skip in light mode)
                if (!_useLightMode)
                {
                    var demographics = _demographicsModule.Analyze(
                        image, result.Landmarks, result.Parsing, result.FaceRect);
                    
                    if (demographics != null)
                    {
                        result.Gender = demographics.IsFemale ? GenderEnum.Female : GenderEnum.Male;
                        result.GenderConfidence = demographics.Gender?.Confidence ?? 0;
                        result.HasFacialHair = demographics.HasFacialHair;
                        result.Age = demographics.EstimatedAge;
                        result.AgeConfidence = demographics.Age?.Confidence ?? 0;
                        result.SkinTone = demographics.NormalizedSkinTone;
                        result.SkinToneConfidence = demographics.SkinTone?.Confidence ?? 0;
                    }
                }
                
                // Overall confidence
                int confCount = 0;
                float confSum = 0;
                
                if (result.Proportions != null)
                {
                    confSum += result.Proportions.Confidence;
                    confCount++;
                }
                if (result.GenderConfidence > 0)
                {
                    confSum += result.GenderConfidence;
                    confCount++;
                }
                
                result.Confidence = confCount > 0 ? confSum / confCount : 0.5f;
            }
            catch (Exception ex)
            {
                SubModule.Log($"FaceAnalyzer.Analyze: {ex.Message}");
                result.FaceDetected = false;
            }
            
            sw.Stop();
            result.ProcessingTimeMs = sw.ElapsedMilliseconds;
            
            return result;
        }
        
        /// <summary>
        /// Analyze using pre-computed landmarks (fast mode - no re-detection).
        /// Use this when you already have landmarks from another detector.
        /// </summary>
        public FaceAnalysisResult AnalyzeWithLandmarks(float[] landmarks, int imageWidth = 0, int imageHeight = 0)
        {
            var result = new FaceAnalysisResult();
            
            if (landmarks == null || landmarks.Length < 936)
            {
                return result;
            }
            
            try
            {
                result.Landmarks = landmarks;
                result.FaceDetected = true;
                
                // Estimate face rect from landmarks
                if (imageWidth > 0 && imageHeight > 0)
                {
                    result.FaceRect = EstimateFaceRect(landmarks, imageWidth, imageHeight);
                }
                
                // Analyze proportions (no parsing in this mode)
                result.Proportions = _proportionsModule.Analyze(landmarks, null);
                
                // Overall confidence
                result.Confidence = result.Proportions?.Confidence ?? 0.5f;
            }
            catch (Exception ex)
            {
                SubModule.Log($"FaceAnalyzer.AnalyzeWithLandmarks: {ex.Message}");
                result.FaceDetected = false;
            }
            
            return result;
        }
        
        /// <summary>
        /// Compare two face analyses and get per-feature scores
        /// </summary>
        public FeatureScoreResult Compare(FaceAnalysisResult target, FaceAnalysisResult current)
        {
            return _scoringModule.Compare(
                target?.Proportions,
                current?.Proportions,
                target?.Landmarks,
                current?.Landmarks);
        }
        
        /// <summary>
        /// Compare raw landmarks (backward compatibility)
        /// </summary>
        public float CompareLandmarks(float[] target, float[] current)
        {
            if (target == null || current == null) return 0f;
            
            int count = Math.Min(target.Length, current.Length);
            if (count == 0) return 0f;
            
            float sumSqDiff = 0;
            for (int i = 0; i < count; i++)
            {
                float diff = target[i] - current[i];
                sumSqDiff += diff * diff;
            }
            
            float rmse = (float)Math.Sqrt(sumSqDiff / count);
            return (float)Math.Exp(-rmse * 5);
        }
        
        /// <summary>
        /// Estimate face bounding box from landmarks
        /// </summary>
        private Rectangle EstimateFaceRect(float[] landmarks, int imageWidth, int imageHeight)
        {
            if (landmarks == null || landmarks.Length < 4)
                return Rectangle.Empty;
            
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            
            for (int i = 0; i < landmarks.Length; i += 2)
            {
                float x = landmarks[i] * imageWidth;
                float y = landmarks[i + 1] * imageHeight;
                
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            
            // Expand by 10%
            float width = maxX - minX;
            float height = maxY - minY;
            float padX = width * 0.1f;
            float padY = height * 0.1f;
            
            return new Rectangle(
                (int)Math.Max(0, minX - padX),
                (int)Math.Max(0, minY - padY),
                (int)Math.Min(imageWidth, width + 2 * padX),
                (int)Math.Min(imageHeight, height + 2 * padY));
        }
        
        public void Dispose()
        {
            _landmarkDetector = null;
            _parsingModule?.Dispose();
            _isReady = false;
        }
    }
    
    /// <summary>
    /// Factory for creating FaceAnalyzer instances
    /// </summary>
    public static class FaceAnalyzerFactory
    {
        /// <summary>
        /// Create FaceAnalyzer (full mode with all features)
        /// </summary>
        public static FaceAnalyzer Create(string basePath)
        {
            try
            {
                var analyzer = new FaceAnalyzer(basePath);
                return analyzer.IsReady ? analyzer : null;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Create FaceAnalyzer in light mode (faster, skips face parsing)
        /// </summary>
        public static FaceAnalyzer CreateLight(string basePath)
        {
            try
            {
                var analyzer = new FaceAnalyzer(basePath, lightMode: true);
                return analyzer.IsReady ? analyzer : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
