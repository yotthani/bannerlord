using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace FaceLearner.ML.DataSources
{
    /// <summary>
    /// UTKFace dataset (~20K faces with age, gender, ethnicity labels)
    /// Auto-downloads from Internet Archive if not present
    /// </summary>
    public class UTKFaceDataSource : IFaceDataSource
    {
        private string _dataDir;
        private List<string> _allImages = new List<string>();
        private HashSet<string> _processedIds = new HashSet<string>();
        private Random _random = new Random();
        private int _currentIndex = 0;
        
        // UTKFace download URL from Internet Archive (reliable, fast)
        private const string UTK_URL = "https://archive.org/download/UTKFace/UTKFace.tar.gz";
        private const string UTK_FILENAME = "UTKFace.tar.gz";
        
        public string Name => "UTKFace";
        public string Description => "UTKFace Dataset (20,000+ faces with age/gender/ethnicity)";
        public int TotalCount => _allImages.Count;
        public int ProcessedCount => _processedIds.Count;
        public bool IsReady { get; private set; }
        
        public static event Action<string> OnProgress;
        
        public bool Initialize(string basePath)
        {
            string datasetsPath = Path.Combine(basePath, "datasets");
            if (!Directory.Exists(datasetsPath))
                Directory.CreateDirectory(datasetsPath);
            
            // Try multiple possible locations
            string[] possiblePaths = new[]
            {
                Path.Combine(basePath, "datasets", "UTKFace"),
                Path.Combine(basePath, "datasets", "utkface"),
                Path.Combine(basePath, "datasets", "utk_face"),
                Path.Combine(basePath, "datasets", "UTKFace", "UTKFace"), // Sometimes nested
                Path.Combine(basePath, "UTKFace"),
            };
            
            _dataDir = null;
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    // Check for actual jpg files
                    var jpgFiles = Directory.GetFiles(path, "*.jpg", SearchOption.TopDirectoryOnly);
                    if (jpgFiles.Length > 100) // UTKFace should have thousands
                    {
                        _dataDir = path;
                        SubModule.Log($"UTKFace found at: {path} ({jpgFiles.Length} files)");
                        break;
                    }
                }
            }
            
            // Not found - try to download
            if (_dataDir == null)
            {
                OnProgress?.Invoke("UTKFace not found, downloading (~102MB)...");
                SubModule.Log("UTKFace not found, downloading...");
                
                string targetDir = Path.Combine(basePath, "datasets");
                if (DownloadAndExtract(targetDir))
                {
                    // Find what was extracted
                    foreach (var path in possiblePaths)
                    {
                        if (Directory.Exists(path) && Directory.GetFiles(path, "*.jpg").Length > 100)
                        {
                            _dataDir = path;
                            SubModule.Log($"UTKFace extracted to: {path}");
                            break;
                        }
                    }
                }
            }
            
            if (_dataDir == null || !Directory.Exists(_dataDir))
            {
                SubModule.Log("UTKFace: No valid directory found");
                return false;
            }
            
            // Scan for images
            ScanImages();
            
            IsReady = _allImages.Count > 0;
            if (IsReady)
                SubModule.Log($"UTKFace: {_allImages.Count} images ready");
            return IsReady;
        }
        
        private void ScanImages()
        {
            _allImages.Clear();
            
            // UTKFace structure: UTKFace/[age]_[gender]_[race]_[date].jpg (flat)
            // SHUFFLE ONCE at init for diverse age/gender sampling
            _allImages = Directory.GetFiles(_dataDir, "*.jpg", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(_dataDir, "*.png", SearchOption.AllDirectories))
                .Where(f => !f.EndsWith(".chip.gz"))
                .OrderBy(_ => _random.Next())  // Shuffle once
                .ToList();
            _currentIndex = 0;
        }
        
        private bool DownloadAndExtract(string targetDir)
        {
            string tgzPath = Path.Combine(targetDir, UTK_FILENAME);
            
            try
            {
                // Download
                using (var client = new WebClient())
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        if (e.ProgressPercentage % 10 == 0)
                            OnProgress?.Invoke($"Downloading UTKFace: {e.ProgressPercentage}%");
                    };
                    
                    OnProgress?.Invoke("Starting UTKFace download...");
                    SubModule.Log("Starting UTKFace download from Internet Archive...");
                    client.DownloadFile(UTK_URL, tgzPath);
                }
                
                OnProgress?.Invoke("Extracting UTKFace...");
                SubModule.Log("Extracting UTKFace...");
                
                // Extract .tar.gz
                ExtractTgz(tgzPath, targetDir);
                
                // Cleanup
                if (File.Exists(tgzPath))
                    File.Delete(tgzPath);
                
                OnProgress?.Invoke("UTKFace ready!");
                SubModule.Log("UTKFace extraction complete");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"UTKFace download error: {ex.Message}");
                OnProgress?.Invoke($"Download failed: {ex.Message}");
                return false;
            }
        }
        
        private void ExtractTgz(string tgzPath, string targetDir)
        {
            string tarPath = tgzPath.Replace(".tar.gz", ".tar").Replace(".tgz", ".tar");
            
            // Decompress gzip
            using (var inStream = File.OpenRead(tgzPath))
            using (var gzip = new GZipStream(inStream, CompressionMode.Decompress))
            using (var outStream = File.Create(tarPath))
            {
                gzip.CopyTo(outStream);
            }
            
            // Extract tar
            ExtractTar(tarPath, targetDir);
            
            // Cleanup tar
            if (File.Exists(tarPath))
                File.Delete(tarPath);
        }
        
        private void ExtractTar(string tarPath, string targetDir)
        {
            using (var stream = File.OpenRead(tarPath))
            {
                byte[] header = new byte[512];
                while (stream.Read(header, 0, 512) == 512)
                {
                    if (header[0] == 0) break;
                    
                    string name = System.Text.Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0');
                    if (string.IsNullOrEmpty(name)) break;
                    
                    string sizeStr = System.Text.Encoding.ASCII.GetString(header, 124, 12).Trim().TrimEnd('\0');
                    long size = 0;
                    if (!string.IsNullOrEmpty(sizeStr))
                    {
                        try { size = Convert.ToInt64(sizeStr, 8); } catch { }
                    }
                    
                    char type = (char)header[156];
                    string fullPath = Path.Combine(targetDir, name.Replace('/', Path.DirectorySeparatorChar));
                    
                    if (type == '5' || name.EndsWith("/"))
                    {
                        if (!Directory.Exists(fullPath))
                            Directory.CreateDirectory(fullPath);
                    }
                    else if (type == '0' || type == '\0')
                    {
                        string dir = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        
                        if (size > 0)
                        {
                            byte[] content = new byte[size];
                            stream.Read(content, 0, (int)size);
                            File.WriteAllBytes(fullPath, content);
                        }
                    }
                    
                    long remainder = size % 512;
                    if (remainder > 0)
                        stream.Seek(512 - remainder, SeekOrigin.Current);
                }
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
                
                string fileName = Path.GetFileNameWithoutExtension(file);
                string id = $"utk_{fileName}";
                
                if (_processedIds.Contains(id)) continue;
                
                found++;
                yield return ParseUtkSample(file, fileName, id);
            }
        }
        
        private FaceSampleInfo ParseUtkSample(string file, string fileName, string id)
        {
            var sample = new FaceSampleInfo
            {
                Id = id,
                ImagePath = file,
                Source = Name,
                Attributes = new Dictionary<string, float>()
            };
            
            // Parse filename: [age]_[gender]_[race]_[date].jpg
            var parts = fileName.Split('_');
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[0], out int age))
                {
                    sample.Age = age;
                    sample.Attributes["age"] = age;
                }
                if (int.TryParse(parts[1], out int gender))
                {
                    sample.IsFemale = gender == 1; // 0=male, 1=female
                    sample.Attributes["gender"] = gender;
                }
                if (int.TryParse(parts[2], out int race))
                    sample.Attributes["race"] = race; // 0=White, 1=Black, 2=Asian, 3=Indian, 4=Other
            }
            
            return sample;
        }
        
        public void MarkProcessed(string sampleId) => _processedIds.Add(sampleId);
        
        public TargetFace GetNextTarget()
        {
            if (!IsReady || _allImages.Count == 0) return null;
            
            // Try up to 100 times to find a valid adult image
            for (int attempts = 0; attempts < 100; attempts++)
            {
                if (_currentIndex >= _allImages.Count)
                {
                    _currentIndex = 0;
                    _allImages = _allImages.OrderBy(_ => _random.Next()).ToList();
                }
                
                string file = _allImages[_currentIndex];
                _currentIndex++;
                
                // Parse age from filename: [age]_[gender]_[race]_[date].jpg
                string fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('_');
                if (parts.Length >= 1 && int.TryParse(parts[0], out int age))
                {
                    // FILTER: Skip children (age < 18) and very old (age > 80)
                    if (age < 18)
                    {
                        continue;  // Skip, try next image
                    }
                    if (age > 80)
                    {
                        continue;  // Skip very old faces (often low quality)
                    }
                }
                
                try
                {
                    string id = $"utk_{fileName}";
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
            
            return null;  // Could not find valid image after 100 attempts
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
