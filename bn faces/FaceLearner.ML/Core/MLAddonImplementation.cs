using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FaceLearner.Core;
using FaceLearner.Core.LivingKnowledge;
using FaceLearner.ML;
using FaceLearner.ML.Core;
using FaceLearner.ML.Core.RaceSystem;
using FaceLearner.ML.DataSources;
using FaceLearner.ML.Core.LivingKnowledge;
using FaceLearner.ML.Core.HierarchicalScoring;

namespace FaceLearner.ML
{
    /// <summary>
    /// Implementation of IMLAddon that wraps the heavy ML components.
    /// </summary>
    public class MLAddonImplementation : IMLAddon, IDisposable
    {
        private string _modulePath;
        private string _dataDir;
        
        // Heavy ML components
        private LandmarkDetector _landmarkDetector;
        private LearningOrchestratorV3 _orchestrator;
        private DataSourceManager _dataSourceManager;
        private FaceController _faceController;
        
        private bool _isInitialized;
        private bool _isInitializing;
        private string _statusText = "ML Addon Ready";
        
        // Target race for face generation (optional - when set, applies race presets)
        private string _targetRaceId;
        
        public bool IsReady => _isInitialized;
        public bool IsLearning => _orchestrator?.IsLearning ?? false;
        public int TotalIterations => _orchestrator?.TotalIterations ?? 0;
        public int TargetsProcessed => _orchestrator?.TargetsProcessed ?? 0;
        
        public MLAddonImplementation()
        {
            // Default constructor - Initialize() will be called later with parameters
        }
        
        public bool Initialize(FaceController faceController, string coreModulePath)
        {
            if (_isInitialized || _isInitializing)
                return _isInitialized;
            
            _isInitializing = true;
            _statusText = "Initializing ML...";
            _faceController = faceController;
            
            // ML-specific data (models, datasets) in FaceLearner.ML
            _modulePath = SubModule.ModulePath;
            _dataDir = Path.Combine(_modulePath, "Data") + Path.DirectorySeparatorChar;
            
            // Knowledge files go to Core's Data folder so they persist and Core can read them
            string coreDataDir = Path.Combine(coreModulePath, "Data") + Path.DirectorySeparatorChar;
            
            try
            {
                SubModule.Log("Initializing ML components...");
                SubModule.Log($"  ML data path: {_dataDir}");
                SubModule.Log($"  Knowledge path: {coreDataDir}");
                
                // Ensure ML directories exist
                Directory.CreateDirectory(_dataDir);
                Directory.CreateDirectory(Path.Combine(_dataDir, "Models"));
                Directory.CreateDirectory(Path.Combine(_dataDir, "Temp"));
                
                // Ensure Core data directory exists (for knowledge files)
                Directory.CreateDirectory(coreDataDir);
                
                // Initialize landmark detector
                _landmarkDetector = new LandmarkDetector();
                var modelsDir = Path.Combine(_dataDir, "Models");
                if (!_landmarkDetector.Initialize(modelsDir))
                {
                    SubModule.Log($"Landmark detector init failed: {_landmarkDetector.LastError}");
                    _statusText = "Error: Models not found. Run SetupHelper.bat";
                    _isInitializing = false;
                    return false;
                }
                SubModule.Log("Landmark detector initialized");
                
                // Initialize data sources
                _dataSourceManager = new DataSourceManager(_dataDir);
                // DEBUG: Only use fixed test dataset for reproducible debugging
                _dataSourceManager.Register(new TestDataSource());
                // _dataSourceManager.Register(new GeneratedFaceSource());
                // _dataSourceManager.Register(new UTKFaceDataSource());
                // _dataSourceManager.Register(new CelebADataSource());
                // _dataSourceManager.Register(new LFWDataSource());
                _dataSourceManager.InitializeAll();  // IMPORTANT: Actually initialize the sources!
                SubModule.Log($"Data sources initialized: {_dataSourceManager.ReadySources}/{_dataSourceManager.TotalSources} ready, {_dataSourceManager.TotalSamples} samples");
                
                // Log each source status
                foreach (var source in _dataSourceManager.GetAllSources())
                {
                    string status = source.IsReady 
                        ? $"✓ {source.TotalCount} samples" 
                        : "✗ Not found";
                    SubModule.Log($"  - {source.Name}: {status}");
                }
                
                // Log data path for debugging
                SubModule.Log($"  Data path: {_dataDir}");
                SubModule.Log($"  Datasets should be in: {Path.Combine(_dataDir, "datasets")}");
                
                // Initialize V3 orchestrator (Hierarchical System)
                _orchestrator = new LearningOrchestratorV3(
                    _landmarkDetector,
                    _faceController,
                    _dataSourceManager,
                    _dataDir,      // ML data (models, datasets, temp)
                    coreDataDir    // Knowledge files (legacy, not used in V3)
                );
                SubModule.Log("Orchestrator V3 (Hierarchical) initialized");
                
                _isInitialized = true;
                _isInitializing = false;
                _statusText = "ML Ready";
                
                SubModule.Log("ML Addon fully initialized");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"ML initialization error: {ex.Message}");
                _statusText = $"Init Error: {ex.Message}";
                _isInitializing = false;
                return false;
            }
        }
        
        #region Race System API
        
        /// <summary>
        /// Set the target race for face generation.
        /// When set, generated faces will be adjusted to match race aesthetics.
        /// Example: SetTargetRace("high_elf") for elvish features
        /// </summary>
        /// <param name="raceId">Race ID (e.g., "high_elf", "dwarf", "orc") or null to clear</param>
        /// <returns>True if race preset was found and set</returns>
        public bool SetTargetRace(string raceId)
        {
            if (string.IsNullOrEmpty(raceId))
            {
                _targetRaceId = null;
                SubModule.Log("[ML] Target race cleared - will generate human features");
                return true;
            }
            
            var raceManager = _orchestrator?.GetRacePresetManager();
            if (raceManager == null)
            {
                SubModule.Log("[ML] Race system not initialized");
                return false;
            }
            
            var preset = raceManager.GetPreset(raceId);
            if (preset != null)
            {
                _targetRaceId = raceId;
                SubModule.Log($"[ML] Target race set: {preset.DisplayName} ({preset.Category})");
                return true;
            }
            
            SubModule.Log($"[ML] Unknown race: {raceId}");
            return false;
        }
        
        /// <summary>
        /// Get currently set target race
        /// </summary>
        public string GetTargetRace() => _targetRaceId;
        
        /// <summary>
        /// Get list of available race presets
        /// </summary>
        public IEnumerable<string> GetAvailableRaces()
        {
            return _orchestrator?.GetAvailableRaces() ?? Enumerable.Empty<string>();
        }
        
        /// <summary>
        /// Get race categories (Elven, Dwarven, Orcish, Mannish, Hobbit)
        /// </summary>
        public IEnumerable<string> GetRaceCategories()
        {
            return _orchestrator?.GetRaceCategories() ?? Enumerable.Empty<string>();
        }
        
        /// <summary>
        /// Get named presets available for the current target race
        /// </summary>
        public IEnumerable<string> GetRacePresets()
        {
            if (string.IsNullOrEmpty(_targetRaceId)) return Enumerable.Empty<string>();
            
            var raceManager = _orchestrator?.GetRacePresetManager();
            var preset = raceManager?.GetPreset(_targetRaceId);
            return preset?.NamedPresets.Keys ?? Enumerable.Empty<string>();
        }
        
        #endregion
        
        public (float[] morphs, float score) GenerateFace(string imagePath, bool isFemale, Action<float> progress = null)
        {
            if (!_isInitialized)
            {
                SubModule.Log("Cannot generate face - ML not initialized");
                return (new float[62], 0f);
            }
            
            try
            {
                _statusText = "Generating face...";
                progress?.Invoke(0.1f);
                
                // Load and detect landmarks from image
                using (var bitmap = new System.Drawing.Bitmap(imagePath))
                {
                    var fullLandmarks = _landmarkDetector.DetectLandmarks(bitmap);
                    if (fullLandmarks == null || fullLandmarks.Length < 68)
                    {
                        SubModule.Log("No face detected in image");
                        return (new float[62], 0f);
                    }
                    
                    // v3.0.27: Pass full FaceMesh 468 landmarks directly
                    var landmarks = fullLandmarks;

                    if (landmarks == null || landmarks.Length < 936)
                    {
                        SubModule.Log("Landmark detection failed (need FaceMesh 468)");
                        return (new float[62], 0f);
                    }

                    progress?.Invoke(0.3f);

                    // Detect gender and age from the image
                    float detectedAge = 30f;
                    bool detectedFemale = isFemale;  // Use passed value as fallback
                    
                    if (_orchestrator != null && _orchestrator.IsFairFaceLoaded)
                    {
                        try
                        {
                            var (ffFemale, ffGenderConf, ffAge) = _orchestrator.DetectDemographics(bitmap);
                            if (ffGenderConf > 0.60f)
                            {
                                detectedFemale = ffFemale;
                                detectedAge = ffAge;
                            }
                        }
                        catch { }
                    }
                    
                    progress?.Invoke(0.5f);
                    
                    // V3: Generate random starting morphs with demographic hints
                    // (HierarchicalKnowledge is not used in V3)
                    float[] morphs = new float[62];
                    float score = 0f;
                    
                    // Generate random face with slight demographic bias
                    SubModule.Log("  V3: Generating random starting face");
                    var random = new Random();
                    for (int i = 0; i < 62; i++)
                    {
                        morphs[i] = (float)(random.NextDouble() - 0.5) * 0.3f;
                    }
                    score = 0.3f;
                    
                    progress?.Invoke(1.0f);
                    _statusText = $"Face generated ({(detectedFemale ? "F" : "M")}, {detectedAge:F0}y)";
                    
                    return (morphs, score);
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"Face generation error: {ex.Message}");
                _statusText = $"Error: {ex.Message}";
                return (new float[62], 0f);
            }
        }
        
        /// <summary>
        /// Generate complete face from an image including all attributes.
        /// This is used for Photo Detection mode (not learning).
        /// </summary>
        public FaceGenerationResult GenerateFaceComplete(string imagePath, Action<float> progress = null)
        {
            var result = new FaceGenerationResult
            {
                Morphs = new float[62],
                Score = 0f,
                Success = false,
                Weight = 0.5f,
                Build = 0.5f,
                Height = 0.5f
            };
            
            if (!_isInitialized)
            {
                result.ErrorMessage = "ML not initialized";
                return result;
            }
            
            try
            {
                _statusText = "Analyzing photo...";
                progress?.Invoke(0.05f);
                
                using (var bitmap = new System.Drawing.Bitmap(imagePath))
                {
                    // === 1. DETECT LANDMARKS ===
                    var fullLandmarks = _landmarkDetector.DetectLandmarks(bitmap);
                    if (fullLandmarks == null || fullLandmarks.Length < 68)
                    {
                        result.ErrorMessage = "No face detected in image";
                        return result;
                    }
                    
                    // v3.0.27: Pass full FaceMesh 468 landmarks directly
                    var landmarks = fullLandmarks;

                    if (landmarks == null || landmarks.Length < 936)
                    {
                        result.ErrorMessage = "Landmark detection failed (need FaceMesh 468)";
                        return result;
                    }
                    progress?.Invoke(0.15f);
                    
                    // === 2. DETECT DEMOGRAPHICS (Gender, Age, Race) ===
                    float detectedAge = 30f;
                    bool detectedFemale = false;
                    string detectedRace = "Unknown";
                    float raceConf = 0f;
                    
                    // === 2. DETECT DEMOGRAPHICS using GenderEnsemble ===
                    // GenderEnsemble combines FairFace + HairAppearance + Landmarks for more robust detection
                    // This catches edge cases like older women with strong facial features
                    var genderEnsemble = _orchestrator?.GetGenderEnsemble();
                    if (genderEnsemble != null)
                    {
                        try
                        {
                            var ensembleResult = genderEnsemble.Detect(imagePath, landmarks, null, 0.5f);
                            detectedFemale = ensembleResult.IsFemale;
                            detectedAge = ensembleResult.Age;
                            detectedRace = ensembleResult.Race;
                            raceConf = ensembleResult.RaceConfidence;
                            
                            result.Confidences["gender"] = ensembleResult.Confidence;
                            result.Confidences["age"] = 0.7f;
                            result.Confidences["race"] = ensembleResult.RaceConfidence;
                            
                            string votesSummary = string.Join(", ", ensembleResult.Votes?.Select(v => v.ToString()) ?? new[] { "N/A" });
                            SubModule.Log($"  Photo GenderEnsemble: {(ensembleResult.IsFemale ? "Female" : "Male")} (conf={ensembleResult.Confidence:F2}) age={ensembleResult.Age:F0}");
                            SubModule.Log($"    Decision: {ensembleResult.Decision}");
                        }
                        catch (Exception ex)
                        {
                            SubModule.Log($"  GenderEnsemble failed, falling back to FairFace: {ex.Message}");
                            // Fall through to FairFace below
                        }
                    }
                    
                    // Fallback to raw FairFace if GenderEnsemble not available or failed
                    if (result.Confidences.Count == 0 && _orchestrator != null && _orchestrator.IsFairFaceLoaded)
                    {
                        try
                        {
                            var fairFace = _orchestrator.GetFairFaceDetector();
                            if (fairFace != null)
                            {
                                var (ffFemale, ffGenderConf, ffAge, ffRace, ffRaceConf) = fairFace.Detect(bitmap);
                                detectedFemale = ffFemale;
                                detectedAge = ffAge;
                                detectedRace = ffRace;
                                raceConf = ffRaceConf;
                                
                                result.Confidences["gender"] = ffGenderConf;
                                result.Confidences["age"] = 0.7f;
                                result.Confidences["race"] = ffRaceConf;
                                
                                SubModule.Log($"  Photo FairFace: {(ffFemale ? "Female" : "Male")} (conf={ffGenderConf:F2}) age={ffAge:F0} race={ffRace} (conf={ffRaceConf:F2})");
                            }
                        }
                        catch (Exception ex)
                        {
                            SubModule.Log($"  Demographics detection failed: {ex.Message}");
                        }
                    }
                    
                    result.IsFemale = detectedFemale;
                    result.Age = Math.Max(20f, detectedAge); // Minimum 20 for Bannerlord
                    progress?.Invoke(0.30f);
                    
                    // === 3. DETECT COLORS (Skin, Eye, Hair) ===
                    var colorEnsemble = _orchestrator?.GetColorEnsemble();
                    if (colorEnsemble != null)
                    {
                        try
                        {
                            // Skin color
                            var skinResult = colorEnsemble.Detect(imagePath, landmarks, null, detectedRace, raceConf);
                            if (skinResult.Confidence > 0.3f)
                            {
                                result.SkinColor = skinResult.Value;
                                result.Confidences["skinColor"] = skinResult.Confidence;
                            }
                            
                            // Eye color
                            var eyeResult = colorEnsemble.DetectEyeColor(imagePath, landmarks);
                            if (eyeResult.Confidence > 0.3f)
                            {
                                result.EyeColor = eyeResult.Value;
                                result.Confidences["eyeColor"] = eyeResult.Confidence;
                            }
                            
                            // Hair color
                            var (hairColorResult, hairDetected) = colorEnsemble.DetectHairColor(imagePath, landmarks, result.SkinColor);
                            if (hairDetected && hairColorResult.Confidence > 0.3f)
                            {
                                result.HairColor = hairColorResult.Value;
                                result.Confidences["hairColor"] = hairColorResult.Confidence;
                            }
                            
                            SubModule.Log($"  Colors: Skin={result.SkinColor:F2} Eye={result.EyeColor:F2} Hair={result.HairColor:F2}");
                        }
                        catch (Exception ex)
                        {
                            SubModule.Log($"  Color detection failed: {ex.Message}");
                        }
                    }
                    progress?.Invoke(0.45f);
                    
                    // === 4. DETECT HAIR/BEARD STYLE (Photo Detection only!) ===
                    try
                    {
                        var styleResult = HairBeardStyleDetector.Detect(bitmap, landmarks, detectedFemale);
                        result.HairIndex = styleResult.HairIndex;
                        result.BeardIndex = styleResult.BeardIndex;
                        result.Confidences["hairStyle"] = styleResult.HairConfidence;
                        result.Confidences["beardStyle"] = styleResult.BeardConfidence;
                    }
                    catch (Exception ex)
                    {
                        SubModule.Log($"  Hair/Beard style detection failed: {ex.Message}");
                    }
                    progress?.Invoke(0.55f);
                    
                    // === 5. ESTIMATE BODY PROPS FROM FACE SHAPE ===
                    var moduleIntegration = _orchestrator?.GetModuleIntegration();
                    if (moduleIntegration != null)
                    {
                        try
                        {
                            // Analyze face proportions
                            moduleIntegration.SetTargetLandmarks(landmarks);
                            var proportions = moduleIntegration.TargetProportions;
                            
                            if (proportions?.FaceGeometry != null)
                            {
                                var faceGeom = proportions.FaceGeometry;
                                var shape = faceGeom.Shape;
                                float ratio = faceGeom.WidthHeightRatio;
                                
                                // Estimate body props from face shape
                                EstimateBodyPropsFromFace(shape, ratio, faceGeom.JawAngle, result);
                                
                                SubModule.Log($"  Body: Weight={result.Weight:F2} Build={result.Build:F2} Height={result.Height:F2} (from {shape} face)");
                            }
                        }
                        catch (Exception ex)
                        {
                            SubModule.Log($"  Body estimation failed: {ex.Message}");
                        }
                    }
                    progress?.Invoke(0.70f);
                    
                    // === 6. GENERATE MORPHS FROM KNOWLEDGE ===
                    var knowledge = _orchestrator?.GetHierarchicalKnowledge();
                    if (knowledge != null && !knowledge.IsEmpty)
                    {
                        var metadata = new Dictionary<string, float>
                        {
                            { "gender", detectedFemale ? 1f : 0f },
                            { "age", detectedAge }
                        };
                        
                        result.Morphs = knowledge.GetStartingMorphs(landmarks, metadata, 62);
                        result.Score = 0.65f;
                    }
                    else
                    {
                        // No knowledge - generate random
                        var random = new Random();
                        for (int i = 0; i < 62; i++)
                        {
                            result.Morphs[i] = (float)(random.NextDouble() - 0.5) * 0.3f;
                        }
                        result.Score = 0.3f;
                    }
                    
                    // === 7. APPLY RACE PRESETS (if target race is set) ===
                    if (!string.IsNullOrEmpty(_targetRaceId))
                    {
                        var raceGenerator = _orchestrator?.GetRaceGenerator();
                        if (raceGenerator != null && raceGenerator.SetRace(_targetRaceId))
                        {
                            // Apply race biases and constraints to morphs
                            result.Morphs = raceGenerator.ApplyBiases(result.Morphs);
                            result.Morphs = raceGenerator.ConstrainToRace(result.Morphs);
                            
                            // Adjust body proportions for race
                            var (heightBias, buildBias, weightBias) = raceGenerator.GetBodyBiases();
                            result.Height = Math.Max(0f, Math.Min(1f, result.Height + heightBias * 0.3f));
                            result.Build = Math.Max(0f, Math.Min(1f, result.Build + buildBias * 0.3f));
                            result.Weight = Math.Max(0f, Math.Min(1f, result.Weight + weightBias * 0.3f));
                            
                            // Adjust skin color for race
                            result.SkinColor = raceGenerator.GetSkinTone(result.SkinColor);
                            
                            SubModule.Log($"  Applied race preset: {_targetRaceId}");
                        }
                    }
                    progress?.Invoke(0.90f);
                    
                    result.Success = true;
                    _statusText = $"Photo analyzed: {(detectedFemale ? "F" : "M")}, {detectedAge:F0}y";
                    progress?.Invoke(1.0f);
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                SubModule.Log($"GenerateFaceComplete error: {ex.Message}");
                return result;
            }
        }
        
        /// <summary>
        /// Estimate body proportions from face shape (used in Photo Detection)
        /// </summary>
        private void EstimateBodyPropsFromFace(ML.Modules.Core.FaceShape shape, float ratio, float jawAngle, FaceGenerationResult result)
        {
            // Weight estimation
            switch (shape)
            {
                case ML.Modules.Core.FaceShape.Round:
                    result.Weight = 0.55f + (ratio - 0.9f) * 0.5f;
                    break;
                case ML.Modules.Core.FaceShape.Square:
                    result.Weight = 0.45f + (ratio - 0.85f) * 0.3f;
                    break;
                case ML.Modules.Core.FaceShape.Oval:
                    result.Weight = 0.35f + (ratio - 0.8f) * 0.4f;
                    break;
                case ML.Modules.Core.FaceShape.Oblong:
                    result.Weight = 0.30f + ratio * 0.2f;
                    break;
                default:
                    result.Weight = 0.35f + (ratio - 0.8f) * 0.3f;
                    break;
            }
            
            // Build estimation
            switch (shape)
            {
                case ML.Modules.Core.FaceShape.Square:
                    result.Build = 0.55f + (100f - jawAngle) / 100f * 0.25f;
                    break;
                case ML.Modules.Core.FaceShape.Diamond:
                    result.Build = 0.50f + (100f - jawAngle) / 100f * 0.20f;
                    break;
                case ML.Modules.Core.FaceShape.Round:
                    result.Build = 0.30f + (100f - jawAngle) / 200f * 0.15f;
                    break;
                default:
                    result.Build = 0.45f + (100f - jawAngle) / 150f * 0.15f;
                    break;
            }
            
            // Height estimation
            switch (shape)
            {
                case ML.Modules.Core.FaceShape.Oblong:
                    result.Height = 0.60f + (0.85f - ratio) * 1.0f;
                    break;
                case ML.Modules.Core.FaceShape.Oval:
                    result.Height = 0.50f + (0.85f - ratio) * 0.5f;
                    break;
                case ML.Modules.Core.FaceShape.Round:
                    result.Height = 0.40f + (0.95f - ratio) * 0.3f;
                    break;
                default:
                    result.Height = 0.50f + (0.85f - ratio) * 0.4f;
                    break;
            }
            
            // Clamp and add variation
            var rng = new Random();
            result.Weight = Math.Max(0.1f, Math.Min(0.9f, result.Weight + (float)(rng.NextDouble() - 0.5) * 0.1f));
            result.Build = Math.Max(0.1f, Math.Min(0.9f, result.Build + (float)(rng.NextDouble() - 0.5) * 0.1f));
            result.Height = Math.Max(0.1f, Math.Min(0.9f, result.Height + (float)(rng.NextDouble() - 0.5) * 0.1f));
        }
        
        public void StartLearning()
        {
            if (!_isInitialized)
            {
                SubModule.Log("Cannot start learning - ML not initialized");
                return;
            }
            
            if (_faceController == null)
            {
                SubModule.Log("Cannot start learning - FaceController not available");
                throw new InvalidOperationException("Learning requires FaceController. Initialize with a valid FaceController.");
            }
            
            if (IsLearning)
            {
                SubModule.Log("Already learning");
                return;
            }
            
            SubModule.Log($"StartLearning with FaceController: {_faceController.GetType().Name}");
            _orchestrator.Start();
            _statusText = "Learning started";
            SubModule.Log("Learning started");
        }
        
        public void StopLearning()
        {
            if (!IsLearning)
                return;
            
            _orchestrator.Stop();
            _statusText = "Learning stopped";
            SubModule.Log("Learning stopped");
        }
        
        public string GetStatusText()
        {
            if (_orchestrator != null && IsLearning)
            {
                return _orchestrator.GetStatus();
            }
            return _statusText;
        }
        
        public string GetKnowledgeStats()
        {
            if (_orchestrator == null)
                return "Not initialized";
            
            return _orchestrator.GetExtendedStatus();
        }
        
        public string GetRunStatus()
        {
            if (_orchestrator == null)
                return "Not initialized";
            
            return _orchestrator.GetRunStatus();
        }
        
        public string GetTreeStats()
        {
            if (_orchestrator == null)
                return "Not initialized";
            
            return _orchestrator.GetTreeStatus();
        }
        
        public string GetPhaseStats()
        {
            if (_orchestrator == null)
                return "Not initialized";
            
            return _orchestrator.GetPhaseStats();
        }
        
        public string GetSessionTrend()
        {
            if (_orchestrator == null)
                return "Not initialized";
            
            return _orchestrator.GetSessionTrend();
        }
        
        public void Tick(float dt)
        {
            // Tick is now just for status updates - the real work happens in ProcessRenderCapture
            if (!_isInitialized || !IsLearning)
                return;
        }
        
        public bool ProcessRenderCapture(string renderImagePath)
        {
            if (!_isInitialized || !IsLearning || _orchestrator == null || _landmarkDetector == null)
                return false;
            
            try
            {
                // Update orchestrator's screenshot path so comparisons work
                if (_orchestrator.ScreenshotPath != renderImagePath)
                {
                    _orchestrator.ScreenshotPath = renderImagePath;
                }
                
                // Detect landmarks on the rendered face
                var fullLandmarks = _landmarkDetector.DetectLandmarks(renderImagePath);
                
                if (fullLandmarks == null || fullLandmarks.Length == 0)
                {
                    // No face detected - skip this frame
                    return false;
                }
                
                // v3.0.27: Pass FULL FaceMesh 468 landmarks directly — no more lossy 68 conversion!
                float[] renderedLandmarks = fullLandmarks;
                
                // Feed to orchestrator - this does scoring, learning, mutation
                _orchestrator.OnRenderCaptured(renderedLandmarks);
                
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"[ML] ProcessRenderCapture error: {ex.Message}");
                SubModule.Log($"[ML]   StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    SubModule.Log($"[ML]   Inner: {ex.InnerException.Message}");
                return false;
            }
        }
        
        public bool PrepareNextIteration()
        {
            if (!_isInitialized || !IsLearning || _orchestrator == null)
                return false;
            
            try
            {
                // This sets new morphs on the face controller
                return _orchestrator.Iterate();
            }
            catch (Exception ex)
            {
                SubModule.Log($"PrepareNextIteration error: {ex.Message}");
                return false;
            }
        }
        
        public void Dispose()
        {
            try
            {
                if (IsLearning)
                    StopLearning();
                
                _landmarkDetector?.Dispose();
                _orchestrator?.Dispose();
                
                _landmarkDetector = null;
                _orchestrator = null;
                _faceController = null;
                
                SubModule.Log("ML Addon disposed");
            }
            catch (Exception ex)
            {
                SubModule.Log($"Dispose error: {ex.Message}");
            }
        }
        
        #region Knowledge Sharing
        
        public bool ExportKnowledge(string exportPath, string exporterName = null)
        {
            if (_orchestrator == null)
            {
                SubModule.Log("[Export] Cannot export: orchestrator not initialized");
                return false;
            }
            
            // V3 uses hierarchical phase system, not HierarchicalKnowledge tree
            SubModule.Log("[Export] Knowledge export not yet implemented for V3 Hierarchical System");
            SubModule.Log("[Export] V3 learns directly via HierarchicalScorer - no exportable tree");
            return false;
        }
        
        public string ImportKnowledge(string importPath, float trustLevel = 0.5f)
        {
            // V3 uses hierarchical phase system, not HierarchicalKnowledge tree
            return "Knowledge import not yet implemented for V3 Hierarchical System.\n" +
                   "V3 learns morphs hierarchically and doesn't use the old tree structure.";
        }
        
        public string GetExportFileInfo(string path)
        {
            if (!File.Exists(path))
            {
                return "File not found";
            }
            
            var info = KnowledgeSharing.GetExportInfo(path);
            if (info == null)
            {
                return "Invalid or corrupt knowledge file";
            }
            
            return $"Knowledge Export: {info.ExportId}\n" +
                   $"  From: {info.ExporterName}\n" +
                   $"  Date: {info.ExportDate:yyyy-MM-dd HH:mm}\n" +
                   $"  Experiments: {info.TotalExperiments}\n" +
                   $"  Nodes: {info.NodeCount}\n" +
                   $"  Shared entries: {info.SharedEntryCount}\n" +
                   $"  Success rate: {info.AverageSuccessRate:P0}\n" +
                   $"  Confidence: {info.AverageConfidence:F2}";
        }
        
        public string GetFeatureIntelligenceStatus()
        {
            if (_orchestrator == null)
            {
                return "Not initialized";
            }
            
            return _orchestrator.GetFeatureIntelligenceStatus();
        }
        
        #endregion
        
        #region Batch Generation
        
        public int GenerateRandomFaces(int count, string outputPath, Action<int, int> progress = null)
        {
            if (!_isInitialized || _faceController == null)
            {
                SubModule.Log("[BatchGen] Not initialized");
                return 0;
            }
            
            count = Math.Min(count, 100);  // Cap at 100
            var random = new Random();
            var results = new List<GeneratedFaceResult>();
            
            SubModule.Log($"[BatchGen] Generating {count} random faces...");
            
            for (int i = 0; i < count; i++)
            {
                progress?.Invoke(i + 1, count);
                
                try
                {
                    // Generate random face using knowledge tree
                    var morphs = GenerateRandomFaceFromKnowledge(random);
                    
                    // Create 3 variants with small variations
                    var variants = new List<float[]> { morphs };
                    for (int v = 0; v < 2; v++)
                    {
                        var variant = CreateVariant(morphs, random, 0.05f + v * 0.05f);
                        variants.Add(variant);
                    }
                    
                    results.Add(new GeneratedFaceResult
                    {
                        Index = i + 1,
                        IsFemale = random.NextDouble() > 0.5,
                        Age = 18 + (float)(random.NextDouble() * 47),  // 18-65
                        Variants = variants
                    });
                }
                catch (Exception ex)
                {
                    SubModule.Log($"[BatchGen] Error generating face {i + 1}: {ex.Message}");
                }
            }
            
            // Write to XML file
            WriteResultsToXml(results, outputPath, "Generated Faces");
            SubModule.Log($"[BatchGen] Generated {results.Count} faces to {outputPath}");
            
            return results.Count;
        }
        
        public int ProcessPhotoFolder(string folderPath, string outputPath, Action<int, int, string> progress = null)
        {
            if (!_isInitialized || _landmarkDetector == null)
            {
                SubModule.Log("[PhotoMatch] Not initialized");
                return 0;
            }
            
            if (!Directory.Exists(folderPath))
            {
                SubModule.Log($"[PhotoMatch] Folder not found: {folderPath}");
                return 0;
            }
            
            // Find all image files
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
            var imageFiles = Directory.GetFiles(folderPath)
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToArray();
            
            if (imageFiles.Length == 0)
            {
                SubModule.Log("[PhotoMatch] No images found in folder");
                return 0;
            }
            
            SubModule.Log($"[PhotoMatch] Processing {imageFiles.Length} photos...");
            var results = new List<GeneratedFaceResult>();
            
            for (int i = 0; i < imageFiles.Length; i++)
            {
                var imagePath = imageFiles[i];
                var imageName = Path.GetFileName(imagePath);
                progress?.Invoke(i + 1, imageFiles.Length, imageName);
                
                try
                {
                    // Detect gender AND age from image (uses FairFace when available)
                    var (isFemale, detectedAge) = DetectGenderAndAgeFromImage(imagePath);
                    
                    // Generate face from photo
                    var (morphs, score) = GenerateFace(imagePath, isFemale, null);
                    
                    if (score > 0.3f)  // Minimum quality threshold
                    {
                        // Create 3 variants
                        var variants = new List<float[]> { morphs };
                        var random = new Random();
                        for (int v = 0; v < 2; v++)
                        {
                            var variant = CreateVariant(morphs, random, 0.03f + v * 0.02f);
                            variants.Add(variant);
                        }
                        
                        results.Add(new GeneratedFaceResult
                        {
                            Index = i + 1,
                            SourceImage = imageName,
                            IsFemale = isFemale,
                            Age = detectedAge,  // Use detected age instead of morph-based estimate
                            Score = score,
                            Variants = variants
                        });
                        
                        SubModule.Log($"  ✓ {imageName}: {(isFemale ? "F" : "M")} age={detectedAge:F0} score={score:F2}");
                    }
                    else
                    {
                        SubModule.Log($"  ✗ {imageName}: low score ({score:F2})");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"  ✗ {imageName}: {ex.Message}");
                }
            }
            
            // Write to XML file
            WriteResultsToXml(results, outputPath, "Photo Matched Faces");
            SubModule.Log($"[PhotoMatch] Processed {results.Count}/{imageFiles.Length} photos to {outputPath}");
            
            return results.Count;
        }
        
        /// <summary>
        /// v3.0.22: Morphospace-aware random generation.
        /// Instead of clustering all faces around neutral (0.5), explicitly target diverse face types
        /// (different shapes, ages, skin tones) and use the knowledge tree's learned morphs for each.
        /// This breaks out of Bannerlord's vanilla sameness where all generated faces look similar.
        /// </summary>
        private float[] GenerateRandomFaceFromKnowledge(Random random)
        {
            var morphs = new float[62];  // Standard morph count

            // Diverse face type options — randomly pick a combination
            var shapes = new[] { "Round", "Oval", "Square", "Heart", "Oblong", "Diamond" };
            var skinTones = new[] { 0.15f, 0.30f, 0.50f, 0.70f, 0.85f };

            // Random demographic combo for this face
            bool isFemale = random.NextDouble() > 0.5;
            float age = 18f + (float)(random.NextDouble() * 50f);  // 18-68 range
            float skinTone = skinTones[random.Next(skinTones.Length)];

            var metadata = new Dictionary<string, float>
            {
                { "gender", isFemale ? 1f : 0f },
                { "age", age },
                { "skinTone", skinTone }
            };

            // Get starting morphs from knowledge tree if available
            var knowledge = _orchestrator?.GetHierarchicalKnowledge();
            if (knowledge != null && !knowledge.IsEmpty)
            {
                // Create dummy landmarks — zero is fine, the tree classifies from metadata
                var dummyLandmarks = new float[136];

                // Get learned morphs for this face type
                var startMorphs = knowledge.GetStartingMorphs(dummyLandmarks, metadata, 62);
                if (startMorphs != null && startMorphs.Length > 0)
                {
                    Array.Copy(startMorphs, morphs, Math.Min(startMorphs.Length, morphs.Length));
                }
            }
            else
            {
                // No knowledge tree — initialize with range-aware random values
                for (int i = 0; i < morphs.Length; i++)
                {
                    var (min, max, range) = MorphGroups.GetMorphRange(i);
                    // Random value within full Bannerlord range
                    morphs[i] = min + (float)random.NextDouble() * (max - min);
                }
            }

            // Add controlled noise using actual morph ranges for diversity
            // 15% of range as noise — enough for variety, not so much it destroys the character
            for (int i = 0; i < morphs.Length; i++)
            {
                var (min, max, range) = MorphGroups.GetMorphRange(i);
                float noise = (float)(random.NextDouble() - 0.5) * range * 0.15f;
                morphs[i] = Math.Max(min, Math.Min(max, morphs[i] + noise));
            }

            return morphs;
        }
        
        /// <summary>
        /// v3.0.22: Use actual Bannerlord morph ranges for variation.
        /// Old version clamped to -1..1, losing 64% of jaw_line range (-2.75 to 0.0)
        /// and similar for other wide-range morphs. Now range-proportional variations
        /// explore the full engine capability.
        /// </summary>
        private float[] CreateVariant(float[] baseMorphs, Random random, float variationStrength)
        {
            var variant = new float[baseMorphs.Length];
            Array.Copy(baseMorphs, variant, baseMorphs.Length);

            // Apply range-proportional random changes to 30% of morphs
            int changesToMake = (int)(baseMorphs.Length * 0.3);
            for (int i = 0; i < changesToMake; i++)
            {
                int idx = random.Next(baseMorphs.Length);
                var (min, max, range) = MorphGroups.GetMorphRange(idx);

                // Variation scaled to actual morph range (not fixed -1..1)
                float variation = (float)(random.NextDouble() - 0.5) * 2 * variationStrength * range;
                variant[idx] += variation;

                // Clamp to actual Bannerlord range, not arbitrary -1..1
                variant[idx] = Math.Max(min, Math.Min(max, variant[idx]));
            }

            return variant;
        }
        
        private (bool isFemale, float age) DetectGenderAndAgeFromImage(string imagePath)
        {
            bool isFemale = false;
            float age = 30f;
            
            // Try FairFace first (best accuracy)
            if (_orchestrator != null && _orchestrator.IsFairFaceLoaded)
            {
                try
                {
                    using (var bmp = new System.Drawing.Bitmap(imagePath))
                    {
                        var (ffFemale, ffGenderConf, ffAge) = _orchestrator.DetectDemographics(bmp);
                        if (ffGenderConf > 0.60f)
                        {
                            isFemale = ffFemale;
                            age = ffAge;
                            SubModule.Log($"  FairFace: {(isFemale ? "F" : "M")}({ffGenderConf:F2}) Age={age:F0}");
                            return (isFemale, age);
                        }
                    }
                }
                catch { }
            }
            
            // Fallback to landmark-based gender estimation
            var fullLandmarks = _landmarkDetector?.DetectLandmarks(imagePath);
            if (fullLandmarks != null && fullLandmarks.Length >= 936)
            {
                // v3.0.27: Pass full FaceMesh 468 landmarks directly
                {
                    var result = LandmarkGenderEstimator.EstimateGender(fullLandmarks);
                    isFemale = result.isFemale;
                    SubModule.Log($"  Landmarks: {(isFemale ? "F" : "M")}({result.confidence:F2})");
                }
            }
            
            return (isFemale, age);
        }
        
        // Keep old method for compatibility but deprecated
        private bool DetectGenderFromImage(string imagePath)
        {
            var (isFemale, _) = DetectGenderAndAgeFromImage(imagePath);
            return isFemale;
        }
        
        private float EstimateAgeFromMorphs(float[] morphs)
        {
            // Simple heuristic based on face morphs
            // Higher values in certain morphs suggest older age
            float ageEstimate = 25f;
            if (morphs.Length > 10)
            {
                // Wrinkle-related morphs tend to be higher indices
                float avgHighMorph = 0;
                for (int i = 50; i < Math.Min(60, morphs.Length); i++)
                {
                    avgHighMorph += Math.Abs(morphs[i]);
                }
                avgHighMorph /= 10f;
                ageEstimate = 20 + avgHighMorph * 40;  // 20-60 range
            }
            return Math.Max(18, Math.Min(65, ageEstimate));
        }
        
        private void WriteResultsToXml(List<GeneratedFaceResult> results, string outputPath, string title)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<!-- {title} - Generated by FaceLearner v{SubModule.VERSION} -->");
            sb.AppendLine($"<!-- Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss} -->");
            sb.AppendLine($"<!-- Count: {results.Count} faces with 3 variants each -->");
            sb.AppendLine("<!--");
            sb.AppendLine("  FORMAT 1 (StringCode): Copy-paste into character creation console or code");
            sb.AppendLine("  FORMAT 2 (BodyPropertiesXml): Use in NPCCharacter XML files");
            sb.AppendLine("  FORMAT 3 (HeroXml): Use in Heroes XML files for lords/companions");
            sb.AppendLine("-->");
            sb.AppendLine("<FaceLearnerExport>");
            
            foreach (var result in results)
            {
                string genderStr = result.IsFemale ? "Female" : "Male";
                sb.AppendLine($"  <Face index=\"{result.Index}\" gender=\"{genderStr}\" age=\"{result.Age:F1}\"{(result.SourceImage != null ? $" source=\"{result.SourceImage}\"" : "")}{(result.Score > 0 ? $" score=\"{result.Score:F3}\"" : "")}>");
                
                for (int v = 0; v < result.Variants.Count; v++)
                {
                    var morphs = result.Variants[v];
                    float weight = 0.3f + (float)(new Random(result.Index * 10 + v).NextDouble() * 0.4);
                    float build = 0.3f + (float)(new Random(result.Index * 10 + v + 100).NextDouble() * 0.4);
                    
                    // Use FaceController to generate proper key
                    string key;
                    if (_faceController != null)
                    {
                        // Save current state
                        var savedMorphs = _faceController.GetAllMorphs();
                        var savedFemale = _faceController.IsFemale;
                        var savedAge = _faceController.Age;
                        var savedWeight = _faceController.Weight;
                        var savedBuild = _faceController.Build;
                        
                        // Apply result to get proper key
                        _faceController.IsFemale = result.IsFemale;
                        _faceController.Age = result.Age;
                        _faceController.Weight = weight;
                        _faceController.Build = build;
                        _faceController.SetAllMorphs(morphs);
                        
                        key = _faceController.GetKeyString();
                        
                        // Restore state
                        _faceController.IsFemale = savedFemale;
                        _faceController.Age = savedAge;
                        _faceController.Weight = savedWeight;
                        _faceController.Build = savedBuild;
                        _faceController.SetAllMorphs(savedMorphs);
                    }
                    else
                    {
                        // Fallback to simplified key
                        key = MorphsToKeyString(morphs);
                    }
                    
                    sb.AppendLine($"    <Variant id=\"{v + 1}\">");
                    
                    // FORMAT 1: Simple string code (for console/clipboard)
                    sb.AppendLine($"      <!-- FORMAT 1: StringCode (copy this entire line for character creation) -->");
                    sb.AppendLine($"      <StringCode>{key}</StringCode>");
                    
                    // FORMAT 2: BodyProperties XML (for NPCCharacter files)
                    sb.AppendLine($"      <!-- FORMAT 2: BodyPropertiesXml (use in spnpccharacters.xml) -->");
                    sb.AppendLine($"      <BodyPropertiesXml>");
                    sb.AppendLine($"        <BodyProperties version=\"4\" age=\"{result.Age:F1}\" weight=\"{weight:F2}\" build=\"{build:F2}\" key=\"{key}\" />");
                    sb.AppendLine($"      </BodyPropertiesXml>");
                    
                    // FORMAT 3: Hero XML (for lords.xml / companions.xml)
                    sb.AppendLine($"      <!-- FORMAT 3: HeroXml (use in lords.xml or companions.xml) -->");
                    sb.AppendLine($"      <HeroXml>");
                    sb.AppendLine($"        <Hero id=\"generated_{result.Index:D3}_v{v + 1}\" faction=\"\" default_group=\"Infantry\" is_female=\"{result.IsFemale.ToString().ToLower()}\">");
                    sb.AppendLine($"          <face>");
                    sb.AppendLine($"            <BodyProperties version=\"4\" age=\"{result.Age:F1}\" weight=\"{weight:F2}\" build=\"{build:F2}\" key=\"{key}\" />");
                    sb.AppendLine($"          </face>");
                    sb.AppendLine($"        </Hero>");
                    sb.AppendLine($"      </HeroXml>");
                    
                    sb.AppendLine($"    </Variant>");
                }
                
                sb.AppendLine("  </Face>");
            }
            
            sb.AppendLine("</FaceLearnerExport>");
            
            // Also create separate format-specific files for easy use
            string basePath = Path.GetDirectoryName(outputPath);
            string baseName = Path.GetFileNameWithoutExtension(outputPath);
            
            // Write main combined file
            File.WriteAllText(outputPath, sb.ToString());
            
            // Write StringCodes only (easy copy-paste)
            WriteStringCodesFile(results, Path.Combine(basePath, baseName + "_codes.txt"));
            
            // Write NPCCharacters XML snippet
            WriteNpcCharactersFile(results, Path.Combine(basePath, baseName + "_npcs.xml"));
            
            // Write Heroes XML snippet  
            WriteHeroesFile(results, Path.Combine(basePath, baseName + "_heroes.xml"));
        }
        
        private void WriteStringCodesFile(List<GeneratedFaceResult> results, string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# FaceLearner String Codes - Generated {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"# {results.Count} faces with 3 variants each");
            sb.AppendLine($"# Copy-paste these codes into Bannerlord character creation console");
            sb.AppendLine();
            
            foreach (var result in results)
            {
                string source = result.SourceImage != null ? $" (from {result.SourceImage})" : "";
                sb.AppendLine($"=== Face {result.Index}: {(result.IsFemale ? "Female" : "Male")}, Age {result.Age:F0}{source} ===");
                
                for (int v = 0; v < result.Variants.Count; v++)
                {
                    var key = MorphsToKeyString(result.Variants[v]);
                    sb.AppendLine($"Variant {v + 1}: {key}");
                }
                sb.AppendLine();
            }
            
            File.WriteAllText(path, sb.ToString());
        }
        
        private void WriteNpcCharactersFile(List<GeneratedFaceResult> results, string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<!-- NPCCharacter definitions - Generated by FaceLearner {DateTime.Now:yyyy-MM-dd} -->");
            sb.AppendLine("<!-- Copy these into your spnpccharacters.xml or similar -->");
            sb.AppendLine("<NPCCharacters>");
            
            foreach (var result in results)
            {
                for (int v = 0; v < Math.Min(result.Variants.Count, 1); v++)  // Only first variant for NPCs
                {
                    var key = MorphsToKeyString(result.Variants[v]);
                    float weight = 0.3f + (float)(new Random(result.Index).NextDouble() * 0.4);
                    float build = 0.3f + (float)(new Random(result.Index + 100).NextDouble() * 0.4);
                    string id = result.SourceImage != null 
                        ? Path.GetFileNameWithoutExtension(result.SourceImage).Replace(" ", "_").ToLower()
                        : $"generated_{result.Index:D3}";
                    
                    sb.AppendLine($"  <NPCCharacter id=\"fl_{id}\" name=\"Generated {result.Index}\" is_female=\"{result.IsFemale.ToString().ToLower()}\" default_group=\"Infantry\">");
                    sb.AppendLine($"    <face>");
                    sb.AppendLine($"      <BodyProperties version=\"4\" age=\"{result.Age:F1}\" weight=\"{weight:F2}\" build=\"{build:F2}\" key=\"{key}\" />");
                    sb.AppendLine($"    </face>");
                    sb.AppendLine($"  </NPCCharacter>");
                }
            }
            
            sb.AppendLine("</NPCCharacters>");
            File.WriteAllText(path, sb.ToString());
        }
        
        private void WriteHeroesFile(List<GeneratedFaceResult> results, string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<!-- Hero definitions - Generated by FaceLearner {DateTime.Now:yyyy-MM-dd} -->");
            sb.AppendLine("<!-- Copy these into your lords.xml or companions.xml -->");
            sb.AppendLine("<Heroes>");
            
            foreach (var result in results)
            {
                for (int v = 0; v < result.Variants.Count; v++)
                {
                    var key = MorphsToKeyString(result.Variants[v]);
                    float weight = 0.3f + (float)(new Random(result.Index * 10 + v).NextDouble() * 0.4);
                    float build = 0.3f + (float)(new Random(result.Index * 10 + v + 100).NextDouble() * 0.4);
                    string id = result.SourceImage != null 
                        ? Path.GetFileNameWithoutExtension(result.SourceImage).Replace(" ", "_").ToLower()
                        : $"generated_{result.Index:D3}";
                    
                    sb.AppendLine($"  <Hero id=\"fl_{id}_v{v + 1}\" faction=\"\" default_group=\"Infantry\" is_female=\"{result.IsFemale.ToString().ToLower()}\">");
                    sb.AppendLine($"    <face>");
                    sb.AppendLine($"      <BodyProperties version=\"4\" age=\"{result.Age:F1}\" weight=\"{weight:F2}\" build=\"{build:F2}\" key=\"{key}\" />");
                    sb.AppendLine($"    </face>");
                    sb.AppendLine($"    <!-- Add: culture, skills, traits, equipment as needed -->");
                    sb.AppendLine($"  </Hero>");
                }
            }
            
            sb.AppendLine("</Heroes>");
            File.WriteAllText(path, sb.ToString());
        }
        
        private string MorphsToKeyString(float[] morphs)
        {
            // Convert morphs to Bannerlord key string format
            // This is a simplified version - actual format uses 8 ulongs
            var sb = new System.Text.StringBuilder();
            
            // Pack morphs into hex string (simplified)
            for (int i = 0; i < Math.Min(morphs.Length, 62); i++)
            {
                // Convert -1..1 to 0..255
                int val = (int)((morphs[i] + 1f) * 127.5f);
                val = Math.Max(0, Math.Min(255, val));
                sb.Append(val.ToString("X2"));
            }
            
            // Pad to expected length
            while (sb.Length < 128)
            {
                sb.Append("00");
            }
            
            return sb.ToString();
        }
        
        private class GeneratedFaceResult
        {
            public int Index { get; set; }
            public string SourceImage { get; set; }
            public bool IsFemale { get; set; }
            public float Age { get; set; }
            public float Score { get; set; }
            public List<float[]> Variants { get; set; } = new List<float[]>();
        }
        
        #endregion
        
        #region Morph Tester
        
        private MorphTester _morphTester;
        
        public void StartMorphTest()
        {
            if (!_isInitialized || _faceController == null || _landmarkDetector == null)
            {
                SubModule.Log("[ML] Cannot start morph test: not initialized");
                return;
            }
            
            if (IsLearning)
            {
                SubModule.Log("[ML] Cannot start morph test: learning is active");
                return;
            }
            
            string outputDir = Path.Combine(_modulePath, "Data", "MorphTest");
            _morphTester = new MorphTester(_faceController, _landmarkDetector, outputDir);
            _morphTester.Start();
            
            SubModule.Log("[ML] Morph test started");
        }
        
        public void StopMorphTest()
        {
            if (_morphTester != null && _morphTester.IsRunning)
            {
                _morphTester.Stop();
                SubModule.Log("[ML] Morph test stopped");
            }
        }
        
        public bool IsMorphTestRunning => _morphTester?.IsRunning ?? false;
        
        public string MorphTestStatus => _morphTester?.Status ?? "Not running";
        
        public void OnMorphTestScreenshot(string imagePath)
        {
            _morphTester?.OnScreenshotCaptured(imagePath);
        }
        
        public bool TickMorphTest()
        {
            if (_morphTester == null || !_morphTester.IsRunning)
                return false;
            
            return _morphTester.Tick();
        }
        
        #endregion
    }
}
