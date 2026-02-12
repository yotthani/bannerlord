using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceLearner.ML
{
    /// <summary>
    /// FairFace-based demographic detection.
    /// Much more accurate than InsightFace for diverse ethnicities.
    /// 
    /// Paper: "FairFace: Face Attribute Dataset for Balanced Race, Gender, and Age"
    /// - Trained on 108,501 balanced images across 7 race groups
    /// - Much better accuracy for Asian, Black, Latino faces
    /// 
    /// Model: https://huggingface.co/facefusion/models-3.0.0/blob/main/fairface.onnx
    /// </summary>
    public class FairFaceDetector : IDisposable
    {
        private InferenceSession _session;
        private bool _disposed;
        private string _inputName;
        private int _inputWidth = 224;
        private int _inputHeight = 224;
        
        // FairFace outputs
        private static readonly string[] RACES = { "White", "Black", "Latino", "East Asian", "Southeast Asian", "Indian", "Middle Eastern" };
        private static readonly string[] GENDERS = { "Male", "Female" };
        private static readonly string[] AGES = { "0-2", "3-9", "10-19", "20-29", "30-39", "40-49", "50-59", "60-69", "70+" };
        
        public bool IsLoaded => _session != null;
        
        /// <summary>
        /// Load FairFace ONNX model
        /// </summary>
        public bool Load(string modelPath)
        {
            try
            {
                if (!System.IO.File.Exists(modelPath))
                {
                    SubModule.Log($"FairFace: Model not found at {modelPath}");
                    return false;
                }
                
                var options = new SessionOptions();
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                
                _session = new InferenceSession(modelPath, options);
                
                // Get input info
                var inputMeta = _session.InputMetadata.First();
                _inputName = inputMeta.Key;
                var dims = inputMeta.Value.Dimensions;
                
                // FairFace expects [1, 3, 224, 224] (NCHW format)
                if (dims.Length == 4)
                {
                    _inputHeight = dims[2];
                    _inputWidth = dims[3];
                }
                
                SubModule.Log($"FairFace: Loaded successfully");
                SubModule.Log($"  Input: {_inputName} [{string.Join(",", dims)}]");
                
                // Log outputs
                foreach (var output in _session.OutputMetadata)
                {
                    SubModule.Log($"  Output: {output.Key} [{string.Join(",", output.Value.Dimensions)}]");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"FairFace: Failed to load - {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Detect demographics from a face image
        /// </summary>
        /// <param name="facePixels">RGB pixels of cropped face (224x224 expected)</param>
        /// <returns>(isFemale, confidence, age, race)</returns>
        public (bool isFemale, float genderConf, float age, string race, float raceConf) Detect(byte[] facePixels, int width, int height)
        {
            if (_session == null)
                return (false, 0f, 25f, "Unknown", 0f);
            
            try
            {
                // Preprocess: resize to 224x224 and normalize
                var inputTensor = PreprocessImage(facePixels, width, height);
                
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                };
                
                using var results = _session.Run(inputs);
                
                // Parse outputs - FairFace outputs vary by model version
                // Common format: race (7), gender (2), age (9 bins or continuous)
                float[] raceLogits = null;
                float[] genderLogits = null;
                float[] ageLogits = null;
                
                foreach (var result in results)
                {
                    var tensor = result.AsTensor<float>();
                    var data = tensor.ToArray();
                    
                    // FairFace facefusion model outputs 18 values
                    // FORMAT: race(7), gender(2), age(9)
                    // Gender order: [Male, Female]
                    if (data.Length == 18)
                    {
                        // Log raw output for debugging
                        SubModule.Log($"    FF raw[0:6]: {string.Join(", ", data.Take(6).Select(x => x.ToString("F2")))}");
                        SubModule.Log($"    FF raw[6:12]: {string.Join(", ", data.Skip(6).Take(6).Select(x => x.ToString("F2")))}");
                        SubModule.Log($"    FF raw[12:18]: {string.Join(", ", data.Skip(12).Take(6).Select(x => x.ToString("F2")))}");
                        
                        // FairFace model format: race(7), gender(2), age(9)
                        raceLogits = data.Take(7).ToArray();
                        genderLogits = data.Skip(7).Take(2).ToArray();
                        ageLogits = data.Skip(9).Take(9).ToArray();
                    }
                    else if (data.Length == 7)
                        raceLogits = data;
                    else if (data.Length == 2)
                        genderLogits = data;
                    else if (data.Length == 9)
                        ageLogits = data;
                }
                
                // Parse gender - FairFace uses [Male, Female] order
                bool isFemale = false;
                float genderConf = 0.5f;
                if (genderLogits != null && genderLogits.Length >= 2)
                {
                    var genderProbs = Softmax(genderLogits);
                    // FairFace format: [Male, Female] - index 1 is Female
                    isFemale = genderProbs[1] > genderProbs[0];
                    genderConf = isFemale ? genderProbs[1] : genderProbs[0];
                }
                
                // Parse age - use weighted average for better accuracy
                float age = 30f;
                if (ageLogits != null && ageLogits.Length >= 9)
                {
                    var ageProbs = Softmax(ageLogits);
                    // FairFace age bins: [0-2, 3-9, 10-19, 20-29, 30-39, 40-49, 50-59, 60-69, 70+]
                    float[] ageCenters = { 1f, 6f, 15f, 25f, 35f, 45f, 55f, 65f, 75f };
                    
                    // Use weighted average instead of just argmax for more accurate age
                    float weightedAge = 0f;
                    for (int i = 0; i < Math.Min(ageProbs.Length, ageCenters.Length); i++)
                    {
                        weightedAge += ageProbs[i] * ageCenters[i];
                    }
                    age = weightedAge;
                    
                    // Also get the dominant bin for logging
                    int maxIdx = Array.IndexOf(ageProbs, ageProbs.Max());
                    float maxProb = ageProbs.Max();
                    
                    // If one bin is very dominant (>60%), use its center instead
                    if (maxProb > 0.6f)
                    {
                        age = ageCenters[Math.Min(maxIdx, ageCenters.Length - 1)];
                    }
                }
                
                // Parse race
                string race = "Unknown";
                float raceConf = 0f;
                if (raceLogits != null)
                {
                    var raceProbs = Softmax(raceLogits);
                    int maxIdx = Array.IndexOf(raceProbs, raceProbs.Max());
                    race = RACES[Math.Min(maxIdx, RACES.Length - 1)];
                    raceConf = raceProbs[maxIdx];
                }
                
                return (isFemale, genderConf, age, race, raceConf);
            }
            catch (Exception ex)
            {
                SubModule.Log($"FairFace: Detection failed - {ex.Message}");
                return (false, 0f, 25f, "Unknown", 0f);
            }
        }
        
        /// <summary>
        /// Detect from System.Drawing.Bitmap
        /// </summary>
        public (bool isFemale, float genderConf, float age, string race, float raceConf) Detect(System.Drawing.Bitmap faceBitmap)
        {
            // Resize to 224x224
            using var resized = new System.Drawing.Bitmap(faceBitmap, _inputWidth, _inputHeight);
            
            var pixels = new byte[_inputWidth * _inputHeight * 3];
            for (int y = 0; y < _inputHeight; y++)
            {
                for (int x = 0; x < _inputWidth; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    int idx = (y * _inputWidth + x) * 3;
                    pixels[idx] = pixel.R;
                    pixels[idx + 1] = pixel.G;
                    pixels[idx + 2] = pixel.B;
                }
            }
            
            return Detect(pixels, _inputWidth, _inputHeight);
        }
        
        private DenseTensor<float> PreprocessImage(byte[] pixels, int width, int height)
        {
            // Resize to 224x224 if needed (simple bilinear)
            byte[] resized = pixels;
            if (width != _inputWidth || height != _inputHeight)
            {
                resized = ResizeImage(pixels, width, height, _inputWidth, _inputHeight);
            }
            
            // Create NCHW tensor [1, 3, 224, 224]
            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
            
            // ImageNet normalization
            float[] mean = { 0.485f, 0.456f, 0.406f };
            float[] std = { 0.229f, 0.224f, 0.225f };
            
            for (int y = 0; y < _inputHeight; y++)
            {
                for (int x = 0; x < _inputWidth; x++)
                {
                    int srcIdx = (y * _inputWidth + x) * 3;
                    
                    // RGB -> normalized
                    tensor[0, 0, y, x] = (resized[srcIdx] / 255f - mean[0]) / std[0];     // R
                    tensor[0, 1, y, x] = (resized[srcIdx + 1] / 255f - mean[1]) / std[1]; // G
                    tensor[0, 2, y, x] = (resized[srcIdx + 2] / 255f - mean[2]) / std[2]; // B
                }
            }
            
            return tensor;
        }
        
        private byte[] ResizeImage(byte[] src, int srcW, int srcH, int dstW, int dstH)
        {
            var dst = new byte[dstW * dstH * 3];
            float scaleX = (float)srcW / dstW;
            float scaleY = (float)srcH / dstH;
            
            for (int y = 0; y < dstH; y++)
            {
                for (int x = 0; x < dstW; x++)
                {
                    int srcX = Math.Min((int)(x * scaleX), srcW - 1);
                    int srcY = Math.Min((int)(y * scaleY), srcH - 1);
                    
                    int srcIdx = (srcY * srcW + srcX) * 3;
                    int dstIdx = (y * dstW + x) * 3;
                    
                    dst[dstIdx] = src[srcIdx];
                    dst[dstIdx + 1] = src[srcIdx + 1];
                    dst[dstIdx + 2] = src[srcIdx + 2];
                }
            }
            
            return dst;
        }
        
        private float[] Softmax(float[] logits)
        {
            float maxVal = logits.Max();
            float[] exp = logits.Select(x => (float)Math.Exp(x - maxVal)).ToArray();
            float sum = exp.Sum();
            return exp.Select(x => x / sum).ToArray();
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
        }
    }
}
