namespace FaceLearner.ML
{
    /// <summary>
    /// Interface for landmark detectors (68-point Dlib, 468-point FaceMesh, etc.)
    /// </summary>
    public interface ILandmarkDetector
    {
        bool IsInitialized { get; }
        string LastError { get; }
        int NumLandmarks { get; }
        
        bool Initialize(string modelPath);
        float[] DetectLandmarks(string imagePath);
        void Dispose();
    }
}
