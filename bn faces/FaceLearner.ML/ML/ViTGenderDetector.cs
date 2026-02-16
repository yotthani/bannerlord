using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceLearner.ML
{
    /// <summary>
    /// ViT-based age and gender detector using onnx-community/age-gender-prediction-ONNX.
    /// Architecture: google/vit-base-patch16-224, dual-head (age regression + gender classification).
    /// 86.8M parameters, 224x224 input, ImageNet normalization.
    /// Gender accuracy: 94.3% on UTKFace.
    ///
    /// Preprocessing:
    ///   1. Resize to 224x224 (bilinear)
    ///   2. RGB order, float32
    ///   3. Rescale: pixel / 255.0
    ///   4. Normalize: (pixel - mean) / std
    ///      mean = [0.485, 0.456, 0.406] (ImageNet)
    ///      std  = [0.229, 0.224, 0.225] (ImageNet)
    ///
    /// Output: [ageLogit, genderLogit]
    ///   age = clamp(round(ageLogit), 0, 100)
    ///   gender >= 0.5 → Female, else Male
    /// </summary>
    public class ViTGenderDetector : IDisposable
    {
        private const int INPUT_SIZE = 224;
        private static readonly float[] MEAN = { 0.485f, 0.456f, 0.406f };
        private static readonly float[] STD = { 0.229f, 0.224f, 0.225f };

        private InferenceSession _session;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        public struct Result
        {
            public bool IsFemale;
            public float GenderConfidence;  // 0-1
            public float Age;               // 0-100
            public float GenderLogit;       // Raw logit for debugging
        }

        /// <summary>
        /// Initialize with path to the ONNX model file.
        /// Expected: vit_age_gender.onnx (~330 MB)
        /// </summary>
        public bool Initialize(string modelPath)
        {
            try
            {
                if (!File.Exists(modelPath))
                {
                    SubModule.Log($"ViTGenderDetector: Model not found at {modelPath}");
                    return false;
                }

                var options = new SessionOptions();
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                // Use single thread to avoid slowing down the game
                options.InterOpNumThreads = 1;
                options.IntraOpNumThreads = 2;

                _session = new InferenceSession(modelPath, options);
                _isInitialized = true;

                // Log input/output info
                var inputMeta = _session.InputMetadata;
                var outputMeta = _session.OutputMetadata;
                SubModule.Log($"ViTGenderDetector: Loaded ({new FileInfo(modelPath).Length / 1024 / 1024} MB)");
                SubModule.Log($"  Inputs: {string.Join(", ", inputMeta.Select(kv => $"{kv.Key}={string.Join("x", kv.Value.Dimensions)}"))}");
                SubModule.Log($"  Outputs: {string.Join(", ", outputMeta.Select(kv => $"{kv.Key}={string.Join("x", kv.Value.Dimensions)}"))}");

                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"ViTGenderDetector: Init error - {ex.Message}");
                _isInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// Detect gender and age from an image file path.
        /// </summary>
        public Result? Detect(string imagePath)
        {
            if (!_isInitialized || _session == null)
                return null;

            try
            {
                float[] inputData = PreprocessImage(imagePath);
                if (inputData == null) return null;

                // Create input tensor [1, 3, 224, 224]
                var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 3, INPUT_SIZE, INPUT_SIZE });
                var inputName = _session.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

                using (var results = _session.Run(inputs))
                {
                    // Output: logits tensor with [ageLogit, genderLogit]
                    var outputTensor = results.First().AsTensor<float>();
                    var values = outputTensor.ToArray();

                    if (values.Length < 2)
                    {
                        SubModule.Log($"ViTGenderDetector: Unexpected output length {values.Length}");
                        return null;
                    }

                    float ageLogit = values[0];
                    float genderLogit = values[1];

                    // Age: clamp(round(ageLogit), 0, 100)
                    float age = Math.Max(0f, Math.Min(100f, (float)Math.Round(ageLogit)));

                    // Gender: >= 0.5 is Female
                    bool isFemale = genderLogit >= 0.5f;
                    float confidence = isFemale
                        ? Math.Min(1f, genderLogit)           // How female (0.5-1.0 → 0.0-1.0)
                        : Math.Min(1f, 1f - genderLogit);     // How male (0.5-0.0 → 0.0-1.0)
                    // Rescale to 0-1 confidence: 0.5 = uncertain, 0.0/1.0 = certain
                    confidence = Math.Abs(genderLogit - 0.5f) * 2f;

                    return new Result
                    {
                        IsFemale = isFemale,
                        GenderConfidence = confidence,
                        Age = age,
                        GenderLogit = genderLogit
                    };
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"ViTGenderDetector: Detection error - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Preprocess image for ViT: resize 224x224, RGB, ImageNet normalization.
        /// Returns float[3 * 224 * 224] in CHW format.
        /// </summary>
        private float[] PreprocessImage(string imagePath)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                using (var resized = new Bitmap(INPUT_SIZE, INPUT_SIZE))
                using (var graphics = Graphics.FromImage(resized))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                    graphics.DrawImage(bitmap, 0, 0, INPUT_SIZE, INPUT_SIZE);

                    float[] data = new float[3 * INPUT_SIZE * INPUT_SIZE];

                    for (int y = 0; y < INPUT_SIZE; y++)
                    {
                        for (int x = 0; x < INPUT_SIZE; x++)
                        {
                            var pixel = resized.GetPixel(x, y);
                            int idx = y * INPUT_SIZE + x;

                            // RGB order, ImageNet normalization:
                            // (pixel/255.0 - mean) / std
                            data[0 * INPUT_SIZE * INPUT_SIZE + idx] = (pixel.R / 255f - MEAN[0]) / STD[0];
                            data[1 * INPUT_SIZE * INPUT_SIZE + idx] = (pixel.G / 255f - MEAN[1]) / STD[1];
                            data[2 * INPUT_SIZE * INPUT_SIZE + idx] = (pixel.B / 255f - MEAN[2]) / STD[2];
                        }
                    }

                    return data;
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"ViTGenderDetector: Preprocess error - {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
            _isInitialized = false;
        }
    }
}