using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FaceLearner.ML.DataSources
{
    /// <summary>
    /// Fixed test dataset for reproducible debugging.
    /// Loads JPGs from Datasets/testing/ folder (relative to project root).
    /// No metadata parsing — demographics detected by ML pipeline.
    /// </summary>
    public class TestDataSource : IFaceDataSource
    {
        private string _dataDir;
        private List<string> _allImages = new List<string>();
        private HashSet<string> _processedIds = new HashSet<string>();
        private int _currentIndex = 0;

        public string Name => "TestSet";
        public string Description => "Fixed test dataset (49 curated faces for reproducible debugging)";
        public int TotalCount => _allImages.Count;
        public int ProcessedCount => _processedIds.Count;
        public bool IsReady { get; private set; }

        public bool Initialize(string basePath)
        {
            // Look for testing folder in multiple locations
            string[] possiblePaths = new[]
            {
                Path.Combine(basePath, "datasets", "testing"),   // Deployed: Data\datasets\testing
                Path.Combine(basePath, "Datasets", "testing"),   // Alt casing
                Path.Combine(basePath, "..", "Datasets", "testing"),
                // Source code location (for development)
                @"D:\Work\Sources\github\bannerlord\bn faces\FaceLearner.ML\Datasets\testing",
            };

            _dataDir = null;
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    var jpgFiles = Directory.GetFiles(path, "*.jpg", SearchOption.TopDirectoryOnly);
                    if (jpgFiles.Length > 0)
                    {
                        _dataDir = path;
                        SubModule.Log($"TestSet found at: {path} ({jpgFiles.Length} files)");
                        break;
                    }
                }
            }

            if (_dataDir == null || !Directory.Exists(_dataDir))
            {
                SubModule.Log("TestSet: No valid directory found");
                return false;
            }

            // Load all images, sorted by name for deterministic order
            _allImages = Directory.GetFiles(_dataDir, "*.jpg", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(_dataDir, "*.png", SearchOption.TopDirectoryOnly))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
            _currentIndex = 0;

            IsReady = _allImages.Count > 0;
            if (IsReady)
                SubModule.Log($"TestSet: {_allImages.Count} images ready (deterministic order)");
            return IsReady;
        }

        public TargetFace GetNextTarget()
        {
            if (!IsReady || _allImages.Count == 0) return null;

            // Stop at end — no duplicates during testing cycle
            if (_currentIndex >= _allImages.Count)
                return null;

            string file = _allImages[_currentIndex];
            _currentIndex++;

            try
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string id = $"test_{fileName}";
                byte[] imageBytes = File.ReadAllBytes(file);

                return new TargetFace
                {
                    Id = id,
                    Source = Name,
                    ImageBytes = imageBytes,
                    Landmarks = null
                };
            }
            catch (Exception ex)
            {
                SubModule.Log($"TestSet: Error loading {file}: {ex.Message}");
                return null;
            }
        }

        public IEnumerable<FaceSampleInfo> GetBatch(int batchSize)
        {
            int found = 0;
            while (found < batchSize && _currentIndex < _allImages.Count)
            {
                string file = _allImages[_currentIndex];
                _currentIndex++;

                string fileName = Path.GetFileNameWithoutExtension(file);
                string id = $"test_{fileName}";

                if (_processedIds.Contains(id)) continue;

                found++;
                yield return new FaceSampleInfo
                {
                    Id = id,
                    ImagePath = file,
                    Source = Name
                };
            }
        }

        public void MarkProcessed(string sampleId) => _processedIds.Add(sampleId);

        public void ResetProcessed()
        {
            _processedIds.Clear();
            _currentIndex = 0;
        }
    }
}
