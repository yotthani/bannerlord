using System;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceLearner.ML
{
    /// <summary>
    /// Detects age and gender from face images using InsightFace genderage.onnx model.
    /// Model is small (1.26 MB) and fast.
    /// </summary>
    public class DemographicDetector : IDisposable
    {
        private InferenceSession _session;
        private bool _isInitialized = false;
        private string _modelPath;
        
        private const string MODEL_URL = "https://github.com/yakhyo/facial-analysis/releases/download/v0.0.1/genderage.onnx";
        private const string MODEL_FILENAME = "genderage.onnx";
        private const int INPUT_SIZE = 96;  // Model expects 96x96 input
        
        public bool IsInitialized => _isInitialized;
        
        public struct Demographics
        {
            public bool IsFemale;
            public float Age;
            public float Confidence;  // How confident the gender prediction is
            
            /// <summary>
            /// True if male, false if female. Inverse of IsFemale.
            /// </summary>
            public bool IsMale => !IsFemale;
            
            public override string ToString() =>
                $"{(IsFemale ? "Female" : "Male")}, Age={Age:F0} (conf={Confidence:F2})";
        }
        
        public bool Initialize(string basePath)
        {
            try
            {
                string modelsPath = Path.Combine(basePath, "models");
                if (!Directory.Exists(modelsPath))
                    Directory.CreateDirectory(modelsPath);
                
                _modelPath = Path.Combine(modelsPath, MODEL_FILENAME);
                
                // Download if not present
                if (!File.Exists(_modelPath))
                {
                    SubModule.Log($"DemographicDetector: Downloading {MODEL_FILENAME}...");
                    if (!DownloadModel())
                    {
                        SubModule.Log("DemographicDetector: Download failed");
                        return false;
                    }
                }
                
                // Load model
                var options = new SessionOptions();
                options.EnableMemoryPattern = true;
                options.EnableCpuMemArena = true;
                
                _session = new InferenceSession(_modelPath, options);
                _isInitialized = true;
                
                // Log input/output info
                var inputMeta = _session.InputMetadata;
                var outputMeta = _session.OutputMetadata;
                SubModule.Log($"DemographicDetector: Loaded successfully");
                SubModule.Log($"  Input: {string.Join(", ", inputMeta.Keys)}");
                SubModule.Log($"  Output: {string.Join(", ", outputMeta.Keys)}");
                
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"DemographicDetector init error: {ex.Message}");
                return false;
            }
        }
        
        private bool DownloadModel()
        {
            try
            {
                using (var client = new System.Net.WebClient())
                {
                    client.DownloadFile(MODEL_URL, _modelPath);
                }
                
                var fileInfo = new FileInfo(_modelPath);
                SubModule.Log($"DemographicDetector: Downloaded {fileInfo.Length / 1024}KB");
                return File.Exists(_modelPath);
            }
            catch (Exception ex)
            {
                SubModule.Log($"DemographicDetector download error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Detect age and gender from an image file
        /// </summary>
        public Demographics? Detect(string imagePath)
        {
            if (!_isInitialized || !File.Exists(imagePath))
                return null;
            
            try
            {
                // Load and preprocess image
                float[] inputData = PreprocessImage(imagePath);
                if (inputData == null)
                    return null;
                
                // Create input tensor: [1, 3, 96, 96]
                var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 3, INPUT_SIZE, INPUT_SIZE });
                
                // Get input name
                string inputName = _session.InputMetadata.Keys.First();
                
                // Run inference
                var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
                using (var results = _session.Run(inputs))
                {
                    var output = results.First().AsTensor<float>();
                    return ParseOutput(output);
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"DemographicDetector inference error: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Detect age and gender from raw pixel data (for integration with game renderer)
        /// </summary>
        public Demographics? Detect(byte[] rgbPixels, int width, int height)
        {
            if (!_isInitialized || rgbPixels == null)
                return null;
            
            try
            {
                // Resize and preprocess
                float[] inputData = PreprocessPixels(rgbPixels, width, height);
                if (inputData == null)
                    return null;
                
                // Create input tensor
                var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 3, INPUT_SIZE, INPUT_SIZE });
                
                string inputName = _session.InputMetadata.Keys.First();
                var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
                
                using (var results = _session.Run(inputs))
                {
                    var output = results.First().AsTensor<float>();
                    return ParseOutput(output);
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"DemographicDetector pixel inference error: {ex.Message}");
                return null;
            }
        }
        
        private float[] PreprocessImage(string imagePath)
        {
            try
            {
                using (var bitmap = new System.Drawing.Bitmap(imagePath))
                {
                    // Resize to INPUT_SIZE x INPUT_SIZE
                    using (var resized = new System.Drawing.Bitmap(INPUT_SIZE, INPUT_SIZE))
                    using (var graphics = System.Drawing.Graphics.FromImage(resized))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                        graphics.DrawImage(bitmap, 0, 0, INPUT_SIZE, INPUT_SIZE);
                        
                        // Convert to CHW format with 0-1 normalization
                        // BGR order (InsightFace standard)
                        float[] data = new float[3 * INPUT_SIZE * INPUT_SIZE];
                        
                        for (int y = 0; y < INPUT_SIZE; y++)
                        {
                            for (int x = 0; x < INPUT_SIZE; x++)
                            {
                                var pixel = resized.GetPixel(x, y);
                                int idx = y * INPUT_SIZE + x;
                                
                                // BGR order, 0-1 normalization
                                data[0 * INPUT_SIZE * INPUT_SIZE + idx] = pixel.B / 255f;
                                data[1 * INPUT_SIZE * INPUT_SIZE + idx] = pixel.G / 255f;
                                data[2 * INPUT_SIZE * INPUT_SIZE + idx] = pixel.R / 255f;
                            }
                        }
                        
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"DemographicDetector preprocess error: {ex.Message}");
                return null;
            }
        }
        
        private float[] PreprocessPixels(byte[] rgbPixels, int width, int height)
        {
            // Log input stats to verify different images are being processed
            int checksum = 0;
            for (int i = 0; i < Math.Min(1000, rgbPixels.Length); i++)
                checksum += rgbPixels[i] * (i % 256 + 1);
            SubModule.Log($"  PreprocessPixels: {width}x{height}, checksum={checksum}");
            
            float[] data = new float[3 * INPUT_SIZE * INPUT_SIZE];
            
            float scaleX = (float)width / INPUT_SIZE;
            float scaleY = (float)height / INPUT_SIZE;
            
            // Sample center pixel for debug
            int centerX = width / 2;
            int centerY = height / 2;
            int centerIdx = (centerY * width + centerX) * 3;
            SubModule.Log($"  Center pixel RGB: [{rgbPixels[centerIdx]}, {rgbPixels[centerIdx+1]}, {rgbPixels[centerIdx+2]}]");
            
            for (int y = 0; y < INPUT_SIZE; y++)
            {
                for (int x = 0; x < INPUT_SIZE; x++)
                {
                    int srcX = (int)(x * scaleX);
                    int srcY = (int)(y * scaleY);
                    srcX = Math.Min(srcX, width - 1);
                    srcY = Math.Min(srcY, height - 1);
                    
                    int srcIdx = (srcY * width + srcX) * 3;
                    int dstIdx = y * INPUT_SIZE + x;
                    
                    // Try 0-1 normalization with BGR order (InsightFace standard)
                    // Many InsightFace models use simple 0-255 or 0-1 range
                    data[0 * INPUT_SIZE * INPUT_SIZE + dstIdx] = rgbPixels[srcIdx + 2] / 255f;  // B
                    data[1 * INPUT_SIZE * INPUT_SIZE + dstIdx] = rgbPixels[srcIdx + 1] / 255f;  // G
                    data[2 * INPUT_SIZE * INPUT_SIZE + dstIdx] = rgbPixels[srcIdx + 0] / 255f;  // R
                }
            }
            
            return data;
        }
        
        private Demographics? ParseOutput(Tensor<float> output)
        {
            var values = output.ToArray();
            
            // Log output shape and all values for debugging
            SubModule.Log($"  AI output: length={values.Length}, dims={string.Join("x", output.Dimensions.ToArray())}");
            
            if (values.Length < 3)
            {
                SubModule.Log($"DemographicDetector: Unexpected output length {values.Length}");
                SubModule.Log($"  Values: [{string.Join(", ", values.Take(Math.Min(10, values.Length)).Select(v => v.ToString("F4")))}]");
                return null;
            }
            
            // Show first 10 values if more than 3
            if (values.Length > 3)
            {
                SubModule.Log($"  First 10 values: [{string.Join(", ", values.Take(10).Select(v => v.ToString("F3")))}]");
            }
            
            // InsightFace genderage model output format:
            // Interpretation 1: [male_logit, female_logit, age_normalized]
            // Interpretation 2: [gender_score, -, age_normalized] where negative = female
            
            float val0 = values[0];
            float val1 = values[1];
            float val2 = values[2];
            
            // Check if val0 and val1 are opposites (suggests single gender score)
            bool isSymmetric = Math.Abs(val0 + val1) < 0.01f;
            
            bool isFemale;
            float confidence;
            
            if (isSymmetric)
            {
                // Model outputs single score: negative = female, positive = male
                // Use val0 directly as gender indicator
                isFemale = val0 < 0;
                // Confidence based on magnitude (larger = more confident)
                float magnitude = Math.Abs(val0);
                confidence = Math.Min(1f, magnitude);  // Cap at 1.0
                SubModule.Log($"  Gender interpretation: symmetric (val0={val0:F3} → {(isFemale ? "Female" : "Male")}, conf={confidence:F2})");
            }
            else
            {
                // Standard softmax interpretation
                float expFemale = (float)Math.Exp(val1);
                float expMale = (float)Math.Exp(val0);
                float femaleProbability = expFemale / (expFemale + expMale);
                isFemale = femaleProbability > 0.5f;
                confidence = Math.Abs(femaleProbability - 0.5f) * 2f;
                SubModule.Log($"  Gender interpretation: softmax (femaleProb={femaleProbability:F2}, conf={confidence:F2})");
            }
            
            // Age: Multiply by 100 if small (normalized) or use directly
            float age;
            if (val2 < 1.5f)
            {
                // Normalized age (0-1 scale, multiply by 100)
                age = val2 * 100f;
            }
            else
            {
                // Direct age output
                age = val2;
            }
            age = Math.Max(1f, Math.Min(100f, age));
            
            SubModule.Log($"  AI raw: [{val0:F4}, {val1:F4}, {val2:F4}]");
            SubModule.Log($"  AI result: {(isFemale ? "Female" : "Male")} conf={confidence:F2} age={age:F0} (raw={val2:F3})");
            
            return new Demographics
            {
                IsFemale = isFemale,
                Age = age,
                Confidence = confidence
            };
        }
        
        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
            _isInitialized = false;
        }
    }
}
