using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FaceLearner.ML.DataSources
{
    /// <summary>
    /// CelebA dataset (~200K faces with 40 attributes)
    /// </summary>
    public class CelebADataSource : IFaceDataSource
    {
        private string _dataDir;
        private List<string> _allImages = new List<string>();
        private Dictionary<string, Dictionary<string, float>> _attributes = new Dictionary<string, Dictionary<string, float>>();
        private HashSet<string> _processedIds = new HashSet<string>();
        private Random _random = new Random();
        private int _currentIndex = 0;  // Sequential index into pre-shuffled list
        
        // CelebA 40 attributes
        private static readonly string[] ATTR_NAMES = {
            "5_o_Clock_Shadow", "Arched_Eyebrows", "Attractive", "Bags_Under_Eyes",
            "Bald", "Bangs", "Big_Lips", "Big_Nose", "Black_Hair", "Blond_Hair",
            "Blurry", "Brown_Hair", "Bushy_Eyebrows", "Chubby", "Double_Chin",
            "Eyeglasses", "Goatee", "Gray_Hair", "Heavy_Makeup", "High_Cheekbones",
            "Male", "Mouth_Slightly_Open", "Mustache", "Narrow_Eyes", "No_Beard",
            "Oval_Face", "Pale_Skin", "Pointy_Nose", "Receding_Hairline", "Rosy_Cheeks",
            "Sideburns", "Smiling", "Straight_Hair", "Wavy_Hair", "Wearing_Earrings",
            "Wearing_Hat", "Wearing_Lipstick", "Wearing_Necklace", "Wearing_Necktie", "Young"
        };
        
        public string Name => "CelebA";
        public string Description => "CelebFaces Attributes (200,000 faces, 40 attributes)";
        public int TotalCount => _allImages.Count;
        public int ProcessedCount => _processedIds.Count;
        public bool IsReady { get; private set; }
        
        public bool Initialize(string basePath)
        {
            // Standard location: Data/datasets/celeba/img_align_celeba
            string imgDir = Path.Combine(basePath, "datasets", "celeba", "img_align_celeba");
            _dataDir = Path.Combine(basePath, "datasets", "celeba");
            
            if (!Directory.Exists(imgDir))
            {
                SubModule.Log($"CelebA: Not found at {imgDir}");
                return false;
            }
            
            var jpgCount = Directory.GetFiles(imgDir, "*.jpg").Length;
            if (jpgCount < 1000)
            {
                SubModule.Log($"CelebA: Only {jpgCount} images (expected 200K+)");
                return false;
            }
            
            SubModule.Log($"CelebA: Found {jpgCount} images");
            
            // Load attributes if available
            string attrFile = Path.Combine(_dataDir, "list_attr_celeba.txt");
            if (File.Exists(attrFile))
            {
                LoadAttributes(attrFile);
                SubModule.Log($"CelebA: Loaded {_attributes.Count} attribute entries");
            }
            
            // Scan and shuffle
            _allImages = Directory.GetFiles(imgDir, "*.jpg")
                .OrderBy(_ => _random.Next())
                .ToList();
            _currentIndex = 0;
            
            IsReady = true;
            return true;
        }
        
        private void LoadAttributes(string attrFile)
        {
            var lines = File.ReadAllLines(attrFile);
            if (lines.Length < 3) return;
            
            // Line 0: count, Line 1: header, Line 2+: data
            foreach (var line in lines.Skip(2))
            {
                var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 41) continue;
                
                string imgName = parts[0];
                var attrs = ATTR_NAMES
                    .Select((name, idx) => (name, idx))
                    .Where(x => x.idx + 1 < parts.Length && int.TryParse(parts[x.idx + 1], out _))
                    .ToDictionary(
                        x => x.name,
                        x => int.Parse(parts[x.idx + 1]) > 0 ? 1f : 0f
                    );
                
                _attributes[imgName] = attrs;
            }
        }
        
        public IEnumerable<FaceSampleInfo> GetBatch(int batchSize)
        {
            // O(1) sequential access into pre-shuffled list
            int found = 0;
            
            while (found < batchSize && _currentIndex < _allImages.Count)
            {
                string file = _allImages[_currentIndex];
                _currentIndex++;
                
                string fileName = Path.GetFileName(file);
                string id = $"celeba_{Path.GetFileNameWithoutExtension(file)}";
                
                if (_processedIds.Contains(id)) continue;
                
                found++;
                yield return BuildSample(file, fileName, id);
            }
        }
        
        private FaceSampleInfo BuildSample(string file, string fileName, string id)
        {
            var sample = new FaceSampleInfo
            {
                Id = id,
                ImagePath = file,
                Source = Name
            };
            
            if (_attributes.TryGetValue(fileName, out var attrs))
            {
                sample.Attributes = attrs;
                sample.IsFemale = attrs.TryGetValue("Male", out float male) && male < 0.5f;
                
                // Estimate age from Young/Gray_Hair attributes
                bool isYoung = attrs.TryGetValue("Young", out float young) && young > 0.5f;
                bool hasGrayHair = attrs.TryGetValue("Gray_Hair", out float gray) && gray > 0.5f;
                
                sample.Age = hasGrayHair ? 55f : isYoung ? 25f : 38f;
            }
            
            return sample;
        }
        
        public void MarkProcessed(string sampleId) => _processedIds.Add(sampleId);
        
        public TargetFace GetNextTarget()
        {
            if (!IsReady || _allImages.Count == 0) return null;
            
            // Try up to 50 times to find a valid (non-blurry) image
            for (int attempts = 0; attempts < 50; attempts++)
            {
                if (_currentIndex >= _allImages.Count)
                {
                    _currentIndex = 0;
                    _allImages = _allImages.OrderBy(_ => _random.Next()).ToList();
                }
                
                string file = _allImages[_currentIndex];
                _currentIndex++;
                
                string fileName = Path.GetFileName(file);
                
                // QUALITY FILTER: Skip blurry images
                if (_attributes.TryGetValue(fileName, out var attrs))
                {
                    if (attrs.TryGetValue("Blurry", out float blurry) && blurry > 0.5f)
                    {
                        continue;  // Skip blurry image
                    }
                }
                
                try
                {
                    string id = $"celeba_{Path.GetFileNameWithoutExtension(file)}";
                    byte[] imageBytes = File.ReadAllBytes(file);
                    
                    return new TargetFace
                    {
                        Id = id,
                        Source = Name,
                        ImageBytes = imageBytes,
                        Landmarks = null
                    };
                }
                catch
                {
                    continue;  // Try next on error
                }
            }
            
            return null;  // Could not find valid image
        }
        
        public void ResetProcessed() 
        { 
            _processedIds.Clear();
            _currentIndex = 0;
            // Re-shuffle for next epoch
            _allImages = _allImages.OrderBy(_ => _random.Next()).ToList();
        }
    }
}
