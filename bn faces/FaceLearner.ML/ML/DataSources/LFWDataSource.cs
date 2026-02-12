using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace FaceLearner.ML.DataSources
{
    /// <summary>
    /// Labeled Faces in the Wild dataset (~13K faces)
    /// Auto-downloads if not present
    /// </summary>
    public class LFWDataSource : IFaceDataSource
    {
        private string _dataDir;
        private List<string> _allImages = new List<string>();
        private HashSet<string> _processedIds = new HashSet<string>();
        private Random _random = new Random();
        private int _currentIndex = 0;
        
        // LFW download URL - Internet Archive mirror (ZIP format, reliable)
        private const string LFW_URL = "https://archive.org/download/lfw-dataset/lfw-dataset.zip";
        private const string LFW_FILENAME = "lfw-dataset.zip";
        
        public string Name => "LFW";
        public string Description => "Labeled Faces in the Wild (13,000 faces)";
        public int TotalCount => _allImages.Count;
        public int ProcessedCount => _processedIds.Count;
        public bool IsReady { get; private set; }
        
        public static event Action<string> OnProgress;
        
        public bool Initialize(string basePath)
        {
            string datasetsPath = Path.Combine(basePath, "datasets");
            if (!Directory.Exists(datasetsPath))
                Directory.CreateDirectory(datasetsPath);
            
            // Try multiple possible locations (order matters - most likely first)
            string[] possiblePaths = new[]
            {
                Path.Combine(basePath, "datasets", "lfw-deepfunneled"),
                Path.Combine(basePath, "datasets", "lfw"),
                Path.Combine(basePath, "datasets", "lfw-funneled"),
                Path.Combine(basePath, "datasets", "lfw_funneled"),
                Path.Combine(basePath, "datasets", "lfw-dataset"),
                Path.Combine(basePath, "datasets", "LFW"),  // Case variation
                Path.Combine(basePath, "datasets", "LFW", "lfw-deepfunneled"),  // Nested
                Path.Combine(basePath, "lfw-deepfunneled"),
                Path.Combine(basePath, "lfw"),
            };
            
            // First try direct paths
            _dataDir = FindLfwDirectory(possiblePaths);
            
            // If not found directly, search recursively (handles nested extractions)
            if (_dataDir == null)
            {
                _dataDir = FindLfwDirectoryRecursive(datasetsPath);
            }
            
            // Still not found - download
            if (_dataDir == null)
            {
                OnProgress?.Invoke("LFW not found, downloading (~111MB)...");
                SubModule.Log("LFW not found in any location, downloading...");
                
                string targetDir = Path.Combine(basePath, "datasets");
                if (DownloadAndExtract(targetDir))
                {
                    // Search again after extraction - search recursively
                    _dataDir = FindLfwDirectoryRecursive(targetDir);
                    
                    if (_dataDir == null)
                    {
                        SubModule.Log($"Could not find LFW data after extraction. Contents of {targetDir}:");
                        foreach (var dir in Directory.GetDirectories(targetDir))
                        {
                            var subDirs = Directory.GetDirectories(dir);
                            SubModule.Log($"  {Path.GetFileName(dir)}: {subDirs.Length} subdirs");
                        }
                    }
                }
            }
            
            if (_dataDir == null || !Directory.Exists(_dataDir))
            {
                SubModule.Log("LFW: No valid directory found after download");
                return false;
            }
            
            // Scan for images
            ScanImages();
            
            IsReady = _allImages.Count > 0;
            if (IsReady)
                SubModule.Log($"LFW: {_allImages.Count} images ready from {Path.GetFileName(_dataDir)}");
            else
                SubModule.Log($"LFW: Directory exists but no images found in {_dataDir}");
            
            return IsReady;
        }
        
        private string FindLfwDirectory(string[] paths)
        {
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    var subDirs = Directory.GetDirectories(path);
                    
                    // LFW has >5000 person folders, but accept 50+ for partial datasets
                    if (subDirs.Length > 50)
                    {
                        bool hasImages = subDirs.Take(5).Any(d => 
                            Directory.GetFiles(d, "*.jpg").Length > 0);
                        
                        if (hasImages)
                        {
                            SubModule.Log($"LFW found at: {path} ({subDirs.Length} persons)");
                            return path;
                        }
                    }
                }
            }
            return null;
        }
        
        private string FindLfwDirectoryRecursive(string basePath)
        {
            if (!Directory.Exists(basePath)) return null;
            
            // First check direct children
            foreach (var dir in Directory.GetDirectories(basePath))
            {
                string dirName = Path.GetFileName(dir).ToLower();
                
                // Skip non-LFW directories
                if (!dirName.Contains("lfw") && dirName != "celeba" && dirName != "utkface")
                {
                    var subDirs = Directory.GetDirectories(dir);
                    
                    // Check if this directory itself contains person folders
                    if (subDirs.Length > 50)
                    {
                        bool hasImages = subDirs.Take(5).Any(d => 
                            Directory.GetFiles(d, "*.jpg").Length > 0);
                        if (hasImages)
                        {
                            SubModule.Log($"Found LFW at: {dir} ({subDirs.Length} persons)");
                            return dir;
                        }
                    }
                }
                
                // Check LFW-named directories
                if (dirName.Contains("lfw"))
                {
                    var subDirs = Directory.GetDirectories(dir);
                    
                    // Direct match (lfw-deepfunneled with person folders)
                    if (subDirs.Length > 50)
                    {
                        bool hasImages = subDirs.Take(5).Any(d => 
                            Directory.GetFiles(d, "*.jpg").Length > 0);
                        if (hasImages)
                        {
                            SubModule.Log($"Found LFW at: {dir} ({subDirs.Length} persons)");
                            return dir;
                        }
                    }
                    
                    // Check one level deeper (nested extraction like LFW/lfw-deepfunneled/)
                    foreach (var subDir in subDirs)
                    {
                        var subSubDirs = Directory.GetDirectories(subDir);
                        if (subSubDirs.Length > 50)
                        {
                            bool hasImages = subSubDirs.Take(5).Any(d => 
                                Directory.GetFiles(d, "*.jpg").Length > 0);
                            if (hasImages)
                            {
                                SubModule.Log($"Found nested LFW at: {subDir} ({subSubDirs.Length} persons)");
                                return subDir;
                            }
                        }
                    }
                }
            }
            
            return null;
        }
        
        private void ScanImages()
        {
            _allImages.Clear();
            
            // LFW structure: lfw/Person_Name/Person_Name_0001.jpg
            foreach (var personDir in Directory.GetDirectories(_dataDir))
            {
                var images = Directory.GetFiles(personDir, "*.jpg")
                    .Concat(Directory.GetFiles(personDir, "*.png"))
                    .Concat(Directory.GetFiles(personDir, "*.jpeg"));
                _allImages.AddRange(images);
            }
            
            // Fallback: flat or recursive
            if (_allImages.Count == 0)
            {
                var images = Directory.GetFiles(_dataDir, "*.jpg", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(_dataDir, "*.png", SearchOption.AllDirectories));
                _allImages.AddRange(images);
            }
            
            // SHUFFLE ONCE at init
            _allImages = _allImages.OrderBy(_ => _random.Next()).ToList();
            _currentIndex = 0;
        }
        
        private bool DownloadAndExtract(string targetDir)
        {
            string zipPath = Path.Combine(targetDir, LFW_FILENAME);
            
            // Check if already downloaded
            if (File.Exists(zipPath))
            {
                SubModule.Log($"Found existing {LFW_FILENAME}, extracting...");
            }
            else
            {
                try
                {
                    // Download
                    using (var client = new WebClient())
                    {
                        OnProgress?.Invoke("Starting LFW download (~111MB)...");
                        SubModule.Log($"Downloading LFW from {LFW_URL}...");
                        client.DownloadFile(LFW_URL, zipPath);
                        SubModule.Log($"Download complete: {new FileInfo(zipPath).Length / 1024 / 1024}MB");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"LFW download error: {ex.Message}");
                    OnProgress?.Invoke($"Download failed: {ex.Message}");
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    return false;
                }
            }
            
            // Extract ZIP
            try
            {
                OnProgress?.Invoke("Extracting LFW...");
                SubModule.Log($"Extracting {zipPath} to {targetDir}...");
                
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    int count = 0;
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;  // Skip directories
                        
                        string destPath = Path.Combine(targetDir, entry.FullName);
                        string destDir = Path.GetDirectoryName(destPath);
                        
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        
                        try
                        {
                            entry.ExtractToFile(destPath, overwrite: true);
                            count++;
                            if (count % 1000 == 0)
                                SubModule.Log($"Extracted {count} files...");
                        }
                        catch { }  // Skip files that fail
                    }
                    SubModule.Log($"Extracted {count} files total");
                }
                
                // Log what folders exist now
                var dirs = Directory.GetDirectories(targetDir);
                SubModule.Log($"Directories after extraction: {string.Join(", ", dirs.Select(Path.GetFileName))}");
                
                // Cleanup ZIP
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                
                OnProgress?.Invoke("LFW extraction complete!");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"LFW extraction error: {ex.Message}");
                OnProgress?.Invoke($"Extraction failed: {ex.Message}");
                return false;
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
                
                string id = $"lfw_{Path.GetFileNameWithoutExtension(file)}";
                
                if (_processedIds.Contains(id)) continue;
                
                // Extract person name from folder
                string personFolder = Path.GetFileName(Path.GetDirectoryName(file));
                string firstName = ExtractFirstName(personFolder);
                
                // Try to infer gender from first name
                float? genderHint = GuessGenderFromName(firstName);
                
                var attributes = new Dictionary<string, float>
                {
                    ["person"] = personFolder.GetHashCode()
                };
                
                // Add gender hint if we have one
                if (genderHint.HasValue)
                {
                    attributes["gender"] = genderHint.Value;
                }
                
                found++;
                yield return new FaceSampleInfo
                {
                    Id = id,
                    ImagePath = file,
                    Source = Name,
                    Attributes = attributes
                };
            }
        }
        
        /// <summary>
        /// Extract first name from LFW folder name (e.g., "John_Stockton" → "John")
        /// </summary>
        private string ExtractFirstName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return "";
            
            // LFW format: First_Last or First_Middle_Last
            int underscoreIdx = folderName.IndexOf('_');
            if (underscoreIdx > 0)
                return folderName.Substring(0, underscoreIdx).ToLower();
            
            return folderName.ToLower();
        }
        
        /// <summary>
        /// Guess gender from first name (0 = male, 1 = female, null = unknown)
        /// Based on common English first names
        /// </summary>
        private float? GuessGenderFromName(string firstName)
        {
            if (string.IsNullOrEmpty(firstName)) return null;
            
            // Common male names
            var maleNames = new HashSet<string>
            {
                "james", "john", "robert", "michael", "william", "david", "richard", "joseph",
                "thomas", "charles", "christopher", "daniel", "matthew", "anthony", "mark",
                "donald", "steven", "paul", "andrew", "joshua", "kenneth", "kevin", "brian",
                "george", "edward", "ronald", "timothy", "jason", "jeffrey", "ryan", "jacob",
                "gary", "nicholas", "eric", "jonathan", "stephen", "larry", "justin", "scott",
                "brandon", "benjamin", "samuel", "raymond", "gregory", "frank", "alexander",
                "patrick", "jack", "dennis", "jerry", "tyler", "aaron", "jose", "adam", "henry",
                "nathan", "douglas", "zachary", "peter", "kyle", "noah", "ethan", "jeremy",
                "walter", "christian", "keith", "roger", "terry", "carl", "sean", "austin",
                "arthur", "lawrence", "jesse", "dylan", "bryan", "joe", "jordan", "billy",
                "bruce", "albert", "willie", "gabriel", "logan", "alan", "ralph", "eugene",
                "russell", "bobby", "harry", "philip", "louis", "barry", "howard", "vincent",
                "colin", "gerald", "craig", "wayne", "tony", "jim", "tom", "bill", "mike", "bob",
                // Additional names found in LFW
                "brook", "brooks", "clint", "colin", "dale", "darren", "derek", "dick", "don",
                "doug", "drew", "earl", "ed", "eddie", "edwin", "eli", "elliot", "elvis",
                "ernest", "ernie", "fred", "freddy", "glen", "glenn", "gordon", "grant", "greg",
                "hal", "hank", "harold", "harvey", "heath", "herman", "hugh", "hugo", "ian",
                "irving", "ivan", "jacques", "jake", "jared", "jay", "jeff", "jerome", "jimmy",
                "joel", "johnny", "jon", "jorge", "juan", "karl", "ken", "kenny", "kirk", "lance",
                "lee", "len", "leo", "leon", "leonard", "leroy", "lester", "lewis", "lloyd",
                "lou", "luke", "luther", "mac", "marcus", "mario", "marvin", "matt", "max",
                "mel", "miguel", "mitch", "morgan", "morris", "murray", "neil", "nelson", "nick",
                "norman", "oliver", "omar", "oscar", "otto", "owen", "pablo", "pat", "pedro",
                "percy", "perry", "pete", "pierre", "prince", "quincy", "rafael", "raj", "randy",
                "ray", "rex", "rick", "ricky", "rob", "rod", "rodney", "roger", "roland", "ron",
                "ross", "roy", "ruben", "rudy", "russ", "saddam", "sal", "sam", "scott", "seth",
                "shane", "shawn", "sid", "simon", "spencer", "stan", "stanley", "steve", "stuart",
                "ted", "theo", "theodore", "todd", "travis", "trevor", "troy", "vernon", "vic",
                "victor", "vince", "virgil", "wade", "warren", "wendell", "wesley", "wilbur",
                "will", "willy", "winston", "woody", "yuri", "zach", "zack"
            };
            
            // Common female names - expanded list
            var femaleNames = new HashSet<string>
            {
                "mary", "patricia", "jennifer", "linda", "elizabeth", "barbara", "susan",
                "jessica", "sarah", "karen", "lisa", "nancy", "betty", "margaret", "sandra",
                "ashley", "kimberly", "emily", "donna", "michelle", "dorothy", "carol",
                "amanda", "melissa", "deborah", "stephanie", "rebecca", "sharon", "laura",
                "cynthia", "kathleen", "amy", "angela", "shirley", "anna", "brenda", "pamela",
                "emma", "nicole", "helen", "samantha", "katherine", "christine", "debra",
                "rachel", "carolyn", "janet", "catherine", "maria", "heather", "diane",
                "ruth", "julie", "olivia", "joyce", "virginia", "victoria", "kelly", "lauren",
                "christina", "joan", "evelyn", "judith", "megan", "andrea", "cheryl", "hannah",
                "jacqueline", "martha", "gloria", "teresa", "ann", "sara", "madison", "frances",
                "kathryn", "janice", "jean", "abigail", "alice", "judy", "sophia", "grace",
                "denise", "amber", "doris", "marilyn", "danielle", "beverly", "isabella",
                "theresa", "diana", "natalie", "brittany", "charlotte", "marie", "kayla", "alexis",
                // Additional celebrity and common names
                "tyra", "oprah", "beyonce", "rihanna", "shakira", "cher", "madonna", "adele",
                "taylor", "britney", "whitney", "mariah", "janet", "tina", "aretha", "diana",
                "serena", "venus", "naomi", "gisele", "heidi", "claudia", "cindy", "kate",
                "angelina", "scarlett", "jennifer", "julia", "meryl", "cate", "nicole", "halle",
                "gwyneth", "reese", "charlize", "sandra", "anne", "natalie", "emma", "mila",
                "penelope", "salma", "sofia", "cameron", "drew", "demi", "meg", "kirsten",
                "hilary", "lindsay", "paris", "britney", "christina", "jessica", "mandy",
                "avril", "gwen", "pink", "fergie", "ciara", "ashanti", "aaliyah", "brandy",
                "monica", "toni", "mary", "faith", "shania", "carrie", "dolly", "loretta",
                "reba", "martina", "leann", "trisha", "wynonna", "tammy", "patsy", "emmylou",
                "alicia", "norah", "diana", "celine", "barbra", "etta", "billie", "ella",
                "nina", "sarah", "nelly", "dido", "enya", "bjork", "sade", "annie", "kate",
                "condi", "condoleezza", "hillary", "madeline", "janet", "nancy", "ruth",
                "elena", "sonia", "sandra", "harriet", "rosa", "coretta", "maya", "toni",
                "oprah", "ellen", "whoopi", "rosie", "joy", "barbara", "diane", "katie",
                "meredith", "robin", "gayle", "wendy", "rachael", "martha", "paula", "giada",
                "ina", "sandra", "sunny", "anne", "mary", "joanna", "padma", "daphne",
                // International names
                "ingrid", "greta", "astrid", "brigitte", "claudette", "colette", "monique",
                "simone", "juliette", "amelie", "chloe", "margot", "marion", "audrey", "lea",
                "carmen", "pilar", "penelope", "paz", "javier", "elena", "cristina", "leticia",
                "ana", "lucia", "maria", "rosa", "isabel", "ines", "fatima", "aisha", "amira",
                "layla", "nadia", "yasmin", "zara", "priya", "anjali", "deepika", "aishwarya"
            };
            
            if (maleNames.Contains(firstName))
                return 0f;  // Male
            
            if (femaleNames.Contains(firstName))
                return 1f;  // Female
            
            return null;  // Unknown - let AI decide
        }
        
        public void MarkProcessed(string sampleId) => _processedIds.Add(sampleId);
        
        public TargetFace GetNextTarget()
        {
            if (!IsReady || _allImages.Count == 0) return null;
            
            // Try up to 20 times to find a valid image (LFW is generally high quality)
            for (int attempts = 0; attempts < 20; attempts++)
            {
                // Wrap around if needed
                if (_currentIndex >= _allImages.Count)
                {
                    _currentIndex = 0;
                    // Shuffle for variety
                    for (int i = _allImages.Count - 1; i > 0; i--)
                    {
                        int j = _random.Next(i + 1);
                        var temp = _allImages[i];
                        _allImages[i] = _allImages[j];
                        _allImages[j] = temp;
                    }
                }
                
                string file = _allImages[_currentIndex];
                _currentIndex++;
                
                try
                {
                    // Basic file size check - skip very small files (corrupt/placeholder)
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length < 1000)  // Less than 1KB = probably corrupt
                    {
                        continue;
                    }
                    
                    string id = $"lfw_{Path.GetFileNameWithoutExtension(file)}";
                    byte[] imageBytes = File.ReadAllBytes(file);
                    
                    return new TargetFace
                    {
                        Id = id,
                        Source = Name,
                        ImageBytes = imageBytes,
                        Landmarks = null  // Will be detected by orchestrator
                    };
                }
                catch
                {
                    continue;  // Try next on error
                }
            }
            
            return null;
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
