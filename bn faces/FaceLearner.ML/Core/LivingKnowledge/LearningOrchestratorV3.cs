using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using FaceLearner.Core;
using FaceLearner.Core.LivingKnowledge;  // Legacy systems for generation
using FaceLearner.ML.Core.HierarchicalScoring;
using FaceLearner.ML.Core.RaceSystem;
using FaceLearner.ML.DataSources;
using FaceLearner.ML.Utils;

namespace FaceLearner.ML.Core.LivingKnowledge
{
    /// <summary>
    /// Learning Orchestrator V3 - Hierarchical system.
    /// </summary>
    public class LearningOrchestratorV3 : IDisposable
    {
        #region State
        
        private enum State { Idle, WaitingForRender, Evaluating }
        private State _state = State.Idle;
        
        #endregion
        
        #region Core Systems
        
        private readonly LandmarkDetector _detector;
        private readonly FaceController _faceController;
        private readonly DataSourceManager _dataSourceManager;
        private readonly string _basePath;
        private readonly string _coreDataPath;
        private string _screenshotPath;
        
        public string ScreenshotPath 
        { 
            get => _screenshotPath; 
            set => _screenshotPath = value; 
        }
        
        // Hierarchical systems (for LEARNING)
        private readonly HierarchicalScorer _scorer;
        private readonly HierarchicalPhaseController _phaseController;
        private readonly MorphLockManager _lockManager;
        private readonly FeatureExtractor _featureExtractor;
        
        // Knowledge systems (SHARED between learning & generation)
        private HierarchicalKnowledge _hierarchicalKnowledge;
        private ModuleIntegration _moduleIntegration;
        private FeatureMorphLearning _featureMorphLearning;  // Learns which morphs affect which features
        private FeatureIntelligence _featureIntelligence;    // Tracks feature difficulty
        private PhaseManager _phaseManager;                   // Learns optimal phase transitions
        private OrchestratorMemory _orchestratorMemory;       // Learns which strategies work best
        
        private readonly Random _random = new Random();
        
        #endregion
        
        #region Demographics & Race
        
        private DemographicDetector _demographicDetector;
        private FairFaceDetector _fairFaceDetector;
        private GenderEnsemble _genderEnsemble;
        private ColorEnsemble _colorEnsemble;
        private AgeEnsemble _ageEnsemble;
        private RacePresetManager _racePresetManager;
        private RaceAwareFaceGenerator _raceGenerator;
        
        public bool IsFairFaceLoaded => _fairFaceDetector?.IsLoaded ?? false;
        
        #endregion
        
        #region Target State
        
        private TargetFace _currentTarget;
        private float[] _targetLandmarks;
        private FeatureSet _targetFeatures;
        private string _currentTargetImagePath;
        private DemographicDetector.Demographics? _cachedDemographics;
        private int _lastSourceIndex = -1;
        
        #endregion
        
        #region Morph State
        
        private float[] _currentMorphs;
        private float[] _bestMorphs;
        private float[] _startingMorphs;  // For learning: track starting state
        private float[] _bestMorphsForSubPhase;
        private float[] _previousMorphs;  // For learning: track previous iteration
        private float _bestScoreForSubPhase;
        
        #endregion
        
        #region Score State
        
        private float _currentScore;
        private float _previousScore;  // For learning: track previous iteration
        private float _bestTotalScore;
        private float _startingScore;  // v3.0.20: Track initial score for improvement calculation
        private HierarchicalScore _lastHierarchicalScore;
        private HierarchicalScore _previousHierarchicalScore;  // For learning
        private HierarchicalScore _bestHierarchicalScore;  // v3.0.24: For feature report at comparison time
        private FeatureSet _bestRenderFeatures;  // v3.0.24: Best render features for diagnostics
        private readonly List<float> _scoreHistory = new List<float>();
        
        #endregion
        
        #region Statistics
        
        public bool IsLearning { get; private set; }
        public int TotalIterations { get; private set; }
        public int TargetsProcessed { get; private set; }
        public int IterationsOnTarget { get; private set; }
        
        private DateTime _sessionStart;
        private int _peakCount;
        private float _sessionBestScore;
        
        // Phase tracking for learning
        private float _phaseStartScore;
        private int _phaseStartIteration;
        private SubPhase _lastSubPhase;
        
        #endregion
        
        #region Constants
        
        private const int NUM_MORPHS = 62;
        private const int MAX_ITERATIONS_PER_TARGET = 250;
        
        #endregion
        
        #region Constructor
        
        public LearningOrchestratorV3(
            LandmarkDetector detector,
            FaceController faceController,
            DataSourceManager dataSourceManager,
            string basePath,
            string coreDataPath = null)
        {
            _detector = detector;
            _faceController = faceController;
            _dataSourceManager = dataSourceManager;
            _basePath = basePath;
            _coreDataPath = coreDataPath ?? basePath;
            
            // Hierarchical systems
            _scorer = new HierarchicalScorer();
            _lockManager = new MorphLockManager();
            _phaseController = new HierarchicalPhaseController { LockManager = _lockManager };
            _featureExtractor = new FeatureExtractor();
            
            _phaseController.OnPhaseChanged += OnPhaseChanged;
            _phaseController.OnSubPhaseComplete += OnSubPhaseComplete;
            _phaseController.OnMainPhaseComplete += OnMainPhaseComplete;
            
            _currentMorphs = new float[NUM_MORPHS];
            _bestMorphs = new float[NUM_MORPHS];
            
            InitializeDemographics();
            InitializeLegacySystems();
            ComparisonImageCreator.Initialize(_basePath);
            
            SubModule.Log("LearningOrchestratorV3 initialized (Hierarchical)");
        }
        
        private void InitializeDemographics()
        {
            _demographicDetector = new DemographicDetector();
            _demographicDetector.Initialize(_basePath);
            
            _fairFaceDetector = new FairFaceDetector();
            string fairFacePath = _basePath + "Models/fairface.onnx";
            if (File.Exists(fairFacePath))
            {
                _fairFaceDetector.Load(fairFacePath);
            }
            
            _genderEnsemble = new GenderEnsemble();
            _genderEnsemble.Initialize(fairFacePath);
            
            _colorEnsemble = new ColorEnsemble();
            _colorEnsemble.Initialize(_fairFaceDetector);
            
            _ageEnsemble = new AgeEnsemble();
            _ageEnsemble.Initialize(_fairFaceDetector);
            
            _racePresetManager = new RacePresetManager(_basePath);
            _racePresetManager.Load();
            _raceGenerator = new RaceAwareFaceGenerator(_racePresetManager);
        }
        
        private void InitializeLegacySystems()
        {
            // All 5 knowledge systems accumulate intelligence over time!
            string knowledgePath = _coreDataPath ?? _basePath;
            
            // 1. Hierarchical Knowledge - landmark→morph mappings
            _hierarchicalKnowledge = new HierarchicalKnowledge(knowledgePath + "hierarchical_knowledge.dat");
            if (_hierarchicalKnowledge.IsEmpty)
                SubModule.Log($"  HierarchicalKnowledge: EMPTY (starting fresh)");
            else
                SubModule.Log($"  HierarchicalKnowledge: {_hierarchicalKnowledge.GetSummary()}");
            
            // 2. Feature-Morph Learning - which morphs affect which features
            _featureMorphLearning = new FeatureMorphLearning(knowledgePath + "feature_morph_learning.dat");
            if (_featureMorphLearning.IsEmpty)
                SubModule.Log($"  FeatureMorphLearning: EMPTY (starting fresh)");
            else
                SubModule.Log($"  FeatureMorphLearning: {_featureMorphLearning.GetSummary()}");
            
            // 3. Feature Intelligence - difficulty tracking
            _featureIntelligence = new FeatureIntelligence(knowledgePath + "feature_intelligence.dat");
            SubModule.Log($"  FeatureIntelligence: loaded");
            
            // 4. Phase Manager - optimal phase transitions
            _phaseManager = new PhaseManager(knowledgePath + "phase_manager.dat");
            SubModule.Log($"  PhaseManager: {_phaseManager.GetSummary()}");
            
            // 5. Orchestrator Memory - strategy effectiveness
            _orchestratorMemory = new OrchestratorMemory(knowledgePath + "orchestrator_memory.dat");
            SubModule.Log($"  OrchestratorMemory: {_orchestratorMemory.GetStatsSummary()}");
            
            // 6. Module Integration - face analysis
            _moduleIntegration = new ModuleIntegration();
            if (_moduleIntegration.Initialize(_basePath))
                SubModule.Log("  ModuleIntegration: ready");
        }
        
        #endregion
        
        #region Public API - Lifecycle
        
        public void Start()
        {
            IsLearning = true;
            TotalIterations = 0;
            _sessionStart = DateTime.Now;
            _sessionBestScore = 0;
            _peakCount = 0;
            
            SubModule.Log("=== LEARNING V3 STARTED (Hierarchical) ===");
            NextTarget();
        }
        
        public void Stop()
        {
            if (IsLearning)
            {
                // Save current target's learned data before stopping
                SaveToKnowledgeTree();
                
                // Persist ALL 5 knowledge systems to disk
                _hierarchicalKnowledge?.Save();
                _featureMorphLearning?.Save();
                _featureIntelligence?.Save();
                _phaseManager?.Save();
                _orchestratorMemory?.Save();
                SubModule.Log($"  [Learn] All 5 knowledge systems saved");
            }
            
            IsLearning = false;
            _state = State.Idle;
            SubModule.Log($"=== LEARNING V3 STOPPED | Targets: {TargetsProcessed} | Iter: {TotalIterations} ===");
        }
        
        #endregion
        
        #region Public API - Main Loop
        
        public bool Iterate()
        {
            if (!IsLearning || _targetLandmarks == null)
                return false;
            
            TotalIterations++;
            IterationsOnTarget++;
            
            _faceController.SetAllMorphs(_currentMorphs);
            
            return true;
        }
        
        public void OnRenderCaptured(float[] renderedLandmarks)
        {
            if (!IsLearning || renderedLandmarks == null || _targetLandmarks == null)
                return;
            
            // Store previous state for learning
            _previousScore = _currentScore;
            _previousHierarchicalScore = _lastHierarchicalScore;
            if (_previousMorphs == null) _previousMorphs = new float[NUM_MORPHS];
            Array.Copy(_currentMorphs, _previousMorphs, NUM_MORPHS);
            
            // Score
            var currentFeatures = _featureExtractor.Extract(renderedLandmarks);
            var score = _scorer.Calculate(_targetFeatures, currentFeatures);
            
            _lastHierarchicalScore = score;
            _currentScore = score.Total;
            _scoreHistory.Add(_currentScore);
            
            // v3.0.20: Track starting score for improvement calculation
            if (IterationsOnTarget == 0 || _startingScore <= 0)
            {
                _startingScore = _currentScore;
            }
            
            // LEARNING: Record experiment for FeatureMorphLearning
            if (_featureMorphLearning != null && _previousHierarchicalScore != null && IterationsOnTarget > 1)
            {
                RecordMorphExperiment(score);
            }
            
            float subPhaseScore = _scorer.CalculateSubPhaseScore(
                _phaseController.CurrentSubPhase,
                _targetFeatures,
                currentFeatures);
            
            // CRITICAL FIX: Track best morphs for THIS SubPhase based on TOTAL score, not SubPhase score!
            // This prevents accepting changes that improve one feature but hurt others.
            if (score.Total > _bestScoreForSubPhase)
            {
                _bestScoreForSubPhase = score.Total;  // Changed from subPhaseScore!
                _bestMorphsForSubPhase = (float[])_currentMorphs.Clone();
            }
            
            // Detailed status logging every 25 iterations
            if (IterationsOnTarget % 25 == 0)
            {
                SubModule.Log($"  [{_phaseController.CurrentSubPhase}] iter={IterationsOnTarget}");
                SubModule.Log($"    Total: current={_currentScore:F3} best={_bestTotalScore:F3}");
                SubModule.Log($"    SubPhase: current={subPhaseScore:F3} best={_bestScoreForSubPhase:F3}");
                SubModule.Log($"    Breakdown: F={score.FoundationScore:F2} S={score.StructureScore:F2} M={score.MajorFeaturesScore:F2} D={score.FineDetailsScore:F2}");
                
                // DEBUG: Log raw feature values to check if they're changing
                if (IterationsOnTarget == 25 || IterationsOnTarget == 100)
                {
                    SubModule.Log($"    [DEBUG] Target Eyes: W={_targetFeatures.EyeWidth:F3} H={_targetFeatures.EyeHeight:F3} D={_targetFeatures.EyeDistance:F3}");
                    SubModule.Log($"    [DEBUG] Current Eyes: W={currentFeatures.EyeWidth:F3} H={currentFeatures.EyeHeight:F3} D={currentFeatures.EyeDistance:F3}");
                    SubModule.Log($"    [DEBUG] Target Mouth: W={_targetFeatures.MouthWidth:F3} H={_targetFeatures.MouthHeight:F3}");
                    SubModule.Log($"    [DEBUG] Current Mouth: W={currentFeatures.MouthWidth:F3} H={currentFeatures.MouthHeight:F3}");
                    SubModule.Log($"    [DEBUG] Target Brows: H={_targetFeatures.EyebrowHeight:F3} A={_targetFeatures.EyebrowArch:F3}");
                    SubModule.Log($"    [DEBUG] Current Brows: H={currentFeatures.EyebrowHeight:F3} A={currentFeatures.EyebrowArch:F3}");
                    // NEW: Jaw/Chin features (critical for round vs pointed!)
                    SubModule.Log($"    [DEBUG] Target Jaw: Curve={_targetFeatures.JawCurvature:F3} Taper={_targetFeatures.JawTaper:F3}");
                    SubModule.Log($"    [DEBUG] Current Jaw: Curve={currentFeatures.JawCurvature:F3} Taper={currentFeatures.JawTaper:F3}");
                    SubModule.Log($"    [DEBUG] Target Chin: Point={_targetFeatures.ChinPointedness:F3} Drop={_targetFeatures.ChinDrop:F3}");
                    SubModule.Log($"    [DEBUG] Current Chin: Point={currentFeatures.ChinPointedness:F3} Drop={currentFeatures.ChinDrop:F3}");
                }
            }
            
            if (score.Total > _bestTotalScore)
            {
                _bestTotalScore = score.Total;
                _bestMorphs = (float[])_currentMorphs.Clone();
                _bestHierarchicalScore = score;  // v3.0.24: Save for feature report
                _bestRenderFeatures = currentFeatures;  // v3.0.24: Save for feature report
                _peakCount++;
                if (score.Total > _sessionBestScore) _sessionBestScore = score.Total;
                SubModule.Log($"  ★ Peak: {score.Total:F3}");
            }
            
            // CRITICAL FIX: Report TOTAL score to PhaseController, not SubPhase score!
            // This ensures we only accept changes that improve the OVERALL face.
            var action = _phaseController.ReportScore(score.Total, _currentMorphs);
            
            switch (action)
            {
                case PhaseAction.Continue:
                    GenerateNextMorphs();
                    break;
                case PhaseAction.Complete:
                    SaveToKnowledgeTree();  // LEARNING: Save to knowledge tree!
                    SaveComparisonImage();
                    NextTarget();
                    break;
                case PhaseAction.Abort:
                    NextTarget();
                    break;
            }
            
            if (IterationsOnTarget >= MAX_ITERATIONS_PER_TARGET)
            {
                SaveToKnowledgeTree();  // LEARNING: Save even on timeout
                SaveComparisonImage();
                NextTarget();
            }
        }
        
        /// <summary>
        /// v3.0.22: Record morph experiment using ACTUAL SubPhase scores instead of coarse MainPhase scores.
        /// Previously Eyes, Nose, and Mouth all shared MajorFeaturesScore — when nose improved but mouth
        /// worsened, both got recorded as "improved." Now each feature gets its own SubPhase score.
        /// </summary>
        private void RecordMorphExperiment(HierarchicalScore currentScore)
        {
            if (_featureMorphLearning == null || _previousMorphs == null) return;
            if (_previousHierarchicalScore == null) return;

            // v3.0.22: Use SubPhaseScores for per-feature accuracy.
            // Foundation SubPhases (FaceWidth, FaceHeight, FaceShape) → average for "Face"
            // Structure SubPhases (Jaw, Chin) → direct mapping
            // MajorFeatures SubPhases (Nose, Eyes, Mouth) → direct mapping (was ALL sharing one score!)
            // FineDetails SubPhases (Eyebrows, Ears) → direct mapping
            var scoresBefore = BuildFeatureScores(_previousHierarchicalScore);
            var scoresAfter = BuildFeatureScores(currentScore);

            // Record! (API: previousMorphs, currentMorphs, prevScores, currScores)
            _featureMorphLearning.RecordExperiment(
                _previousMorphs,
                _currentMorphs,
                scoresBefore,
                scoresAfter
            );

            // Update feature intelligence (which features improved/declined)
            if (_featureIntelligence != null)
            {
                foreach (var feature in scoresAfter.Keys)
                {
                    if (scoresBefore.TryGetValue(feature, out float before))
                    {
                        bool improved = scoresAfter[feature] > before;
                        _featureIntelligence.RecordImprovementAttempt(feature, improved);
                    }
                }
            }
        }

        /// <summary>
        /// v3.0.22: Build per-feature scores from SubPhaseScores.
        /// Maps SubPhase enum values to FeatureMorphLearning's expected string keys.
        /// Foundation SubPhases averaged into "Face", others map directly.
        /// </summary>
        private Dictionary<string, float> BuildFeatureScores(HierarchicalScore score)
        {
            var sub = score.SubPhaseScores;
            var result = new Dictionary<string, float>();

            // Foundation → "Face": average of FaceWidth, FaceHeight, FaceShape
            float faceSum = 0; int faceCount = 0;
            if (sub.TryGetValue(SubPhase.FaceWidth, out float fw)) { faceSum += fw; faceCount++; }
            if (sub.TryGetValue(SubPhase.FaceHeight, out float fh)) { faceSum += fh; faceCount++; }
            if (sub.TryGetValue(SubPhase.FaceShape, out float fs)) { faceSum += fs; faceCount++; }
            result["Face"] = faceCount > 0 ? faceSum / faceCount : score.FoundationScore;

            // Structure → direct mapping
            result["Jaw"] = sub.TryGetValue(SubPhase.Jaw, out float jaw) ? jaw : score.StructureScore;
            result["Chin"] = sub.TryGetValue(SubPhase.Chin, out float chin) ? chin : score.StructureScore;

            // MajorFeatures → direct mapping (THIS IS THE KEY FIX - each gets its own score!)
            result["Eyes"] = sub.TryGetValue(SubPhase.Eyes, out float eyes) ? eyes : score.MajorFeaturesScore;
            result["Nose"] = sub.TryGetValue(SubPhase.Nose, out float nose) ? nose : score.MajorFeaturesScore;
            result["Mouth"] = sub.TryGetValue(SubPhase.Mouth, out float mouth) ? mouth : score.MajorFeaturesScore;

            // FineDetails → direct mapping
            result["Brows"] = sub.TryGetValue(SubPhase.Eyebrows, out float brows) ? brows : score.FineDetailsScore;
            result["Ears"] = sub.TryGetValue(SubPhase.Ears, out float ears) ? ears : score.FineDetailsScore;

            return result;
        }
        
        #endregion
        
        #region Morph Generation
        
        /// <summary>Map V3's OptPhase to the old LearningPhase for OrchestratorMemory</summary>
        private LearningPhase MapToLearningPhase(OptPhase opt)
        {
            switch (opt)
            {
                case OptPhase.Exploration: return LearningPhase.Exploration;
                case OptPhase.LockIn: return LearningPhase.Refinement;
                case OptPhase.Refinement: return LearningPhase.Refinement;
                case OptPhase.PlateauEscape: return LearningPhase.Plateau;
                default: return LearningPhase.Exploration;
            }
        }
        
        private void GenerateNextMorphs()
        {
            var settings = _phaseController.GetCurrentSettings();
            var activeMorphs = _phaseController.GetActiveMorphs();
            
            // DEBUG: Log which morphs we're modifying
            if (IterationsOnTarget == 1 || IterationsOnTarget % 50 == 0)
            {
                SubModule.Log($"  [DEBUG] Phase: {_phaseController.CurrentSubPhase}, ActiveMorphs: [{string.Join(",", activeMorphs)}]");
            }
            
            if (_bestMorphsForSubPhase != null)
                Array.Copy(_bestMorphsForSubPhase, _currentMorphs, NUM_MORPHS);
            
            // Get learned step size from OrchestratorMemory
            float sigma = settings.Sigma;
            if (_orchestratorMemory != null)
            {
                var learningPhase = MapToLearningPhase(_phaseController.CurrentOptPhase);
                float recommendedStep = _orchestratorMemory.GetRecommendedStepSize(learningPhase, _currentScore);
                if (recommendedStep > 0)
                {
                    sigma = recommendedStep;  // Use learned step size!
                }
            }
            
            // Get learned morph correlations for current feature
            Dictionary<int, float> learnedMorphWeights = null;
            if (_featureMorphLearning != null && !_featureMorphLearning.IsEmpty)
            {
                string featureName = _phaseController.CurrentSubPhase.ToString().ToLower();
                learnedMorphWeights = _featureMorphLearning.GetBestMorphsForFeature(featureName, _currentScore);
            }
            
            foreach (int idx in activeMorphs)
            {
                if (idx >= NUM_MORPHS) continue;
                
                var (minVal, maxVal) = _lockManager.GetAllowedRange(idx);
                
                // Get the official morph range for proportional variation
                var (morphMin, morphMax, morphRange) = MorphGroups.GetMorphRange(idx);
                float rangeScale = morphRange / 1.5f;  // Normalize to average range
                
                // Apply learned morph weight if available
                float morphWeight = 1.0f;
                if (learnedMorphWeights != null && learnedMorphWeights.TryGetValue(idx, out float weight))
                {
                    morphWeight = 0.5f + weight;  // Boost morphs that are known to help
                }
                
                float u1 = (float)_random.NextDouble();
                float u2 = (float)_random.NextDouble();
                float gaussian = (float)(Math.Sqrt(-2.0 * Math.Log(u1 + 0.0001)) * Math.Cos(2.0 * Math.PI * u2));
                
                float oldValue = _currentMorphs[idx];
                // Variation = gaussian * sigma * rangeScale * morphWeight
                // rangeScale makes variations proportional to the morph's natural range
                float variation = gaussian * sigma * rangeScale * morphWeight;
                float newValue = Math.Max(minVal, Math.Min(maxVal, _currentMorphs[idx] + variation));
                _currentMorphs[idx] = newValue;
                
                // DEBUG: Log first few morph changes
                if (IterationsOnTarget == 1 && idx == activeMorphs[0])
                {
                    SubModule.Log($"  [DEBUG] Morph[{idx}]: {oldValue:F3} -> {newValue:F3} (σ={sigma:F3}, var={variation:F3})");
                }
            }
            
            // Record this mutation for learning
            if (_orchestratorMemory != null)
            {
                var learningPhase = MapToLearningPhase(_phaseController.CurrentOptPhase);
                bool usedTree = learnedMorphWeights != null && learnedMorphWeights.Count > 0;
                _orchestratorMemory.RecordMutation(
                    learningPhase,
                    sigma,
                    activeMorphs.Length,
                    _previousScore,
                    _currentScore,
                    usedTree);
            }
        }
        
        #endregion
        
        #region Target Management
        
        private bool NextTarget()
        {
            var sources = _dataSourceManager.GetReadySources().ToList();
            if (sources.Count == 0)
            {
                SubModule.Log("No data sources!");
                return false;
            }
            
            // Try up to 50 times to find a valid target (children, low quality get skipped)
            int maxAttempts = 50;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                _lastSourceIndex = (_lastSourceIndex + 1) % sources.Count;
                var source = sources[_lastSourceIndex];
                
                _currentTarget = source.GetNextTarget();
                if (_currentTarget == null)
                {
                    SubModule.Log($"  No targets from {source.Name}, trying next...");
                    continue;
                }
                
                // Detect landmarks if not provided by data source
                if (_currentTarget.Landmarks == null && _currentTarget.ImageBytes != null && _detector != null)
                {
                    try
                    {
                        using (var ms = new MemoryStream(_currentTarget.ImageBytes))
                        using (var bitmap = new Bitmap(ms))
                        {
                            var fullLandmarks = _detector.DetectLandmarks(bitmap);
                            
                            // CRITICAL: Convert FaceMesh 468 to Dlib 68 format for FeatureExtractor!
                            // FaceMesh returns 468*2=936 or 468*3=1404 floats
                            // FeatureExtractor expects Dlib 68*2=136 floats
                            if (fullLandmarks != null && fullLandmarks.Length > 200)
                            {
                                _currentTarget.Landmarks = LandmarkDetector.ConvertFaceMeshTo68(fullLandmarks);
                                SubModule.Log($"  Converted {fullLandmarks.Length} landmarks to Dlib 68 format");
                            }
                            else
                            {
                                _currentTarget.Landmarks = fullLandmarks;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SubModule.Log($"  Landmark detection failed: {ex.Message}");
                    }
                }
                
                if (_currentTarget.Landmarks == null)
                {
                    SubModule.Log($"  No landmarks for {_currentTarget.Id}, skipping...");
                    continue;
                }
                
                // Found a valid target with landmarks!
                ResetForNewTarget();
                
                _targetLandmarks = _currentTarget.Landmarks;
                _targetFeatures = _featureExtractor.Extract(_targetLandmarks);
                
                // Save ImageBytes to temp file for demographics
                _currentTargetImagePath = null;
                if (_currentTarget.ImageBytes != null)
                {
                    try
                    {
                        using (var ms = new MemoryStream(_currentTarget.ImageBytes))
                        using (var bitmap = new Bitmap(ms))
                        {
                            _currentTargetImagePath = Path.Combine(_basePath, "Temp", "current_target.png");
                            Directory.CreateDirectory(Path.GetDirectoryName(_currentTargetImagePath));
                            bitmap.Save(_currentTargetImagePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    catch { }
                }
                
                DetectAndApplyDemographics();
                
                // VALIDATION: Skip children and low-quality images
                if (!IsValidTarget())
                {
                    SubModule.Log($"  ⚠️ SKIPPING: {_currentTarget.Id} (invalid: child or low quality)");
                    continue;  // Try next target
                }
                
                InitializeMorphsForTarget();
                
                TargetsProcessed++;
                SubModule.Log($"=== TARGET #{TargetsProcessed}: {_currentTarget.Id} ({source.Name}) ===");
                
                _state = State.WaitingForRender;
                return true;
            }
            
            // All sources exhausted
            SubModule.Log("All data sources exhausted - no more targets!");
            return false;
        }
        
        private void ResetForNewTarget()
        {
            IterationsOnTarget = 0;
            _bestTotalScore = 0f;
            _startingScore = 0f;  // v3.0.20: Reset starting score
            _bestScoreForSubPhase = 0f;
            _bestMorphsForSubPhase = null;
            _bestHierarchicalScore = null;  // v3.0.24
            _bestRenderFeatures = null;  // v3.0.24
            _cachedDemographics = null;
            _scoreHistory.Clear();
            _phaseController.Reset();
        }
        
        private void DetectAndApplyDemographics()
        {
            if (string.IsNullOrEmpty(_currentTargetImagePath) || !File.Exists(_currentTargetImagePath))
                return;
            
            try
            {
                // Gender & Age Detection
                if (_genderEnsemble != null)
                {
                    // v3.0.23: Pass landmarks to enable LandmarkGenderEstimator and Long-Hair-Male detection.
                    // Previously null was passed, disabling landmark-based gender features entirely!
                    var genderResult = _genderEnsemble.Detect(_currentTargetImagePath, _targetLandmarks, null, 0.5f);
                    
                    _cachedDemographics = new DemographicDetector.Demographics
                    {
                        IsFemale = genderResult.IsFemale,
                        Age = genderResult.Age,
                        Confidence = genderResult.Confidence
                    };
                    
                    // Log ensemble results
                    string gender = genderResult.IsFemale ? "Female" : "Male";
                    SubModule.Log($"  [Demographics] {gender} (conf={genderResult.Confidence:F2}) Age={genderResult.Age:F0}");
                    SubModule.Log($"    Decision: {genderResult.Decision}");
                    if (genderResult.Votes != null && genderResult.Votes.Count > 0)
                    {
                        SubModule.Log($"    Votes: {string.Join(", ", genderResult.Votes)}");
                    }
                    if (!string.IsNullOrEmpty(genderResult.Race))
                    {
                        SubModule.Log($"    Race: {genderResult.Race} (conf={genderResult.RaceConfidence:F2})");
                    }
                    
                    // Apply to character
                    if (_faceController.IsFemale != genderResult.IsFemale)
                    {
                        SubModule.Log($"    → Gender change: {(_faceController.IsFemale ? "F" : "M")} → {(genderResult.IsFemale ? "F" : "M")}");
                        _faceController.IsFemale = genderResult.IsFemale;
                    }
                    
                    _faceController.Age = Math.Max(18, Math.Min(70, (int)genderResult.Age));
                }
                
                // Skin Color Detection
                if (_colorEnsemble != null)
                {
                    var skinResult = _colorEnsemble.Detect(_currentTargetImagePath, _targetLandmarks, null);
                    SubModule.Log($"  [SkinColor] Value={skinResult.Value:F2} (conf={skinResult.Confidence:F2})");
                    
                    if (skinResult.Confidence > 0.5f)
                    {
                        _faceController.SkinColor = skinResult.Value;
                    }
                    
                    // Eye color detection
                    var eyeResult = _colorEnsemble.DetectEyeColor(_currentTargetImagePath, _targetLandmarks);
                    if (eyeResult.Confidence > 0.3f)
                    {
                        SubModule.Log($"  [EyeColor] Value={eyeResult.Value:F2} (conf={eyeResult.Confidence:F2})");
                        _faceController.EyeColor = eyeResult.Value;
                    }
                    
                    // Hair color detection
                    var (hairResult, hairDetected) = _colorEnsemble.DetectHairColor(_currentTargetImagePath, _targetLandmarks, skinResult.Value);
                    if (hairDetected && hairResult.Confidence > 0.3f)
                    {
                        SubModule.Log($"  [HairColor] Value={hairResult.Value:F2} (conf={hairResult.Confidence:F2})");
                        _faceController.HairColor = hairResult.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"  Demographics error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validate target image before processing.
        /// Returns false for children, low-quality images, etc.
        /// </summary>
        private bool IsValidTarget()
        {
            // 1. CHECK AGE: Skip children (must be 18+)
            if (_cachedDemographics.HasValue)
            {
                float age = _cachedDemographics.Value.Age;
                if (age < 18)
                {
                    SubModule.Log($"    → Child detected (age={age:F0}), skipping");
                    return false;
                }
            }
            
            // 2. CHECK LANDMARKS: Must have enough landmarks for analysis
            if (_targetLandmarks == null || _targetLandmarks.Length < 100)
            {
                SubModule.Log($"    → Insufficient landmarks ({_targetLandmarks?.Length ?? 0}), skipping");
                return false;
            }
            
            // 3. CHECK LANDMARK QUALITY: Detect bad detections (all zeros, extreme values)
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            int zeroCount = 0;
            
            for (int i = 0; i < _targetLandmarks.Length - 1; i += 2)
            {
                float x = _targetLandmarks[i];
                float y = _targetLandmarks[i + 1];
                
                if (Math.Abs(x) < 0.001f && Math.Abs(y) < 0.001f)
                    zeroCount++;
                    
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
            
            // Too many zero landmarks = failed detection
            if (zeroCount > _targetLandmarks.Length / 4)
            {
                SubModule.Log($"    → Bad landmark detection ({zeroCount} zeros), skipping");
                return false;
            }
            
            // Face too small (landmarks clustered) = low quality or partial face
            float faceWidth = maxX - minX;
            float faceHeight = maxY - minY;
            if (faceWidth < 0.1f || faceHeight < 0.1f)
            {
                SubModule.Log($"    → Face too small ({faceWidth:F2}x{faceHeight:F2}), skipping");
                return false;
            }
            
            // Valid target!
            return true;
        }
        
        private void InitializeMorphsForTarget()
        {
            // ALWAYS start with neutral morphs, then optionally load from knowledge tree
            _currentMorphs = new float[NUM_MORPHS];
            for (int i = 0; i < NUM_MORPHS; i++)
                _currentMorphs[i] = 0.5f;  // Neutral starting point
            
            // TRY TO GET BETTER STARTING MORPHS FROM KNOWLEDGE TREE!
            if (_hierarchicalKnowledge != null && !_hierarchicalKnowledge.IsEmpty && _targetLandmarks != null)
            {
                var metadata = new Dictionary<string, float>();
                if (_cachedDemographics.HasValue)
                {
                    metadata["gender"] = _cachedDemographics.Value.IsFemale ? 1f : 0f;
                    metadata["age"] = _cachedDemographics.Value.Age;
                }
                
                // === FEATURE CLASSIFICATION ===
                LogFeatureClassification(metadata);
                
                var startingMorphs = _hierarchicalKnowledge.GetStartingMorphs(_targetLandmarks, metadata, NUM_MORPHS);
                if (startingMorphs != null && startingMorphs.Length > 0)
                {
                    // Use morphs from knowledge tree!
                    Array.Copy(startingMorphs, _currentMorphs, Math.Min(startingMorphs.Length, _currentMorphs.Length));
                    SubModule.Log($"  [Init] Starting morphs from knowledge tree (not empty)");
                }
                else
                {
                    // v3.0.20: FALLBACK - use demographic-based starting morphs!
                    ApplyDemographicFallback(_currentMorphs, metadata);
                    SubModule.Log($"  [Init] No tree match, using demographic fallback");
                }
            }
            else
            {
                // v3.0.20: Even with empty tree, use demographic fallback!
                var metadata = new Dictionary<string, float>();
                if (_cachedDemographics.HasValue)
                {
                    metadata["gender"] = _cachedDemographics.Value.IsFemale ? 1f : 0f;
                    metadata["age"] = _cachedDemographics.Value.Age;
                }
                ApplyDemographicFallback(_currentMorphs, metadata);
                SubModule.Log($"  [Init] Tree empty, using demographic fallback");
            }
            
            // Apply to character
            _faceController.SetAllMorphs(_currentMorphs);
            
            _startingMorphs = (float[])_currentMorphs.Clone();  // Save for learning comparison
            _bestMorphs = (float[])_currentMorphs.Clone();
            _startingScore = 0;  // Will be set after first evaluation
        }
        
        /// <summary>
        /// Apply demographic-based starting morphs when knowledge tree is empty.
        /// These are reasonable defaults based on gender and age.
        /// </summary>
        private void ApplyDemographicFallback(float[] morphs, Dictionary<string, float> metadata)
        {
            bool isFemale = metadata.TryGetValue("gender", out float g) && g > 0.5f;
            float age = metadata.TryGetValue("age", out float a) ? a : 30f;
            
            // Female vs Male typical differences
            if (isFemale)
            {
                // v3.0.23: All face structure morphs set to NEUTRAL (0.50).
                // Previous versions biased face_width=0.45, jaw=0.45, chin=0.45 which
                // produced pointed/narrow chins on ALL female faces regardless of source photo.
                // Let the optimizer determine face shape from actual photo landmarks.
                morphs[0] = 0.50f;   // face_width NEUTRAL - v3.0.23: was 0.45 (caused narrow face → pointed chin look)
                morphs[4] = 0.55f;   // cheeks slightly fuller
                morphs[5] = 0.55f;   // cheekbone_height higher
                morphs[43] = 0.55f;  // lip_thickness fuller
                morphs[48] = 0.50f;  // jaw_line NEUTRAL - v3.0.22: was 0.45
                morphs[52] = 0.50f;  // chin_shape NEUTRAL - v3.0.22: was 0.45
            }
            else
            {
                // Males typically have:
                // - Wider jaw (morph 48)
                // - More prominent chin (morph 51, 52)
                // - Stronger brow ridge (morph 14)
                morphs[0] = 0.55f;   // face_width slightly wider
                morphs[14] = 0.55f;  // eyebrow_depth stronger
                morphs[48] = 0.55f;  // jaw_line wider
                morphs[51] = 0.55f;  // chin_forward more prominent
                morphs[52] = 0.55f;  // chin_shape stronger
            }
            
            // Age-based adjustments
            if (age > 50)
            {
                morphs[56] = 0.6f + (age - 50) * 0.01f;  // old_face
                morphs[4] = Math.Max(0.3f, morphs[4] - 0.1f);  // less cheek fullness
            }
            else if (age < 25)
            {
                morphs[56] = 0.3f;  // younger face
                morphs[4] = Math.Min(0.7f, morphs[4] + 0.1f);  // more cheek fullness
            }
            
            SubModule.Log($"    Demographic fallback: {(isFemale ? "Female" : "Male")}, Age={age:F0}");
        }
        
        #endregion
        
        #region Feature Classification Logging
        
        private void LogFeatureClassification(Dictionary<string, float> metadata)
        {
            if (_hierarchicalKnowledge == null || _targetLandmarks == null)
                return;
            
            try
            {
                var classification = _hierarchicalKnowledge.ClassifyFace(_targetLandmarks, metadata);
                if (classification == null || classification.Count == 0)
                    return;
                
                // Build formatted log output
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("  ┌─────────────────────────────────────────────┐");
                sb.AppendLine("  │          FEATURE CLASSIFICATION            │");
                sb.AppendLine("  ├─────────────────────────────────────────────┤");
                
                // Group 1: Face Structure
                AppendFeature(sb, classification, FeatureCategory.FaceShape, "Face");
                AppendFeature(sb, classification, FeatureCategory.FaceWidth, "Width");
                AppendFeature(sb, classification, FeatureCategory.FaceLength, "Length");
                AppendFeature(sb, classification, FeatureCategory.JawShape, "Jaw");
                
                sb.AppendLine("  ├─────────────────────────────────────────────┤");
                
                // Group 2: Features
                AppendFeature(sb, classification, FeatureCategory.NoseWidth, "Nose W");
                AppendFeature(sb, classification, FeatureCategory.NoseLength, "Nose L");
                AppendFeature(sb, classification, FeatureCategory.EyeShape, "Eyes");
                AppendFeature(sb, classification, FeatureCategory.EyeSize, "Eye Sz");
                AppendFeature(sb, classification, FeatureCategory.EyeSpacing, "Eye Sp");
                AppendFeature(sb, classification, FeatureCategory.MouthWidth, "Mouth");
                AppendFeature(sb, classification, FeatureCategory.LipFullness, "Lips");
                
                sb.AppendLine("  ├─────────────────────────────────────────────┤");
                
                // Group 3: Cheeks & Demographics
                AppendFeature(sb, classification, FeatureCategory.CheekFullness, "Cheeks");
                AppendFeature(sb, classification, FeatureCategory.CheekboneProminence, "Bones");
                AppendFeature(sb, classification, FeatureCategory.Gender, "Gender");
                AppendFeature(sb, classification, FeatureCategory.AgeGroup, "Age");
                AppendFeature(sb, classification, FeatureCategory.SmileLevel, "Smile");
                
                sb.AppendLine("  └─────────────────────────────────────────────┘");
                
                // Log as single block
                SubModule.Log(sb.ToString());
                
                // Also log compact one-liner for quick scanning
                var compact = new List<string>();
                if (classification.TryGetValue(FeatureCategory.FaceShape, out var fs)) compact.Add(fs);
                if (classification.TryGetValue(FeatureCategory.JawShape, out var js)) compact.Add($"Jaw:{js}");
                if (classification.TryGetValue(FeatureCategory.EyeShape, out var es)) compact.Add($"Eye:{es}");
                if (classification.TryGetValue(FeatureCategory.LipFullness, out var lf)) compact.Add($"Lip:{lf}");
                if (classification.TryGetValue(FeatureCategory.NoseWidth, out var nw)) compact.Add($"Nose:{nw}");
                
                SubModule.Log($"  [Features] {string.Join(" | ", compact)}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"  Feature classification error: {ex.Message}");
            }
        }
        
        private void AppendFeature(System.Text.StringBuilder sb, 
            Dictionary<FeatureCategory, string> classification, 
            FeatureCategory category, 
            string label)
        {
            if (classification.TryGetValue(category, out var value))
            {
                sb.AppendLine($"  │  {label,-10}: {value,-15}            │");
            }
        }
        
        #endregion
        
        #region Learning - Save to Knowledge Tree
        
        private void SaveToKnowledgeTree()
        {
            // Only save if we have good data
            if (_hierarchicalKnowledge == null) return;
            if (_targetLandmarks == null) return;
            if (_startingMorphs == null) return;
            if (_bestMorphs == null) return;
            
            // Build metadata from demographics
            var metadata = new Dictionary<string, float>();
            if (_cachedDemographics.HasValue)
            {
                metadata["gender"] = _cachedDemographics.Value.IsFemale ? 1f : 0f;
                metadata["age"] = _cachedDemographics.Value.Age;
            }
            // v3.0.22: Include skin tone for knowledge tree classification
            metadata["skinTone"] = _faceController.SkinColor;
            
            // v3.0.20: FEATURE-LEVEL LEARNING!
            // Even if total score is bad, individual features might be good.
            // Learn from features that scored well, regardless of total!
            
            var featureScores = GetFeatureScores();
            int featuresLearned = 0;
            
            foreach (var kvp in featureScores)
            {
                SubPhase phase = kvp.Key;
                float score = kvp.Value;
                
                // v3.0.22: Lowered from 0.6 to 0.45 due to stricter scoring (Tukey biweight + soft gating)
                // Old exp(-5x²) gave inflated scores; new scoring is 15-25% lower on average
                if (score > 0.45f)
                {
                    // Get morphs relevant to this feature
                    int[] relevantMorphs = GetMorphsForPhase(phase);
                    if (relevantMorphs != null && relevantMorphs.Length > 0)
                    {
                        // Create partial morph array (only the relevant ones)
                        _hierarchicalKnowledge.LearnFeature(
                            phase,
                            _targetLandmarks,
                            metadata,
                            _bestMorphs,
                            relevantMorphs,
                            score);
                        featuresLearned++;
                    }
                }
            }
            
            // Also do full-face learning if total score is decent
            float improvement = _bestTotalScore - 0.5f;
            float absoluteImprovement = _bestTotalScore - (_startingScore > 0 ? _startingScore : 0.3f);
            // v3.0.22: Lowered from 0.25/0.05 due to stricter scoring
            bool shouldLearnFull = _bestTotalScore > 0.20f || absoluteImprovement > 0.03f;
            
            if (shouldLearnFull)
            {
                // Check how many morphs actually changed
                int changedMorphs = 0;
                float maxDelta = 0;
                for (int i = 0; i < Math.Min(_startingMorphs.Length, _bestMorphs.Length); i++)
                {
                    float delta = Math.Abs(_bestMorphs[i] - _startingMorphs[i]);
                    if (delta > 0.01f)
                    {
                        changedMorphs++;
                        maxDelta = Math.Max(maxDelta, delta);
                    }
                }
                
                if (changedMorphs > 0 || _bestTotalScore >= 0.4f)
                {
                    float learningWeight = Math.Max(improvement, 0.01f);
                    
                    _hierarchicalKnowledge.LearnFrom(
                        _targetLandmarks,
                        metadata,
                        _startingMorphs,
                        _bestMorphs,
                        learningWeight);
                    
                    SubModule.Log($"  [Learn] Full: score={_bestTotalScore:F3} weight={learningWeight:F3} changes={changedMorphs}");
                }
            }
            
            if (featuresLearned > 0)
            {
                SubModule.Log($"  [Learn] Features: {featuresLearned} features learned (even with total={_bestTotalScore:F3})");
            }
            else if (!shouldLearnFull)
            {
                SubModule.Log($"  [Learn] Score {_bestTotalScore:F3} too low, no good features found");
            }
        }
        
        /// <summary>
        /// Get the current score for each SubPhase from the last hierarchical score.
        /// </summary>
        private Dictionary<SubPhase, float> GetFeatureScores()
        {
            var result = new Dictionary<SubPhase, float>();
            
            if (_lastHierarchicalScore == null || _targetFeatures == null)
                return result;
            
            // Use the scores already calculated in _lastHierarchicalScore
            // These are available from the HierarchicalScore breakdown
            result[SubPhase.FaceWidth] = _lastHierarchicalScore.FoundationScore;
            result[SubPhase.FaceHeight] = _lastHierarchicalScore.FoundationScore;
            result[SubPhase.FaceShape] = _lastHierarchicalScore.FoundationScore;
            result[SubPhase.Forehead] = _lastHierarchicalScore.StructureScore;
            result[SubPhase.Jaw] = _lastHierarchicalScore.StructureScore;
            result[SubPhase.Chin] = _lastHierarchicalScore.StructureScore;
            result[SubPhase.Cheeks] = _lastHierarchicalScore.StructureScore;
            result[SubPhase.Nose] = _lastHierarchicalScore.MajorFeaturesScore;
            result[SubPhase.Eyes] = _lastHierarchicalScore.MajorFeaturesScore;
            result[SubPhase.Mouth] = _lastHierarchicalScore.MajorFeaturesScore;
            result[SubPhase.Eyebrows] = _lastHierarchicalScore.FineDetailsScore;
            
            return result;
        }
        
        /// <summary>
        /// Get the morph indices relevant to a specific SubPhase.
        /// </summary>
        private int[] GetMorphsForPhase(SubPhase phase)
        {
            return phase switch
            {
                SubPhase.FaceWidth => MorphGroups.FaceWidth,
                SubPhase.FaceHeight => MorphGroups.FaceHeight,
                SubPhase.FaceShape => MorphGroups.FaceShape,
                SubPhase.Forehead => MorphGroups.Forehead,
                SubPhase.Jaw => MorphGroups.Jaw,
                SubPhase.Chin => MorphGroups.Chin,
                SubPhase.Cheeks => MorphGroups.Cheeks,
                SubPhase.Nose => MorphGroups.Nose,
                SubPhase.Eyes => MorphGroups.Eyes,
                SubPhase.Mouth => MorphGroups.Mouth,
                SubPhase.Eyebrows => MorphGroups.Eyebrows,
                _ => null
            };
        }
        
        #endregion
        
        #region Comparison Images
        
        private void SaveComparisonImage()
        {
            try
            {
                string screenshotPath = !string.IsNullOrEmpty(_screenshotPath)
                    ? _screenshotPath
                    : Path.Combine(_basePath, "Data", "Screenshots", "current_render.png");

                if (!string.IsNullOrEmpty(_currentTargetImagePath) &&
                    File.Exists(screenshotPath) &&
                    File.Exists(_currentTargetImagePath))
                {
                    string info = _cachedDemographics.HasValue
                        ? $"{(_cachedDemographics.Value.IsFemale ? "F" : "M")} Age:{_cachedDemographics.Value.Age:F0}"
                        : "";

                    ComparisonImageCreator.CreateComparison(
                        _currentTargetImagePath,
                        screenshotPath,
                        _bestTotalScore,
                        _currentTarget?.Id ?? "unknown",
                        info
                    );

                    // v3.0.24: Save feature comparison report alongside image
                    SaveFeatureReport();
                }
            }
            catch { }
        }

        /// <summary>
        /// v3.0.24: Save detailed feature comparison report as .txt alongside comparison image.
        /// Shows Photo vs Render feature values per SubPhase with diffs, normalized diffs, and scores.
        /// This data is used to calibrate expected ranges from real measurements instead of guessing.
        /// </summary>
        private void SaveFeatureReport()
        {
            try
            {
                if (_targetFeatures == null || _bestRenderFeatures == null || _bestHierarchicalScore == null)
                    return;

                string targetId = _currentTarget?.Id ?? "unknown";
                string sanitizedId = targetId;
                foreach (char c in Path.GetInvalidFileNameChars())
                    sanitizedId = sanitizedId.Replace(c, '_');
                if (sanitizedId.Length > 50) sanitizedId = sanitizedId.Substring(0, 50);

                // Same naming convention as ComparisonImageCreator: score_targetId
                string filename = $"{_bestTotalScore:F3}_{sanitizedId}.txt";
                string comparisonsFolder = Path.Combine(_basePath, "Comparisons");

                if (!Directory.Exists(comparisonsFolder))
                    Directory.CreateDirectory(comparisonsFolder);

                string outputPath = Path.Combine(comparisonsFolder, filename);

                // Generate the report
                string report = _scorer.GenerateFeatureReport(_targetFeatures, _bestRenderFeatures, _bestHierarchicalScore);

                // Add header with target info
                string header = $"Target: {targetId}\n";
                header += $"Image: {Path.GetFileName(_currentTargetImagePath)}\n";
                header += $"Score: {_bestTotalScore:F3}\n";
                if (_cachedDemographics.HasValue)
                {
                    var demo = _cachedDemographics.Value;
                    header += $"Demographics: {(demo.IsFemale ? "Female" : "Male")} Age:{demo.Age:F0}\n";
                }
                header += $"Iterations: {IterationsOnTarget}\n";
                header += $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                header += "\n";

                File.WriteAllText(outputPath, header + report);
                SubModule.Log($"  [FeatureReport] Saved to {outputPath}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"  [FeatureReport] Error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Phase Events
        
        private void OnPhaseChanged(MainPhase main, SubPhase sub, OptPhase opt)
        {
            // Only reset when the SUBPHASE changes (e.g., FaceWidth → FaceHeight)
            // NOT when optimization stage changes (Exploration → Refinement → PlateauEscape)
            if (_lastSubPhase != sub)
            {
                _phaseStartScore = _currentScore;
                _phaseStartIteration = IterationsOnTarget;
                _lastSubPhase = sub;
                
                _bestScoreForSubPhase = 0f;
                _bestMorphsForSubPhase = null;
                
                // Restore global best morphs at the START of each new SubPhase
                if (_bestMorphs != null)
                {
                    Array.Copy(_bestMorphs, _currentMorphs, NUM_MORPHS);
                    _faceController.SetAllMorphs(_currentMorphs);
                    SubModule.Log($"  [RESET] Restored global best for: {sub}");
                }
            }
            
            SubModule.Log($"  [PHASE] {main}/{sub}/{opt}");
        }
        
        private void OnSubPhaseComplete(SubPhase sub, float score, float[] bestMorphs)
        {
            SubModule.Log($"  [DONE] {sub} = {score:F3}");
            
            // Record phase result for learning!
            if (_phaseManager != null)
            {
                float scoreGain = score - _phaseStartScore;
                int iterations = IterationsOnTarget - _phaseStartIteration;
                // v3.0.22: Lowered from 0.7 to 0.55 due to stricter scoring
                bool wasSuccessful = score >= 0.55f;  // Consider successful if above threshold
                
                string phaseId = sub.ToString().ToLower();
                _phaseManager.RecordPhaseResult(phaseId, scoreGain, iterations, wasSuccessful);
            }
            
            // Update feature intelligence
            if (_featureIntelligence != null)
            {
                string featureName = sub.ToString().ToLower();
                var featureScores = new Dictionary<string, float> { { featureName, score } };
                _featureIntelligence.UpdateFeatureDifficulty(featureScores);
            }
        }
        
        private void OnMainPhaseComplete(MainPhase main, float score)
        {
            SubModule.Log($"  [MAIN] {main} = {score:F3}");
            
            // Record main phase completion
            if (_phaseManager != null)
            {
                string phaseId = $"main_{main.ToString().ToLower()}";
                _phaseManager.RecordPhaseResult(phaseId, score - 0.5f, IterationsOnTarget, score >= 0.8f);
            }
        }
        
        #endregion
        
        #region Public API - Status
        
        public string GetStatus()
        {
            if (!IsLearning) return "Idle";
            return $"V3 | #{TargetsProcessed} | {_phaseController.GetStatus()} | {_bestTotalScore:F3}";
        }
        
        public string GetExtendedStatus() => GetStatus();
        public string GetRunStatus() => $"Target {TargetsProcessed} | Iter {IterationsOnTarget}\nScore: {_currentScore:F3} / Best: {_bestTotalScore:F3}";
        public string GetTreeStatus()
        {
            if (_hierarchicalKnowledge == null) return "Knowledge: not initialized";
            return _hierarchicalKnowledge.GetSummary();
        }
        
        /// <summary>Get current phase info (for left panel - COMPACT)</summary>
        public string GetFeatureIntelligenceStatus()
        {
            if (!IsLearning) return "Not learning";
            
            var main = _phaseController.CurrentMainPhase;
            var sub = _phaseController.CurrentSubPhase;
            var opt = _phaseController.CurrentOptPhase;
            
            string phaseInfo = $"[{main}] {sub} ({opt})";
            string morphInfo = $"Morphs: {string.Join(",", _phaseController.GetActiveMorphs())}";
            string lockInfo = $"Locked: {_lockManager.LockedCount}/62";
            
            return $"{phaseInfo}\n{morphInfo}\n{lockInfo}\nIter: {IterationsOnTarget} | Best: {_bestTotalScore:F3}";
        }
        
        /// <summary>Get score breakdown (for right panel - DETAILED)</summary>
        public string GetPhaseStats()
        {
            if (_lastHierarchicalScore == null) return "Waiting for score...";
            
            var s = _lastHierarchicalScore;
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"Total: {s.Total:F3}");
            sb.AppendLine($"─────────────────");
            sb.AppendLine($"Foundation: {s.FoundationScore:F3} (gate: {s.FoundationScore:F2})");
            sb.AppendLine($"Structure:  {s.StructureScore:F3}");
            sb.AppendLine($"Features:   {s.MajorFeaturesScore:F3}");
            sb.AppendLine($"Details:    {s.FineDetailsScore:F3}");
            
            // Show weak areas
            var weak = s.GetWeakSubPhases(0.7f);
            if (weak.Length > 0)
            {
                sb.AppendLine($"─────────────────");
                sb.AppendLine($"Weak: {string.Join(", ", weak)}");
            }
            
            return sb.ToString();
        }
        
        public string GetSessionTrend()
        {
            if (_scoreHistory.Count < 10) return "Gathering...";
            float recent = _scoreHistory.Skip(_scoreHistory.Count - 10).Average();
            float early = _scoreHistory.Take(10).Average();
            return (recent - early) > 0 ? "↑ Improving" : "↓ Declining";
        }
        
        #endregion
        
        #region Public API - Demographics & Race
        
        public (bool isFemale, float genderConf, float age) DetectDemographics(Bitmap bitmap)
        {
            if (_fairFaceDetector == null || !_fairFaceDetector.IsLoaded)
                return (false, 0f, 30f);
            
            var result = _fairFaceDetector.Detect(bitmap);
            return (result.isFemale, result.genderConf, result.age);
        }
        
        public RacePresetManager GetRacePresetManager() => _racePresetManager;
        public RaceAwareFaceGenerator GetRaceGenerator() => _raceGenerator;
        public GenderEnsemble GetGenderEnsemble() => _genderEnsemble;
        public ColorEnsemble GetColorEnsemble() => _colorEnsemble;
        public FairFaceDetector GetFairFaceDetector() => _fairFaceDetector;
        
        public IEnumerable<string> GetAvailableRaces() => 
            _racePresetManager?.Presets.Keys ?? Enumerable.Empty<string>();
        
        public IEnumerable<string> GetRaceCategories() => 
            _racePresetManager?.Presets.Values.Select(p => p.Category).Distinct() ?? Enumerable.Empty<string>();
        
        #endregion
        
        #region Public API - Knowledge Systems
        
        // All 5 knowledge systems for learning AND generation
        public HierarchicalKnowledge GetHierarchicalKnowledge() => _hierarchicalKnowledge;
        public ModuleIntegration GetModuleIntegration() => _moduleIntegration;
        public FeatureMorphLearning GetFeatureMorphLearning() => _featureMorphLearning;
        public FeatureIntelligence GetFeatureIntelligence() => _featureIntelligence;
        public PhaseManager GetPhaseManager() => _phaseManager;
        public OrchestratorMemory GetOrchestratorMemory() => _orchestratorMemory;
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            Stop();
            _fairFaceDetector?.Dispose();
        }
        
        #endregion
    }
}
