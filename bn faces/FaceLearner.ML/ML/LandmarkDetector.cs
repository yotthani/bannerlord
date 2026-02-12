using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DlibDotNet;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceLearner.ML
{
    /// <summary>
    /// Landmark detection mode
    /// </summary>
    public enum LandmarkMode
    {
        Dlib68,      // Dlib 68-point (fallback)
        FaceMesh468, // MediaPipe FaceMesh 468-point (primary)
        Auto         // Try FaceMesh first, fallback to Dlib
    }
    
    /// <summary>
    /// Extracts facial landmarks using either FaceMesh (468 points) or Dlib (68 points)
    /// FaceMesh provides much more detailed face topology for better matching
    /// </summary>
    public class LandmarkDetector : IDisposable
    {
        // Dlib components
        private FrontalFaceDetector _faceDetector;
        private ShapePredictor _shapePredictor;
        
        // FaceMesh components
        private InferenceSession _faceMeshSession;
        private InferenceSession _faceDetectSession;
        
        private bool _dlibInitialized = false;
        private bool _faceMeshInitialized = false;
        
        private const int FACEMESH_INPUT_SIZE = 192;
        private const int FACEDETECT_INPUT_SIZE = 128;
        public const int FACEMESH_LANDMARKS = 468;
        public const int DLIB_LANDMARKS = 68;
        
        /// <summary>
        /// If true, convert images to grayscale before detection.
        /// </summary>
        public bool UseGrayscale { get; set; } = true;
        
        /// <summary>
        /// Detection mode - FaceMesh468 is recommended for better accuracy
        /// </summary>
        public LandmarkMode Mode { get; set; } = LandmarkMode.Auto;
        
        /// <summary>
        /// Number of landmarks from last detection
        /// </summary>
        public int LastLandmarkCount { get; private set; } = 0;
        
        private string _grayscaleTempPath;
        private string _modelsDir;
        
        public bool IsInitialized => _dlibInitialized || _faceMeshInitialized;
        public bool IsFaceMeshAvailable => _faceMeshInitialized;
        public string LastError { get; private set; } = "";
        
        /// <summary>
        /// Initialize with models directory or dlib model path. Auto-downloads Dlib if missing.
        /// FaceMesh (468 landmarks) requires manual install of face_landmark.onnx
        /// </summary>
        public bool Initialize(string pathOrDir)
        {
            // Determine if we got a file path or directory
            if (File.Exists(pathOrDir))
            {
                _modelsDir = Path.GetDirectoryName(pathOrDir);
            }
            else if (Directory.Exists(pathOrDir))
            {
                _modelsDir = pathOrDir;
            }
            else
            {
                _modelsDir = pathOrDir;
                try { Directory.CreateDirectory(_modelsDir); } catch { }
            }
            
            bool success = false;
            
            // Try to initialize FaceMesh first (468 landmarks - much better!)
            if (InitializeFaceMesh(_modelsDir))
            {
                SubModule.Log($"✓ FaceMesh loaded: {FACEMESH_LANDMARKS} landmarks (7x more detail!)");
                success = true;
            }
            else
            {
                SubModule.Log("FaceMesh not found - install face_landmark.onnx for 468 landmarks");
            }
            
            // Initialize Dlib (68 landmarks + 41 shape ratios as fallback)
            string dlibPath = Path.Combine(_modelsDir, "shape_predictor_68_face_landmarks.dat");
            if (InitializeDlib(dlibPath))
            {
                if (!_faceMeshInitialized)
                {
                    SubModule.Log($"✓ Dlib loaded: {DLIB_LANDMARKS} landmarks + 41 shape ratios");
                }
                success = true;
            }
            
            // Summary
            if (_faceMeshInitialized)
            {
                SubModule.Log(">>> Using FaceMesh 468 landmarks (PRIMARY)");
            }
            else if (_dlibInitialized)
            {
                SubModule.Log(">>> Using Dlib 68 landmarks (FALLBACK)");
            }
            
            // Set up temp path
            _grayscaleTempPath = Path.Combine(_modelsDir, "temp_grayscale.png");
            
            return success;
        }
        
        private bool InitializeFaceMesh(string modelsDir)
        {
            try
            {
                string meshPath = Path.Combine(modelsDir, "face_landmark.onnx");
                
                SubModule.Log($"FaceMesh: Checking path: {meshPath}");
                SubModule.Log($"FaceMesh: File exists: {File.Exists(meshPath)}");
                
                if (!File.Exists(meshPath))
                {
                    return false;
                }
                
                var fileInfo = new FileInfo(meshPath);
                SubModule.Log($"FaceMesh: File size: {fileInfo.Length / 1024} KB");
                
                SubModule.Log($"FaceMesh: Creating ONNX session...");
                var options = new SessionOptions();
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                
                _faceMeshSession = new InferenceSession(meshPath, options);
                _faceMeshInitialized = true;
                
                // Log model info
                var inputMeta = _faceMeshSession.InputMetadata;
                var outputMeta = _faceMeshSession.OutputMetadata;
                SubModule.Log($"FaceMesh: Inputs: {string.Join(", ", inputMeta.Keys)}");
                SubModule.Log($"FaceMesh: Outputs: {string.Join(", ", outputMeta.Keys)}");
                
                foreach (var inp in inputMeta)
                {
                    SubModule.Log($"FaceMesh: Input '{inp.Key}' dims: [{string.Join(",", inp.Value.Dimensions)}]");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"FaceMesh: EXCEPTION: {ex.GetType().Name}");
                SubModule.Log($"FaceMesh: {ex.Message}");
                if (ex.InnerException != null)
                {
                    SubModule.Log($"FaceMesh: Inner: {ex.InnerException.Message}");
                }
                return false;
            }
        }
        
        private bool InitializeDlib(string shapePredictorPath)
        {
            try
            {
                // Auto-download if missing
                if (!File.Exists(shapePredictorPath))
                {
                    string dir = Path.GetDirectoryName(shapePredictorPath);
                    SubModule.Log("Dlib model not found, downloading...");
                    
                    ModelDownloader.OnProgress += msg => SubModule.Log($"  {msg}");
                    bool downloaded = ModelDownloader.DownloadModel(dir);
                    ModelDownloader.OnProgress -= msg => SubModule.Log($"  {msg}");
                    
                    if (!downloaded || !File.Exists(shapePredictorPath))
                    {
                        LastError = "Failed to download Dlib model";
                        SubModule.Log(LastError);
                        return false;
                    }
                }
                
                _faceDetector = Dlib.GetFrontalFaceDetector();
                _shapePredictor = ShapePredictor.Deserialize(shapePredictorPath);
                _dlibInitialized = true;
                
                SubModule.Log($"Dlib initialized: {DLIB_LANDMARKS} landmarks");
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Dlib init failed: {ex.Message}";
                SubModule.Log(LastError);
                return false;
            }
        }
        
        /// <summary>
        /// Detect landmarks - returns full data (468 for FaceMesh, 68 for Dlib)
        /// </summary>
        public float[] DetectLandmarks(string imagePath)
        {
            // Return full landmarks - 468 if FaceMesh, 68 if Dlib
            return DetectLandmarksFull(imagePath);
        }
        
        /// <summary>
        /// Detect landmarks from Bitmap - saves to temp file and detects
        /// </summary>
        public float[] DetectLandmarks(Bitmap image)
        {
            if (image == null) return null;
            
            // Save to temp file
            string tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), 
                $"facelearner_temp_{Guid.NewGuid()}.png");
            
            try
            {
                image.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                return DetectLandmarks(tempPath);
            }
            finally
            {
                // Clean up temp file
                try { System.IO.File.Delete(tempPath); } catch { }
            }
        }
        
        /// <summary>
        /// Detect landmarks - returns 68 points for backward compatibility
        /// </summary>
        public float[] DetectLandmarks68(string imagePath)
        {
            var full = DetectLandmarksFull(imagePath);
            if (full == null) return null;
            
            // If we got 468 points, convert to 68 for compatibility
            if (LastLandmarkCount == FACEMESH_LANDMARKS)
            {
                return ConvertFaceMeshTo68(full);
            }
            
            return full;
        }
        
        /// <summary>
        /// Detect landmarks - returns full data (468 for FaceMesh, 68 for Dlib)
        /// </summary>
        public float[] DetectLandmarksFull(string imagePath)
        {
            if (!IsInitialized) return null;
            
            // Determine which detector to use
            bool useFaceMesh = Mode == LandmarkMode.FaceMesh468 || 
                               (Mode == LandmarkMode.Auto && _faceMeshInitialized);
            
            float[] result = null;
            
            if (useFaceMesh && _faceMeshInitialized)
            {
                result = DetectWithFaceMesh(imagePath);
                if (result != null)
                {
                    LastLandmarkCount = FACEMESH_LANDMARKS;
                    return result;
                }
                // Fallback to Dlib if FaceMesh failed
            }
            
            if (_dlibInitialized)
            {
                result = DetectWithDlib(imagePath);
                if (result != null)
                {
                    LastLandmarkCount = DLIB_LANDMARKS;
                }
            }
            
            return result;
        }
        
        private float[] DetectWithFaceMesh(string imagePath)
        {
            try
            {
                using (var bitmap = new System.Drawing.Bitmap(imagePath))
                {
                    // Get input shape from model
                    var inputMeta = _faceMeshSession.InputMetadata.First();
                    var inputName = inputMeta.Key;
                    var inputShape = inputMeta.Value.Dimensions;
                    
                    // Preprocess image
                    float[] input = PreprocessForFaceMesh(bitmap);
                    if (input == null) return null;
                    
                    // Use model's expected shape [1, 192, 192, 3]
                    int[] shape = new[] { 1, FACEMESH_INPUT_SIZE, FACEMESH_INPUT_SIZE, 3 };
                    
                    var tensor = new DenseTensor<float>(input, shape);
                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };
                    
                    using (var results = _faceMeshSession.Run(inputs))
                    {
                        // Log output format once
                        if (!_faceMeshDebugLogged)
                        {
                            _faceMeshDebugLogged = true;
                            SubModule.Log($"FaceMesh outputs ({results.Count}):");
                            foreach (var r in results)
                            {
                                var t = r.AsTensor<float>();
                                var dims = string.Join("×", t.Dimensions.ToArray());
                                SubModule.Log($"  {r.Name}: [{dims}] = {t.Length} values");
                                
                                // Sample first few values
                                var sample = new List<float>();
                                int count = 0;
                                foreach (var v in t)
                                {
                                    sample.Add(v);
                                    if (++count >= 10) break;
                                }
                                SubModule.Log($"    First 10: [{string.Join(", ", sample.Select(v => v.ToString("F2")))}]");
                            }
                        }
                        
                        // Try to find landmark data
                        // PINTO FaceMesh model outputs: conv2d_20 (face flag?), conv2d_30 (landmarks)
                        foreach (var result in results)
                        {
                            var outTensor = result.AsTensor<float>();
                            var dims = outTensor.Dimensions.ToArray();
                            int len = (int)outTensor.Length;
                            
                            // Skip small outputs (likely face confidence)
                            if (len < 100) continue;
                            
                            // Convert tensor to flat array
                            var data = new float[len];
                            int idx = 0;
                            foreach (var val in outTensor)
                            {
                                data[idx++] = val;
                            }
                            
                            // Check for 468×3 = 1404 (x, y, z per landmark)
                            if (len == 1404 || len == FACEMESH_LANDMARKS * 3)
                            {
                                var landmarks = new float[FACEMESH_LANDMARKS * 2];
                                for (int i = 0; i < FACEMESH_LANDMARKS; i++)
                                {
                                    float x = data[i * 3];
                                    float y = data[i * 3 + 1];
                                    // Normalize - values might be 0-192 or 0-1
                                    if (x > 2 || y > 2)
                                    {
                                        landmarks[i * 2] = x / FACEMESH_INPUT_SIZE;
                                        landmarks[i * 2 + 1] = y / FACEMESH_INPUT_SIZE;
                                    }
                                    else
                                    {
                                        landmarks[i * 2] = x;
                                        landmarks[i * 2 + 1] = y;
                                    }
                                }
                                if (!_faceMeshSuccessLogged)
                                {
                                    _faceMeshSuccessLogged = true;
                                    SubModule.Log($"FaceMesh detection OK: 468×3 format, {result.Name}");
                                }
                                return landmarks;
                            }
                            
                            // Check for 468×2 = 936 (x, y only)
                            if (len == 936 || len == FACEMESH_LANDMARKS * 2)
                            {
                                var landmarks = new float[FACEMESH_LANDMARKS * 2];
                                for (int i = 0; i < FACEMESH_LANDMARKS * 2; i++)
                                {
                                    float v = data[i];
                                    landmarks[i] = (v > 2) ? v / FACEMESH_INPUT_SIZE : v;
                                }
                                if (!_faceMeshSuccessLogged)
                                {
                                    _faceMeshSuccessLogged = true;
                                    SubModule.Log($"FaceMesh detection OK: 468×2 format, {result.Name}");
                                }
                                return landmarks;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_faceMeshErrorLogged)
                {
                    _faceMeshErrorLogged = true;
                    LastError = $"FaceMesh detection failed: {ex.Message}";
                    SubModule.Log(LastError);
                }
            }
            
            return null;
        }
        
        private bool _faceMeshDebugLogged = false;
        private bool _faceMeshSuccessLogged = false;
        private bool _faceMeshErrorLogged = false;
        
        private float[] PreprocessForFaceMesh(System.Drawing.Bitmap bitmap)
        {
            try
            {
                using (var resized = new System.Drawing.Bitmap(FACEMESH_INPUT_SIZE, FACEMESH_INPUT_SIZE))
                {
                    using (var g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        
                        // Center crop to square
                        int size = Math.Min(bitmap.Width, bitmap.Height);
                        int x = (bitmap.Width - size) / 2;
                        int y = (bitmap.Height - size) / 2;
                        
                        g.DrawImage(bitmap, 
                            new RectangleF(0, 0, FACEMESH_INPUT_SIZE, FACEMESH_INPUT_SIZE),
                            new RectangleF(x, y, size, size),
                            GraphicsUnit.Pixel);
                    }
                    
                    return BitmapToTensor(resized, FACEMESH_INPUT_SIZE);
                }
            }
            catch
            {
                return null;
            }
        }
        
        private float[] BitmapToTensor(System.Drawing.Bitmap bitmap, int size)
        {
            var tensor = new float[size * size * 3];
            
            var data = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, size, size),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            
            try
            {
                int stride = data.Stride;
                byte[] pixels = new byte[stride * size];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int pixelOffset = y * stride + x * 3;
                        int tensorOffset = (y * size + x) * 3;
                        
                        // BGR to RGB and normalize to 0-1
                        tensor[tensorOffset + 0] = pixels[pixelOffset + 2] / 255f;
                        tensor[tensorOffset + 1] = pixels[pixelOffset + 1] / 255f;
                        tensor[tensorOffset + 2] = pixels[pixelOffset + 0] / 255f;
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            
            return tensor;
        }
        
        private float[] DetectWithDlib(string imagePath)
        {
            try
            {
                string pathToLoad = UseGrayscale ? ConvertToGrayscale(imagePath) : imagePath;
                
                using (var img = Dlib.LoadImage<RgbPixel>(pathToLoad))
                {
                    var faces = _faceDetector.Operator(img);
                    if (faces.Length == 0)
                    {
                        LastError = "No face detected (Dlib)";
                        return null;
                    }
                    
                    var face = faces.OrderByDescending(f => f.Width * f.Height).First();
                    
                    using (var shape = _shapePredictor.Detect(img, face))
                    {
                        if (shape.Parts != DLIB_LANDMARKS)
                        {
                            LastError = $"Expected {DLIB_LANDMARKS} landmarks, got {shape.Parts}";
                            return null;
                        }
                        
                        var landmarks = new float[DLIB_LANDMARKS * 2];
                        
                        float faceX = face.Left - face.Width * 0.1f;
                        float faceY = face.Top - face.Height * 0.1f;
                        float faceWidth = face.Width * 1.2f;
                        float faceHeight = face.Height * 1.2f;
                        
                        for (int i = 0; i < DLIB_LANDMARKS; i++)
                        {
                            var point = shape.GetPart((uint)i);
                            landmarks[i * 2] = (point.X - faceX) / faceWidth;
                            landmarks[i * 2 + 1] = (point.Y - faceY) / faceHeight;
                        }
                        
                        return landmarks;
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = $"Dlib detection failed: {ex.Message}";
                return null;
            }
        }
        
        private string ConvertToGrayscale(string imagePath)
        {
            try
            {
                using (var original = new System.Drawing.Bitmap(imagePath))
                using (var grayscale = new System.Drawing.Bitmap(original.Width, original.Height))
                {
                    using (var g = Graphics.FromImage(grayscale))
                    {
                        var colorMatrix = new ColorMatrix(new float[][]
                        {
                            new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                            new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                            new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                            new float[] { 0, 0, 0, 1, 0 },
                            new float[] { 0, 0, 0, 0, 1 }
                        });
                        
                        using (var attributes = new ImageAttributes())
                        {
                            attributes.SetColorMatrix(colorMatrix);
                            g.DrawImage(original,
                                new System.Drawing.Rectangle(0, 0, original.Width, original.Height),
                                0, 0, original.Width, original.Height,
                                GraphicsUnit.Pixel, attributes);
                        }
                    }
                    
                    grayscale.Save(_grayscaleTempPath, ImageFormat.Png);
                }
                return _grayscaleTempPath;
            }
            catch
            {
                return imagePath;
            }
        }
        
        /// <summary>
        /// Convert 468-point FaceMesh to 68-point Dlib format
        /// </summary>
        public static float[] ConvertFaceMeshTo68(float[] mesh468)
        {
            if (mesh468 == null || mesh468.Length < FACEMESH_LANDMARKS * 2) return null;
            
            // Mapping from 68-point indices to 468-point mesh
            int[] mapping = {
                // Jawline (0-16)
                234, 93, 132, 58, 172, 136, 150, 149, 152, 148, 176, 365, 397, 288, 361, 323, 454,
                // Right eyebrow (17-21)
                70, 63, 105, 66, 107,
                // Left eyebrow (22-26)
                336, 296, 334, 293, 300,
                // Nose bridge (27-30)
                168, 6, 197, 195,
                // Nose bottom (31-35)
                98, 97, 2, 326, 327,
                // Right eye (36-41)
                33, 160, 158, 133, 153, 144,
                // Left eye (42-47)
                362, 385, 387, 263, 373, 380,
                // Outer lips (48-59)
                61, 40, 37, 0, 267, 270, 291, 321, 314, 17, 84, 181,
                // Inner lips (60-67)
                78, 82, 13, 312, 308, 317, 14, 87
            };
            
            var result = new float[DLIB_LANDMARKS * 2];
            
            for (int i = 0; i < mapping.Length && i < DLIB_LANDMARKS; i++)
            {
                int srcIdx = mapping[i];
                if (srcIdx < FACEMESH_LANDMARKS)
                {
                    result[i * 2] = mesh468[srcIdx * 2];
                    result[i * 2 + 1] = mesh468[srcIdx * 2 + 1];
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get landmark names for the 68 points
        /// </summary>
        public static string GetLandmarkName(int index)
        {
            if (index < 0 || index > 67) return "unknown";
            
            if (index <= 16) return $"jaw_{index}";
            if (index <= 21) return $"eyebrow_right_{index - 17}";
            if (index <= 26) return $"eyebrow_left_{index - 22}";
            if (index <= 30) return $"nose_bridge_{index - 27}";
            if (index <= 35) return $"nose_tip_{index - 31}";
            if (index <= 41) return $"eye_right_{index - 36}";
            if (index <= 47) return $"eye_left_{index - 42}";
            if (index <= 59) return $"mouth_outer_{index - 48}";
            if (index <= 67) return $"mouth_inner_{index - 60}";
            
            return "unknown";
        }
        
        public void Dispose()
        {
            _shapePredictor?.Dispose();
            _faceDetector?.Dispose();
            _faceMeshSession?.Dispose();
            _faceDetectSession?.Dispose();
            _dlibInitialized = false;
            _faceMeshInitialized = false;
            
            try
            {
                if (!string.IsNullOrEmpty(_grayscaleTempPath) && File.Exists(_grayscaleTempPath))
                    File.Delete(_grayscaleTempPath);
            }
            catch { }
        }
    }
}
