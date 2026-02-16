using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceLearner.ML
{
    /// <summary>
    /// Landmark detection mode
    /// </summary>
    public enum LandmarkMode
    {
        FaceMesh468, // MediaPipe FaceMesh 468-point
        Auto         // FaceMesh 468-point (same as FaceMesh468)
    }

    /// <summary>
    /// Extracts facial landmarks using FaceMesh (468 points).
    /// FaceMesh provides detailed face topology for accurate matching.
    /// </summary>
    public class LandmarkDetector : IDisposable
    {
        // FaceMesh components
        private InferenceSession _faceMeshSession;
        private InferenceSession _faceDetectSession;

        private bool _faceMeshInitialized = false;

        private const int FACEMESH_INPUT_SIZE = 192;
        private const int FACEDETECT_INPUT_SIZE = 128;
        public const int FACEMESH_LANDMARKS = 468;

        /// <summary>
        /// If true, convert images to grayscale before detection.
        /// </summary>
        public bool UseGrayscale { get; set; } = true;

        /// <summary>
        /// Detection mode - FaceMesh 468 only
        /// </summary>
        public LandmarkMode Mode { get; set; } = LandmarkMode.Auto;

        /// <summary>
        /// Number of landmarks from last detection
        /// </summary>
        public int LastLandmarkCount { get; private set; } = 0;

        private string _grayscaleTempPath;
        private string _modelsDir;

        public bool IsInitialized => _faceMeshInitialized;
        public bool IsFaceMeshAvailable => _faceMeshInitialized;
        public string LastError { get; private set; } = "";

        /// <summary>
        /// Initialize with models directory. Requires face_landmark.onnx for FaceMesh 468 landmarks.
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

            // Initialize FaceMesh (468 landmarks)
            if (InitializeFaceMesh(_modelsDir))
            {
                SubModule.Log($"FaceMesh loaded: {FACEMESH_LANDMARKS} landmarks");
            }
            else
            {
                SubModule.Log("ERROR: FaceMesh initialization failed - face_landmark.onnx is required");
            }

            // Set up temp path
            _grayscaleTempPath = Path.Combine(_modelsDir, "temp_grayscale.png");

            return _faceMeshInitialized;
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

        /// <summary>
        /// Detect landmarks - returns FaceMesh 468 landmarks (x,y pairs = 936 floats)
        /// </summary>
        public float[] DetectLandmarks(string imagePath)
        {
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
        /// Detect landmarks - returns FaceMesh 468 landmarks (x,y pairs = 936 floats)
        /// </summary>
        public float[] DetectLandmarksFull(string imagePath)
        {
            if (!IsInitialized) return null;

            float[] result = DetectWithFaceMesh(imagePath);
            if (result != null)
            {
                LastLandmarkCount = FACEMESH_LANDMARKS;
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
                                var dims = string.Join("x", t.Dimensions.ToArray());
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

                            // Check for 468x3 = 1404 (x, y, z per landmark)
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
                                    SubModule.Log($"FaceMesh detection OK: 468x3 format, {result.Name}");
                                }
                                return landmarks;
                            }

                            // Check for 468x2 = 936 (x, y only)
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
                                    SubModule.Log($"FaceMesh detection OK: 468x2 format, {result.Name}");
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

        public void Dispose()
        {
            _faceMeshSession?.Dispose();
            _faceDetectSession?.Dispose();
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
