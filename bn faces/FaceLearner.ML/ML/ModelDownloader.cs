using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;

namespace FaceLearner.ML
{
    /// <summary>
    /// Downloads and extracts ML models for face detection
    /// </summary>
    public static class ModelDownloader
    {
        // MediaPipe FaceMesh 468-point model (ONNX)
        // From PINTO Model Zoo: 032_FaceMesh/01_float32/face_landmark.onnx
        // Place in: FaceLearner/Models/face_landmark.onnx
        private const string FACEMESH_MODEL_FILENAME = "face_landmark.onnx";
        
        // BlazeFace detection model (for face cropping before landmark detection)
        // From PINTO Model Zoo: 030_BlazeFace/01_float32/face_detection_front_128x128_float32.onnx
        // Place in: FaceLearner/Models/face_detection_front_128x128_float32.onnx
        private const string FACE_DETECTION_FILENAME = "face_detection_front_128x128_float32.onnx";
        
        // FairFace model for accurate gender/age/race detection across all ethnicities
        // Paper: "FairFace: Face Attribute Dataset for Balanced Race, Gender, and Age"
        private const string FAIRFACE_MODEL_URL = "https://huggingface.co/facefusion/models-3.0.0/resolve/main/fairface.onnx";
        private const string FAIRFACE_MODEL_FILENAME = "fairface.onnx";
        
        public static event Action<string> OnProgress;
        public static event Action<int> OnPercentProgress;
        
        private static bool _tlsInitialized = false;
        
        private static void EnsureTls()
        {
            if (!_tlsInitialized)
            {
                // Enable TLS 1.2 for HTTPS downloads (required on older .NET Framework)
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                }
                catch { }
                _tlsInitialized = true;
            }
        }
        
        /// <summary>
        /// Check if FaceMesh model exists
        /// </summary>
        public static bool FaceMeshModelExists(string modelsDir)
        {
            return File.Exists(Path.Combine(modelsDir, FACEMESH_MODEL_FILENAME));
        }
        
        /// <summary>
        /// Get full path to FaceMesh model file
        /// </summary>
        public static string GetFaceMeshModelPath(string modelsDir)
        {
            return Path.Combine(modelsDir, FACEMESH_MODEL_FILENAME);
        }
        
        /// <summary>
        /// Get full path to face detection model
        /// </summary>
        public static string GetFaceDetectionModelPath(string modelsDir)
        {
            return Path.Combine(modelsDir, FACE_DETECTION_FILENAME);
        }
        
        /// <summary>
        /// Check if FairFace model exists
        /// </summary>
        public static bool FairFaceModelExists(string modelsDir)
        {
            return File.Exists(Path.Combine(modelsDir, FAIRFACE_MODEL_FILENAME));
        }
        
        /// <summary>
        /// Get full path to FairFace model file
        /// </summary>
        public static string GetFairFaceModelPath(string modelsDir)
        {
            return Path.Combine(modelsDir, FAIRFACE_MODEL_FILENAME);
        }
        
        /// <summary>
        /// Download FairFace model (~85 MB)
        /// </summary>
        public static bool DownloadFairFaceModel(string modelsDir)
        {
            try
            {
                if (!Directory.Exists(modelsDir))
                    Directory.CreateDirectory(modelsDir);
                
                string modelPath = Path.Combine(modelsDir, FAIRFACE_MODEL_FILENAME);
                
                if (File.Exists(modelPath))
                {
                    var info = new FileInfo(modelPath);
                    if (info.Length > 80_000_000) // >80MB = valid
                    {
                        OnProgress?.Invoke("FairFace model already exists");
                        return true;
                    }
                    // Corrupted/incomplete, delete and redownload
                    File.Delete(modelPath);
                }
                
                OnProgress?.Invoke("Downloading FairFace model (~85MB)...");
                EnsureTls();
                
                using (var client = new WebClient())
                {
                    client.DownloadProgressChanged += (s, e) => 
                    {
                        OnPercentProgress?.Invoke(e.ProgressPercentage);
                    };
                    
                    // Synchronous download
                    client.DownloadFile(FAIRFACE_MODEL_URL, modelPath);
                }
                
                // Verify download
                var finalInfo = new FileInfo(modelPath);
                if (finalInfo.Length < 80_000_000)
                {
                    OnProgress?.Invoke($"FairFace download incomplete ({finalInfo.Length / 1_000_000}MB)");
                    return false;
                }
                
                OnProgress?.Invoke($"FairFace model downloaded ({finalInfo.Length / 1_000_000}MB)");
                return true;
            }
            catch (Exception ex)
            {
                OnProgress?.Invoke($"FairFace download failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Check for FaceMesh ONNX model - no auto-download, must be in project
        /// </summary>
        public static bool DownloadFaceMeshModel(string modelsDir)
        {
            string landmarkPath = Path.Combine(modelsDir, FACEMESH_MODEL_FILENAME);
            
            if (File.Exists(landmarkPath))
            {
                OnProgress?.Invoke($"FaceMesh model found: {FACEMESH_MODEL_FILENAME}");
                return true;
            }
            
            OnProgress?.Invoke($"FaceMesh model not found: {landmarkPath}");
            OnProgress?.Invoke("Place face_landmark.onnx in FaceLearner/Models/ folder");
            return false;
        }
        
        /// <summary>
        /// Download all FaceMesh related models (landmark + detection)
        /// </summary>
        public static bool DownloadAllFaceMeshModels(string modelsDir)
        {
            // DownloadFaceMeshModel now handles both landmark and detection models
            return DownloadFaceMeshModel(modelsDir);
        }
        
        /// <summary>
        /// Download FaceMesh models async
        /// </summary>
        public static async Task<bool> DownloadFaceMeshModelsAsync(string modelsDir)
        {
            return await Task.Run(() => DownloadAllFaceMeshModels(modelsDir));
        }
        
        private static bool DownloadFile(string url, string outputPath)
        {
            try
            {
                EnsureTls();
                
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "FaceLearner/1.0");
                    client.DownloadProgressChanged += (s, e) => OnPercentProgress?.Invoke(e.ProgressPercentage);
                    client.DownloadFile(url, outputPath);
                }
                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 1000; // At least 1KB
            }
            catch (Exception ex)
            {
                OnProgress?.Invoke($"Download error: {ex.Message}");
                // Clean up partial download
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
                return false;
            }
        }
        
        private static void ExtractBz2(string bz2Path, string outputPath)
        {
            using (var fs = File.OpenRead(bz2Path))
            using (var bz2 = new BZip2InputStream(fs))
            using (var output = File.Create(outputPath))
            {
                byte[] buffer = new byte[4096];
                int read;
                while ((read = bz2.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
            }
        }
    }
}
