using System;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace FaceLearner.Core
{
    /// <summary>
    /// Body Preview Screen - EXACT COPY of GauntletBannerBuilderScreen pattern
    /// </summary>
    [GameStateScreen(typeof(FaceLearnerState))]
    public class FaceLearnerScreen : ScreenBase, IGameStateListener
    {
        // UI
        private FaceLearnerVM _dataSource;
        private GauntletLayer _gauntletLayer;
        private GauntletMovieIdentifier _movie;
        
        // State
        private FaceLearnerState _state;
        private bool _isFinalized;
        
        // 3D Scene - EXACT same fields as BannerBuilderScreen
        private Camera _camera;
        private AgentVisuals _agentVisuals;
        private Scene _scene;
        private MBAgentRendererSceneController _agentRendererSceneController;
        private MatrixFrame _characterFrame;
        private bool _currentGenderIsFemale = false;  // Track current visual gender for recreation
        
        // Camera control - EXACT same as BannerBuilderScreen
        private float _cameraCurrentRotation;
        private float _cameraTargetRotation;
        private float _cameraCurrentDistanceAdder;
        private float _cameraTargetDistanceAdder;
        private float _cameraCurrentElevationAdder;
        private float _cameraTargetElevationAdder;
        
        // Character
        private BasicCharacterObject _character;
        private BodyProperties _currentBodyProperties;
        private BodyProperties _normalizedBodyProperties;  // Stored default state for reset
        private FaceGenerationParams _faceParams;
        private FaceGenerationParams _normalizedFaceParams;  // Stored default state for reset
        private FaceController _faceController;  // For ML integration
        private bool _firstCharacterRender = true;
        private bool _checkWhetherAgentVisualIsReady;
        private bool _isFaceZoomed = false;
        private int _numDeformKeys = 0;
        
        // Learning state
        private string _tempCapturePath;
        private bool _isLearningActive = false;
        private bool _captureWaitFrame = false;
        private int _learningWarmupFrames = 0;
        private const int WARMUP_FRAMES = 2;
        
        /// <summary>
        /// Get or create FaceController for ML integration.
        /// Syncs with current _faceParams state.
        /// </summary>
        public FaceController GetFaceController()
        {
            if (_faceController == null)
            {
                _faceController = new FaceController();
                FaceLearner.SubModule.Log("Created FaceController for ML integration");
            }
            
            // Sync FaceController from current state
            SyncFaceControllerFromParams();
            return _faceController;
        }
        
        /// <summary>
        /// Sync FaceController state FROM our _faceParams
        /// </summary>
        private void SyncFaceControllerFromParams()
        {
            if (_faceController == null) return;
            
            _faceController.IsFemale = _faceParams.CurrentGender == 1;
            _faceController.Age = _faceParams.CurrentAge;
            _faceController.Weight = _faceParams.CurrentWeight;
            _faceController.Build = _faceParams.CurrentBuild;
            _faceController.Height = _faceParams.HeightMultiplier;
            _faceController.Hair = _faceParams.CurrentHair;
            _faceController.Beard = _faceParams.CurrentBeard;
            _faceController.SkinColor = _faceParams.CurrentSkinColorOffset;
            _faceController.EyeColor = _faceParams.CurrentEyeColorOffset;
            _faceController.HairColor = _faceParams.CurrentHairColorOffset;
            
            // Sync morph values
            int numKeys = Math.Min(62, _numDeformKeys > 0 ? _numDeformKeys : 62);
            for (int i = 0; i < numKeys; i++)
            {
                _faceController.SetMorph(i, _faceParams.KeyWeights[i]);
            }
        }
        
        /// <summary>
        /// Apply FaceController state TO our _faceParams and character
        /// Call this after ML modifies the FaceController
        /// </summary>
        public void ApplyFaceControllerToCharacter()
        {
            if (_faceController == null) return;
            
            // Check if gender changed - requires full character recreation
            bool newGenderIsFemale = _faceController.IsFemale;
            bool genderChanged = (newGenderIsFemale != _currentGenderIsFemale);
            
            _faceParams.CurrentGender = _faceController.IsFemale ? 1 : 0;
            _faceParams.CurrentAge = MathF.Max(20f, _faceController.Age);  // Minimum 20 to avoid teenager scaling
            _faceParams.CurrentWeight = _faceController.Weight;
            _faceParams.CurrentBuild = _faceController.Build;
            _faceParams.HeightMultiplier = _faceController.Height;
            _faceParams.CurrentHair = _faceController.Hair;
            _faceParams.CurrentBeard = _faceController.Beard;
            _faceParams.CurrentSkinColorOffset = _faceController.SkinColor;
            _faceParams.CurrentEyeColorOffset = _faceController.EyeColor;
            _faceParams.CurrentHairColorOffset = _faceController.HairColor;
            
            // Sync morph values
            var morphs = _faceController.GetAllMorphs();
            for (int i = 0; i < morphs.Length; i++)
            {
                _faceParams.KeyWeights[i] = morphs[i];
            }
            
            // Update BodyProperties and refresh character
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            _currentBodyProperties = new BodyProperties(
                new DynamicBodyProperties(_faceParams.CurrentAge, _faceParams.CurrentWeight, _faceParams.CurrentBuild),
                _currentBodyProperties.StaticProperties
            );
            
            // Gender change requires full recreation of AgentVisuals (different skeleton)
            if (genderChanged)
            {
                FaceLearner.SubModule.Log($"Gender changed: {(_currentGenderIsFemale ? "Female" : "Male")} -> {(newGenderIsFemale ? "Female" : "Male")} - recreating character");
                _currentGenderIsFemale = newGenderIsFemale;
                RecreateCharacterWithGender(newGenderIsFemale);
            }
            else
            {
                RefreshCharacter();
            }
            
            _dataSource?.UpdateFromParams(_faceParams);
            
            // Re-freeze animation if we're in learning mode
            if (_isLearningActive)
            {
                FreezeAnimationOnly();
            }
        }
        
        /// <summary>
        /// Freeze animation and maintain frontal rotation (for use after refresh during learning)
        /// </summary>
        private void FreezeAnimationOnly()
        {
            // Maintain frontal rotation during learning (consistent with SetCharacterFrontal)
            float frontalRotation = 0f;
            _cameraTargetRotation = frontalRotation;
            _cameraCurrentRotation = frontalRotation;
            
            try
            {
                var skeleton = _agentVisuals?.GetVisuals()?.GetSkeleton();
                if (skeleton != null)
                {
                    var staticAction = ActionIndexCache.Create("act_character_developer_idle");
                    skeleton.SetAgentActionChannel(0, staticAction, forceFaceMorphRestart: true, blendPeriodOverride: 0f);
                    skeleton.Freeze(true);
                }
            }
            catch { }
        }
        
        /// <summary>
        /// Start the learning capture loop
        /// </summary>
        public void StartLearning()
        {
            FaceLearner.SubModule.Log("StartLearning: Beginning...");
            
            // Dump deform key data for reference (mapping of morph index to UI name and category)
            DumpDeformKeyData();
            
            // Setup temp capture path - use simple path in module folder
            // Engine adds "View_Output.png" to this base path
            // Save in Data/Temp folder to keep module root clean
            string tempDir = System.IO.Path.Combine(FaceLearner.SubModule.ModulePath, "Data", "Temp");
            System.IO.Directory.CreateDirectory(tempDir);  // Ensure folder exists
            _tempCapturePath = System.IO.Path.Combine(tempDir, "learning_capture_");
            FaceLearner.SubModule.Log($"StartLearning: Capture path: {_tempCapturePath}View_Output.png");
            
            // Set character frontal with face zoom and frozen animation
            SetCharacterFrontal();
            
            // NOW activate learning (after all setup)
            _learningWarmupFrames = WARMUP_FRAMES;
            _captureWaitFrame = false;
            _isLearningActive = true;
            
            FaceLearner.SubModule.Log("StartLearning: ACTIVE - waiting for first capture frame");
        }
        
        /// <summary>
        /// Set character/camera to frontal view for consistent captures
        /// Freezes animation - camera rotation handles the view angle
        /// </summary>
        private void SetCharacterFrontal()
        {
            // Camera rotation to see face frontally
            float frontalRotation = 0f;
            
            _cameraTargetRotation = frontalRotation;
            _cameraCurrentRotation = frontalRotation;
            
            // Ensure face zoom is active
            if (!_isFaceZoomed)
            {
                _isFaceZoomed = true;
                if (_dataSource != null) _dataSource.IsFaceZoomed = true;
            }
            UpdateCameraForZoomState();  // Sets elevation and distance for face zoom
            
            // Override rotation back to frontal (UpdateCameraForZoomState doesn't change it)
            _cameraTargetRotation = frontalRotation;
            _cameraCurrentRotation = frontalRotation;
            
            // Apply camera immediately
            if (_camera != null && SceneLayer != null)
            {
                UpdateCamera(0.1f);
            }
            
            // Freeze animation on static pose (no blink, no head movement)
            FreezeAnimationOnly();
            
            FaceLearner.SubModule.Log($"SetCharacterFrontal: rotation={frontalRotation:F3}, face zoom, animation frozen");
        }
        
        /// <summary>
        /// Unfreeze animation after capture
        /// </summary>
        private void UnfreezeAnimation()
        {
            try
            {
                var skeleton = _agentVisuals?.GetVisuals()?.GetSkeleton();
                if (skeleton != null)
                {
                    skeleton.Freeze(false);
                    // Resume normal idle animation
                    var idleAction = ActionIndexCache.Create("act_inventory_idle");
                    skeleton.SetAgentActionChannel(0, idleAction, forceFaceMorphRestart: true, blendPeriodOverride: 0f);
                }
            }
            catch { }
        }
        
        /// <summary>
        /// Reset character rotation to default (facing camera)
        /// </summary>
        private void ResetCharacterRotation()
        {
            _cameraTargetRotation = 0f;
            _cameraCurrentRotation = 0f;
            
            // Force immediate camera update to apply the reset
            // Use dt=1.0 to ensure lerp completes instantly
            if (_camera != null && SceneLayer != null)
            {
                UpdateCamera(1.0f);
            }
            
            FaceLearner.SubModule.Log($"Character rotation reset: target={_cameraTargetRotation:F3}, current={_cameraCurrentRotation:F3}");
        }
        
        /// <summary>
        /// Stop the learning capture loop
        /// </summary>
        public void StopLearning()
        {
            _isLearningActive = false;
            UnfreezeAnimation();
            ResetCharacterRotation();  // Reset rotation back to normal
            FaceLearner.SubModule.Log("Learning capture stopped");
        }
        
        /// <summary>
        /// Single capture test - saves SceneView to file
        /// </summary>
        public void Capture(string filePath)
        {
            try
            {
                var sceneView = SceneLayer?.SceneView;
                if (sceneView == null)
                {
                    FaceLearner.SubModule.Log("Capture: No SceneView");
                    return;
                }
                
                // Set frontal view with frozen animation
                SetCharacterFrontal();
                
                // Note: Engine appends "View_Output.png" to the base path
                // filePath is already the base path without extension from ExecuteCapture
                
                // Configure and trigger save
                sceneView.SetFilePathToSaveResult(filePath);
                sceneView.SetFileTypeToSave(View.TextureSaveFormat.TextureTypePng);
                sceneView.SetSaveFinalResultToDisk(true);
                
                // Note: Animation stays frozen until user rotates the character manually
            }
            catch (Exception ex)
            {
                FaceLearner.SubModule.Log($"Capture error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Called each frame during learning - captures and processes
        /// </summary>
        private void DoLearningTick()
        {
            if (!_isLearningActive) return;
            
            var addon = MLAddonRegistry.Addon;
            if (addon == null) 
            {
                _isLearningActive = false;
                return;
            }
            
            // Check if we're in morph test mode
            bool isMorphTest = addon.IsMorphTestRunning;
            bool isLearning = addon.IsLearning;
            
            if (!isLearning && !isMorphTest) 
            {
                FaceLearner.SubModule.Log("DoLearningTick: Neither learning nor morph testing, stopping");
                _isLearningActive = false;
                return;
            }
            
            // Wait for warmup frames after morph changes
            if (_learningWarmupFrames > 0)
            {
                _learningWarmupFrames--;
                return;  // Silent during warmup
            }
            
            // Capture using SceneView - saves exactly what's on screen with current zoom
            try
            {
                var sceneView = SceneLayer?.SceneView;
                if (sceneView == null) return;  // Silent fail, will retry next frame
                
                // Two-frame capture: first frame triggers save, second frame processes
                if (_captureWaitFrame)
                {
                    _captureWaitFrame = false;
                    
                    // Reset save flag
                    sceneView.SetSaveFinalResultToDisk(false);
                    
                    // Process the captured image (engine adds "View_Output.png" suffix)
                    string actualCapturePath = _tempCapturePath + "View_Output.png";
                    
                    // Only process if file exists (no logging on fail - just retry)
                    if (File.Exists(actualCapturePath))
                    {
                        if (isMorphTest)
                        {
                            // MORPH TEST MODE
                            // First: process the screenshot we just captured
                            addon.OnMorphTestScreenshot(actualCapturePath);
                            
                            // Then: tick to advance state machine (may set new morphs)
                            if (addon.TickMorphTest())
                            {
                                ApplyFaceControllerToCharacter();
                                _learningWarmupFrames = WARMUP_FRAMES;
                            }
                            
                            // Update status
                            _dataSource?.AddLogLine(addon.MorphTestStatus);
                            
                            // Check if test finished
                            if (!addon.IsMorphTestRunning)
                            {
                                _isLearningActive = false;
                                _dataSource?.AddLogLine("Morph test complete! See Data/MorphTest/morph_test_report.txt");
                                FaceLearner.SubModule.Log("DoLearningTick: Morph test complete");
                            }
                        }
                        else
                        {
                            // NORMAL LEARNING MODE
                            addon.ProcessRenderCapture(actualCapturePath);
                            
                            // Prepare next iteration (updates morphs on FaceController)
                            if (addon.PrepareNextIteration())
                            {
                                ApplyFaceControllerToCharacter();
                                _learningWarmupFrames = WARMUP_FRAMES;
                            }
                            
                            int iteration = addon.TotalIterations;
                            
                            // Update stats panel every 5 iterations (not every frame)
                            if (iteration % 5 == 0)
                            {
                                _dataSource?.RefreshStats();
                            }
                            
                            // Check if learning finished
                            if (!addon.IsLearning)
                            {
                                _isLearningActive = false;
                                _dataSource?.AddLogLine("Learning complete!");
                                _dataSource?.RefreshStats();  // Final refresh
                                FaceLearner.SubModule.Log("DoLearningTick: Learning complete");
                            }
                        }
                    }
                }
                else
                {
                    // BEFORE triggering capture, for MorphTest: run initial tick to set morphs
                    if (isMorphTest)
                    {
                        // Check if we need to set morphs before capturing
                        if (addon.TickMorphTest())
                        {
                            ApplyFaceControllerToCharacter();
                            _learningWarmupFrames = WARMUP_FRAMES;
                            return;  // Wait for warmup before capturing
                        }
                    }
                    
                    // Trigger capture - frontal rotation maintained
                    sceneView.SetFilePathToSaveResult(_tempCapturePath);
                    sceneView.SetFileTypeToSave(View.TextureSaveFormat.TextureTypePng);
                    sceneView.SetSaveFinalResultToDisk(true);
                    _captureWaitFrame = true;
                }
            }
            catch (Exception ex)
            {
                // Only log errors occasionally to avoid spam
                FaceLearner.SubModule.Log($"DoLearningTick ERROR: {ex.Message}");
            }
        }
        
        // SceneLayer as property - EXACT same as BannerBuilderScreen
        public SceneLayer SceneLayer { get; private set; }
        
        // Public access to refresh character with new body properties
        public BodyProperties CurrentBodyProperties => _currentBodyProperties;
        
        public void SetBodyProperties(BodyProperties newProps)
        {
            _currentBodyProperties = newProps;
            // Sync faceParams
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            RefreshCharacter();
        }
        
        // Reset character to normalized/default state (for tab switching)
        public void ResetToNormalized()
        {
            FaceLearner.SubModule.Log("Resetting character to normalized state");
            _currentBodyProperties = _normalizedBodyProperties;
            _faceParams = _normalizedFaceParams;
            
            // Reset camera to body view - set BOTH target and current for immediate effect
            _isFaceZoomed = false;
            _cameraTargetDistanceAdder = 3.5f;
            _cameraTargetElevationAdder = 1.0f;
            _cameraCurrentDistanceAdder = 3.5f;
            _cameraCurrentElevationAdder = 1.0f;
            
            // Reset character rotation to face camera directly
            ResetCharacterRotation();
            
            // Unfreeze animation if it was frozen
            UnfreezeAnimation();
            
            // Update VM
            _dataSource?.UpdateFromParams(_faceParams);
            _dataSource?.ResetZoom();
            
            RefreshCharacter();
        }
        
        // Face Zoom - toggle between body view and face view
        public void ToggleFaceZoom()
        {
            _isFaceZoomed = !_isFaceZoomed;
            if (_dataSource != null)
            {
                _dataSource.IsFaceZoomed = _isFaceZoomed;
            }
            
            UpdateCameraForZoomState();
        }
        
        private void UpdateCameraForZoomState()
        {
            // Get current height multiplier (0-1, where 0.5 is average)
            float heightMult = _faceParams.HeightMultiplier;
            
            if (_isFaceZoomed)
            {
                // Face zoom: need to see the full FACE including top of head
                // Distance: close (0.5-0.7 range based on height)
                _cameraTargetDistanceAdder = 0.5f + heightMult * 0.2f;
                
                // Elevation: needs to be high enough to see full head
                // Increased to capture forehead and hair
                float baseElevation = 1.8f;  // Higher to see full head
                float heightAdjustment = (heightMult - 0.5f) * 0.3f;
                _cameraTargetElevationAdder = baseElevation + heightAdjustment;
                
                FaceLearner.SubModule.Log($"Camera: Face Zoom ON - dist={_cameraTargetDistanceAdder:F2}, elev={_cameraTargetElevationAdder:F2} (height={heightMult:F2})");
            }
            else
            {
                // Body view: show full body
                _cameraTargetDistanceAdder = 3.5f;
                _cameraTargetElevationAdder = 1.0f;
                FaceLearner.SubModule.Log("Camera: Face Zoom OFF (Body view)");
            }
        }
        
        // Random morph - change a random deform key
        public void RandomizeMorph()
        {
            if (_numDeformKeys <= 0) return;
            
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            int keyIndex = MBRandom.RandomInt(_numDeformKeys);
            float randomValue = MBRandom.RandomFloat;
            
            // Get key info
            DeformKeyData keyData = MBBodyProperties.GetDeformKeyData(keyIndex, _faceParams.CurrentRace, _faceParams.CurrentGender, (int)_faceParams.CurrentAge);
            
            // Set random value within key's range
            float newValue = keyData.KeyMin + (keyData.KeyMax - keyData.KeyMin) * randomValue;
            _faceParams.KeyWeights[keyIndex] = newValue;
            
            // Apply changes
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        /// <summary>
        /// Generate a completely random face - all morphs, hair, beard, body props
        /// </summary>
        public void GenerateRandomFace()
        {
            FaceLearner.SubModule.Log("GenerateRandomFace called");
            if (_numDeformKeys <= 0) return;
            
            // Re-sync faceParams
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // Randomize ALL face morphs
            for (int i = 0; i < _numDeformKeys; i++)
            {
                DeformKeyData keyData = MBBodyProperties.GetDeformKeyData(i, _faceParams.CurrentRace, _faceParams.CurrentGender, (int)_faceParams.CurrentAge);
                float randomValue = MBRandom.RandomFloat;
                _faceParams.KeyWeights[i] = keyData.KeyMin + (keyData.KeyMax - keyData.KeyMin) * randomValue;
            }
            
            // Randomize hair and beard (use reasonable max values - game will clamp if needed)
            const int MAX_HAIR_STYLES = 30;
            const int MAX_BEARD_STYLES = 20;
            _faceParams.CurrentHair = MBRandom.RandomInt(MAX_HAIR_STYLES);
            _faceParams.CurrentBeard = _faceParams.CurrentGender == 0 ? MBRandom.RandomInt(MAX_BEARD_STYLES) : 0;
            
            // Randomize colors
            _faceParams.CurrentHairColorOffset = MBRandom.RandomFloat;
            _faceParams.CurrentEyeColorOffset = MBRandom.RandomFloat;
            _faceParams.CurrentSkinColorOffset = MBRandom.RandomFloat;
            
            // Randomize body proportions slightly
            _faceParams.HeightMultiplier = 0.3f + MBRandom.RandomFloat * 0.4f;  // 0.3 - 0.7 range
            
            // Apply changes
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            
            // Randomize age/weight/build
            _currentBodyProperties = new BodyProperties(
                new DynamicBodyProperties(
                    20f + MBRandom.RandomFloat * 50f,  // Age: 20-70
                    MBRandom.RandomFloat,               // Weight: 0-1
                    MBRandom.RandomFloat                // Build: 0-1
                ),
                _currentBodyProperties.StaticProperties
            );
            
            RefreshCharacter();
            
            // Update VM
            if (_dataSource != null)
            {
                _dataSource.SetInitialValues(_currentBodyProperties.Age, _currentBodyProperties.Weight, _currentBodyProperties.Build, _faceParams.HeightMultiplier);
                _dataSource.AddLogLine("Generated random face");
            }
            
            // Sync all morph sliders to new values
            SyncMorphValuesToVM();
            
            FaceLearner.SubModule.Log($"GenerateRandomFace: Done (hair={_faceParams.CurrentHair}, beard={_faceParams.CurrentBeard})");
        }
        
        /// <summary>
        /// Reset character to default
        /// </summary>
        public void ResetCharacter()
        {
            FaceLearner.SubModule.Log("ResetCharacter called");
            
            // Get default body properties
            _currentBodyProperties = _character?.GetBodyPropertiesMax() ?? BodyProperties.Default;
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            _numDeformKeys = MBBodyProperties.GetNumEditableDeformKeys(_faceParams.CurrentRace, _faceParams.CurrentGender == 1, (int)_faceParams.CurrentAge);
            
            RefreshCharacter();
            
            // Update VM
            _dataSource?.SetInitialValues(_currentBodyProperties.Age, _currentBodyProperties.Weight, _currentBodyProperties.Build, _faceParams.HeightMultiplier);
            _dataSource?.AddLogLine("Character reset to default");
            
            // Sync all morph sliders
            SyncMorphValuesToVM();
        }
        
        /// <summary>
        /// Dump all DeformKeyData to a file for analysis.
        /// This reveals the exact mapping of morph index to UI name and category (GroupId).
        /// </summary>
        public void DumpDeformKeyData()
        {
            FaceLearner.SubModule.Log("DumpDeformKeyData called");
            
            try
            {
                string outputDir = System.IO.Path.Combine(BasePath.Name, "Modules", "FaceLearner", "Data");
                Directory.CreateDirectory(outputDir);
                string outputPath = System.IO.Path.Combine(outputDir, "deform_key_data.csv");
                
                int race = _faceParams.CurrentRace;
                int gender = _faceParams.CurrentGender;
                int age = (int)_faceParams.CurrentAge;
                
                using (var writer = new StreamWriter(outputPath))
                {
                    writer.WriteLine("Index,Id,GroupId,GroupName,KeyMin,KeyMax,DefaultValue,KeyTimePoint");
                    
                    for (int i = 0; i < _numDeformKeys; i++)
                    {
                        DeformKeyData keyData = MBBodyProperties.GetDeformKeyData(i, race, gender, age);
                        
                        // Map GroupId to tab name
                        string groupName = keyData.GroupId switch
                        {
                            0 => "Body",
                            1 => "Face",
                            2 => "Eyes",
                            3 => "Nose",
                            4 => "Mouth",
                            5 => "Hair",
                            6 => "Taint",
                            _ => "Unknown"
                        };
                        
                        writer.WriteLine($"{i},{keyData.Id},{keyData.GroupId},{groupName},{keyData.KeyMin:F2},{keyData.KeyMax:F2},{keyData.Value:F2},{keyData.KeyTimePoint}");
                    }
                }
                
                FaceLearner.SubModule.Log($"DeformKeyData dumped to: {outputPath}");
                _dataSource?.AddLogLine($"Dumped {_numDeformKeys} deform keys to deform_key_data.csv");
            }
            catch (Exception ex)
            {
                FaceLearner.SubModule.Log($"DumpDeformKeyData error: {ex.Message}");
                _dataSource?.AddLogLine($"Error: {ex.Message}");
            }
        }
        
        // Change specific morph key
        public void ChangeMorphKey(int keyIndex, float delta)
        {
            if (keyIndex < 0 || keyIndex >= _numDeformKeys) return;
            
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            float currentValue = _faceParams.KeyWeights[keyIndex];
            
            // Allow extended range like learning system (-1 to 5)
            float newValue = MathF.Clamp(currentValue + delta, -1f, 5f);
            _faceParams.KeyWeights[keyIndex] = newValue;
            
            // Apply changes
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
            
            FaceLearner.SubModule.Log($"MorphKey {keyIndex}: {currentValue:F2} -> {newValue:F2}");
        }
        
        private void RefreshCharacter()
        {
            if (_agentVisuals == null || _character == null) return;
            
            // Empty equipment to see body
            Equipment equipment = new Equipment();
            
            AgentVisualsData data = _agentVisuals.GetCopyAgentVisualsData();
            data.BodyProperties(_currentBodyProperties)
                .Equipment(equipment)
                .Frame(_characterFrame);
            
            _agentVisuals.Refresh(needBatchedVersionForWeaponMeshes: false, data, forceUseFaceCache: true);
            _agentVisuals.GetEntity().CheckResources(addToQueue: true, checkFaceResources: true);
            _agentVisuals.GetVisuals().GetSkeleton().TickAnimationsAndForceUpdate(0.001f, _characterFrame, tickAnimsForChildren: true);
            
            // Force visibility update
            _agentVisuals.SetVisible(false);
            _agentVisuals.SetVisible(true);
            _checkWhetherAgentVisualIsReady = true;
            
            // Update DNA code in UI
            if (_dataSource != null)
            {
                _dataSource.PreviewCode = _currentBodyProperties.ToString();
            }
        }
        
        /// <summary>
        /// Recreate character with different gender (different skeleton required)
        /// </summary>
        private void RecreateCharacterWithGender(bool isFemale)
        {
            if (_character == null || _scene == null) return;
            
            // Dispose old visuals
            if (_agentVisuals != null)
            {
                _agentVisuals.Reset();
                _agentVisuals = null;
            }
            
            // Create new visuals with correct gender
            Equipment equipment = new Equipment();
            Monster baseMonsterFromRace = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(_character.Race);
            
            ActionIndexCache action = ActionIndexCache.act_inventory_idle_start;
            
            _agentVisuals = AgentVisuals.Create(
                new AgentVisualsData()
                    .Equipment(equipment)
                    .BodyProperties(_currentBodyProperties)
                    .Frame(_characterFrame)
                    .ActionSet(MBGlobals.GetActionSetWithSuffix(baseMonsterFromRace, isFemale, "_facegen"))
                    .ActionCode(action)
                    .Scene(_scene)
                    .Monster(baseMonsterFromRace)
                    .SkeletonType(isFemale ? SkeletonType.Female : SkeletonType.Male)
                    .Race(_character.Race)
                    .PrepareImmediately(prepareImmediately: true)
                    .UseMorphAnims(useMorphAnims: true),
                "BodyPreviewChar",
                isRandomProgress: false,
                needBatchedVersionForWeaponMeshes: false,
                forceUseFaceCache: true);
            
            _agentVisuals.SetAgentLodZeroOrMaxExternal(makeZero: true);
            _agentVisuals.Refresh(needBatchedVersionForWeaponMeshes: false, _agentVisuals.GetCopyAgentVisualsData(), forceUseFaceCache: true);
            _agentVisuals.SetVisible(value: true);
            _agentVisuals.GetEntity().CheckResources(addToQueue: true, checkFaceResources: true);
            _checkWhetherAgentVisualIsReady = true;
            
            FaceLearner.SubModule.Log($"RecreateCharacterWithGender: Created {(isFemale ? "Female" : "Male")} character");
        }

        public FaceLearnerScreen(FaceLearnerState state)
        {
            _state = state;
            _character = MBObjectManager.Instance.GetObject<BasicCharacterObject>("main_hero");
            FaceLearner.SubModule.Log($"Screen: Constructor, character={_character?.StringId}");
        }

        // EXACT COPY of BannerBuilderScreen.OnInitialize
        protected override void OnInitialize()
        {
            base.OnInitialize();
            FaceLearner.SubModule.Log("Screen: OnInitialize");
            
            // Load sprite categories BEFORE brushes (like FaceLearner)
            try
            {
                var spriteData = UIResourceManager.SpriteData;
                var resourceContext = UIResourceManager.ResourceContext;
                var resourceDepot = UIResourceManager.ResourceDepot;
                
                // Load clan sprites (for Clan\header, Clan\panel_header, etc.)
                if (spriteData.SpriteCategories.ContainsKey("ui_clan"))
                {
                    var clanCategory = spriteData.SpriteCategories["ui_clan"];
                    clanCategory.Load(resourceContext, resourceDepot);
                    FaceLearner.SubModule.Log("Loaded sprite category: ui_clan");
                }
                
                // Load inventory sprites (for panels, buttons)
                if (spriteData.SpriteCategories.ContainsKey("ui_inventory"))
                {
                    var inventoryCategory = spriteData.SpriteCategories["ui_inventory"];
                    inventoryCategory.Load(resourceContext, resourceDepot);
                    FaceLearner.SubModule.Log("Loaded sprite category: ui_inventory");
                }
            }
            catch (Exception ex)
            {
                FaceLearner.SubModule.Log($"Warning: Could not load sprite categories: {ex.Message}");
            }
            
            // Load custom brushes BEFORE movie
            try
            {
                UIResourceManager.BrushFactory.LoadBrushFile("FaceLearnerBrushes");
                FaceLearner.SubModule.Log("Loaded FaceLearnerBrushes");
            }
            catch (Exception ex)
            {
                FaceLearner.SubModule.Log($"Warning: Could not load brushes: {ex.Message}");
            }
            
            // Create ViewModel with callbacks
            _dataSource = new FaceLearnerVM(
                onExit: Exit,
                onFaceZoom: ToggleFaceZoom,
                onRandomMorph: RandomizeMorph,
                onAgeChanged: OnAgeSliderChanged,
                onWeightChanged: OnWeightSliderChanged,
                onBuildChanged: OnBuildSliderChanged,
                onHeightChanged: OnHeightSliderChanged,
                onMorphChanged: OnMorphSliderChanged,
                onPageChanged: OnPageChanged,
                onHairChanged: OnHairChanged,
                onBeardChanged: OnBeardChanged,
                onGenderChanged: OnGenderChanged,
                onLoadCode: OnLoadCode,
                onCopyCode: OnCopyCode,
                onApplyToGame: OnApplyToGame,
                onGenerateFace: GenerateRandomFace,
                onResetCharacter: ResetCharacter,
                onHairColorChanged: OnHairColorChanged,
                onEyeColorChanged: OnEyeColorChanged,
                onSkinToneChanged: OnSkinToneChanged,
                getFaceController: GetFaceController,
                applyFaceController: ApplyFaceControllerToCharacter,
                applyMorphsBatch: ApplyMorphsBatch,
                onStartLearning: StartLearning,
                onStopLearning: StopLearning,
                onCapture: Capture
            );
            
            // GauntletLayer FIRST - exact same as BannerBuilderScreen
            _gauntletLayer = new GauntletLayer("BodyPreview", 100);
            _gauntletLayer.IsFocusLayer = true;
            AddLayer(_gauntletLayer);
            _gauntletLayer.InputRestrictions.SetInputRestrictions();
            ScreenManager.TrySetFocus(_gauntletLayer);
            _movie = _gauntletLayer.LoadMovie("FaceLearnerScreen", _dataSource);
            _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("FaceGenHotkeyCategory"));
            
            // CreateScene - creates SceneLayer internally
            CreateScene();
            
            // Initialize VM with character's initial values
            _dataSource.SetInitialValues(_currentBodyProperties.Age, _currentBodyProperties.Weight, _currentBodyProperties.Build, _faceParams.HeightMultiplier);
            _dataSource.PreviewCode = _currentBodyProperties.ToString();
            
            // Sync morph values from character
            SyncMorphValuesToVM();
            
            // Add SceneLayer AFTER GauntletLayer - exact same order as BannerBuilderScreen
            AddLayer(SceneLayer);
            
            _checkWhetherAgentVisualIsReady = true;
            _firstCharacterRender = true;
            
            FaceLearner.SubModule.Log("Screen: OnInitialize done");
        }

        // EXACT COPY of BannerBuilderScreen.CreateScene but with inventory_character_scene
        private void CreateScene()
        {
            FaceLearner.SubModule.Log("CreateScene: Starting...");
            
            _scene = Scene.CreateNewScene(initialize_physics: true, enable_decals: false, DecalAtlasGroup.All);
            _scene.SetName("BodyPreviewScreen");
            _scene.DisableStaticShadows(value: true);
            _scene.SetClothSimulationState(state: true);
            
            SceneInitializationData initData = new SceneInitializationData(initializeWithDefaults: true)
            {
                InitPhysicsWorld = false,
                DoNotUseLoadingScreen = true
            };
            
            // Try inventory_character_scene first (cleaner, no banner)
            try
            {
                _scene.Read("inventory_character_scene", ref initData);
                FaceLearner.SubModule.Log("CreateScene: Loaded inventory_character_scene");
            }
            catch
            {
                // Fallback to banner_editor_scene
                _scene.Read("banner_editor_scene", ref initData);
                FaceLearner.SubModule.Log("CreateScene: Fallback to banner_editor_scene");
            }
            
            _scene.SetShadow(shadowEnabled: true);
            
            _agentRendererSceneController = MBAgentRendererSceneController.CreateNewAgentRendererSceneController(_scene);
            
            float aspectRatio = Screen.AspectRatio;
            
            // Try inventory spawn point first, then banner scene spawn point
            GameEntity gameEntity = _scene.FindEntityWithTag("agent_inv");
            if (gameEntity == null)
            {
                gameEntity = _scene.FindEntityWithTag("spawnpoint_player");
            }
            
            if (gameEntity != null)
            {
                _characterFrame = gameEntity.GetGlobalFrame();
                FaceLearner.SubModule.Log($"CreateScene: Found spawn point at {_characterFrame.origin}");
            }
            else
            {
                _characterFrame = MatrixFrame.Identity;
                FaceLearner.SubModule.Log("CreateScene: Using default spawn frame");
            }
            _characterFrame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            
            // Camera setup - centered body view with space for footer
            _cameraTargetDistanceAdder = 3.5f;
            _cameraCurrentDistanceAdder = _cameraTargetDistanceAdder;
            _cameraTargetElevationAdder = 1.0f;  // Slightly lower = character appears higher with feet visible
            _cameraCurrentElevationAdder = _cameraTargetElevationAdder;
            
            // No rotation offset
            _cameraTargetRotation = 0f;
            _cameraCurrentRotation = 0f;
            
            _camera = Camera.CreateCamera();
            _camera.SetFovVertical(0.6981317f, aspectRatio, 0.2f, 200f);
            
            // SceneLayer setup - EXACT same as BannerBuilderScreen
            SceneLayer = new SceneLayer();
            SceneLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("FaceGenHotkeyCategory"));
            SceneLayer.SetScene(_scene);
            UpdateCamera(0f);
            SceneLayer.SetSceneUsesShadows(value: true);
            SceneLayer.SceneView.SetResolutionScaling(value: true);
            int num = -1;
            num &= -5;
            SceneLayer.SetPostfxConfigParams(num);
            
            FaceLearner.SubModule.Log("CreateScene: SceneLayer configured");
            
            // Create character
            AddCharacterEntity();
            
            FaceLearner.SubModule.Log("CreateScene: Done");
        }

        private void AddCharacterEntity()
        {
            if (_character == null)
            {
                FaceLearner.SubModule.Log("AddCharacterEntity: No character!");
                return;
            }
            
            FaceLearner.SubModule.Log($"AddCharacterEntity: Creating {_character.StringId}");
            
            // EMPTY equipment so we can see the body!
            Equipment equipment = new Equipment();
            Monster baseMonsterFromRace = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(_character.Race);
            
            // Store initial body properties
            _currentBodyProperties = _character.GetBodyProperties(equipment);
            
            // Initialize FaceGenerationParams
            _faceParams = FaceGenerationParams.Create();
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // === CREATE NORMALIZED CHARACTER ===
            // Remove hair and beard for clean face learning
            _faceParams.CurrentHair = 0;
            _faceParams.CurrentBeard = 0;
            
            // Set normalized body parameters (middle values)
            _faceParams.CurrentAge = 25f;        // Young adult
            _faceParams.CurrentWeight = 0.5f;    // Average weight
            _faceParams.CurrentBuild = 0.5f;     // Average build
            _faceParams.HeightMultiplier = 0.5f; // Average height
            
            // Set normalized color values (middle of preset ranges)
            // Hair: index 2 (Brown) = 2/10 = 0.2
            // Eyes: index 3 (Green) = 3/7 = 0.43
            // Skin: middle value
            _faceParams.CurrentHairColorOffset = 0.2f;   // Brown
            _faceParams.CurrentEyeColorOffset = 0.43f;   // Green
            _faceParams.CurrentSkinColorOffset = 0.5f;   // Medium
            
            // Apply normalized params to body properties
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            
            // Save normalized state for reset functionality
            _normalizedBodyProperties = _currentBodyProperties;
            _normalizedFaceParams = _faceParams;  // struct copy
            
            // Update VM with normalized values
            _dataSource?.UpdateFromParams(_faceParams);
            
            FaceLearner.SubModule.Log($"Normalized Character: hair={_faceParams.CurrentHair}, beard={_faceParams.CurrentBeard}");
            FaceLearner.SubModule.Log($"Body: age={_faceParams.CurrentAge}, weight={_faceParams.CurrentWeight}, build={_faceParams.CurrentBuild}, height={_faceParams.HeightMultiplier}");
            
            // Get number of editable deform keys
            _numDeformKeys = MBBodyProperties.GetNumEditableDeformKeys(_faceParams.CurrentRace, _faceParams.CurrentGender == 1, (int)_faceParams.CurrentAge);
            
            FaceLearner.SubModule.Log($"FaceParams: race={_faceParams.CurrentRace}, gender={_faceParams.CurrentGender}, numDeformKeys={_numDeformKeys}");
            
            ActionIndexCache action = ActionIndexCache.act_inventory_idle_start;
            
            // Initialize gender tracking with main_hero's gender
            _currentGenderIsFemale = _character.IsFemale;
            
            _agentVisuals = AgentVisuals.Create(
                new AgentVisualsData()
                    .Equipment(equipment)
                    .BodyProperties(_currentBodyProperties)  // Use our normalized body properties
                    .Frame(_characterFrame)
                    .ActionSet(MBGlobals.GetActionSetWithSuffix(baseMonsterFromRace, _currentGenderIsFemale, "_facegen"))
                    .ActionCode(action)
                    .Scene(_scene)
                    .Monster(baseMonsterFromRace)
                    .SkeletonType(_currentGenderIsFemale ? SkeletonType.Female : SkeletonType.Male)
                    .Race(_character.Race)
                    .PrepareImmediately(prepareImmediately: true)
                    .UseMorphAnims(useMorphAnims: true),
                "BodyPreviewChar",
                isRandomProgress: false,
                needBatchedVersionForWeaponMeshes: false,
                forceUseFaceCache: true);
            
            _agentVisuals.SetAgentLodZeroOrMaxExternal(makeZero: true);
            _agentVisuals.Refresh(needBatchedVersionForWeaponMeshes: false, _agentVisuals.GetCopyAgentVisualsData(), forceUseFaceCache: true);
            _agentVisuals.SetVisible(value: false);
            _agentVisuals.GetEntity().CheckResources(addToQueue: true, checkFaceResources: true);
            
            FaceLearner.SubModule.Log("AddCharacterEntity: Done with normalized character");
        }

        // EXACT COPY of BannerBuilderScreen.OnFrameTick
        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            
            if (_isFinalized)
            {
                return;
            }
            
            HandleUserInput(dt);
            
            if (_isFinalized)
            {
                return;
            }
            
            UpdateCamera(dt);
            
            SceneLayer sceneLayer = SceneLayer;
            if (sceneLayer != null && sceneLayer.ReadyToRender())
            {
                LoadingWindow.DisableGlobalLoadingWindow();
            }
            
            _scene?.Tick(dt);
            
            // Character visibility check - simplified from BannerBuilderScreen
            if (_checkWhetherAgentVisualIsReady && _agentVisuals != null)
            {
                if (_agentVisuals.GetEntity().CheckResources(_firstCharacterRender, checkFaceResources: true))
                {
                    _agentVisuals.SetVisible(value: true);
                    _checkWhetherAgentVisualIsReady = false;
                    _firstCharacterRender = false;
                    // Note: Removed spam log "Character now visible!" - fires on every morph update
                }
            }
            
            // Learning loop
            if (_isLearningActive)
            {
                DoLearningTick();
            }
        }

        // Camera update with smooth interpolation
        private void UpdateCamera(float dt)
        {
            float amount = MathF.Min(1f, 10f * dt);
            _cameraCurrentRotation = MathF.AngleLerp(_cameraCurrentRotation, _cameraTargetRotation, amount);
            _cameraCurrentElevationAdder = MathF.Lerp(_cameraCurrentElevationAdder, _cameraTargetElevationAdder, amount);
            _cameraCurrentDistanceAdder = MathF.Lerp(_cameraCurrentDistanceAdder, _cameraTargetDistanceAdder, amount);
            
            MatrixFrame characterFrame = _characterFrame;
            characterFrame.rotation.RotateAboutUp(_cameraCurrentRotation);
            
            // Elevation (up) + distance (forward)
            characterFrame.origin += _cameraCurrentElevationAdder * characterFrame.rotation.u + _cameraCurrentDistanceAdder * characterFrame.rotation.f;
            
            // Horizontal offset: scale with distance so face zoom stays centered
            // At body view (dist ~3.5) we want offset -0.7f
            // At face zoom (dist ~0.6) we want proportionally less offset
            float baseOffset = -0.7f;
            float distanceRatio = _cameraCurrentDistanceAdder / 3.5f;
            float horizontalOffset = baseOffset * distanceRatio;
            characterFrame.origin += horizontalOffset * characterFrame.rotation.s;
            
            characterFrame.rotation.RotateAboutSide(-(float)Math.PI / 2f);
            characterFrame.rotation.RotateAboutUp((float)Math.PI);
            characterFrame.rotation.RotateAboutForward((float)Math.PI * 3f / 50f);
            
            _camera.Frame = characterFrame;
            SceneLayer.SetCamera(_camera);
        }

        // Simplified HandleUserInput - sliders handle parameter changes
        private void HandleUserInput(float dt)
        {
            // Exit on ESC
            if (_gauntletLayer.Input.IsHotKeyReleased("Exit") || SceneLayer.Input.IsHotKeyReleased("Exit"))
            {
                Exit();
                return;
            }
            
            // Focus switching - EXACT same as BannerBuilderScreen
            if (SceneLayer.IsHitThisFrame && ScreenManager.FocusedLayer == _gauntletLayer)
            {
                _gauntletLayer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_gauntletLayer);
                SceneLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(SceneLayer);
            }
            else if (!SceneLayer.IsHitThisFrame && ScreenManager.FocusedLayer == SceneLayer)
            {
                SceneLayer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(SceneLayer);
                _gauntletLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_gauntletLayer);
            }
            
            // Mouse visibility
            _gauntletLayer.InputRestrictions.SetMouseVisibility(isVisible: true);
        }
        
        private void ChangeAge(float delta)
        {
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            float newAge = MathF.Clamp(_faceParams.CurrentAge + delta, 20f, 90f);
            _faceParams.CurrentAge = newAge;
            
            // Re-get number of deform keys (can change with age)
            _numDeformKeys = MBBodyProperties.GetNumEditableDeformKeys(_faceParams.CurrentRace, _faceParams.CurrentGender == 1, (int)newAge);
            
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
            
            FaceLearner.SubModule.Log($"ChangeAge: {_currentBodyProperties.Age:F1} (numDeformKeys now: {_numDeformKeys})");
        }
        
        private void ChangeWeight(float delta)
        {
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            float newWeight = MathF.Clamp(_faceParams.CurrentWeight + delta, -1f, 5f);
            _faceParams.CurrentWeight = newWeight;
            
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
            
            FaceLearner.SubModule.Log($"ChangeWeight: {_currentBodyProperties.Weight:F2}");
        }
        
        private void ChangeBuild(float delta)
        {
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            float newBuild = MathF.Clamp(_faceParams.CurrentBuild + delta, -1f, 5f);
            _faceParams.CurrentBuild = newBuild;
            
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
            
            FaceLearner.SubModule.Log($"ChangeBuild: {_currentBodyProperties.Build:F2}");
        }
        
        // Tab change callback - resets character to normalized state
        private void OnPageChanged()
        {
            FaceLearner.SubModule.Log("OnPageChanged: Resetting to normalized state");
            
            // Stop learning if it was active
            if (_isLearningActive)
            {
                StopLearning();
            }
            
            ResetToNormalized();
        }
        
        // Slider callbacks (called from VM when slider changes)
        private void OnAgeSliderChanged(float newAge)
        {
            newAge = MathF.Clamp(newAge, 20f, 90f);
            FaceLearner.SubModule.Log($"OnAgeSliderChanged: {newAge}");
            
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            _faceParams.CurrentAge = newAge;
            _numDeformKeys = MBBodyProperties.GetNumEditableDeformKeys(_faceParams.CurrentRace, _faceParams.CurrentGender == 1, (int)newAge);
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        private void OnWeightSliderChanged(float newWeight)
        {
            newWeight = MathF.Clamp(newWeight, -1f, 5f);
            FaceLearner.SubModule.Log($"OnWeightSliderChanged: {newWeight:F2} (was {_faceParams.CurrentWeight:F2})");
            
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // Now apply the change
            _faceParams.CurrentWeight = newWeight;
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            FaceLearner.SubModule.Log($"  After ProduceNumericKey: BodyProps.Weight={_currentBodyProperties.Weight:F2}");
            RefreshCharacter();
        }
        
        private void OnBuildSliderChanged(float newBuild)
        {
            newBuild = MathF.Clamp(newBuild, -1f, 5f);
            FaceLearner.SubModule.Log($"OnBuildSliderChanged: {newBuild:F2} (was {_faceParams.CurrentBuild:F2})");
            
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // Now apply the change
            _faceParams.CurrentBuild = newBuild;
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            FaceLearner.SubModule.Log($"  After ProduceNumericKey: BodyProps.Build={_currentBodyProperties.Build:F2}");
            RefreshCharacter();
        }
        
        private void OnHeightSliderChanged(float newHeight)
        {
            newHeight = MathF.Clamp(newHeight, -1f, 5f);
            FaceLearner.SubModule.Log($"OnHeightSliderChanged: {newHeight:F2} (was {_faceParams.HeightMultiplier:F2})");
            
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // Now apply the change
            _faceParams.HeightMultiplier = newHeight;
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
            
            // Update camera if in face zoom mode (face position changes with height)
            if (_isFaceZoomed)
            {
                UpdateCameraForZoomState();
            }
        }
        
        /// <summary>
        /// Sync all morph values from current character to VM sliders
        /// </summary>
        private void SyncMorphValuesToVM()
        {
            if (_dataSource == null || _numDeformKeys <= 0) return;
            
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            for (int i = 0; i < _numDeformKeys && i < _faceParams.KeyWeights.Length; i++)
            {
                _dataSource.UpdateMorphValue(i, _faceParams.KeyWeights[i]);
            }
            
            // Also update hair/beard
            _dataSource.HairStyleText = _faceParams.CurrentHair.ToString();
            _dataSource.BeardStyleText = _faceParams.CurrentBeard.ToString();
            _dataSource.GenderText = _faceParams.CurrentGender == 1 ? "Female" : "Male";
            
            FaceLearner.SubModule.Log($"SyncMorphValuesToVM: Synced {_numDeformKeys} morph values");
        }
        
        private void OnMorphSliderChanged(int keyIndex, float newValue)
        {
            // Avoid logging in batch operations
            if (keyIndex < 0 || keyIndex >= _numDeformKeys) return;
            
            // Re-sync faceParams from current body properties first
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // Allow extended range like learning system (-1 to 5)
            newValue = MathF.Clamp(newValue, -1f, 5f);
            _faceParams.KeyWeights[keyIndex] = newValue;
            
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        /// <summary>
        /// Apply all morphs at once - much faster than individual calls
        /// </summary>
        public void ApplyMorphsBatch(float[] morphs)
        {
            if (morphs == null || morphs.Length == 0) return;
            
            FaceLearner.SubModule.Log($"ApplyMorphsBatch: Applying {morphs.Length} morphs");
            
            // Re-sync faceParams once
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // Apply all morphs
            int count = Math.Min(morphs.Length, _numDeformKeys);
            for (int i = 0; i < count; i++)
            {
                _faceParams.KeyWeights[i] = MathF.Clamp(morphs[i], -1f, 5f);
            }
            
            // Produce body properties once
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            
            // Refresh character once
            RefreshCharacter();
            
            // Sync sliders to new values
            SyncMorphValuesToVM();
            
            FaceLearner.SubModule.Log("ApplyMorphsBatch: Done");
        }
        
        private void OnHairChanged(int hairIndex)
        {
            FaceLearner.SubModule.Log($"OnHairChanged: {hairIndex}");
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // Clamp to reasonable range (game will handle if out of bounds)
            _faceParams.CurrentHair = Math.Max(0, hairIndex);
            
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        private void OnBeardChanged(int beardIndex)
        {
            FaceLearner.SubModule.Log($"OnBeardChanged: {beardIndex}");
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            // Clamp to reasonable range (game will handle if out of bounds)
            _faceParams.CurrentBeard = Math.Max(0, beardIndex);
            
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        private void OnGenderChanged(bool isFemale)
        {
            FaceLearner.SubModule.Log($"OnGenderChanged: {(isFemale ? "Female" : "Male")}");
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            
            _faceParams.CurrentGender = isFemale ? 1 : 0;
            _faceParams.CurrentHair = 0;  // Reset hair for new gender
            _faceParams.CurrentBeard = 0;  // Reset beard
            
            // Update deform key count for new gender
            _numDeformKeys = MBBodyProperties.GetNumEditableDeformKeys(_faceParams.CurrentRace, isFemale, (int)_faceParams.CurrentAge);
            
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        private void OnHairColorChanged(float offset)
        {
            FaceLearner.SubModule.Log($"OnHairColorChanged: {offset:F2}");
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            _faceParams.CurrentHairColorOffset = MathF.Clamp(offset, -1f, 5f);
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        private void OnEyeColorChanged(float offset)
        {
            FaceLearner.SubModule.Log($"OnEyeColorChanged: {offset:F2}");
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            _faceParams.CurrentEyeColorOffset = MathF.Clamp(offset, -1f, 5f);
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        private void OnSkinToneChanged(float offset)
        {
            FaceLearner.SubModule.Log($"OnSkinToneChanged: {offset:F2}");
            MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
            _faceParams.CurrentSkinColorOffset = MathF.Clamp(offset, -1f, 5f);
            MBBodyProperties.ProduceNumericKeyWithParams(_faceParams, false, false, ref _currentBodyProperties);
            RefreshCharacter();
        }
        
        private void OnLoadCode(string code)
        {
            FaceLearner.SubModule.Log($"OnLoadCode: {code?.Substring(0, Math.Min(code?.Length ?? 0, 40))}...");
            if (string.IsNullOrEmpty(code)) return;
            
            try
            {
                // Parse the DNA code and apply to character
                BodyProperties parsed = BodyProperties.Default;
                if (BodyProperties.FromString(code, out parsed))
                {
                    _currentBodyProperties = parsed;
                    MBBodyProperties.GetParamsFromKey(ref _faceParams, _currentBodyProperties, false, false);
                    _numDeformKeys = MBBodyProperties.GetNumEditableDeformKeys(_faceParams.CurrentRace, _faceParams.CurrentGender == 1, (int)_faceParams.CurrentAge);
                    RefreshCharacter();
                    
                    // Update VM with new values
                    _dataSource?.SetInitialValues(_currentBodyProperties.Age, _currentBodyProperties.Weight, _currentBodyProperties.Build, _faceParams.HeightMultiplier);
                    
                    // Sync all morph sliders
                    SyncMorphValuesToVM();
                    
                    FaceLearner.SubModule.Log("DNA code loaded successfully!");
                }
                else
                {
                    FaceLearner.SubModule.Log("Failed to parse DNA code!");
                }
            }
            catch (Exception ex)
            {
                FaceLearner.SubModule.Log($"Error loading DNA code: {ex.Message}");
            }
        }
        
        private void OnCopyCode()
        {
            string code = _currentBodyProperties.ToString();
            FaceLearner.SubModule.Log($"OnCopyCode: {code.Substring(0, Math.Min(code.Length, 40))}...");
            
            // Copy to clipboard using Input
            try
            {
                Input.SetClipboardText(code);
                FaceLearner.SubModule.Log("DNA code copied to clipboard!");
                
                // Update PreviewCode in VM
                _dataSource.PreviewCode = code;
            }
            catch (Exception ex)
            {
                FaceLearner.SubModule.Log($"Error copying to clipboard: {ex.Message}");
            }
        }
        
        private void OnApplyToGame()
        {
            FaceLearner.SubModule.Log("OnApplyToGame called");
            
            // In preview mode, we can't apply to an actual game character
            // But we can copy the DNA code for use elsewhere
            string code = _currentBodyProperties.ToString();
            
            try
            {
                Input.SetClipboardText(code);
                FaceLearner.SubModule.Log("DNA code copied - use in campaign character creation");
                
                // Update the preview code display
                if (_dataSource != null)
                {
                    _dataSource.PreviewCode = code;
                    _dataSource.AddLogLine("DNA code copied to clipboard!");
                    _dataSource.AddLogLine("Paste in character creation screen");
                }
            }
            catch (Exception ex)
            {
                FaceLearner.SubModule.Log($"Error: {ex.Message}");
                _dataSource?.AddLogLine("Copy failed - use Export instead");
            }
        }

        private void Exit()
        {
            FaceLearner.SubModule.Log("Exit called");
            MBGameManager.EndGame();
        }

        protected override void OnFinalize()
        {
            base.OnFinalize();
            _dataSource?.OnFinalize();
            _isFinalized = true;
        }

        // IGameStateListener
        void IGameStateListener.OnActivate() { }
        void IGameStateListener.OnDeactivate() { }
        void IGameStateListener.OnInitialize() { }
        void IGameStateListener.OnFinalize() { }
    }

    /// <summary>
    /// ViewModel with slider bindings for body/face parameters
    /// </summary>
    public class FaceLearnerVM : ViewModel
    {
        private readonly Action _onExit;
        private readonly Action _onFaceZoom;
        private readonly Action _onRandomMorph;
        private readonly Action<float> _onAgeChanged;
        private readonly Action<float> _onWeightChanged;
        private readonly Action<float> _onBuildChanged;
        private readonly Action<float> _onHeightChanged;
        private readonly Action<int, float> _onMorphChanged;
        private readonly Action _onPageChanged;
        private readonly Action<int> _onHairChanged;
        private readonly Action<int> _onBeardChanged;
        private readonly Action<bool> _onGenderChanged;
        
        private float _age = 25f;
        private float _weight = 0.5f;
        private float _build = 0.5f;
        private float _height = 0.5f;
        private float _morph0 = 0.5f;
        private float _morph1 = 0.5f;
        private bool _isFaceZoomed = false;
        private bool _isFaceLocked = false;
        
        // Tab state
        private bool _isGeneratePageActive = true;
        private bool _isEditPageActive = false;
        private bool _isLearnPageActive = false;
        
        // UI bindings
        private string _currentImagePath = "No image selected";
        private string _genderText = "Male";
        private string _viewModeText = "Body View";
        private string _hairStyleText = "0";
        private string _beardStyleText = "0";
        private string _faceLockText = "[  ] Face Unlocked";
        private string _generateLog = "Ready to generate...";
        private string _sessionStats = "Session: 0 / 0";
        private string _runStatus = "Not started";
        private string _treeStats = "Tree: -";
        private string _phaseStats = "Phase: -";
        private string _featureStats = "";
        private string _sessionTrend = "";
        private string _availableImagesInfo = "0 images";
        private string _previewCode = "";
        private string _logText = "System ready...\n";
        
        // Image browsing state
        private string[] _availableImages = new string[0];
        private int _currentImageIndex = 0;
        
        // ML state
        private bool _isMLInitialized = false;
        private bool _isMLLearning = false;
        
        // Additional callbacks
        private readonly Action<string> _onLoadCode;
        private readonly Action _onCopyCode;
        private readonly Action _onApplyToGame;
        private readonly Action _onGenerateFace;
        private readonly Action _onResetCharacter;
        private readonly Action<float> _onHairColorChanged;
        private readonly Action<float> _onEyeColorChanged;
        private readonly Action<float> _onSkinToneChanged;
        private readonly Func<FaceController> _getFaceController;  // For ML integration
        private readonly Action _applyFaceController;  // Apply ML changes to character
        private readonly Action<float[]> _applyMorphsBatch;  // Apply all morphs at once
        private readonly Action _onStartLearning;  // Notify screen to start capture loop
        private readonly Action _onStopLearning;   // Notify screen to stop capture loop
        private readonly Action<string> _onCapture;  // Capture to file
        
        // Color indices - defaults match normalized character
        private int _hairColorIndex = 2;   // Brown (matches 0.2f offset)
        private int _eyeColorIndex = 3;    // Green (matches 0.43f offset)
        private int _skinToneIndex = 3;    // Medium

        public FaceLearnerVM(
            Action onExit,
            Action onFaceZoom,
            Action onRandomMorph,
            Action<float> onAgeChanged,
            Action<float> onWeightChanged,
            Action<float> onBuildChanged,
            Action<float> onHeightChanged,
            Action<int, float> onMorphChanged,
            Action onPageChanged = null,
            Action<int> onHairChanged = null,
            Action<int> onBeardChanged = null,
            Action<bool> onGenderChanged = null,
            Action<string> onLoadCode = null,
            Action onCopyCode = null,
            Action onApplyToGame = null,
            Action onGenerateFace = null,
            Action onResetCharacter = null,
            Action<float> onHairColorChanged = null,
            Action<float> onEyeColorChanged = null,
            Action<float> onSkinToneChanged = null,
            Func<FaceController> getFaceController = null,
            Action applyFaceController = null,
            Action<float[]> applyMorphsBatch = null,
            Action onStartLearning = null,
            Action onStopLearning = null,
            Action<string> onCapture = null)
        {
            _onExit = onExit;
            _onFaceZoom = onFaceZoom;
            _onRandomMorph = onRandomMorph;
            _onAgeChanged = onAgeChanged;
            _onWeightChanged = onWeightChanged;
            _onBuildChanged = onBuildChanged;
            _onHeightChanged = onHeightChanged;
            _onMorphChanged = onMorphChanged;
            _onPageChanged = onPageChanged;
            _onHairChanged = onHairChanged;
            _onBeardChanged = onBeardChanged;
            _onGenderChanged = onGenderChanged;
            _onLoadCode = onLoadCode;
            _onCopyCode = onCopyCode;
            _onApplyToGame = onApplyToGame;
            _onGenerateFace = onGenerateFace;
            _onResetCharacter = onResetCharacter;
            _onHairColorChanged = onHairColorChanged;
            _onEyeColorChanged = onEyeColorChanged;
            _onSkinToneChanged = onSkinToneChanged;
            _getFaceController = getFaceController;
            _applyFaceController = applyFaceController;
            _applyMorphsBatch = applyMorphsBatch;
            _onStartLearning = onStartLearning;
            _onStopLearning = onStopLearning;
            _onCapture = onCapture;
            
            FaceLearner.SubModule.Log($"FaceLearnerVM created");
            
            // Create accordion categories for old test
            Categories = new MBBindingList<CategoryVM>();
            Categories.Add(new CategoryVM("Body", OnCategoryToggle));
            Categories.Add(new CategoryVM("Face", OnCategoryToggle));
            Categories.Add(new CategoryVM("Morphs", OnCategoryToggle));
            Categories[0].IsExpanded = true;
            
            // Body sections accordion
            BodySections = new MBBindingList<BodySectionVM>();
            BodySections.Add(new BodySectionVM("Proportions", OnBodySectionToggle));
            BodySections[0].IsExpanded = true;
            
            // Add sliders to body section
            BodySections[0].Sliders.Add(new SliderVM("Age", 25f, 20f, 90f, v => _onAgeChanged?.Invoke(v)));
            BodySections[0].Sliders.Add(new SliderVM("Weight", 0.5f, -1f, 5f, v => _onWeightChanged?.Invoke(v)));
            BodySections[0].Sliders.Add(new SliderVM("Build", 0.5f, -1f, 5f, v => _onBuildChanged?.Invoke(v)));
            BodySections[0].Sliders.Add(new SliderVM("Height", 0.5f, -1f, 5f, v => _onHeightChanged?.Invoke(v)));
            
            // Morph categories accordion - load ALL 62 morphs from MorphDefinitions
            MorphCategories = new MBBindingList<MorphCategoryVM>();
            InitializeMorphCategories();
        }
        
        // Tab visibility properties
        [DataSourceProperty]
        public bool IsGeneratePageActive
        {
            get => _isGeneratePageActive;
            set { if (_isGeneratePageActive != value) { _isGeneratePageActive = value; OnPropertyChangedWithValue(value, "IsGeneratePageActive"); } }
        }
        
        [DataSourceProperty]
        public bool IsEditPageActive
        {
            get => _isEditPageActive;
            set { if (_isEditPageActive != value) { _isEditPageActive = value; OnPropertyChangedWithValue(value, "IsEditPageActive"); } }
        }
        
        [DataSourceProperty]
        public bool IsLearnPageActive
        {
            get => _isLearnPageActive;
            set { if (_isLearnPageActive != value) { _isLearnPageActive = value; OnPropertyChangedWithValue(value, "IsLearnPageActive"); } }
        }
        
        // Tab selection methods
        public void ExecuteSelectGeneratePage()
        {
            FaceLearner.SubModule.Log("Tab: Generate");
            IsGeneratePageActive = true;
            IsEditPageActive = false;
            IsLearnPageActive = false;
            _onPageChanged?.Invoke();
        }
        
        public void ExecuteSelectEditPage()
        {
            FaceLearner.SubModule.Log("Tab: Edit");
            IsGeneratePageActive = false;
            IsEditPageActive = true;
            IsLearnPageActive = false;
            _onPageChanged?.Invoke();
        }
        
        public void ExecuteSelectLearnPage()
        {
            FaceLearner.SubModule.Log("Tab: Learn");
            IsGeneratePageActive = false;
            IsEditPageActive = false;
            IsLearnPageActive = true;
            _onPageChanged?.Invoke();
        }
        
        // Generate page properties
        [DataSourceProperty] public string CurrentImagePath { get => _currentImagePath; set { _currentImagePath = value; OnPropertyChanged(nameof(CurrentImagePath)); } }
        [DataSourceProperty] public string AvailableImagesInfo { get => _availableImagesInfo; set { _availableImagesInfo = value; OnPropertyChanged(nameof(AvailableImagesInfo)); } }
        [DataSourceProperty] public string GenderText { get => _genderText; set { _genderText = value; OnPropertyChanged(nameof(GenderText)); } }
        [DataSourceProperty] public string GenerateLog { get => _generateLog; set { _generateLog = value; OnPropertyChanged(nameof(GenerateLog)); } }
        [DataSourceProperty] public bool HasMultipleImages => _availableImages.Length > 1;
        [DataSourceProperty] public bool CanGenerateFace => _availableImages.Length > 0 && MLAddonRegistry.HasAddon;
        
        // Edit page properties
        [DataSourceProperty] public string ViewModeText { get => _viewModeText; set { _viewModeText = value; OnPropertyChanged(nameof(ViewModeText)); } }
        [DataSourceProperty] public string HairStyleText { get => _hairStyleText; set { _hairStyleText = value; OnPropertyChanged(nameof(HairStyleText)); } }
        [DataSourceProperty] public string BeardStyleText { get => _beardStyleText; set { _beardStyleText = value; OnPropertyChanged(nameof(BeardStyleText)); } }
        [DataSourceProperty] public string FaceLockText { get => _faceLockText; set { _faceLockText = value; OnPropertyChanged(nameof(FaceLockText)); } }
        [DataSourceProperty] public string PreviewCode { get => _previewCode; set { _previewCode = value; OnPropertyChanged(nameof(PreviewCode)); } }
        [DataSourceProperty] public string LogText { get => _logText; set { _logText = value; OnPropertyChanged(nameof(LogText)); } }
        
        // Learn page properties
        [DataSourceProperty] public string SessionStats { get => _sessionStats; set { _sessionStats = value; OnPropertyChanged(nameof(SessionStats)); } }
        [DataSourceProperty] public string RunStatus { get => _runStatus; set { _runStatus = value; OnPropertyChanged(nameof(RunStatus)); } }
        [DataSourceProperty] public string TreeStats { get => _treeStats; set { _treeStats = value; OnPropertyChanged(nameof(TreeStats)); } }
        [DataSourceProperty] public string PhaseStats { get => _phaseStats; set { _phaseStats = value; OnPropertyChanged(nameof(PhaseStats)); } }
        [DataSourceProperty] public string FeatureStats { get => _featureStats; set { _featureStats = value; OnPropertyChanged(nameof(FeatureStats)); } }
        [DataSourceProperty] public string SessionTrend { get => _sessionTrend; set { _sessionTrend = value; OnPropertyChanged(nameof(SessionTrend)); } }
        
        // Button state properties - control which buttons are enabled during learning
        [DataSourceProperty] 
        public bool IsMLLearning 
        { 
            get => _isMLLearning; 
            private set 
            { 
                if (_isMLLearning != value) 
                { 
                    _isMLLearning = value; 
                    OnPropertyChanged(nameof(IsMLLearning)); 
                    OnPropertyChanged(nameof(IsNotMLLearning)); 
                } 
            } 
        }
        [DataSourceProperty] public bool IsNotMLLearning => !_isMLLearning;
        
        // Accordion lists
        [DataSourceProperty] public MBBindingList<CategoryVM> Categories { get; }
        [DataSourceProperty] public MBBindingList<BodySectionVM> BodySections { get; }
        [DataSourceProperty] public MBBindingList<MorphCategoryVM> MorphCategories { get; }
        
        private void OnCategoryToggle(CategoryVM clicked)
        {
            bool wasExpanded = clicked.IsExpanded;
            foreach (var cat in Categories) cat.IsExpanded = false;
            clicked.IsExpanded = !wasExpanded;
        }
        
        private void OnBodySectionToggle(BodySectionVM clicked)
        {
            bool wasExpanded = clicked.IsExpanded;
            foreach (var sec in BodySections) sec.IsExpanded = false;
            clicked.IsExpanded = !wasExpanded;
        }
        
        private void OnMorphCategoryToggle(MorphCategoryVM clicked)
        {
            bool wasExpanded = clicked.IsExpanded;
            foreach (var cat in MorphCategories) cat.IsExpanded = false;
            clicked.IsExpanded = !wasExpanded;
        }
        
        /// <summary>
        /// Initialize all 62 morphs from MorphDefinitions organized by category
        /// </summary>
        private void InitializeMorphCategories()
        {
            // Create category for each unique category in MorphDefinitions
            foreach (string category in MorphDefinitions.Categories)
            {
                var catVM = new MorphCategoryVM(category, OnMorphCategoryToggle);
                
                // Add all morphs for this category
                var morphsInCategory = MorphDefinitions.GetMorphsByCategory(category);
                foreach (var morphDef in morphsInCategory)
                {
                    catVM.Morphs.Add(new MorphVM(
                        morphDef.ShortName, 
                        morphDef.Index, 
                        0.5f,  // Default value
                        (idx, val) => _onMorphChanged?.Invoke(idx, val)
                    ));
                }
                
                MorphCategories.Add(catVM);
            }
            
            // Expand first category by default
            if (MorphCategories.Count > 0)
                MorphCategories[0].IsExpanded = true;
                
            FaceLearner.SubModule.Log($"Initialized {MorphCategories.Count} morph categories with 62 morphs total");
        }
        
        /// <summary>
        /// Update morph value from external source (e.g., after character refresh)
        /// </summary>
        public void UpdateMorphValue(int index, float value)
        {
            foreach (var cat in MorphCategories)
            {
                foreach (var morph in cat.Morphs)
                {
                    if (morph.Index == index)
                    {
                        morph.SetValueSilent(value);
                        return;
                    }
                }
            }
        }
        
        /// <summary>
        /// Append a line to the activity log
        /// </summary>
        public void AddLogLine(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logText = $"[{timestamp}] {message}\n{_logText}";
            // Keep log limited to ~25 lines to avoid overflow
            if (_logText.Length > 2000)
                _logText = _logText.Substring(0, 2000);
            OnPropertyChanged(nameof(LogText));
        }
        
        // Action methods - Image Browsing
        public void ExecuteBrowseImage()
        {
            FaceLearner.SubModule.Log("BrowseImage");
            
            var photosDir = FaceLearner.SubModule.ModulePath + "Photos/";
            if (!System.IO.Directory.Exists(photosDir))
            {
                System.IO.Directory.CreateDirectory(photosDir);
                AddLogLine($"Created Photos folder");
            }
            
            // Collect all images
            _availableImages = System.IO.Directory.GetFiles(photosDir, "*.*")
                .Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp" }
                    .Contains(System.IO.Path.GetExtension(f).ToLower()))
                .OrderBy(f => System.IO.Path.GetFileName(f))
                .ToArray();
            
            if (_availableImages.Length > 0)
            {
                _currentImageIndex = 0;
                SelectCurrentImage();
                AddLogLine($"Found {_availableImages.Length} images");
            }
            else
            {
                AddLogLine($"No images in: {photosDir}");
                AddLogLine("Supported: .jpg, .png, .bmp");
            }
            
            RefreshImageInfo();
        }
        
        public void ExecuteNextImage()
        {
            if (_availableImages.Length == 0) 
            {
                ExecuteBrowseImage(); // Auto-browse if no images loaded
                return;
            }
            _currentImageIndex = (_currentImageIndex + 1) % _availableImages.Length;
            SelectCurrentImage();
        }
        
        public void ExecutePrevImage()
        {
            if (_availableImages.Length == 0) 
            {
                ExecuteBrowseImage();
                return;
            }
            _currentImageIndex = (_currentImageIndex - 1 + _availableImages.Length) % _availableImages.Length;
            SelectCurrentImage();
        }
        
        private void SelectCurrentImage()
        {
            if (_currentImageIndex >= 0 && _currentImageIndex < _availableImages.Length)
            {
                CurrentImagePath = System.IO.Path.GetFileName(_availableImages[_currentImageIndex]);
                RefreshImageInfo();
                FaceLearner.SubModule.Log($"Selected image: {CurrentImagePath}");
            }
        }
        
        private void RefreshImageInfo()
        {
            AvailableImagesInfo = _availableImages.Length > 0 
                ? $"{_currentImageIndex + 1}/{_availableImages.Length}" 
                : "0 images";
        }
        
        public void ExecuteToggleGender() 
        { 
            GenderText = GenderText == "Male" ? "Female" : "Male"; 
            _onGenderChanged?.Invoke(GenderText == "Female");
            FaceLearner.SubModule.Log($"Gender: {GenderText}"); 
        }
        
        /// <summary>
        /// Generate face from selected image using ML addon
        /// </summary>
        public void ExecuteGenerateFaceFromImage()
        {
            FaceLearner.SubModule.Log("GenerateFaceFromImage");
            
            if (_availableImages.Length == 0)
            {
                AddLogLine("No image selected!");
                AddLogLine("Click Browse to load images from Photos folder");
                return;
            }
            
            if (!MLAddonRegistry.HasAddon)
            {
                AddLogLine("ML Addon required for image-based generation");
                AddLogLine("Install FaceLearner.ML");
                // Fall back to random
                AddLogLine("Generating random face instead...");
                _onGenerateFace?.Invoke();
                return;
            }
            
            string imagePath = _availableImages[_currentImageIndex];
            
            AddLogLine($"Analyzing: {System.IO.Path.GetFileName(imagePath)}");
            
            try
            {
                // Use GenerateFaceComplete for full Photo Detection
                var result = MLAddonRegistry.Addon.GenerateFaceComplete(imagePath, progress =>
                {
                    GenerateLog = $"Analyzing... {(int)(progress * 100)}%";
                });
                
                if (result.Success && result.Morphs != null && result.Morphs.Length > 0)
                {
                    // Apply all generated morphs
                    _applyMorphsBatch?.Invoke(result.Morphs);
                    
                    // Apply all detected attributes via callbacks
                    // Gender
                    _onGenderChanged?.Invoke(result.IsFemale);
                    
                    // Age
                    _onAgeChanged?.Invoke(result.Age);
                    
                    // Colors
                    _onSkinToneChanged?.Invoke(result.SkinColor);
                    _onEyeColorChanged?.Invoke(result.EyeColor);
                    _onHairColorChanged?.Invoke(result.HairColor);
                    
                    // Hair and Beard Style (Photo Detection only!)
                    _onHairChanged?.Invoke(result.HairIndex);
                    _onBeardChanged?.Invoke(result.BeardIndex);
                    
                    // Body proportions
                    _onWeightChanged?.Invoke(result.Weight);
                    _onBuildChanged?.Invoke(result.Build);
                    _onHeightChanged?.Invoke(result.Height);
                    
                    FaceLearner.SubModule.Log($"  Applied: Gender={(result.IsFemale ? "F" : "M")} Age={result.Age:F0} Hair={result.HairIndex} Beard={result.BeardIndex}");
                    
                    string genderStr = result.IsFemale ? "Female" : "Male";
                    AddLogLine($"Detected: {genderStr}, {result.Age:F0}y");
                    AddLogLine($"Hair style: {result.HairIndex}, Beard: {result.BeardIndex}");
                    AddLogLine($"Score: {result.Score:F2}");
                    GenerateLog = $"Match score: {result.Score:F2}";
                }
                else
                {
                    AddLogLine($"Generation failed: {result.ErrorMessage ?? "Unknown error"}");
                    GenerateLog = "Generation failed";
                }
            }
            catch (Exception ex)
            {
                AddLogLine($"Error: {ex.Message}");
                GenerateLog = "Error during generation";
                FaceLearner.SubModule.Log($"GenerateFaceFromImage error: {ex}");
            }
        }
        
        /// <summary>
        /// Generate random face (not from image)
        /// </summary>
        public void ExecuteGenerateFace() 
        { 
            FaceLearner.SubModule.Log("GenerateFace (random)");
            AddLogLine("Generating random face...");
            _onGenerateFace?.Invoke();
        }
        
        public void ExecuteApplyToCharacter() 
        { 
            FaceLearner.SubModule.Log("ApplyToCharacter");
            AddLogLine("Applying to game character...");
            _onApplyToGame?.Invoke();
        }
        public void ExecuteExportCharacter() 
        { 
            FaceLearner.SubModule.Log("ExportCharacter");
            _onCopyCode?.Invoke();
            AddLogLine("DNA code copied to clipboard");
        }
        
        public void ExecuteImportCharacter()
        {
            FaceLearner.SubModule.Log("ImportCharacter");
            try
            {
                string clipboardText = Input.GetClipboardText();
                if (!string.IsNullOrEmpty(clipboardText) && clipboardText.Contains("key="))
                {
                    PreviewCode = clipboardText;
                    _onLoadCode?.Invoke(clipboardText);
                    AddLogLine("DNA code loaded from clipboard");
                }
                else
                {
                    AddLogLine("No valid DNA code in clipboard");
                    AddLogLine("Copy a BodyProperties string first");
                }
            }
            catch (Exception ex)
            {
                AddLogLine($"Import failed: {ex.Message}");
            }
        }
        
        public void ExecuteResetKnowledge()
        {
            FaceLearner.SubModule.Log("ResetKnowledge");
            
            if (!MLAddonRegistry.HasAddon)
            {
                AddLogLine("ML Addon required");
                return;
            }
            
            // This would reset the learned data
            AddLogLine("Knowledge reset requested");
            AddLogLine("This feature requires ML Addon configuration");
            
            // The ML addon would need a Reset method
            // For now, just re-initialize
            if (_isMLInitialized)
            {
                _isMLInitialized = false;
                AddLogLine("Reset complete - click Init to start fresh");
                RunStatus = "Reset";
            }
        }
        
        public void ExecuteDownloadModels()
        {
            FaceLearner.SubModule.Log("DownloadModels");
            AddLogLine("Model download not available in preview mode");
            AddLogLine("ML models are bundled with FaceLearner.ML addon");
        }
        
        // DNA Code methods
        public void ExecuteLoadCode() 
        { 
            FaceLearner.SubModule.Log($"LoadCode: {_previewCode}");
            _onLoadCode?.Invoke(_previewCode);
        }
        public void ExecuteCopyCode() 
        { 
            FaceLearner.SubModule.Log("CopyCode");
            _onCopyCode?.Invoke();
        }
        public void ExecuteApplyToGame() 
        { 
            FaceLearner.SubModule.Log("ApplyToGame");
            _onApplyToGame?.Invoke();
        }
        
        public void ExecuteToggleView() { IsFaceZoomed = !IsFaceZoomed; _onFaceZoom?.Invoke(); }
        public void ExecutePrevHairStyle() 
        { 
            int h = int.Parse(HairStyleText); 
            h = Math.Max(0, h - 1);
            HairStyleText = h.ToString(); 
            _onHairChanged?.Invoke(h);
        }
        public void ExecuteNextHairStyle() 
        { 
            int h = int.Parse(HairStyleText); 
            h++;
            HairStyleText = h.ToString(); 
            _onHairChanged?.Invoke(h);
        }
        public void ExecutePrevBeardStyle() 
        { 
            int b = int.Parse(BeardStyleText); 
            b = Math.Max(0, b - 1);
            BeardStyleText = b.ToString(); 
            _onBeardChanged?.Invoke(b);
        }
        public void ExecuteNextBeardStyle() 
        { 
            int b = int.Parse(BeardStyleText); 
            b++;
            BeardStyleText = b.ToString(); 
            _onBeardChanged?.Invoke(b);
        }
        
        // Hair Color
        [DataSourceProperty] 
        public string HairColorText => HairColorPresets.Presets[_hairColorIndex].Name;
        
        public void ExecutePrevHairColor()
        {
            _hairColorIndex = Math.Max(0, _hairColorIndex - 1);
            OnPropertyChanged(nameof(HairColorText));
            // Use index as normalized offset (0 to 1 range)
            float offset = (float)_hairColorIndex / (HairColorPresets.Presets.Length - 1);
            _onHairColorChanged?.Invoke(offset);
        }
        
        public void ExecuteNextHairColor()
        {
            _hairColorIndex = Math.Min(HairColorPresets.Presets.Length - 1, _hairColorIndex + 1);
            OnPropertyChanged(nameof(HairColorText));
            float offset = (float)_hairColorIndex / (HairColorPresets.Presets.Length - 1);
            _onHairColorChanged?.Invoke(offset);
        }
        
        // Eye Color
        [DataSourceProperty]
        public string EyeColorText => EyeColorPresets.Presets[_eyeColorIndex].Name;
        
        public void ExecutePrevEyeColor()
        {
            _eyeColorIndex = Math.Max(0, _eyeColorIndex - 1);
            OnPropertyChanged(nameof(EyeColorText));
            float offset = (float)_eyeColorIndex / (EyeColorPresets.Presets.Length - 1);
            _onEyeColorChanged?.Invoke(offset);
        }
        
        public void ExecuteNextEyeColor()
        {
            _eyeColorIndex = Math.Min(EyeColorPresets.Presets.Length - 1, _eyeColorIndex + 1);
            OnPropertyChanged(nameof(EyeColorText));
            float offset = (float)_eyeColorIndex / (EyeColorPresets.Presets.Length - 1);
            _onEyeColorChanged?.Invoke(offset);
        }
        
        // Skin Tone
        [DataSourceProperty]
        public string SkinToneText => SkinTonePresets.Presets[_skinToneIndex].Name;
        
        public void ExecutePrevSkinTone()
        {
            _skinToneIndex = Math.Max(0, _skinToneIndex - 1);
            var preset = SkinTonePresets.Presets[_skinToneIndex];
            OnPropertyChanged(nameof(SkinToneText));
            _onSkinToneChanged?.Invoke(preset.Value);
        }
        
        public void ExecuteNextSkinTone()
        {
            _skinToneIndex = Math.Min(SkinTonePresets.Presets.Length - 1, _skinToneIndex + 1);
            var preset = SkinTonePresets.Presets[_skinToneIndex];
            OnPropertyChanged(nameof(SkinToneText));
            _onSkinToneChanged?.Invoke(preset.Value);
        }
        
        public void ExecuteToggleFaceLock() 
        { 
            _isFaceLocked = !_isFaceLocked;
            FaceLockText = _isFaceLocked ? "[X] Face Locked" : "[  ] Face Unlocked"; 
            AddLogLine($"Face {(_isFaceLocked ? "locked" : "unlocked")}");
        }
        
        public void ExecuteInit() 
        { 
            FaceLearner.SubModule.Log("Init clicked"); 
            
            // Set face zoom for learning
            if (!IsFaceZoomed)
            {
                IsFaceZoomed = true;
                _onFaceZoom?.Invoke();
                AddLogLine("Face zoom enabled");
            }
            
            if (_isMLInitialized)
            {
                AddLogLine("Already initialized");
                return;
            }
            
            if (!MLAddonRegistry.HasAddon)
            {
                AddLogLine("ML Addon not installed!");
                AddLogLine("Install FaceLearner.ML for ML features");
                RunStatus = "No ML Addon";
                return;
            }
            
            // Get FaceController from Screen
            FaceController faceController = null;
            if (_getFaceController != null)
            {
                faceController = _getFaceController();
                AddLogLine("FaceController: Available");
            }
            else
            {
                AddLogLine("FaceController: Not available");
                AddLogLine("Learning will be disabled");
            }
            
            AddLogLine("Initializing ML...");
            RunStatus = "Initializing...";
            
            try
            {
                // Pass FaceController to ML - enables Learning!
                bool success = MLAddonRegistry.Addon.Initialize(faceController, FaceLearner.SubModule.ModulePath);
                
                if (success)
                {
                    _isMLInitialized = true;
                    if (faceController != null)
                    {
                        AddLogLine("ML initialized - All features ready!");
                        RunStatus = "Ready";
                    }
                    else
                    {
                        AddLogLine("ML initialized - Generate only");
                        RunStatus = "Ready (Generate)";
                    }
                    RefreshMLStats();
                }
                else
                {
                    AddLogLine("ML initialization failed");
                    RunStatus = "Init Failed";
                }
            }
            catch (Exception ex)
            {
                AddLogLine($"ML init error: {ex.Message}");
                RunStatus = "Init Error";
                FaceLearner.SubModule.Log($"ML Init exception: {ex}");
            }
        }
        
        public void ExecuteStart() 
        { 
            FaceLearner.SubModule.Log("Start clicked"); 
            
            if (!MLAddonRegistry.HasAddon)
            {
                AddLogLine("ML Addon required");
                return;
            }
            
            // Auto-init if needed
            if (!_isMLInitialized)
            {
                ExecuteInit();
                if (!_isMLInitialized) return;
            }
            
            if (_isMLLearning)
            {
                AddLogLine("Already learning");
                return;
            }
            
            try
            {
                AddLogLine("Starting learning...");
                MLAddonRegistry.Addon.StartLearning();
                IsMLLearning = true;  // Use property to trigger OnPropertyChanged
                RunStatus = "Learning...";
                AddLogLine("Learning started!");
                
                // Notify screen to start capture loop
                _onStartLearning?.Invoke();
            }
            catch (InvalidOperationException ex)
            {
                // FaceController not available
                AddLogLine($"Cannot start: {ex.Message}");
                RunStatus = "No FaceController";
            }
            catch (Exception ex)
            {
                AddLogLine($"Start error: {ex.Message}");
                RunStatus = "Error";
                FaceLearner.SubModule.Log($"StartLearning exception: {ex}");
            }
        }
        
        public void ExecuteStop() 
        { 
            FaceLearner.SubModule.Log("Stop clicked"); 
            
            if (!_isMLLearning)
            {
                AddLogLine("Not currently learning");
                return;
            }
            
            // Stop capture loop on screen
            _onStopLearning?.Invoke();
            
            if (MLAddonRegistry.HasAddon)
            {
                MLAddonRegistry.Addon.StopLearning();
            }
            
            IsMLLearning = false;  // Use property to trigger OnPropertyChanged
            AddLogLine("Learning stopped");
            RunStatus = "Stopped";
            RefreshMLStats();
        }
        
        public void ExecuteCapture()
        {
            FaceLearner.SubModule.Log("Capture clicked");
            
            // Base path without extension - engine adds "View_Output.png"
            // Save in Data/Temp folder to keep module root clean
            string tempDir = System.IO.Path.Combine(FaceLearner.SubModule.ModulePath, "Data", "Temp");
            System.IO.Directory.CreateDirectory(tempDir);
            string basePath = System.IO.Path.Combine(tempDir, "test_capture_");
            string actualPath = basePath + "View_Output.png";
            AddLogLine($"Capturing to: {actualPath}");
            
            if (_onCapture != null)
            {
                _onCapture(basePath);
                AddLogLine("Check module folder!");
            }
            else
            {
                AddLogLine("Test capture not available");
            }
        }
        
        public void ExecuteMorphTest()
        {
            FaceLearner.SubModule.Log("MorphTest clicked");
            
            if (!MLAddonRegistry.HasAddon)
            {
                AddLogLine("ML addon not loaded!");
                return;
            }
            
            var addon = MLAddonRegistry.Addon;
            
            if (addon.IsMorphTestRunning)
            {
                addon.StopMorphTest();
                AddLogLine("Morph test stopped");
                _onStopLearning?.Invoke();  // Stop capture loop
            }
            else
            {
                if (_isMLLearning)
                {
                    AddLogLine("Stop learning first!");
                    return;
                }
                
                addon.StartMorphTest();
                AddLogLine("Morph test started - testing all 62 morphs...");
                AddLogLine("Results will be saved to Data/MorphTest/");
                _onStartLearning?.Invoke();  // Start capture loop for screenshots
            }
        }
        
        public void RefreshStats()
        {
            RefreshMLStats();
        }
        
        private void RefreshMLStats()
        {
            if (MLAddonRegistry.HasAddon && _isMLInitialized)
            {
                var addon = MLAddonRegistry.Addon;
                TreeStats = addon.GetTreeStats() ?? "No data";
                PhaseStats = addon.GetPhaseStats() ?? "No data";
                FeatureStats = addon.GetFeatureIntelligenceStatus() ?? "";
                SessionStats = $"Iterations: {addon.TotalIterations}\nTargets: {addon.TargetsProcessed}";
                SessionTrend = addon.GetSessionTrend() ?? "";
                RunStatus = addon.GetRunStatus() ?? "Idle";
            }
        }
        
        [DataSourceProperty]
        public bool HasMLAddon => MLAddonRegistry.HasAddon;
        
        [DataSourceProperty]
        public bool CanLearn => MLAddonRegistry.HasAddon && (MLAddonRegistry.Addon?.IsReady ?? false);
        
        [DataSourceProperty]
        public string MLAddonStatus => MLAddonRegistry.HasAddon 
            ? (MLAddonRegistry.Addon.IsReady ? "ML Ready" : "ML Loading...")
            : "No ML Addon";
        
        [DataSourceProperty]
        public string VersionText => $"FaceLearner v{FaceLearner.SubModule.VERSION}";
        
        public void ExecuteRandomize() 
        { 
            FaceLearner.SubModule.Log("Randomize"); 
            AddLogLine("Randomizing character...");
            _onGenerateFace?.Invoke(); 
        }
        public void ExecuteReset() 
        { 
            FaceLearner.SubModule.Log("Reset"); 
            AddLogLine("Resetting character...");
            _onResetCharacter?.Invoke(); 
        }
        
        // Initialize values from character
        public void SetInitialValues(float age, float weight, float build, float height)
        {
            _age = age;
            _weight = weight;
            _build = build;
            _height = height;
            OnPropertyChangedWithValue(age, "Age");
            OnPropertyChangedWithValue(weight, "Weight");
            OnPropertyChangedWithValue(build, "Build");
            OnPropertyChangedWithValue(height, "Height");
            OnPropertyChanged(nameof(AgeText));
            OnPropertyChanged(nameof(WeightText));
            OnPropertyChanged(nameof(BuildText));
            OnPropertyChanged(nameof(HeightText));
        }
        
        // Update UI from FaceGenerationParams (used when normalizing character)
        public void UpdateFromParams(FaceGenerationParams faceParams)
        {
            SetInitialValues(
                faceParams.CurrentAge,
                faceParams.CurrentWeight,
                faceParams.CurrentBuild,
                faceParams.HeightMultiplier
            );
            
            // Update hair/beard style text
            HairStyleText = faceParams.CurrentHair.ToString();
            BeardStyleText = faceParams.CurrentBeard.ToString();
            
            // Calculate color indices from offsets (reverse of the offset calculation)
            // offset = index / (count - 1), so index = offset * (count - 1)
            int hairIdx = (int)Math.Round(faceParams.CurrentHairColorOffset * (HairColorPresets.Presets.Length - 1));
            _hairColorIndex = Math.Max(0, Math.Min(HairColorPresets.Presets.Length - 1, hairIdx));
            OnPropertyChanged(nameof(HairColorText));
            
            int eyeIdx = (int)Math.Round(faceParams.CurrentEyeColorOffset * (EyeColorPresets.Presets.Length - 1));
            _eyeColorIndex = Math.Max(0, Math.Min(EyeColorPresets.Presets.Length - 1, eyeIdx));
            OnPropertyChanged(nameof(EyeColorText));
            
            // Skin tone defaults to index 3 ("Medium") when normalized
            _skinToneIndex = 3;
            OnPropertyChanged(nameof(SkinToneText));
        }
        
        // Reset zoom state to body view
        public void ResetZoom()
        {
            IsFaceZoomed = false;
        }

        [DataSourceProperty]
        public float Age
        {
            get => _age;
            set
            {
                if (_age != value)
                {
                    _age = MathF.Clamp(value, 20f, 90f);
                    OnPropertyChangedWithValue(_age, "Age");
                    OnPropertyChanged(nameof(AgeText));
                    _onAgeChanged?.Invoke(_age);
                }
            }
        }
        
        [DataSourceProperty]
        public string AgeText => $"{_age:F0}";
        
        public void ExecuteAgeUp()
        {
            FaceLearner.SubModule.Log($"ExecuteAgeUp: {_age} -> {_age + 5}");
            Age += 5f;
        }
        public void ExecuteAgeDown()
        {
            FaceLearner.SubModule.Log($"ExecuteAgeDown: {_age} -> {_age - 5}");
            Age -= 5f;
        }
        
        [DataSourceProperty]
        public float Weight
        {
            get => _weight;
            set
            {
                if (_weight != value)
                {
                    _weight = MathF.Clamp(value, -1f, 5f);
                    OnPropertyChangedWithValue(_weight, "Weight");
                    OnPropertyChanged(nameof(WeightText));
                    _onWeightChanged?.Invoke(_weight);
                }
            }
        }
        
        [DataSourceProperty]
        public string WeightText => $"{_weight:F2}";
        
        public void ExecuteWeightUp() => Weight += 0.1f;
        public void ExecuteWeightDown() => Weight -= 0.1f;
        
        [DataSourceProperty]
        public float Build
        {
            get => _build;
            set
            {
                if (_build != value)
                {
                    _build = MathF.Clamp(value, -1f, 5f);
                    OnPropertyChangedWithValue(_build, "Build");
                    OnPropertyChanged(nameof(BuildText));
                    _onBuildChanged?.Invoke(_build);
                }
            }
        }
        
        [DataSourceProperty]
        public string BuildText => $"{_build:F2}";
        
        public void ExecuteBuildUp() => Build += 0.1f;
        public void ExecuteBuildDown() => Build -= 0.1f;
        
        [DataSourceProperty]
        public float Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    _height = MathF.Clamp(value, -1f, 5f);
                    OnPropertyChangedWithValue(_height, "Height");
                    OnPropertyChanged(nameof(HeightText));
                    _onHeightChanged?.Invoke(_height);
                }
            }
        }
        
        [DataSourceProperty]
        public string HeightText => $"{_height:F2}";
        
        public void ExecuteHeightUp() => Height += 0.1f;
        public void ExecuteHeightDown() => Height -= 0.1f;
        
        [DataSourceProperty]
        public float Morph0
        {
            get => _morph0;
            set
            {
                if (_morph0 != value)
                {
                    _morph0 = MathF.Clamp(value, -1f, 5f);
                    OnPropertyChangedWithValue(_morph0, "Morph0");
                    OnPropertyChanged(nameof(Morph0Text));
                    _onMorphChanged?.Invoke(0, _morph0);
                }
            }
        }
        
        [DataSourceProperty]
        public string Morph0Text => $"{_morph0:F2}";
        
        public void ExecuteMorph0Up() => Morph0 += 0.1f;
        public void ExecuteMorph0Down() => Morph0 -= 0.1f;
        
        [DataSourceProperty]
        public float Morph1
        {
            get => _morph1;
            set
            {
                if (_morph1 != value)
                {
                    _morph1 = MathF.Clamp(value, -1f, 5f);
                    OnPropertyChangedWithValue(_morph1, "Morph1");
                    OnPropertyChanged(nameof(Morph1Text));
                    _onMorphChanged?.Invoke(1, _morph1);
                }
            }
        }
        
        [DataSourceProperty]
        public string Morph1Text => $"{_morph1:F2}";
        
        public void ExecuteMorph1Up() => Morph1 += 0.1f;
        public void ExecuteMorph1Down() => Morph1 -= 0.1f;
        
        [DataSourceProperty]
        public bool IsFaceZoomed
        {
            get => _isFaceZoomed;
            set
            {
                if (_isFaceZoomed != value)
                {
                    _isFaceZoomed = value;
                    OnPropertyChangedWithValue(value, "IsFaceZoomed");
                    OnPropertyChangedWithValue(FaceZoomText, "FaceZoomText");
                }
            }
        }
        
        [DataSourceProperty]
        public string FaceZoomText => _isFaceZoomed ? "Body View" : "Face Zoom";

        public void ExecuteExit()
        {
            _onExit?.Invoke();
        }
        
        public void ExecuteFaceZoom()
        {
            _onFaceZoom?.Invoke();
        }
        
        public void ExecuteRandomMorph()
        {
            _onRandomMorph?.Invoke();
        }
    }
}
