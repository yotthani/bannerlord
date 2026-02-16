namespace FaceLearner.ML
{
    /// <summary>
    /// Interface for landmark detectors (FaceMesh 468-point)
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
