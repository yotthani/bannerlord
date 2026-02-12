using System;
using System.Drawing;
using System.IO;
using FaceLearner.ML.Modules.Core;

namespace FaceLearner.ML.Modules.FaceParsing
{
    /// <summary>
    /// Face Parsing Module - Semantic segmentation of faces.
    /// 
    /// This module is CENTRAL to the new architecture:
    /// - Proportions Module uses it for accurate region measurements
    /// - Gender Module uses it for beard detection
    /// - SkinTone Module uses it for skin-only sampling
    /// 
    /// Usage:
    ///   var result = faceParsingModule.Analyze(bitmap);
    ///   if (result.HasRegion(FaceRegion.Nose))
    ///   {
    ///       var noseBounds = result.GetBounds(FaceRegion.Nose);
    ///       float noseWidth = noseBounds.Width / (float)result.Width;
    ///   }
    /// </summary>
    public class FaceParsingModule : IDisposable
    {
        private BiSeNetDetector _detector;
        private bool _isReady;
        
        // Expected model filename
        private const string MODEL_FILENAME = "resnet18.onnx";
        
        // Alternative model names to search for (in priority order)
        private static readonly string[] AlternativeModelNames = new[]
        {
            "resnet18.onnx",              // yakhyo/face-parsing (smaller, faster)
            "resnet34.onnx",              // yakhyo/face-parsing (larger, more accurate)
            "bisenet_face_parsing.onnx",  // Generic name
            "face_parsing.onnx",
            "bisenet.onnx",
            "face-parsing-bisenet.onnx",
            "79999_iter.onnx"             // Original PyTorch repo name
        };
        
        #region IModule Implementation
        
        public string Name => "FaceParsing";
        public bool IsReady => _isReady;
        
        public bool Initialize(string basePath)
        {
            _detector = new BiSeNetDetector();
            
            // Search for model file
            string modelPath = null;
            
            foreach (var modelName in AlternativeModelNames)
            {
                var path = Path.Combine(basePath, "Models", modelName);
                if (File.Exists(path))
                {
                    modelPath = path;
                    break;
                }
                
                // Also check in Data/Models
                path = Path.Combine(basePath, "Data", "Models", modelName);
                if (File.Exists(path))
                {
                    modelPath = path;
                    break;
                }
            }
            
            if (modelPath == null)
            {
                SubModule.Log($"FaceParsing: Model not found. Please download BiSeNet ONNX model.");
                SubModule.Log($"  Expected location: {Path.Combine(basePath, "Models", MODEL_FILENAME)}");
                SubModule.Log($"  Download from: https://github.com/zllrunning/face-parsing.PyTorch");
                _isReady = false;
                return false;
            }
            
            if (_detector.Load(modelPath))
            {
                _isReady = true;
                SubModule.Log($"FaceParsing: Initialized successfully");
                return true;
            }
            
            _isReady = false;
            return false;
        }
        
        public FaceParsingResult Analyze(Bitmap image)
        {
            if (!_isReady || _detector == null)
            {
                return CreateEmptyResult("Not initialized");
            }
            
            if (image == null)
            {
                return CreateEmptyResult("Null image");
            }
            
            return _detector.Detect(image);
        }
        
        public void Dispose()
        {
            _detector?.Dispose();
            _detector = null;
            _isReady = false;
        }
        
        #endregion
        
        #region Convenience Methods
        
        /// <summary>
        /// Quick check if face parsing is available.
        /// Other modules should degrade gracefully if this returns false.
        /// </summary>
        public static bool IsAvailable(string basePath)
        {
            foreach (var modelName in AlternativeModelNames)
            {
                if (File.Exists(Path.Combine(basePath, "Models", modelName)) ||
                    File.Exists(Path.Combine(basePath, "Data", "Models", modelName)))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Get download instructions for the model
        /// </summary>
        public static string GetDownloadInstructions()
        {
            return @"
BiSeNet Face Parsing Model Required
===================================

The face parsing model provides semantic segmentation of faces into 19 regions
(skin, eyes, nose, lips, hair, etc.). This is essential for accurate proportions,
skin tone detection, and beard detection.

RECOMMENDED: yakhyo/face-parsing (MIT License, ONNX ready)
--------------------------------------------------------------

Direct Download Links:
  ResNet18 (43MB, faster):  https://github.com/yakhyo/face-parsing/releases/download/v0.0.1/resnet18.onnx
  ResNet34 (82MB, accurate): https://github.com/yakhyo/face-parsing/releases/download/v0.0.1/resnet34.onnx

Or via command line:
  curl -L -o resnet18.onnx https://github.com/yakhyo/face-parsing/releases/download/v0.0.1/resnet18.onnx

Installation:
-------------
Place the .onnx file in: Modules/FaceLearner/Data/Models/resnet18.onnx
                     or: Modules/FaceLearner/Models/resnet18.onnx

Model Specs:
- Input: 512x512 RGB image (NCHW format)
- Output: 512x512 segmentation mask (19 classes)
- Classes: background, skin, l_brow, r_brow, l_eye, r_eye, eye_g, l_ear, r_ear,
           ear_r, nose, mouth, u_lip, l_lip, neck, neck_l, cloth, hair, hat

GitHub: https://github.com/yakhyo/face-parsing
";
        }
        
        #endregion
        
        #region Private
        
        private FaceParsingResult CreateEmptyResult(string reason)
        {
            return new FaceParsingResult
            {
                Mask = null,
                Width = 0,
                Height = 0,
                RegionBounds = new System.Collections.Generic.Dictionary<FaceRegion, RegionBounds>(),
                RegionPixelCounts = new System.Collections.Generic.Dictionary<FaceRegion, int>(),
                Confidence = 0f,
                Source = $"{Name} ({reason})"
            };
        }
        
        #endregion
    }
}
