using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using FaceLearner.Core;
using FaceLearner.Core.LivingKnowledge;
using FaceLearner.ML.Core.RaceSystem;
using FaceLearner.ML.DataSources;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.Proportions;
using FaceLearner.ML.Utils;
using FaceShape = FaceLearner.ML.Modules.Core.FaceShape;

namespace FaceLearner.ML.Core.LivingKnowledge
{
    /// <summary>
    /// Coordinates the face learning process.
    /// 
    /// CLEAN ARCHITECTURE (v2.8.0):
    /// This class coordinates between specialized engines:
    /// - ScoreEngine: Score calculation and best-morphs tracking
    /// - MutationEngine: Mutation generation (CMA-ES, legacy, probe)
    /// - TargetManager: Target selection and lifecycle
    /// - DecisionEngine: Learning flow decisions
    /// - CharacterConfigurator: Character setup for targets
    /// 
    /// Flow:
    /// 1. Start() → Load first target
    /// 2. Iterate() → Called every frame, applies morphs
    /// 3. OnRenderCaptured() → Evaluate, decide, mutate or next target
    /// </summary>
    public class LearningOrchestrator : IDisposable
    {
        #region State Machine
        
        private enum LearningState
        {
            Idle,
            WaitingForRender,
            Evaluating
        }
        
        private LearningState _state = LearningState.Idle;
        
        #endregion
        
        #region Engines (Single Responsibility)
        
        private readonly DecisionEngine _decisionEngine;
        
        #endregion
        
        #region Core Dependencies
        
        private readonly LandmarkDetector _detector;
        private readonly FaceController _faceController;
        private readonly DataSourceManager _dataSourceManager;
        private readonly string _basePath;
        private string _screenshotPath;
        private string _currentTargetImagePath;
        
        #endregion
        
        #region Supporting Systems
        
        private HierarchicalPhaseSystem _hierarchicalPhase;
        private SessionMonitor _sessionMonitor;
        private PhaseManager _phaseManager;
        private HierarchicalKnowledge _hierarchicalTree;
        private FeatureIntelligence _featureIntelligence;
        private FeatureMorphLearning _featureMorphLearning;
        private OrchestratorMemory _memory;
        private ModuleIntegration _moduleIntegration;
        private CmaEs _cmaEs;
        
        #endregion
        
        #region Demographics Detection
        
        private DemographicDetector _demographicDetector;
        private FairFaceDetector _fairFaceDetector;
        private GenderEnsemble _genderEnsemble;
        private ColorEnsemble _colorEnsemble;
        private AgeEnsemble _ageEnsemble;
        
        // Race System
        private RacePresetManager _racePresetManager;
        private RaceAwareFaceGenerator _raceGenerator;
        
        #endregion
        
        #region Target State
        
        private TargetFace _currentTarget;
        private float[] _targetLandmarks;
        private float[] _targetRatios;
        private DemographicDetector.Demographics? _cachedDemographics;
        private int _lastSourceIndex = -1;
        
        #endregion
        
        #region Morph State
        
        private float[] _currentMorphs;
        private float[] _bestMorphs;
        private float[] _previousMorphs;
        private float[] _startingMorphs;  // Initial morphs when target started
        private float[] _currentLandmarks;
        private float[] _previousLandmarks;
        
        #endregion
        
        #region Score State
        
        private float _currentScore;
        private float _previousScore;
        private float _bestScore;
        private Dictionary<string, float[]> _bestMorphsPerFeature = new Dictionary<string, float[]>();
        private Dictionary<string, float> _bestScorePerFeature = new Dictionary<string, float>();
        private Dictionary<string, float> _startingFeatureScores = new Dictionary<string, float>();
        private Dictionary<string, float> _previousFeatureScores = new Dictionary<string, float>();
        
        #endregion
        
        #region CMA-ES State
        
        private double[][] _cmaPopulation;
        private int _cmaPopulationIndex;
        private double[] _cmaFitness;
        
        #endregion
        
        #region Counters
        
        private int _iterationsOnTarget;
        private int _phaseIterations;
        private int _newPeakCount;
        private int _revertCount;
        private int _consecutiveNonImprovements;
        private int _lastFeatureScoreIteration;
        private int _targetsSinceCleanup;
        private int _targetsSinceEvolution;
        private int _qualitySkipCount;
        private int _ageSkipCount;
        private string _detectedRace;
        private float _detectedRaceConf;
        private Random _random = new Random();
        
        #endregion
        
        #region Public Properties
        
        public bool IsLearning { get; private set; }
        public int TotalIterations { get; private set; }
        public int TargetsProcessed { get; private set; }
        public int CurrentEpoch { get; private set; } = 1;
        
        public float CurrentScore => _currentScore;
        public float BestScore => _bestScore;
        public string CurrentPhase => _hierarchicalPhase?.CurrentConfig?.Name ?? "None";
        public string CurrentTargetId => _currentTarget?.Id ?? "None";
        
        public bool IsStagnant => _sessionMonitor?.IsStagnant ?? false;
        public bool IsRegressing => _sessionMonitor?.IsRegressing ?? false;
        public bool IsRapidProgress => _sessionMonitor?.IsRapidProgress ?? false;
        public float Volatility => _sessionMonitor?.Volatility ?? 0f;
        
        public bool HasFeatureScoring => _moduleIntegration?.IsReady ?? false;
        public float WorstFeatureScore => _moduleIntegration?.WorstFeatureScore ?? 0f;
        public string FeatureBreakdown => _moduleIntegration?.GetFeatureBreakdown() ?? "N/A";
        public string FeatureIntelligenceStatus => _featureIntelligence?.GetFullSummary() ?? "Not initialized";
        public List<string> FocusedFeatures => _featureIntelligence?.FocusedFeatures ?? new List<string>();
        public bool HasActiveRegressions => _featureIntelligence?.GetRegressionAlerts()?.Count > 0;
        
        public bool IsFairFaceLoaded => _fairFaceDetector?.IsLoaded ?? false;
        
        public string ScreenshotPath
        {
            get => _screenshotPath;
            set => _screenshotPath = value;
        }
        
        #endregion
        
        #region Constructor
        
        public LearningOrchestrator(
            LandmarkDetector detector,
            FaceController faceController,
            DataSourceManager dataSourceManager,
            string mlDataDir,
            string coreDataDir = null)
        {
            _detector = detector;
            _faceController = faceController;
            _dataSourceManager = dataSourceManager;
            _basePath = mlDataDir ?? SubModule.ModulePath + "Data/";
            _screenshotPath = _basePath + "Temp/learning_capture_View_Output.png";
            
            string knowledgePath = coreDataDir ?? _basePath;
            
            // === Demographics Detection ===
            InitializeDemographicsDetectors(knowledgePath);
            
            // === Knowledge Systems ===
            InitializeKnowledgeSystems(knowledgePath);
            
            // === CMA-ES ===
            // Use 62 face morphs (actual Bannerlord face morph count)
            _cmaEs = new CmaEs(62);
            
            // === Decision Engine ===
            _decisionEngine = new DecisionEngine();
            
            // === Comparison Images ===
            ComparisonImageCreator.Initialize(_basePath);
            
            SubModule.Log("LearningOrchestrator initialized (v2.9.0)");
        }
        
        private void InitializeDemographicsDetectors(string knowledgePath)
        {
            _demographicDetector = new DemographicDetector();
            if (_demographicDetector.Initialize(_basePath))
            {
                SubModule.Log("DemographicDetector ready");
            }
            
            _fairFaceDetector = new FairFaceDetector();
            string fairFacePath = _basePath + "Models/fairface.onnx";
            if (File.Exists(fairFacePath))
            {
                if (_fairFaceDetector.Load(fairFacePath))
                {
                    SubModule.Log("FairFace ready");
                }
            }
            
            // v3.0.33: ViT age-gender as primary gender model
            var vitDetector = new ViTGenderDetector();
            string vitPath = _basePath + "Models/vit_age_gender.onnx";
            vitDetector.Initialize(vitPath);

            _genderEnsemble = new GenderEnsemble();
            _genderEnsemble.Initialize(fairFacePath, _demographicDetector, vitDetector);

            _colorEnsemble = new ColorEnsemble();
            _colorEnsemble.Initialize(_fairFaceDetector);
            SubModule.Log("ColorEnsemble ready");
            
            _ageEnsemble = new AgeEnsemble();
            _ageEnsemble.Initialize(_fairFaceDetector);
            SubModule.Log("AgeEnsemble ready");
            
            // Initialize Race System
            _racePresetManager = new RacePresetManager(_basePath);
            _racePresetManager.Load();
            _raceGenerator = new RaceAwareFaceGenerator(_racePresetManager);
            SubModule.Log($"RaceSystem ready: {_racePresetManager.Presets.Count} presets loaded");
        }
        
        private void InitializeKnowledgeSystems(string knowledgePath)
        {
            _phaseManager = new PhaseManager(knowledgePath + "phase_manager.dat");
            _phaseManager.Load();
            
            _hierarchicalPhase = new HierarchicalPhaseSystem();
            SubModule.Log("HierarchicalPhaseSystem ready");
            
            _memory = new OrchestratorMemory(knowledgePath + "orchestrator_memory.dat");
            _memory.Load();
            
            _hierarchicalTree = new HierarchicalKnowledge(knowledgePath + "hierarchical_knowledge.dat");
            
            _sessionMonitor = new SessionMonitor();
            
            _moduleIntegration = new ModuleIntegration();
            if (_moduleIntegration.Initialize(_basePath))
            {
                SubModule.Log("ModuleIntegration ready");
            }
            
            _featureIntelligence = new FeatureIntelligence(knowledgePath + "feature_intelligence.dat");
            SubModule.Log("FeatureIntelligence ready");
            
            _featureMorphLearning = new FeatureMorphLearning(knowledgePath + "feature_morph_learning.dat");
            SubModule.Log($"FeatureMorphLearning ready - {_featureMorphLearning.GetSummary()}");
        }
        
        #endregion
        
        #region Public API - Lifecycle
        
        public void Start()
        {
            IsLearning = true;
            SubModule.Log($"=== Learning Started (Clean Architecture v2.8.0) ===");
            SubModule.Log($"  FLOW: State Machine (Idle → WaitingForRender → Evaluating)");
            SubModule.Log($"  MODULES: {(_moduleIntegration?.IsReady == true ? "✓ Active" : "⚠ Fallback")}");
            SubModule.Log($"  Sources: {string.Join(", ", _dataSourceManager.GetReadySources().Select(s => s.Name))}");
            
            NextTarget();
        }
        
        public void Stop()
        {
            IsLearning = false;
            _state = LearningState.Idle;
            
            // Save all knowledge
            _phaseManager?.Save();
            _memory?.Save();
            _hierarchicalTree?.Save();
            _featureIntelligence?.Save();
            _featureMorphLearning?.Save();
            
            SubModule.Log($"=== Learning Stopped ===");
            SubModule.Log($"  Targets: {TargetsProcessed} | Iterations: {TotalIterations}");
        }
        
        #endregion
        
        #region Public API - Main Loop
        
        /// <summary>
        /// Called every frame. Updates phase system and applies morphs.
        /// </summary>
        public bool Iterate()
        {
            if (!IsLearning || _targetLandmarks == null || _hierarchicalPhase == null)
                return false;
            
            TotalIterations++;
            _iterationsOnTarget++;
            _phaseIterations++;
            
            // Check for phase transitions
            Dictionary<string, float> featureScores = null;
            if (_moduleIntegration?.IsReady == true && _currentLandmarks != null)
            {
                featureScores = _moduleIntegration.GetFeatureBreakdown(_currentLandmarks);
            }
            
            // Update FeatureIntelligence with current iteration data
            if (featureScores != null && featureScores.Count > 0)
            {
                _featureIntelligence?.UpdateIteration(_iterationsOnTarget, featureScores);
            }
            
            bool phaseChanged = _hierarchicalPhase.Iterate(_currentScore, featureScores);
            if (phaseChanged)
            {
                HandlePhaseChange();
            }
            
            // Log status periodically
            if (_iterationsOnTarget % 15 == 0)
            {
                SubModule.Log($"  [HPS] {_hierarchicalPhase.GetStatus()}");
            }
            
            // Apply morphs to character
            _faceController.SetAllMorphs(_currentMorphs);
            
            return true;
        }
        
        /// <summary>
        /// Called after frame is rendered with detected landmarks.
        /// </summary>
        public void OnRenderCaptured(float[] renderedLandmarks)
        {
            if (!IsLearning || renderedLandmarks == null || _targetLandmarks == null)
                return;
            
            // === Step 1: Calculate Score ===
            var scoreResult = CalculateScore(renderedLandmarks);
            if (!scoreResult.IsValid) return;
            
            // Report to CMA-ES
            ReportFitnessToCmaEs(scoreResult.Score);
            
            // Update session monitor
            _sessionMonitor.RecordScore(scoreResult.Score);
            
            // Log peaks
            if (scoreResult.IsNewBest && scoreResult.Improvement > 0.005f)
            {
                SubModule.Log($"  ★ Peak #{_newPeakCount}: {scoreResult.Score:F3} (+{scoreResult.Improvement:F3})");
            }
            
            // === Step 2: Make Decision ===
            var decision = _decisionEngine.Decide(
                _currentScore,
                _bestScore,
                _iterationsOnTarget,
                _consecutiveNonImprovements,
                _newPeakCount,
                _sessionMonitor.IsRapidProgress,
                _hierarchicalPhase.CurrentPhase,
                _hierarchicalPhase.CurrentSubPhase
            );
            
            // === Step 3: Execute Decision ===
            ExecuteDecision(decision);
        }
        
        #endregion
        
        #region Score Calculation
        
        private ScoreResult CalculateScore(float[] renderedLandmarks)
        {
            _previousLandmarks = _currentLandmarks;
            _currentLandmarks = renderedLandmarks;
            _previousScore = _currentScore;
            
            // NOTE: Gender scale compensation was REMOVED in v2.9.2
            // The scale affects 3D rendering size, not facial PROPORTIONS
            // Landmarks are normalized 0-1, so proportions are preserved regardless of scale
            
            // Calculate landmark score directly (no normalization needed)
            float landmarkScore = _targetRatios != null
                ? LandmarkUtils.CalculateMatchScoreOptimized(_currentLandmarks, _targetLandmarks, _targetRatios)
                : LandmarkUtils.CalculateMatchScore(_currentLandmarks, _targetLandmarks);
            
            float newScore = landmarkScore;
            Dictionary<string, float> featureBreakdown = null;
            
            // Calculate feature scores if available
            if (_moduleIntegration?.IsReady == true && _moduleIntegration.HasTarget)
            {
                bool isExploration = _hierarchicalPhase.CurrentSubPhase == HierarchicalPhaseSystem.SubPhase.Broad;
                int interval = isExploration ? 8 : 3;
                
                bool shouldRecalc = _iterationsOnTarget == 1
                    || (_iterationsOnTarget - _lastFeatureScoreIteration) >= interval
                    || Math.Abs(landmarkScore - _previousScore) > 0.02f;
                
                if (shouldRecalc)
                {
                    // Use landmarks directly for feature scoring
                    newScore = _moduleIntegration.CalculateFeatureScore(_currentLandmarks);
                    featureBreakdown = _moduleIntegration.GetFeatureBreakdown(_currentLandmarks);
                    _lastFeatureScoreIteration = _iterationsOnTarget;
                    
                    // Store starting scores
                    if (_iterationsOnTarget == 1 && featureBreakdown != null)
                    {
                        foreach (var kv in featureBreakdown)
                            _startingFeatureScores[kv.Key] = kv.Value;
                    }
                    
                    // Track morph changes and their correlation with feature scores
                    if (_featureIntelligence != null && featureBreakdown != null && _previousFeatureScores.Count > 0)
                    {
                        // Find morphs that changed significantly
                        for (int i = 0; i < _currentMorphs.Length && i < _previousMorphs.Length; i++)
                        {
                            float delta = _currentMorphs[i] - _previousMorphs[i];
                            if (Math.Abs(delta) > 0.02f)  // Only track significant changes
                            {
                                _featureIntelligence.RecordMorphChange(i, delta, _previousFeatureScores, featureBreakdown);
                            }
                        }
                    }
                    
                    // Save current feature scores for next iteration comparison
                    _previousFeatureScores.Clear();
                    foreach (var kv in featureBreakdown)
                    {
                        _previousFeatureScores[kv.Key] = kv.Value;
                    }
                    
                    // Update feature-specific bests
                    UpdateFeatureBestMorphs(featureBreakdown);
                    
                    // Feed iteration data to FeatureIntelligence for adaptive weighting
                    if (_featureIntelligence != null && featureBreakdown != null)
                    {
                        _featureIntelligence.UpdateIteration(_iterationsOnTarget, featureBreakdown);
                    }
                }
            }
            
            _currentScore = newScore;
            
            // Track improvements
            bool isNewBest = newScore > _bestScore;
            float improvement = 0;
            
            if (isNewBest)
            {
                improvement = newScore - _bestScore;
                _bestScore = newScore;
                _newPeakCount++;
                _consecutiveNonImprovements = 0;
                Array.Copy(_currentMorphs, _bestMorphs, _currentMorphs.Length);
                
                // Record improvement attempts for FeatureIntelligence
                if (_featureIntelligence != null && featureBreakdown != null)
                {
                    foreach (var kv in featureBreakdown)
                    {
                        // Check if this feature improved compared to starting
                        bool improved = _startingFeatureScores.ContainsKey(kv.Key) 
                            && kv.Value > _startingFeatureScores[kv.Key];
                        _featureIntelligence.RecordImprovementAttempt(kv.Key, improved);
                    }
                }
            }
            else
            {
                _consecutiveNonImprovements++;
                
                // Also record failed attempts (to track difficulty)
                if (_featureIntelligence != null && featureBreakdown != null && _iterationsOnTarget > 10)
                {
                    foreach (var kv in featureBreakdown)
                    {
                        _featureIntelligence.RecordImprovementAttempt(kv.Key, false);
                    }
                }
            }
            
            return new ScoreResult
            {
                IsValid = true,
                Score = newScore,
                LandmarkScore = landmarkScore,
                IsNewBest = isNewBest,
                Improvement = improvement,
                FeatureBreakdown = featureBreakdown
            };
        }
        
        private void UpdateFeatureBestMorphs(Dictionary<string, float> featureScores)
        {
            if (featureScores == null) return;
            
            foreach (var kv in featureScores)
            {
                string feature = kv.Key;
                float score = kv.Value;
                
                if (!_bestScorePerFeature.ContainsKey(feature) || score > _bestScorePerFeature[feature])
                {
                    _bestScorePerFeature[feature] = score;
                    
                    if (!_bestMorphsPerFeature.ContainsKey(feature))
                        _bestMorphsPerFeature[feature] = new float[_currentMorphs.Length];
                    
                    Array.Copy(_currentMorphs, _bestMorphsPerFeature[feature], _currentMorphs.Length);
                }
            }
        }
        
        #endregion
        
        #region Decision Execution
        
        private void ExecuteDecision(LearningDecision decision)
        {
            switch (decision.Action)
            {
                case LearningAction.Mutate:
                    GenerateNextMutation();
                    break;
                    
                case LearningAction.RevertAndMutate:
                    RevertToBest();
                    _revertCount++;
                    GenerateNextMutation();
                    break;
                    
                case LearningAction.NextTarget:
                    FinalizeTarget();
                    SubModule.Log($"  → {decision.Reason}");
                    NextTarget();
                    break;
                    
                case LearningAction.Stop:
                    Stop();
                    break;
            }
        }
        
        private void RevertToBest()
        {
            Array.Copy(_bestMorphs, _currentMorphs, _currentMorphs.Length);
            _currentScore = _bestScore;
        }
        
        #endregion
        
        #region Mutation Generation
        
        private void GenerateNextMutation()
        {
            // Get phase config
            var config = _hierarchicalPhase.CurrentConfig;
            var activeIndices = _hierarchicalPhase.ActiveMorphIndices;
            
            // Sample CMA-ES population if needed
            if (_cmaPopulation == null)
            {
                _cmaPopulation = _cmaEs.SamplePopulation();
                _cmaFitness = new double[_cmaPopulation.Length];
                _cmaPopulationIndex = 0;
            }
            
            // Get next candidate
            if (_cmaPopulationIndex < _cmaPopulation.Length)
            {
                double[] cmaCandidate = _cmaPopulation[_cmaPopulationIndex];
                _cmaPopulationIndex++;
                
                // Apply to morphs
                Array.Copy(_currentMorphs, _previousMorphs, _currentMorphs.Length);
                
                for (int i = 0; i < _currentMorphs.Length && i < cmaCandidate.Length; i++)
                {
                    if (activeIndices == null || activeIndices.Contains(i))
                    {
                        _currentMorphs[i] = Clamp((float)cmaCandidate[i]);
                    }
                    else
                    {
                        _currentMorphs[i] = _bestMorphs[i];
                    }
                }
            }
            else
            {
                // Population exhausted - sample new
                _cmaPopulation = null;
                GenerateNextMutation();
            }
        }
        
        private void ReportFitnessToCmaEs(float score)
        {
            if (_cmaEs == null || _cmaFitness == null || _cmaPopulationIndex <= 0)
                return;
            
            _cmaFitness[_cmaPopulationIndex - 1] = -score; // CMA-ES minimizes
            
            if (_cmaPopulationIndex >= _cmaPopulation.Length)
            {
                _cmaEs.Update(_cmaFitness);
                _cmaPopulation = null;
                _cmaPopulationIndex = 0;
            }
        }
        
        private float Clamp(float v) => Math.Max(-1f, Math.Min(1f, v));
        
        #endregion
        
        #region Target Management
        
        private bool NextTarget()
        {
            var sources = _dataSourceManager.GetReadySources()
                .Where(s => s.Name != "Generated")
                .ToList();
            
            if (sources.Count == 0)
            {
                SubModule.Log("No data sources ready!");
                return false;
            }
            
            _lastSourceIndex = (_lastSourceIndex + 1) % sources.Count;
            var source = sources[_lastSourceIndex];
            
            _currentTarget = source.GetNextTarget();
            
            if (_currentTarget == null)
            {
                SubModule.Log($"Failed to get target from {source.Name}");
                return false;
            }
            
            // Check image quality - skip blurry images (max 5 skips to prevent infinite loop)
            if (_currentTarget.ImageBytes != null && _qualitySkipCount < 5)
            {
                try
                {
                    // Save temp image for quality check
                    string tempPath = Path.Combine(_basePath, "Temp", "quality_check.png");
                    using (var ms = new MemoryStream(_currentTarget.ImageBytes))
                    using (var bitmap = new System.Drawing.Bitmap(ms))
                    {
                        bitmap.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    
                    var qualityResult = ImageQualityChecker.CheckQuality(tempPath);
                    
                    if (!qualityResult.IsUsable)
                    {
                        SubModule.Log($"SKIP: Image too blurry - {qualityResult.Reason} ({_currentTarget.Id})");
                        _qualitySkipCount++;
                        // Try next target recursively
                        return NextTarget();
                    }
                    else if (qualityResult.IsLowQuality)
                    {
                        SubModule.Log($"WARNING: Low quality image - {qualityResult.Reason} ({_currentTarget.Id})");
                    }
                    
                    // Reset skip counter on usable image
                    _qualitySkipCount = 0;
                }
                catch (Exception ex)
                {
                    SubModule.Log($"Quality check failed: {ex.Message}");
                    // Continue anyway if check fails
                }
            }
            else if (_qualitySkipCount >= 5)
            {
                SubModule.Log($"WARNING: Skipped 5 blurry images, using current anyway");
                _qualitySkipCount = 0;
            }
            
            // Detect landmarks if not provided
            if (_currentTarget.Landmarks == null && _currentTarget.ImageBytes != null && _detector != null)
            {
                try
                {
                    using (var ms = new MemoryStream(_currentTarget.ImageBytes))
                    using (var bitmap = new System.Drawing.Bitmap(ms))
                    {
                        _currentTarget.Landmarks = _detector.DetectLandmarks(bitmap);
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"Landmark detection failed: {ex.Message}");
                }
            }
            
            if (_currentTarget.Landmarks == null)
            {
                SubModule.Log($"No landmarks for target from {source.Name}");
                return false;
            }
            
            _targetLandmarks = _currentTarget.Landmarks;
            _targetRatios = LandmarkUtils.CalculateFaceShapeRatios(_targetLandmarks);
            
            // Set target in module integration
            if (_moduleIntegration?.IsReady == true)
            {
                _moduleIntegration.SetTargetLandmarks(_targetLandmarks);
            }
            
            // Initialize for new target
            InitializeForTarget();
            
            // Skip children (age < 18) - not useful for learning adult face morphing
            if (_cachedDemographics.HasValue && _cachedDemographics.Value.Age < 18 && _ageSkipCount < 5)
            {
                SubModule.Log($"SKIP: Child detected (age={_cachedDemographics.Value.Age:F0}) - ({_currentTarget.Id})");
                _ageSkipCount++;
                return NextTarget();
            }
            else if (_ageSkipCount >= 5)
            {
                SubModule.Log($"WARNING: Skipped 5 children, using current anyway");
                _ageSkipCount = 0;
            }
            else
            {
                _ageSkipCount = 0;  // Reset on adult
            }
            
            TargetsProcessed++;
            _targetsSinceEvolution++;
            _targetsSinceCleanup++;
            
            // Cleanup periodically
            if (_targetsSinceCleanup >= 100)
            {
                _targetsSinceCleanup = 0;
                GC.Collect(0);
            }
            
            LogTargetInfo();
            _state = LearningState.WaitingForRender;
            
            return true;
        }
        
        private void InitializeForTarget()
        {
            // Detect demographics using GenderEnsemble (combines FairFace + Hair/Appearance + Landmarks)
            // This is more robust than FairFace alone, especially for older women with strong features
            _cachedDemographics = null;
            _currentTargetImagePath = null;
            _detectedRace = null;
            _detectedRaceConf = 0f;
            
            if (_currentTarget?.ImageBytes != null)
            {
                try
                {
                    using (var ms = new MemoryStream(_currentTarget.ImageBytes))
                    using (var bitmap = new System.Drawing.Bitmap(ms))
                    {
                        // Save target image first (needed for GenderEnsemble which uses file path)
                        _currentTargetImagePath = Path.Combine(_basePath, "Temp", "current_target.png");
                        Directory.CreateDirectory(Path.GetDirectoryName(_currentTargetImagePath));
                        bitmap.Save(_currentTargetImagePath, System.Drawing.Imaging.ImageFormat.Png);
                        
                        // Use GenderEnsemble for more robust gender detection
                        // Combines: FairFace + HairAppearance + Landmarks voting
                        // This catches cases like older women with strong facial features
                        if (_genderEnsemble != null)
                        {
                            var ensembleResult = _genderEnsemble.Detect(_currentTargetImagePath, null, null, 0.5f);
                            _cachedDemographics = new DemographicDetector.Demographics
                            {
                                IsFemale = ensembleResult.IsFemale,
                                Age = ensembleResult.Age,
                                Confidence = ensembleResult.Confidence
                            };
                            _detectedRace = ensembleResult.Race;
                            _detectedRaceConf = ensembleResult.RaceConfidence;
                            
                            // Log ensemble decision
                            string votesSummary = string.Join(", ", ensembleResult.Votes?.Select(v => v.ToString()) ?? new[] { "N/A" });
                            SubModule.Log($"  GenderEnsemble: {(ensembleResult.IsFemale ? "Female" : "Male")} (conf={ensembleResult.Confidence:F2}) age={ensembleResult.Age:F0} race={ensembleResult.Race}");
                            SubModule.Log($"    Decision: {ensembleResult.Decision}");
                            SubModule.Log($"    Votes: {votesSummary}");
                        }
                        // Fallback to FairFace only if GenderEnsemble not available
                        else if (_fairFaceDetector != null && _fairFaceDetector.IsLoaded)
                        {
                            var (isFemale, genderConf, age, race, raceConf) = _fairFaceDetector.Detect(bitmap);
                            _cachedDemographics = new DemographicDetector.Demographics
                            {
                                IsFemale = isFemale,
                                Age = age,
                                Confidence = genderConf
                            };
                            _detectedRace = race;
                            _detectedRaceConf = raceConf;
                            SubModule.Log($"  FairFace: {(isFemale ? "Female" : "Male")} (conf={genderConf:F2}) age={age:F0} race={race} (conf={raceConf:F2})");
                        }
                        // Fallback to DemographicDetector if nothing else available
                        else if (_demographicDetector != null)
                        {
                            // Convert to RGB pixel array for DemographicDetector
                            var rgbPixels = new byte[bitmap.Width * bitmap.Height * 3];
                            int idx = 0;
                            for (int y = 0; y < bitmap.Height; y++)
                            {
                                for (int x = 0; x < bitmap.Width; x++)
                                {
                                    var pixel = bitmap.GetPixel(x, y);
                                    rgbPixels[idx++] = pixel.R;
                                    rgbPixels[idx++] = pixel.G;
                                    rgbPixels[idx++] = pixel.B;
                                }
                            }
                            _cachedDemographics = _demographicDetector.Detect(rgbPixels, bitmap.Width, bitmap.Height);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"  Demographics detection failed: {ex.Message}");
                }
            }
            
            // Apply target attributes to character
            ApplyTargetAttributesToCharacter();
            
            // Initialize morphs - use actual count from FaceController
            _currentMorphs = _faceController.GetAllMorphs();
            int numMorphs = _currentMorphs?.Length ?? 62;
            if (_currentMorphs == null)
                _currentMorphs = new float[numMorphs];
            
            _bestMorphs = new float[numMorphs];
            _previousMorphs = new float[numMorphs];
            _startingMorphs = new float[numMorphs];
            Array.Copy(_currentMorphs, _bestMorphs, numMorphs);
            Array.Copy(_currentMorphs, _previousMorphs, numMorphs);
            Array.Copy(_currentMorphs, _startingMorphs, numMorphs);  // Store initial state for learning
            
            // Reset scores
            _currentScore = 0;
            _previousScore = 0;
            _bestScore = 0;
            _newPeakCount = 0;
            _revertCount = 0;
            _consecutiveNonImprovements = 0;
            _iterationsOnTarget = 0;
            _phaseIterations = 0;
            _lastFeatureScoreIteration = -1;
            
            // Reset feature tracking
            _bestMorphsPerFeature.Clear();
            _bestScorePerFeature.Clear();
            _startingFeatureScores.Clear();
            _previousFeatureScores.Clear();
            
            // Reset FeatureIntelligence for new target
            _featureIntelligence?.ResetForNewTarget();
            
            // Reset CMA-ES with correct dimension
            double[] initial = new double[numMorphs];
            for (int i = 0; i < numMorphs; i++)
                initial[i] = _currentMorphs[i];
            _cmaEs.SetMean(initial);
            _cmaEs.Reset();
            _cmaPopulation = null;
            _cmaPopulationIndex = 0;
            
            // Reset phase system
            _hierarchicalPhase.Reset();
        }
        
        private void ApplyTargetAttributesToCharacter()
        {
            // Apply gender from cached demographics
            if (_cachedDemographics.HasValue)
            {
                bool targetIsFemale = _cachedDemographics.Value.IsFemale;
                bool currentIsFemale = _faceController.IsFemale;
                
                if (targetIsFemale != currentIsFemale)
                {
                    _faceController.IsFemale = targetIsFemale;
                    SubModule.Log($"  Gender changed: {(currentIsFemale ? "Female" : "Male")} → {(targetIsFemale ? "Female" : "Male")}");
                }
                
                // Apply age (FaceController clamps to 18-100, we cap below 20 to 20)
                float targetAge = _cachedDemographics.Value.Age;
                float appliedAge = Math.Max(20f, targetAge); // Minimum 20 for Bannerlord
                float currentAge = _faceController.Age;
                
                // Only log if significant change (> 5 years)
                if (Math.Abs(appliedAge - currentAge) > 5f)
                {
                    _faceController.Age = appliedAge;
                    SubModule.Log($"  Age applied: {appliedAge:F0} (detected {targetAge:F0})");
                }
                else
                {
                    _faceController.Age = appliedAge;
                }
            }
            
            // Detect and apply skin tone
            if (_colorEnsemble != null && !string.IsNullOrEmpty(_currentTargetImagePath) && _targetLandmarks != null)
            {
                try
                {
                    var skinResult = _colorEnsemble.Detect(_currentTargetImagePath, _targetLandmarks, null);
                    if (skinResult.Confidence > 0.3f)
                    {
                        _faceController.SkinColor = skinResult.Value;
                        SubModule.Log($"  Skin tone applied: {skinResult.Value:F2} (conf={skinResult.Confidence:F2})");
                    }
                    
                    // Detect and apply eye color
                    var eyeResult = _colorEnsemble.DetectEyeColor(_currentTargetImagePath, _targetLandmarks);
                    if (eyeResult.Confidence > 0.3f)
                    {
                        _faceController.EyeColor = eyeResult.Value;
                        SubModule.Log($"  Eye color applied: {eyeResult.Value:F2} (conf={eyeResult.Confidence:F2})");
                    }
                    
                    // Detect and apply hair color
                    var (hairResult, hairDetected) = _colorEnsemble.DetectHairColor(_currentTargetImagePath, _targetLandmarks, skinResult.Value);
                    if (hairDetected && hairResult.Confidence > 0.3f)
                    {
                        _faceController.HairColor = hairResult.Value;
                        SubModule.Log($"  Hair color applied: {hairResult.Value:F2} (conf={hairResult.Confidence:F2})");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"  Color detection failed: {ex.Message}");
                }
            }
            
            // NOTE: Body props (Weight, Build, Height) and Hair/Beard styles are NOT changed
            // during learning to keep camera stable and focus on face morphs.
            // These are only applied in Photo Detection mode (GenerateFaceComplete).
        }
        
        private void FinalizeTarget()
        {
            // Combine best features
            CombineBestFeatureMorphs();
            
            // Apply final
            _faceController.SetAllMorphs(_bestMorphs);
            
            // Log
            float revertPct = _iterationsOnTarget > 0 ? (_revertCount * 100f / _iterationsOnTarget) : 0;
            SubModule.Log($"  Done: {_bestScore:F3} @ {_iterationsOnTarget} | Peaks:{_newPeakCount} Reverts:{_revertCount} ({revertPct:F0}%)");
            
            // Update feature difficulty tracking
            if (_featureIntelligence != null && _bestScorePerFeature.Count > 0)
            {
                _featureIntelligence.UpdateFeatureDifficulty(_bestScorePerFeature);
            }
            
            // === LEARNING: Update knowledge from this target ===
            float improvement = _bestScore - 0.5f;  // Improvement over baseline (0.5)
            
            // Update hierarchical tree with learned morphs
            if (_hierarchicalTree != null && _targetLandmarks != null && improvement > 0 && _startingMorphs != null)
            {
                var metadata = new Dictionary<string, float>();
                if (_cachedDemographics.HasValue)
                {
                    metadata["gender"] = _cachedDemographics.Value.IsMale ? 0f : 1f;
                    metadata["age"] = _cachedDemographics.Value.Age;
                }
                
                _hierarchicalTree.LearnFrom(
                    _targetLandmarks,
                    metadata,
                    _startingMorphs,
                    _bestMorphs,
                    improvement);
            }
            
            // Update feature-morph correlations
            if (_featureMorphLearning != null && _startingFeatureScores.Count > 0 && _bestScorePerFeature.Count > 0)
            {
                _featureMorphLearning.RecordExperiment(
                    _startingMorphs,
                    _bestMorphs,
                    _startingFeatureScores,
                    _bestScorePerFeature);
            }
            
            // Create comparison image
            if (!string.IsNullOrEmpty(_currentTargetImagePath) && !string.IsNullOrEmpty(_screenshotPath))
            {
                string info = _cachedDemographics.HasValue
                    ? $"{(_cachedDemographics.Value.IsMale ? "Male" : "Female")}, Age ~{_cachedDemographics.Value.Age:F0}"
                    : "";
                
                // Always save comparison for every target
                ComparisonImageCreator.CreateComparison(
                    _currentTargetImagePath,
                    _screenshotPath,
                    _bestScore,
                    _currentTarget?.Id ?? "unknown",
                    info);
                
                // Also update the "current" comparison
                ComparisonImageCreator.UpdateCurrentComparison(
                    _currentTargetImagePath,
                    _screenshotPath,
                    _bestScore,
                    info);
            }
        }
        
        private void CombineBestFeatureMorphs()
        {
            if (_bestMorphsPerFeature.Count < 2) return;
            
            int numMorphs = _bestMorphs.Length;
            float[] combined = new float[numMorphs];
            float[] weights = new float[numMorphs];
            
            foreach (var featureKv in _bestMorphsPerFeature)
            {
                string featureName = featureKv.Key;
                float[] featureMorphs = featureKv.Value;
                float featureScore = _bestScorePerFeature.ContainsKey(featureName)
                    ? _bestScorePerFeature[featureName]
                    : 0.5f;
                
                if (MorphFeatureMapping.FeatureGroups.TryGetValue(featureName, out int[] indices))
                {
                    foreach (int idx in indices)
                    {
                        if (idx < numMorphs && idx < featureMorphs.Length)
                        {
                            combined[idx] += featureMorphs[idx] * featureScore;
                            weights[idx] += featureScore;
                        }
                    }
                }
            }
            
            for (int i = 0; i < numMorphs; i++)
            {
                if (weights[i] > 0)
                    combined[i] /= weights[i];
                else
                    combined[i] = _bestMorphs[i];
            }
            
            Array.Copy(combined, _bestMorphs, numMorphs);
        }
        
        private void LogTargetInfo()
        {
            string demo = _cachedDemographics.HasValue
                ? $"{(_cachedDemographics.Value.IsMale ? "M" : "F")} Age:{_cachedDemographics.Value.Age:F0}"
                : "?";
            
            SubModule.Log($"Target: {_currentTarget.Id} | {_currentTarget.Source ?? "?"} | {demo}");
        }
        
        #endregion
        
        #region Phase Handling
        
        private void HandlePhaseChange()
        {
            // Revert to best
            Array.Copy(_bestMorphs, _currentMorphs, _currentMorphs.Length);
            _currentScore = _bestScore;
            
            // Reset CMA-ES for new phase
            var config = _hierarchicalPhase.CurrentConfig;
            double[] bestAsDouble = new double[_bestMorphs.Length];
            for (int i = 0; i < _bestMorphs.Length; i++)
                bestAsDouble[i] = _bestMorphs[i];
            
            _cmaEs.SetMean(bestAsDouble);
            _cmaEs.SetMinSigma(config.MinSigma);
            _cmaEs.SetSigma(config.MaxSigma);
            _cmaPopulation = null;
            _cmaPopulationIndex = 0;
            
            // Log
            int activeCount = _hierarchicalPhase.ActiveMorphIndices?.Count ?? 0;
            SubModule.Log($"  ═══════════════════════════════════════════════════════════════");
            SubModule.Log($"  [HPS] PHASE CHANGE: {config.Name}");
            SubModule.Log($"        Active: {activeCount} morphs | σ={config.MaxSigma:F2}");
            SubModule.Log($"  ═══════════════════════════════════════════════════════════════");
        }
        
        #endregion
        
        #region Public API - Status Methods
        
        public string GetFeatureIntelligenceStatus()
        {
            if (_featureIntelligence == null) return "Not initialized";
            return _featureIntelligence.GetFullSummary();
        }
        
        public string GetStatus()
        {
            string phaseName = _hierarchicalPhase?.CurrentConfig?.Name ?? "None";
            string subPhase = _hierarchicalPhase?.CurrentSubPhase.ToString() ?? "";
            string trend = _sessionMonitor?.IsRapidProgress == true ? "↑↑" :
                          _sessionMonitor?.IsRegressing == true ? "↓↓" :
                          _sessionMonitor?.IsStagnant == true ? "—" : "↑";
            
            string targetShort = _currentTarget?.Id ?? "None";
            if (targetShort.Length > 20)
                targetShort = targetShort.Substring(0, 20) + "...";
            
            int estMax = GetEstimatedMaxIterations();
            
            return $"[{phaseName}/{subPhase}] {_currentScore:F3} (Best:{_bestScore:F3}) {trend}\n" +
                   $"{targetShort} | {_iterationsOnTarget}/{estMax}";
        }
        
        private int GetEstimatedMaxIterations()
        {
            // Estimate based on score quality
            if (_bestScore >= 0.92f) return 100;
            if (_bestScore >= 0.85f) return 150;
            if (_bestScore >= 0.75f) return 200;
            if (_bestScore >= 0.60f) return 250;
            return 300;
        }
        
        public string GetRunStatus()
        {
            // Target name
            string targetName = _currentTarget?.Id ?? "None";
            if (targetName.Length > 30) targetName = targetName.Substring(0, 27) + "...";
            string targetInfo = $"Target: {targetName}";
            
            // Score info
            string scoreInfo = $"Score: {_currentScore:F3} (Best: {_bestScore:F3})";
            
            // Target/iteration info  
            int estMax = GetEstimatedMaxIterations();
            string iterInfo = $"Iter: {_iterationsOnTarget}/{estMax}";
            
            // Phase info
            string phaseName = _hierarchicalPhase?.CurrentConfig?.Name ?? "None";
            string subPhase = _hierarchicalPhase?.CurrentSubPhase.ToString() ?? "";
            string phaseInfo = $"Phase: {phaseName}/{subPhase}";
            
            // Feature focus
            string focusInfo = "";
            var focusedFeatures = _featureIntelligence?.FocusedFeatures;
            if (focusedFeatures?.Count > 0)
            {
                focusInfo = $"\nFocus: {string.Join(", ", focusedFeatures)}";
            }
            
            // Active features from current phase
            string activeInfo = "";
            var activeFeatures = _hierarchicalPhase?.CurrentConfig?.ActiveFeatures;
            if (activeFeatures?.Count > 0 && !activeFeatures.Contains("All"))
            {
                activeInfo = $"\nActive: {string.Join(", ", activeFeatures)}";
            }
            
            return $"{targetInfo}\n{scoreInfo}\n{iterInfo} | {phaseInfo}{focusInfo}{activeInfo}";
        }
        
        public string GetExtendedStatus()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Learning Status ===");
            sb.AppendLine($"Target: {_currentTarget?.Id ?? "None"}");
            sb.AppendLine($"Score: {_currentScore:F3} (Best: {_bestScore:F3})");
            sb.AppendLine($"Phase: {_hierarchicalPhase?.CurrentConfig?.Name ?? "None"}");
            sb.AppendLine($"Iterations: {_iterationsOnTarget} on target, {TotalIterations} total");
            sb.AppendLine($"Peaks: {_newPeakCount} | Reverts: {_revertCount}");
            return sb.ToString();
        }
        
        public string GetTreeStatus()
        {
            if (_hierarchicalTree == null) return "N/A";
            
            // Get tree summary
            string treeSummary = _hierarchicalTree.GetSummary();
            
            // Get FeatureMorphLearning summary
            string fmlSummary = _featureMorphLearning?.GetSummary() ?? "";
            
            return treeSummary + (string.IsNullOrEmpty(fmlSummary) ? "" : "\n" + fmlSummary);
        }
        
        public string GetPhaseStats()
        {
            if (_hierarchicalPhase == null) return "N/A";
            
            var config = _hierarchicalPhase.CurrentConfig;
            string phaseName = config?.Name ?? "None";
            string subPhase = _hierarchicalPhase.CurrentSubPhase.ToString();
            int activeCount = _hierarchicalPhase.ActiveMorphIndices?.Count ?? 0;
            float sigma = _hierarchicalPhase.GetRecommendedSigma();
            
            // Phase manager stats
            int totalPhases = _phaseManager?.TotalPhases ?? 0;
            int evolvedPhases = _phaseManager?.EvolvedPhases ?? 0;
            
            return $"─ PHASE ─\n" +
                   $"Main: {phaseName}\n" +
                   $"Sub: {subPhase}\n" +
                   $"Active: {activeCount} morphs | σ={sigma:F2}\n" +
                   $"Evolved: {totalPhases} ({evolvedPhases} gen)";
        }
        
        public string GetSessionTrend()
        {
            if (_sessionMonitor == null) return "N/A";
            
            string trendDir = _sessionMonitor.ShortTermTrend > 0.001f ? "📈" : 
                             _sessionMonitor.ShortTermTrend < -0.001f ? "📉" : "➡️";
            
            return $"Trend: {trendDir} {_sessionMonitor.ShortTermTrend:+0.000;-0.000} | Vol: {_sessionMonitor.Volatility:F3} | Interventions: {_sessionMonitor.InterventionCount}";
        }
        
        public string GetTrendIndicator()
        {
            if (_sessionMonitor == null) return "?";
            if (_sessionMonitor.IsRapidProgress) return "↑↑";
            if (_sessionMonitor.IsRegressing) return "↓↓";
            if (_sessionMonitor.IsStagnant) return "—";
            return "↑";
        }
        
        public HierarchicalKnowledge GetHierarchicalKnowledge()
        {
            return _hierarchicalTree;
        }
        
        /// <summary>Get FairFace detector for Photo Detection</summary>
        public FairFaceDetector GetFairFaceDetector()
        {
            return _fairFaceDetector;
        }
        
        /// <summary>Get ColorEnsemble for Photo Detection</summary>
        public ColorEnsemble GetColorEnsemble()
        {
            return _colorEnsemble;
        }
        
        /// <summary>Get ModuleIntegration for Photo Detection</summary>
        public ModuleIntegration GetModuleIntegration()
        {
            return _moduleIntegration;
        }
        
        /// <summary>Get GenderEnsemble for robust gender detection in Photo Detection</summary>
        public GenderEnsemble GetGenderEnsemble()
        {
            return _genderEnsemble;
        }
        
        /// <summary>Get RacePresetManager for race configuration</summary>
        public RacePresetManager GetRacePresetManager()
        {
            return _racePresetManager;
        }
        
        /// <summary>Get RaceAwareFaceGenerator for race-biased generation</summary>
        public RaceAwareFaceGenerator GetRaceGenerator()
        {
            return _raceGenerator;
        }
        
        /// <summary>Set the active race for face generation</summary>
        public bool SetRace(string raceId)
        {
            if (_raceGenerator == null) return false;
            return _raceGenerator.SetRace(raceId);
        }
        
        /// <summary>Get list of available race presets</summary>
        public IEnumerable<string> GetAvailableRaces()
        {
            return _racePresetManager?.Presets.Keys ?? Enumerable.Empty<string>();
        }
        
        /// <summary>Get available race categories (Elven, Dwarven, Orcish, etc.)</summary>
        public IEnumerable<string> GetRaceCategories()
        {
            return _racePresetManager?.Categories ?? Enumerable.Empty<string>();
        }
        
        public void ValidateTree(bool autoClean = false)
        {
            // Perform maintenance instead of validation
            if (autoClean)
            {
                _hierarchicalTree?.PerformMaintenance();
            }
        }
        
        #endregion
        
        #region Public API - Demographics
        
        public bool DownloadFairFace()
        {
            // FairFace download logic - kept for API compatibility
            SubModule.Log("FairFace model should be deployed with the mod");
            return _fairFaceDetector?.IsLoaded ?? false;
        }
        
        public (bool isFemale, float genderConf, float age) DetectDemographics(Bitmap bitmap)
        {
            if (_fairFaceDetector?.IsLoaded == true)
            {
                try
                {
                    var (isFemale, genderConf, age, _, _) = _fairFaceDetector.Detect(bitmap);
                    return (isFemale, genderConf, age);
                }
                catch (Exception ex)
                {
                    SubModule.Log($"FairFace error: {ex.Message}");
                }
            }
            
            // Fallback
            return (true, 0.5f, 25f);
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            Stop();
            _detector?.Dispose();
            _fairFaceDetector?.Dispose();
            _demographicDetector?.Dispose();
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    internal struct ScoreResult
    {
        public bool IsValid;
        public float Score;
        public float LandmarkScore;
        public bool IsNewBest;
        public float Improvement;
        public Dictionary<string, float> FeatureBreakdown;
    }
    
    #endregion
}
