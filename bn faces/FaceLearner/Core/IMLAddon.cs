using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.Core
{
    /// <summary>
    /// Complete result of face generation from photo
    /// </summary>
    public class FaceGenerationResult
    {
        /// <summary>Face morph values (typically 62 floats)</summary>
        public float[] Morphs { get; set; }
        
        /// <summary>Overall match score (0-1)</summary>
        public float Score { get; set; }
        
        /// <summary>Detected gender</summary>
        public bool IsFemale { get; set; }
        
        /// <summary>Detected age</summary>
        public float Age { get; set; }
        
        /// <summary>Skin color value (0=light, 1=dark)</summary>
        public float SkinColor { get; set; }
        
        /// <summary>Eye color value</summary>
        public float EyeColor { get; set; }
        
        /// <summary>Hair color value</summary>
        public float HairColor { get; set; }
        
        /// <summary>Hair style index for Bannerlord</summary>
        public int HairIndex { get; set; }
        
        /// <summary>Beard style index for Bannerlord (0=none)</summary>
        public int BeardIndex { get; set; }
        
        /// <summary>Body weight (0-1)</summary>
        public float Weight { get; set; }
        
        /// <summary>Body build/muscularity (0-1)</summary>
        public float Build { get; set; }
        
        /// <summary>Height (0-1)</summary>
        public float Height { get; set; }
        
        /// <summary>Detection confidence for each attribute</summary>
        public Dictionary<string, float> Confidences { get; set; } = new Dictionary<string, float>();
        
        /// <summary>Was generation successful?</summary>
        public bool Success { get; set; }
        
        /// <summary>Error message if failed</summary>
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Interface for ML addon to register with Core.
    /// If no addon is present, Core uses simple knowledge-based generation.
    /// </summary>
    public interface IMLAddon
    {
        /// <summary>
        /// Whether the ML addon is fully initialized and ready
        /// </summary>
        bool IsReady { get; }
        
        /// <summary>
        /// Initialize the ML systems (models, detectors, etc.)
        /// </summary>
        /// <param name="faceController">FaceController from Core for manipulating morphs</param>
        /// <param name="modulePath">Path to the FaceLearner module folder</param>
        bool Initialize(FaceController faceController, string modulePath);
        
        /// <summary>
        /// Generate face morphs from an image (legacy - use GenerateFaceComplete for full control)
        /// </summary>
        /// <param name="imagePath">Path to source image</param>
        /// <param name="isFemale">Target gender</param>
        /// <param name="progress">Progress callback (0-1)</param>
        /// <returns>Generated morphs and match score</returns>
        (float[] morphs, float score) GenerateFace(string imagePath, bool isFemale, Action<float> progress = null);
        
        /// <summary>
        /// Generate complete face from an image including all attributes (morphs, hair, beard, colors, etc.)
        /// This is the preferred method for Photo Detection mode.
        /// </summary>
        /// <param name="imagePath">Path to source image</param>
        /// <param name="progress">Progress callback (0-1)</param>
        /// <returns>Complete face generation result with all detected attributes</returns>
        FaceGenerationResult GenerateFaceComplete(string imagePath, Action<float> progress = null);
        
        /// <summary>
        /// Start continuous learning from dataset
        /// </summary>
        void StartLearning();
        
        /// <summary>
        /// Stop learning
        /// </summary>
        void StopLearning();
        
        /// <summary>
        /// Whether currently learning
        /// </summary>
        bool IsLearning { get; }
        
        /// <summary>
        /// Get current learning status text
        /// </summary>
        string GetStatusText();
        
        /// <summary>
        /// Get knowledge statistics (legacy, use GetTreeStats for cleaner output)
        /// </summary>
        string GetKnowledgeStats();
        
        /// <summary>
        /// Get current RUN information (what's happening NOW)
        /// </summary>
        string GetRunStatus();
        
        /// <summary>
        /// Get TREE/KNOWLEDGE statistics (what has been LEARNED)
        /// </summary>
        string GetTreeStats();
        
        /// <summary>
        /// Get PHASE evolution statistics
        /// </summary>
        string GetPhaseStats();
        
        /// <summary>
        /// Get SESSION trend information
        /// </summary>
        string GetSessionTrend();
        
        /// <summary>
        /// Total iterations this session
        /// </summary>
        int TotalIterations { get; }
        
        /// <summary>
        /// Total targets processed this session
        /// </summary>
        int TargetsProcessed { get; }
        
        /// <summary>
        /// Tick update for learning loop - just increments counters
        /// </summary>
        void Tick(float dt);
        
        /// <summary>
        /// Process a captured render image - detects landmarks and feeds to learning
        /// This completes the learning loop after VM saves render and calls this.
        /// </summary>
        /// <param name="renderImagePath">Path to saved render image</param>
        /// <returns>True if processing succeeded and morphs were updated</returns>
        bool ProcessRenderCapture(string renderImagePath);
        
        /// <summary>
        /// Called after ProcessRenderCapture - prepares next iteration
        /// Returns true if learning should continue
        /// </summary>
        bool PrepareNextIteration();
        
        /// <summary>
        /// Cleanup resources
        /// </summary>
        void Dispose();
        
        /// <summary>
        /// Export learned knowledge to a shareable file
        /// </summary>
        /// <param name="exportPath">Path for the export file</param>
        /// <param name="exporterName">Optional name of the exporter</param>
        /// <returns>True if export succeeded</returns>
        bool ExportKnowledge(string exportPath, string exporterName = null);
        
        /// <summary>
        /// Import and merge knowledge from a file
        /// </summary>
        /// <param name="importPath">Path to the knowledge file</param>
        /// <param name="trustLevel">How much to trust imported data (0.1-0.9)</param>
        /// <returns>Result message</returns>
        string ImportKnowledge(string importPath, float trustLevel = 0.5f);
        
        /// <summary>
        /// Get info about an export file without importing
        /// </summary>
        string GetExportFileInfo(string path);
        
        /// <summary>
        /// Get Feature Intelligence status (adaptive weights, focus phase, regressions)
        /// </summary>
        string GetFeatureIntelligenceStatus();
        
        /// <summary>
        /// Set the target race for face generation.
        /// When set, generated faces will be adjusted to match race aesthetics.
        /// Example: SetTargetRace("high_elf") for elvish features, "orc" for orcish features
        /// </summary>
        /// <param name="raceId">Race ID or null to clear</param>
        /// <returns>True if race preset was found and set</returns>
        bool SetTargetRace(string raceId);
        
        /// <summary>
        /// Get currently set target race (or null if none)
        /// </summary>
        string GetTargetRace();
        
        /// <summary>
        /// Get list of available race preset IDs
        /// </summary>
        IEnumerable<string> GetAvailableRaces();
        
        /// <summary>
        /// Get race categories (e.g., "Elven", "Dwarven", "Orcish", "Mannish", "Hobbit")
        /// </summary>
        IEnumerable<string> GetRaceCategories();
        
        /// <summary>
        /// Get named presets for the currently set target race (e.g., "Noble", "Warrior")
        /// </summary>
        IEnumerable<string> GetRacePresets();
        
        /// <summary>
        /// Generate multiple random faces with variations based on learned knowledge
        /// </summary>
        /// <param name="count">Number of faces to generate (max 100)</param>
        /// <param name="outputPath">Path for output XML file</param>
        /// <param name="progress">Progress callback</param>
        /// <returns>Number of faces generated</returns>
        int GenerateRandomFaces(int count, string outputPath, Action<int, int> progress = null);
        
        /// <summary>
        /// Process all photos in a folder and generate best matches
        /// </summary>
        /// <param name="folderPath">Path to folder with photos</param>
        /// <param name="outputPath">Path for output XML file</param>
        /// <param name="progress">Progress callback (current, total, imageName)</param>
        /// <returns>Number of photos processed</returns>
        int ProcessPhotoFolder(string folderPath, string outputPath, Action<int, int, string> progress = null);
        
        // === MORPH TESTER ===
        
        /// <summary>
        /// Start automated morph testing (discovers which morph indices affect which face regions)
        /// </summary>
        void StartMorphTest();
        
        /// <summary>
        /// Stop morph testing
        /// </summary>
        void StopMorphTest();
        
        /// <summary>
        /// Whether morph test is currently running
        /// </summary>
        bool IsMorphTestRunning { get; }
        
        /// <summary>
        /// Get morph test status text
        /// </summary>
        string MorphTestStatus { get; }
        
        /// <summary>
        /// Called by screen when screenshot is captured during morph test
        /// </summary>
        void OnMorphTestScreenshot(string imagePath);
        
        /// <summary>
        /// Tick morph test (returns true if character needs refresh)
        /// </summary>
        bool TickMorphTest();
    }
    
    /// <summary>
    /// Registry for ML addon - allows addon to register itself with Core
    /// </summary>
    public static class MLAddonRegistry
    {
        private static IMLAddon _addon;
        
        /// <summary>
        /// Whether an ML addon is registered
        /// </summary>
        public static bool HasAddon => _addon != null;
        
        /// <summary>
        /// Get the registered addon (or null)
        /// </summary>
        public static IMLAddon Addon => _addon;
        
        /// <summary>
        /// Register an ML addon (called by addon's SubModule)
        /// </summary>
        public static void Register(IMLAddon addon)
        {
            _addon = addon;
            SubModule.Log($"[MLAddonRegistry] ML Addon registered: {addon.GetType().Name}");
        }
        
        /// <summary>
        /// Unregister the addon (called on addon unload)
        /// </summary>
        public static void Unregister()
        {
            if (_addon != null)
            {
                SubModule.Log("[MLAddonRegistry] ML Addon unregistered");
                _addon = null;
            }
        }
    }
}
