using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FaceLearner.Core;

namespace FaceLearner.ML.Core
{
    /// <summary>
    /// Automated morph tester that discovers which morph indices affect which facial regions.
    /// Uses full 468 FaceMesh landmarks for accurate region classification.
    /// Tests each morph by setting it to -1.0 and 5.0, capturing landmarks, and measuring differences.
    /// </summary>
    public class MorphTester
    {
        private readonly FaceController _faceController;
        private readonly LandmarkDetector _landmarkDetector;
        private readonly string _outputDir;
        
        // Test state
        private bool _isRunning;
        private int _currentMorphIndex;
        private TestPhase _currentPhase;
        private float[] _neutralLandmarks;
        private float[] _lowLandmarks;
        private float[] _highLandmarks;
        private float[] _savedMorphs;
        private int _waitFrames;
        
        // Results
        private List<MorphTestResult> _results = new List<MorphTestResult>();
        
        private enum TestPhase
        {
            Idle,
            SetNeutral,
            CaptureNeutral,
            SetLow,
            CaptureLow,
            SetHigh,
            CaptureHigh,
            Analyze,
            NextMorph,
            Complete
        }
        
        public bool IsRunning => _isRunning;
        public int CurrentMorphIndex => _currentMorphIndex;
        public int TotalMorphs => 62;
        public string Status => $"Testing morph {_currentMorphIndex}/62 - {_currentPhase}";
        
        // ═══════════════════════════════════════════════════════════════
        // FaceMesh 468 Landmark Region Definitions (MediaPipe)
        // ═══════════════════════════════════════════════════════════════
        
        // Face silhouette/oval (outer contour)
        private static readonly HashSet<int> SilhouetteLandmarks = new HashSet<int> {
            10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 
            400, 377, 152, 148, 176, 149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 
            54, 103, 67, 109, 108, 151, 337, 299, 333, 298, 301, 368, 264, 447, 366, 401,
            435, 416, 434, 430, 431, 262, 428, 199, 208, 32, 211, 210, 212, 202, 204, 194,
            // Lower face/chin area
            175, 171, 140, 170, 169, 135, 138, 215, 177, 137, 227, 34, 143, 111, 117, 118,
            119, 120, 121, 128, 235, 236, 237, 238, 239, 240, 241, 242, 243, 244, 245
        };
        
        // Forehead region
        private static readonly HashSet<int> ForeheadLandmarks = new HashSet<int> {
            10, 151, 108, 69, 104, 68, 71, 139, 70, 63, 105, 66, 107, 9, 336, 296, 334,
            293, 300, 383, 301, 298, 333, 299, 337, 338, 297, 332, 284
        };
        
        // Left eyebrow
        private static readonly HashSet<int> LeftEyebrowLandmarks = new HashSet<int> {
            70, 63, 105, 66, 107, 55, 65, 52, 53, 46, 124, 35, 111, 117, 118, 119, 120, 121
        };
        
        // Right eyebrow
        private static readonly HashSet<int> RightEyebrowLandmarks = new HashSet<int> {
            300, 293, 334, 296, 336, 285, 295, 282, 283, 276, 353, 265, 340, 346, 347, 348, 349, 350
        };
        
        // Left eye
        private static readonly HashSet<int> LeftEyeLandmarks = new HashSet<int> {
            33, 7, 163, 144, 145, 153, 154, 155, 133, 173, 157, 158, 159, 160, 161, 246,
            130, 25, 110, 24, 23, 22, 26, 112, 243, 190, 56, 28, 27, 29, 30, 247
        };
        
        // Right eye
        private static readonly HashSet<int> RightEyeLandmarks = new HashSet<int> {
            362, 382, 381, 380, 374, 373, 390, 249, 263, 466, 388, 387, 386, 385, 384, 398,
            359, 255, 339, 254, 253, 252, 256, 341, 463, 414, 286, 258, 257, 259, 260, 467
        };
        
        // Nose
        private static readonly HashSet<int> NoseLandmarks = new HashSet<int> {
            1, 2, 3, 4, 5, 6, 168, 197, 195, 5, 4, 45, 220, 115, 48, 64, 98, 97, 
            326, 327, 294, 278, 344, 440, 275, 19, 94, 141, 125, 241, 238, 44, 1, 
            274, 457, 438, 439, 455, 460, 328, 327, 326, 2, 98, 97, 331, 279, 360, 
            363, 281, 5, 4, 51, 45, 275, 440, 344, 278, 294, 439, 438, 457, 274
        };
        
        // Lips/Mouth outer
        private static readonly HashSet<int> LipsLandmarks = new HashSet<int> {
            0, 267, 269, 270, 409, 291, 375, 321, 405, 314, 17, 84, 181, 91, 146, 61,
            185, 40, 39, 37, 87, 178, 88, 95, 78, 191, 80, 81, 82, 13, 312, 311, 310,
            415, 308, 324, 318, 402, 317, 14, 
            // Inner lips
            78, 95, 88, 178, 87, 14, 317, 402, 318, 324, 308, 415, 310, 311, 312, 13,
            82, 81, 80, 191
        };
        
        // Cheeks (left)
        private static readonly HashSet<int> LeftCheekLandmarks = new HashSet<int> {
            116, 123, 147, 187, 207, 213, 192, 214, 210, 211, 32, 208, 199, 129, 203,
            206, 216, 212, 202, 204, 194, 201, 200, 428, 262, 431, 430, 434, 416, 435
        };
        
        // Cheeks (right)
        private static readonly HashSet<int> RightCheekLandmarks = new HashSet<int> {
            345, 352, 376, 411, 427, 433, 412, 343, 340, 265, 353, 372, 383, 300, 368,
            264, 447, 366, 401, 436, 426, 423, 422, 432, 421, 418, 424, 335
        };
        
        // Jaw/Chin
        private static readonly HashSet<int> JawChinLandmarks = new HashSet<int> {
            152, 148, 176, 149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 54, 103,
            67, 109, 10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365,
            379, 378, 400, 377, 175, 171, 140, 170, 169, 135, 138, 215, 177, 137,
            // Chin specific
            199, 208, 32, 211, 210, 212, 202, 204, 194, 201, 200, 175, 152
        };
        
        public MorphTester(FaceController faceController, LandmarkDetector landmarkDetector, string outputDir)
        {
            _faceController = faceController;
            _landmarkDetector = landmarkDetector;
            _outputDir = outputDir;
            
            Directory.CreateDirectory(outputDir);
        }
        
        /// <summary>
        /// Start the automated morph test
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            SubModule.Log("[MorphTester] Starting automated morph test (468 FaceMesh landmarks)...");
            
            // Save current morphs
            _savedMorphs = _faceController.GetAllMorphs();
            
            _results.Clear();
            _currentMorphIndex = 0;
            _currentPhase = TestPhase.SetNeutral;
            _isRunning = true;
            _waitFrames = 0;
        }
        
        /// <summary>
        /// Stop the test
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            
            SubModule.Log("[MorphTester] Stopping...");
            
            // Restore original morphs
            if (_savedMorphs != null)
            {
                _faceController.SetAllMorphs(_savedMorphs);
            }
            
            _isRunning = false;
            _currentPhase = TestPhase.Idle;
            
            // Save results so far
            SaveResults();
        }
        
        /// <summary>
        /// Called each frame during testing. Returns true if morphs were changed (need refresh).
        /// </summary>
        public bool Tick()
        {
            if (!_isRunning) return false;
            
            // Wait frames for rendering to settle
            if (_waitFrames > 0)
            {
                _waitFrames--;
                return false;
            }
            
            switch (_currentPhase)
            {
                case TestPhase.SetNeutral:
                    _faceController.SetAllMorphs(0.5f);
                    _currentPhase = TestPhase.CaptureNeutral;
                    _waitFrames = 3;
                    return true;
                    
                case TestPhase.SetLow:
                    _faceController.SetAllMorphs(0.5f);
                    _faceController.SetMorph(_currentMorphIndex, -1.0f);
                    _currentPhase = TestPhase.CaptureLow;
                    _waitFrames = 3;
                    return true;
                    
                case TestPhase.SetHigh:
                    _faceController.SetAllMorphs(0.5f);
                    _faceController.SetMorph(_currentMorphIndex, 5.0f);
                    _currentPhase = TestPhase.CaptureHigh;
                    _waitFrames = 3;
                    return true;
                    
                case TestPhase.Analyze:
                    AnalyzeCurrentMorph();
                    _currentPhase = TestPhase.NextMorph;
                    return false;
                    
                case TestPhase.NextMorph:
                    _currentMorphIndex++;
                    if (_currentMorphIndex >= 62)
                    {
                        _currentPhase = TestPhase.Complete;
                        OnComplete();
                    }
                    else
                    {
                        _currentPhase = TestPhase.SetLow;
                        SubModule.Log($"[MorphTester] Testing morph {_currentMorphIndex}...");
                    }
                    return false;
                    
                case TestPhase.Complete:
                    _isRunning = false;
                    return false;
            }
            
            return false;
        }
        
        /// <summary>
        /// Called when a screenshot is captured. Extracts landmarks for current phase.
        /// Uses full 468 FaceMesh landmarks (936 floats).
        /// </summary>
        public void OnScreenshotCaptured(string imagePath)
        {
            if (!_isRunning) return;
            
            try
            {
                var landmarks = _landmarkDetector.DetectLandmarks(imagePath);
                
                // Full 468 FaceMesh landmarks used directly
                // landmarks should be 936 floats (468 landmarks * 2 coords)
                
                switch (_currentPhase)
                {
                    case TestPhase.CaptureNeutral:
                        _neutralLandmarks = landmarks;
                        _currentPhase = TestPhase.SetLow;
                        int numLandmarks = landmarks?.Length / 2 ?? 0;
                        SubModule.Log($"[MorphTester] Captured neutral: {numLandmarks} landmarks");
                        break;
                        
                    case TestPhase.CaptureLow:
                        _lowLandmarks = landmarks;
                        _currentPhase = TestPhase.SetHigh;
                        break;
                        
                    case TestPhase.CaptureHigh:
                        _highLandmarks = landmarks;
                        _currentPhase = TestPhase.Analyze;
                        break;
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"[MorphTester] Landmark detection error: {ex.Message}");
            }
        }
        
        private void AnalyzeCurrentMorph()
        {
            if (_lowLandmarks == null || _highLandmarks == null)
            {
                SubModule.Log($"[MorphTester] Morph {_currentMorphIndex}: No landmarks detected!");
                _results.Add(new MorphTestResult
                {
                    MorphIndex = _currentMorphIndex,
                    TotalMovement = 0,
                    AffectedRegions = new Dictionary<string, float>(),
                    TopLandmarks = new List<(int, float)>()
                });
                return;
            }
            
            // Calculate movement per landmark (468 landmarks)
            var movements = new List<(int landmarkIndex, float movement)>();
            int numLandmarks = Math.Min(_lowLandmarks.Length, _highLandmarks.Length) / 2;
            
            for (int i = 0; i < numLandmarks; i++)
            {
                float dx = _highLandmarks[i * 2] - _lowLandmarks[i * 2];
                float dy = _highLandmarks[i * 2 + 1] - _lowLandmarks[i * 2 + 1];
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                movements.Add((i, dist));
            }
            
            // Sort by movement
            movements.Sort((a, b) => b.movement.CompareTo(a.movement));
            
            // Total movement
            float totalMovement = movements.Sum(m => m.movement);
            
            // Calculate per-region movement totals
            var regionMovements = CalculateRegionMovements(movements);
            
            // Get primary region(s)
            string primaryRegion = GetPrimaryRegion(regionMovements);
            
            var result = new MorphTestResult
            {
                MorphIndex = _currentMorphIndex,
                TotalMovement = totalMovement,
                AffectedRegions = regionMovements,
                PrimaryRegion = primaryRegion,
                TopLandmarks = movements.Take(10).ToList()
            };
            
            _results.Add(result);
            
            // Log result
            string regionStr = string.Join(", ", regionMovements.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key}:{kv.Value:F2}"));
            SubModule.Log($"[MorphTester] Morph[{_currentMorphIndex}]: total={totalMovement:F2}, primary={primaryRegion}, regions=[{regionStr}]");
        }
        
        /// <summary>
        /// Calculate movement totals per facial region using 468 FaceMesh landmarks
        /// </summary>
        private Dictionary<string, float> CalculateRegionMovements(List<(int landmarkIndex, float movement)> movements)
        {
            var regions = new Dictionary<string, float>
            {
                { "FOREHEAD", 0f },
                { "L_EYEBROW", 0f },
                { "R_EYEBROW", 0f },
                { "L_EYE", 0f },
                { "R_EYE", 0f },
                { "NOSE", 0f },
                { "L_CHEEK", 0f },
                { "R_CHEEK", 0f },
                { "LIPS", 0f },
                { "JAW_CHIN", 0f },
                { "SILHOUETTE", 0f }
            };
            
            foreach (var (idx, movement) in movements)
            {
                // A landmark can belong to multiple regions, add to all matching
                if (ForeheadLandmarks.Contains(idx)) regions["FOREHEAD"] += movement;
                if (LeftEyebrowLandmarks.Contains(idx)) regions["L_EYEBROW"] += movement;
                if (RightEyebrowLandmarks.Contains(idx)) regions["R_EYEBROW"] += movement;
                if (LeftEyeLandmarks.Contains(idx)) regions["L_EYE"] += movement;
                if (RightEyeLandmarks.Contains(idx)) regions["R_EYE"] += movement;
                if (NoseLandmarks.Contains(idx)) regions["NOSE"] += movement;
                if (LeftCheekLandmarks.Contains(idx)) regions["L_CHEEK"] += movement;
                if (RightCheekLandmarks.Contains(idx)) regions["R_CHEEK"] += movement;
                if (LipsLandmarks.Contains(idx)) regions["LIPS"] += movement;
                if (JawChinLandmarks.Contains(idx)) regions["JAW_CHIN"] += movement;
                if (SilhouetteLandmarks.Contains(idx)) regions["SILHOUETTE"] += movement;
            }
            
            return regions;
        }
        
        /// <summary>
        /// Determine primary affected region(s).
        /// SILHOUETTE is de-prioritized because it moves with almost every morph change.
        /// We want to know which SPECIFIC facial feature is most affected.
        /// </summary>
        private string GetPrimaryRegion(Dictionary<string, float> regionMovements)
        {
            // Combine symmetric regions
            var combined = new Dictionary<string, float>
            {
                { "FOREHEAD", regionMovements["FOREHEAD"] },
                { "EYEBROWS", regionMovements["L_EYEBROW"] + regionMovements["R_EYEBROW"] },
                { "EYES", regionMovements["L_EYE"] + regionMovements["R_EYE"] },
                { "NOSE", regionMovements["NOSE"] },
                { "CHEEKS", regionMovements["L_CHEEK"] + regionMovements["R_CHEEK"] },
                { "LIPS", regionMovements["LIPS"] },
                { "JAW_CHIN", regionMovements["JAW_CHIN"] },
                { "SILHOUETTE", regionMovements["SILHOUETTE"] }
            };
            
            // Sort all regions by value
            var sorted = combined.OrderByDescending(kv => kv.Value).ToList();
            
            if (sorted[0].Value == 0) return "NONE";
            
            // Get non-SILHOUETTE regions only for primary classification
            var nonSilhouette = sorted.Where(kv => kv.Key != "SILHOUETTE").ToList();
            
            if (nonSilhouette.Count == 0 || nonSilhouette[0].Value == 0)
            {
                // Only SILHOUETTE has movement - rare but possible
                return "SILHOUETTE";
            }
            
            string primary = nonSilhouette[0].Key;
            float primaryValue = nonSilhouette[0].Value;
            
            // Check if second non-SILHOUETTE region is close (within 50%)
            if (nonSilhouette.Count > 1 && nonSilhouette[1].Value > primaryValue * 0.5f)
            {
                return $"{primary}+{nonSilhouette[1].Key}";
            }
            
            return primary;
        }
        
        private void OnComplete()
        {
            SubModule.Log("[MorphTester] Test complete!");
            
            // Restore original morphs
            if (_savedMorphs != null)
            {
                _faceController.SetAllMorphs(_savedMorphs);
            }
            
            SaveResults();
        }
        
        private void SaveResults()
        {
            if (_results.Count == 0) return;
            
            try
            {
                string reportPath = Path.Combine(_outputDir, "morph_test_report.txt");
                var sb = new StringBuilder();
                
                sb.AppendLine("=== BANNERLORD MORPH TEST REPORT ===");
                sb.AppendLine($"Generated: {DateTime.Now}");
                sb.AppendLine($"Morphs tested: {_results.Count}");
                sb.AppendLine($"Landmarks: 468 FaceMesh (full precision)");
                sb.AppendLine($"Test range: -1.0 to 5.0");
                sb.AppendLine();
                
                // RANKING by impact
                sb.AppendLine("=== RANKING BY IMPACT ===");
                sb.AppendLine("(Higher = more visible change)");
                sb.AppendLine();
                var ranked = _results.OrderByDescending(r => r.TotalMovement).ToList();
                int rank = 1;
                foreach (var r in ranked)
                {
                    string bar = new string('█', Math.Min(20, (int)(r.TotalMovement * 10)));
                    sb.AppendLine($"#{rank++,2} Morph[{r.MorphIndex,2}]: {r.TotalMovement:F2} {bar,-20} [{r.PrimaryRegion}]");
                }
                
                // Group by primary region
                sb.AppendLine();
                sb.AppendLine("=== BY PRIMARY REGION ===");
                var byRegion = _results.GroupBy(r => r.PrimaryRegion.Split('+')[0]).OrderBy(g => g.Key);
                foreach (var group in byRegion)
                {
                    var morphList = string.Join(", ", group.OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex));
                    sb.AppendLine($"\n{group.Key}: [{morphList}]");
                }
                
                // Detailed region breakdown for each morph
                sb.AppendLine();
                sb.AppendLine("=== DETAILED REGION BREAKDOWN ===");
                foreach (var r in ranked.Take(20))
                {
                    sb.AppendLine($"\nMorph[{r.MorphIndex}] (total={r.TotalMovement:F2}):");
                    var sortedRegions = r.AffectedRegions.OrderByDescending(kv => kv.Value).Where(kv => kv.Value > 0.01f);
                    foreach (var kv in sortedRegions)
                    {
                        string bar = new string('▓', Math.Min(15, (int)(kv.Value * 10)));
                        sb.AppendLine($"  {kv.Key,-12}: {kv.Value:F3} {bar}");
                    }
                }
                
                // Suggested MorphGroups
                sb.AppendLine();
                sb.AppendLine("=== SUGGESTED MORPHGROUPS.cs ===");
                sb.AppendLine("// Based on 468-landmark FaceMesh analysis");
                
                var foreheadMorphs = _results.Where(r => r.PrimaryRegion.Contains("FOREHEAD")).OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex);
                var eyebrowMorphs = _results.Where(r => r.PrimaryRegion.Contains("EYEBROW")).OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex);
                var eyesMorphs = _results.Where(r => r.PrimaryRegion.Contains("EYE") && !r.PrimaryRegion.Contains("BROW")).OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex);
                var noseMorphs = _results.Where(r => r.PrimaryRegion.Contains("NOSE")).OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex);
                var cheekMorphs = _results.Where(r => r.PrimaryRegion.Contains("CHEEK")).OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex);
                var lipsMorphs = _results.Where(r => r.PrimaryRegion.Contains("LIPS")).OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex);
                var jawChinMorphs = _results.Where(r => r.PrimaryRegion.Contains("JAW") || r.PrimaryRegion.Contains("CHIN")).OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex);
                var silhouetteMorphs = _results.Where(r => r.PrimaryRegion.Contains("SILHOUETTE")).OrderByDescending(r => r.TotalMovement).Select(r => r.MorphIndex);
                
                sb.AppendLine($"public static readonly int[] Forehead_TESTED = {{ {string.Join(", ", foreheadMorphs)} }};");
                sb.AppendLine($"public static readonly int[] Eyebrows_TESTED = {{ {string.Join(", ", eyebrowMorphs)} }};");
                sb.AppendLine($"public static readonly int[] Eyes_TESTED = {{ {string.Join(", ", eyesMorphs)} }};");
                sb.AppendLine($"public static readonly int[] Nose_TESTED = {{ {string.Join(", ", noseMorphs)} }};");
                sb.AppendLine($"public static readonly int[] Cheeks_TESTED = {{ {string.Join(", ", cheekMorphs)} }};");
                sb.AppendLine($"public static readonly int[] Lips_TESTED = {{ {string.Join(", ", lipsMorphs)} }};");
                sb.AppendLine($"public static readonly int[] JawChin_TESTED = {{ {string.Join(", ", jawChinMorphs)} }};");
                sb.AppendLine($"public static readonly int[] Silhouette_TESTED = {{ {string.Join(", ", silhouetteMorphs)} }};");
                
                // HIGH IMPACT morphs (top 20)
                sb.AppendLine();
                sb.AppendLine("// High impact morphs (prioritize these in learning)");
                var highImpact = ranked.Take(20).Select(r => r.MorphIndex);
                sb.AppendLine($"public static readonly int[] HighImpact = {{ {string.Join(", ", highImpact)} }};");
                
                // LOW IMPACT morphs (bottom 20)
                var lowImpact = ranked.Skip(_results.Count - 20).Select(r => r.MorphIndex);
                sb.AppendLine($"public static readonly int[] LowImpact = {{ {string.Join(", ", lowImpact)} }};");
                
                File.WriteAllText(reportPath, sb.ToString());
                SubModule.Log($"[MorphTester] Report saved to: {reportPath}");
                
                // Also save as CSV with region breakdown
                string csvPath = Path.Combine(_outputDir, "morph_test_results.csv");
                var csvSb = new StringBuilder();
                csvSb.AppendLine("MorphIndex,PrimaryRegion,TotalMovement,Rank,Forehead,Eyebrows,Eyes,Nose,Cheeks,Lips,JawChin,Silhouette");
                int csvRank = 1;
                foreach (var r in ranked)
                {
                    // All keys exist because we initialize them in CalculateRegionMovements
                    float eyebrows = r.AffectedRegions["L_EYEBROW"] + r.AffectedRegions["R_EYEBROW"];
                    float eyes = r.AffectedRegions["L_EYE"] + r.AffectedRegions["R_EYE"];
                    float cheeks = r.AffectedRegions["L_CHEEK"] + r.AffectedRegions["R_CHEEK"];
                    
                    csvSb.AppendLine($"{r.MorphIndex},{r.PrimaryRegion},{r.TotalMovement:F3},{csvRank++}," +
                        $"{r.AffectedRegions["FOREHEAD"]:F3}," +
                        $"{eyebrows:F3}," +
                        $"{eyes:F3}," +
                        $"{r.AffectedRegions["NOSE"]:F3}," +
                        $"{cheeks:F3}," +
                        $"{r.AffectedRegions["LIPS"]:F3}," +
                        $"{r.AffectedRegions["JAW_CHIN"]:F3}," +
                        $"{r.AffectedRegions["SILHOUETTE"]:F3}");
                }
                File.WriteAllText(csvPath, csvSb.ToString());
                SubModule.Log($"[MorphTester] CSV saved to: {csvPath}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"[MorphTester] Error saving results: {ex.Message}");
            }
        }
        
        private class MorphTestResult
        {
            public int MorphIndex { get; set; }
            public float TotalMovement { get; set; }
            public string PrimaryRegion { get; set; }
            public Dictionary<string, float> AffectedRegions { get; set; }
            public List<(int, float)> TopLandmarks { get; set; }
        }
    }
}
