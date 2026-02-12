using System.Collections.Generic;

namespace FaceLearner.ML.DataSources
{
    /// <summary>
    /// Represents a target face for learning
    /// </summary>
    public class TargetFace
    {
        public string Id { get; set; }
        public string Source { get; set; }
        public float[] Landmarks { get; set; }
        public byte[] ImageBytes { get; set; }
        public Dictionary<string, float> Metadata { get; set; }
    }
    
    /// <summary>
    /// Interface for face data sources (generated, LFW, CelebA, etc.)
    /// </summary>
    public interface IFaceDataSource
    {
        string Name { get; }
        string Description { get; }
        int TotalCount { get; }
        int ProcessedCount { get; }
        bool IsReady { get; }
        
        /// <summary>
        /// Initialize/load the data source
        /// </summary>
        bool Initialize(string basePath);
        
        /// <summary>
        /// Get next target face for learning
        /// </summary>
        TargetFace GetNextTarget();
        
        /// <summary>
        /// Get next batch of samples to process
        /// </summary>
        IEnumerable<FaceSampleInfo> GetBatch(int batchSize);
        
        /// <summary>
        /// Mark sample as processed
        /// </summary>
        void MarkProcessed(string sampleId);
        
        /// <summary>
        /// Reset all processed markers - start new epoch
        /// </summary>
        void ResetProcessed();
    }
    
    /// <summary>
    /// Basic info about a face sample from any source
    /// </summary>
    public class FaceSampleInfo
    {
        public string Id { get; set; }
        public string ImagePath { get; set; }
        public string Source { get; set; }
        
        // Optional metadata (may be null)
        public float? Age { get; set; }
        public bool? IsFemale { get; set; }
        public Dictionary<string, float> Attributes { get; set; }
    }
}
