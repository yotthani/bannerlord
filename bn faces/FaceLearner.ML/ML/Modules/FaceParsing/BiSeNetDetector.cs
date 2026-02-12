using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using FaceLearner.ML.Modules.Core;

namespace FaceLearner.ML.Modules.FaceParsing
{
    /// <summary>
    /// BiSeNet-based face parsing (semantic segmentation).
    /// Segments face into 19 regions: skin, eyes, nose, lips, hair, etc.
    /// 
    /// Model: BiSeNet trained on CelebAMask-HQ (19 classes)
    /// Input: 512x512 RGB image
    /// Output: 512x512 segmentation mask
    /// 
    /// Download: https://github.com/zllrunning/face-parsing.PyTorch
    /// ONNX export needed or use: https://github.com/onnx/models (face-parsing)
    /// </summary>
    public class BiSeNetDetector : IDisposable
    {
        private InferenceSession _session;
        private string _inputName;
        private int _inputSize = 512;  // BiSeNet expects 512x512
        
        // ImageNet normalization (BiSeNet uses this)
        private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
        private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };
        
        #region IDetector Implementation
        
        public string Name => "BiSeNet";
        public float ReliabilityWeight => 0.9f;  // High reliability
        public bool IsLoaded => _session != null;
        
        public bool Load(string modelPath)
        {
            try
            {
                if (!File.Exists(modelPath))
                {
                    SubModule.Log($"BiSeNet: Model not found at {modelPath}");
                    return false;
                }
                
                var options = new SessionOptions();
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                
                // Try to use GPU if available
                try
                {
                    // options.AppendExecutionProvider_CUDA(0);  // Uncomment if CUDA available
                }
                catch { /* CPU fallback */ }
                
                _session = new InferenceSession(modelPath, options);
                
                // Get input info
                var inputMeta = _session.InputMetadata.First();
                _inputName = inputMeta.Key;
                var dims = inputMeta.Value.Dimensions;
                
                // Expected: [1, 3, 512, 512] (NCHW) or [-1, 3, 512, 512]
                if (dims.Length >= 4)
                {
                    // Handle dynamic batch size (-1)
                    _inputSize = dims[2] > 0 ? dims[2] : 512;
                }
                
                string modelName = Path.GetFileName(modelPath);
                SubModule.Log($"BiSeNet: ✓ Loaded '{modelName}' successfully");
                SubModule.Log($"  Input: {_inputName} [{string.Join(",", dims)}] → {_inputSize}x{_inputSize}");
                
                foreach (var output in _session.OutputMetadata)
                {
                    SubModule.Log($"  Output: {output.Key} [{string.Join(",", output.Value.Dimensions)}]");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"BiSeNet: Failed to load - {ex.Message}");
                return false;
            }
        }
        
        public FaceParsingResult Detect(Bitmap image)
        {
            if (_session == null || image == null)
            {
                return CreateEmptyResult();
            }
            
            try
            {
                // Preprocess: resize to 512x512 and normalize
                var inputTensor = PreprocessImage(image);
                
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                };
                
                using var results = _session.Run(inputs);
                
                // Get the first output
                var output = results.First();
                var outputTensor = output.AsTensor<float>();
                if (outputTensor == null)
                {
                    return CreateEmptyResult();
                }
                var outputShape = outputTensor.Dimensions.ToArray();
                
                byte[,] mask;
                
                if (outputShape.Length == 4 && outputShape[1] >= 19)
                {
                    // Multi-channel output: [1, 19, H, W] - argmax over channels
                    mask = ParseMultiChannelOutput(outputTensor, outputShape);
                }
                else if (outputShape.Length == 3 || (outputShape.Length == 4 && outputShape[1] == 1))
                {
                    // Single-channel output: [1, H, W] or [1, 1, H, W] - direct labels
                    mask = ParseSingleChannelOutput(outputTensor, outputShape);
                }
                else
                {
                    // Try to parse as int64 tensor (some models output labels directly)
                    try
                    {
                        var int64Tensor = output.AsTensor<long>();
                        outputShape = int64Tensor.Dimensions.ToArray();
                        mask = ParseInt64Output(int64Tensor, outputShape);
                    }
                    catch
                    {
                        // Try int32
                        try
                        {
                            var int32Tensor = output.AsTensor<int>();
                            outputShape = int32Tensor.Dimensions.ToArray();
                            mask = ParseInt32Output(int32Tensor, outputShape);
                        }
                        catch (Exception ex)
                        {
                            SubModule.Log($"BiSeNet: Failed to parse output - {ex.Message}");
                            return CreateEmptyResult();
                        }
                    }
                }
                
                // Resize mask back to original image size
                mask = ResizeMask(mask, image.Width, image.Height);
                
                // Calculate region statistics
                var (regionBounds, regionCounts) = CalculateRegionStats(mask, image.Width, image.Height);
                
                // Calculate confidence based on detected regions
                float confidence = CalculateConfidence(regionCounts);
                
                return new FaceParsingResult
                {
                    Mask = mask,
                    Width = image.Width,
                    Height = image.Height,
                    RegionBounds = regionBounds,
                    RegionPixelCounts = regionCounts,
                    Confidence = confidence,
                    Source = Name
                };
            }
            catch (Exception ex)
            {
                SubModule.Log($"BiSeNet: Detection failed - {ex.Message}");
                return CreateEmptyResult();
            }
        }
        
        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
        }
        
        #endregion
        
        #region Preprocessing
        
        private DenseTensor<float> PreprocessImage(Bitmap image)
        {
            // Resize to input size
            using var resized = new Bitmap(image, new Size(_inputSize, _inputSize));
            
            // Create tensor [1, 3, H, W] (NCHW format)
            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputSize, _inputSize });
            
            var data = resized.LockBits(
                new Rectangle(0, 0, _inputSize, _inputSize),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            
            try
            {
                int stride = data.Stride;
                int bytes = Math.Abs(stride) * _inputSize;
                byte[] rgbValues = new byte[bytes];
                
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, rgbValues, 0, bytes);
                
                for (int y = 0; y < _inputSize; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < _inputSize; x++)
                    {
                        int idx = rowOffset + x * 3;
                        
                        // BGR to RGB, normalize with ImageNet stats
                        float b = rgbValues[idx] / 255f;
                        float g = rgbValues[idx + 1] / 255f;
                        float r = rgbValues[idx + 2] / 255f;
                        
                        tensor[0, 0, y, x] = (r - Mean[0]) / Std[0];  // R
                        tensor[0, 1, y, x] = (g - Mean[1]) / Std[1];  // G
                        tensor[0, 2, y, x] = (b - Mean[2]) / Std[2];  // B
                    }
                }
            }
            finally
            {
                resized.UnlockBits(data);
            }
            
            return tensor;
        }
        
        #endregion
        
        #region Output Parsing
        
        private byte[,] ParseMultiChannelOutput(Tensor<float> tensor, int[] shape)
        {
            // Shape: [1, C, H, W] - argmax over channel dimension (C = num classes)
            int numClasses = shape[1];
            int height = shape[2];
            int width = shape[3];
            
            // Limit to 19 classes max (CelebAMask-HQ standard)
            numClasses = Math.Min(numClasses, 19);
            
            var mask = new byte[width, height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float maxVal = float.MinValue;
                    int maxIdx = 0;
                    
                    for (int c = 0; c < numClasses; c++)
                    {
                        float val = tensor[0, c, y, x];
                        if (val > maxVal)
                        {
                            maxVal = val;
                            maxIdx = c;
                        }
                    }
                    
                    mask[x, y] = (byte)maxIdx;
                }
            }
            
            return mask;
        }
        
        private byte[,] ParseSingleChannelOutput(Tensor<float> tensor, int[] shape)
        {
            // Shape: [1, H, W] or [1, 1, H, W]
            int height = shape.Length == 3 ? shape[1] : shape[2];
            int width = shape.Length == 3 ? shape[2] : shape[3];
            
            var mask = new byte[width, height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float val = shape.Length == 3 ? tensor[0, y, x] : tensor[0, 0, y, x];
                    mask[x, y] = (byte)Math.Max(0, Math.Min(18, (int)Math.Round(val)));
                }
            }
            
            return mask;
        }
        
        private byte[,] ParseInt64Output(Tensor<long> tensor, int[] shape)
        {
            // Shape: [1, H, W] or [1, 1, H, W] - direct label indices
            int height, width;
            
            if (shape.Length == 3)
            {
                height = shape[1];
                width = shape[2];
            }
            else if (shape.Length == 4)
            {
                height = shape[2];
                width = shape[3];
            }
            else
            {
                SubModule.Log($"BiSeNet: Unexpected int64 shape [{string.Join(",", shape)}]");
                return new byte[512, 512];
            }
            
            var mask = new byte[width, height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    long val = shape.Length == 3 ? tensor[0, y, x] : tensor[0, 0, y, x];
                    mask[x, y] = (byte)Math.Max(0, Math.Min(18, val));
                }
            }
            
            return mask;
        }
        
        private byte[,] ParseInt32Output(Tensor<int> tensor, int[] shape)
        {
            // Shape: [1, H, W] or [1, 1, H, W] - direct label indices
            int height, width;
            
            if (shape.Length == 3)
            {
                height = shape[1];
                width = shape[2];
            }
            else if (shape.Length == 4)
            {
                height = shape[2];
                width = shape[3];
            }
            else
            {
                SubModule.Log($"BiSeNet: Unexpected int32 shape [{string.Join(",", shape)}]");
                return new byte[512, 512];
            }
            
            var mask = new byte[width, height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int val = shape.Length == 3 ? tensor[0, y, x] : tensor[0, 0, y, x];
                    mask[x, y] = (byte)Math.Max(0, Math.Min(18, val));
                }
            }
            
            return mask;
        }
        
        private byte[,] ResizeMask(byte[,] mask, int targetWidth, int targetHeight)
        {
            int srcWidth = mask.GetLength(0);
            int srcHeight = mask.GetLength(1);
            
            if (srcWidth == targetWidth && srcHeight == targetHeight)
                return mask;
            
            var resized = new byte[targetWidth, targetHeight];
            
            float xRatio = (float)srcWidth / targetWidth;
            float yRatio = (float)srcHeight / targetHeight;
            
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int srcX = Math.Min((int)(x * xRatio), srcWidth - 1);
                    int srcY = Math.Min((int)(y * yRatio), srcHeight - 1);
                    resized[x, y] = mask[srcX, srcY];
                }
            }
            
            return resized;
        }
        
        #endregion
        
        #region Statistics
        
        private (Dictionary<FaceRegion, RegionBounds>, Dictionary<FaceRegion, int>) 
            CalculateRegionStats(byte[,] mask, int width, int height)
        {
            var bounds = new Dictionary<FaceRegion, RegionBounds>();
            var counts = new Dictionary<FaceRegion, int>();
            
            // Initialize trackers for each region
            var minX = new Dictionary<FaceRegion, int>();
            var minY = new Dictionary<FaceRegion, int>();
            var maxX = new Dictionary<FaceRegion, int>();
            var maxY = new Dictionary<FaceRegion, int>();
            
            foreach (FaceRegion region in Enum.GetValues(typeof(FaceRegion)))
            {
                counts[region] = 0;
                minX[region] = int.MaxValue;
                minY[region] = int.MaxValue;
                maxX[region] = int.MinValue;
                maxY[region] = int.MinValue;
            }
            
            // Single pass through mask
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var region = (FaceRegion)mask[x, y];
                    counts[region]++;
                    
                    if (x < minX[region]) minX[region] = x;
                    if (y < minY[region]) minY[region] = y;
                    if (x > maxX[region]) maxX[region] = x;
                    if (y > maxY[region]) maxY[region] = y;
                }
            }
            
            // Build bounds for regions with pixels
            foreach (FaceRegion region in Enum.GetValues(typeof(FaceRegion)))
            {
                if (counts[region] > 0)
                {
                    bounds[region] = new RegionBounds
                    {
                        MinX = minX[region],
                        MinY = minY[region],
                        MaxX = maxX[region],
                        MaxY = maxY[region],
                        PixelCount = counts[region]
                    };
                }
            }
            
            return (bounds, counts);
        }
        
        private float CalculateConfidence(Dictionary<FaceRegion, int> counts)
        {
            // A good face parsing should have: skin, at least one eye, nose, and lips
            int skinCount, leftEyeCount, rightEyeCount, noseCount, upperLipCount, lowerLipCount;
            counts.TryGetValue(FaceRegion.Skin, out skinCount);
            counts.TryGetValue(FaceRegion.LeftEye, out leftEyeCount);
            counts.TryGetValue(FaceRegion.RightEye, out rightEyeCount);
            counts.TryGetValue(FaceRegion.Nose, out noseCount);
            counts.TryGetValue(FaceRegion.UpperLip, out upperLipCount);
            counts.TryGetValue(FaceRegion.LowerLip, out lowerLipCount);
            
            bool hasSkin = skinCount > 100;
            bool hasEyes = leftEyeCount > 20 || rightEyeCount > 20;
            bool hasNose = noseCount > 50;
            bool hasLips = upperLipCount > 20 || lowerLipCount > 20;
            
            int featuresFound = (hasSkin ? 1 : 0) + (hasEyes ? 1 : 0) + 
                                (hasNose ? 1 : 0) + (hasLips ? 1 : 0);
            
            return featuresFound / 4f;
        }
        
        private FaceParsingResult CreateEmptyResult()
        {
            return new FaceParsingResult
            {
                Mask = null,
                Width = 0,
                Height = 0,
                RegionBounds = new Dictionary<FaceRegion, RegionBounds>(),
                RegionPixelCounts = new Dictionary<FaceRegion, int>(),
                Confidence = 0f,
                Source = Name
            };
        }
        
        #endregion
    }
}
