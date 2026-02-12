using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.ML.DataSources
{
    /// <summary>
    /// Manages multiple face data sources
    /// </summary>
    public class DataSourceManager
    {
        private List<IFaceDataSource> _sources = new List<IFaceDataSource>();
        private string _basePath;
        
        public int TotalSources => _sources.Count;
        public int ReadySources => _sources.Count(s => s.IsReady);
        public int TotalSamples => _sources.Sum(s => s.TotalCount);
        public int ProcessedSamples => _sources.Sum(s => s.ProcessedCount);
        
        public DataSourceManager(string basePath)
        {
            _basePath = basePath;
        }
        
        /// <summary>
        /// Register a data source
        /// </summary>
        public void Register(IFaceDataSource source)
        {
            _sources.Add(source);
        }
        
        /// <summary>
        /// Initialize all registered sources
        /// </summary>
        public void InitializeAll()
        {
            foreach (var source in _sources)
            {
                source.Initialize(_basePath);
            }
        }
        
        /// <summary>
        /// Get all ready sources
        /// </summary>
        public IEnumerable<IFaceDataSource> GetReadySources()
        {
            return _sources.Where(s => s.IsReady);
        }
        
        /// <summary>
        /// Get all registered sources (ready or not)
        /// </summary>
        public IEnumerable<IFaceDataSource> GetAllSources()
        {
            return _sources;
        }
        
        /// <summary>
        /// Get a specific source by name
        /// </summary>
        public IFaceDataSource GetSource(string name)
        {
            return _sources.FirstOrDefault(s => s.Name == name);
        }
        
        /// <summary>
        /// Get samples from all ready sources combined
        /// </summary>
        public IEnumerable<FaceSampleInfo> GetMixedBatch(int batchSize)
        {
            int perSource = batchSize / System.Math.Max(1, ReadySources);
            
            foreach (var source in _sources.Where(s => s.IsReady))
            {
                foreach (var sample in source.GetBatch(perSource))
                {
                    yield return sample;
                }
            }
        }
        
        /// <summary>
        /// Get status string
        /// </summary>
        public string GetStatusString()
        {
            var parts = new List<string>();
            foreach (var source in _sources)
            {
                string status = source.IsReady ? $"{source.ProcessedCount}/{source.TotalCount}" : "N/A";
                parts.Add($"{source.Name}: {status}");
            }
            return string.Join(" | ", parts);
        }
    }
}
