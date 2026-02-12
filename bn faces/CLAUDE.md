# FaceLearner - Bannerlord Face Recreation from Photos

## Project Overview

FaceLearner is a Mount & Blade II: Bannerlord mod that uses ML to reconstruct game character faces from real photographs. It uses landmark detection, CMA-ES optimization, and a knowledge tree to map facial features to Bannerlord's 62 morph parameters.

## Architecture

### Two Modules

1. **FaceLearner** (main module) — UI, face controller, morph definitions, Bannerlord integration
   - `Core/FaceController.cs` — Interface to Bannerlord's face morph system (62 morphs)
   - `Core/MorphDefinitions.cs` — Morph names and categories
   - `Core/IMLAddon.cs` — Interface that FaceLearner.ML implements
   - `SubModule.cs` — Bannerlord mod entry point

2. **FaceLearner.ML** (ML addon) — All machine learning, scoring, optimization, knowledge
   - `Core/MLAddonImplementation.cs` — Main orchestration, implements IMLAddon
   - `Core/HierarchicalScoring/` — Feature extraction, scoring, morph groups
   - `Core/LivingKnowledge/` — Learning orchestrator, knowledge tree, face analysis
   - `Core/RaceSystem/` — Race-aware face generation, race presets
   - `ML/` — Landmark detection, gender/age/race detection, hair analysis

### Pipeline Flow

1. **Input**: Photo of a face
2. **Landmark Detection** (`ML/LandmarkDetector.cs`): Dlib 68-point or FaceMesh 468-point
3. **Demographics** (`ML/GenderEnsemble.cs`, `ML/FairFaceDetector.cs`): Gender, age, race
4. **Feature Extraction** (`Core/HierarchicalScoring/FeatureSet.cs`): 40+ facial features from landmarks
5. **Initialization** (`Core/LivingKnowledge/LearningOrchestratorV3.cs`): Knowledge tree or demographic fallback
6. **CMA-ES Optimization**: Iteratively adjusts 62 morphs to minimize feature difference
7. **Scoring** (`Core/HierarchicalScoring/HierarchicalScorer.cs`): Hierarchical 4-level comparison
8. **Learning** (`Core/LivingKnowledge/`): Saves successful feature→morph mappings

### Scoring Hierarchy (HierarchicalScorer)

- **Foundation (35%)**: FaceWidth, FaceHeight, FaceShape (contour profile)
- **Structure (30%)**: Forehead, Jaw, Chin, Cheeks
- **Major Features (25%)**: Nose, Eyes, Mouth
- **Fine Details (10%)**: Eyebrows, Ears

Soft gating: Poor foundation reduces higher-level contributions (floor 0.3).

### Morph System

62 morphs total. Key groups and indices (from `MorphGroups.cs`):
- **Face** (0-16): face_width, face_weight, face_center_height, face_ratio, cheeks, cheekbone_height, cheekbone_width, cheekbone_depth, face_asymmetry, face_depth, face_eye_socket_size, face_temple_width, ear_size, ear_shape, eyebrow_depth, brow_inner_height, brow_outer_height
- **Eyes** (17-27): eye_position, eye_size, eye_closure, eye_mono_lid, eye_depth, eye_shape, eye_color, eye_outer_corner, eye_inner_corner, eye_asymmetry, eye_iris_size
- **Nose** (28-37): nose_size, nose_width, nose_bridge, nose_tip_height, nose_length, nose_width_low, nose_shape, nose_definition, nose_nostril_size, nose_bridge_shape
- **Mouth** (38-53): mouth_width, mouth_position, mouth_forward, mouth_frown, lip_thickness, lip_definition, lip_shape_bottom, lip_shape_top, lip_concave_convex, **jaw_line (48)**, neck_slope, jaw_height, **chin_forward (51)**, **chin_shape (52)**, **chin_length (53)**
- **Hair/Skin** (54-61): hair_color, hair, skin_color, old_face, face_marks, face_mark_type, blemish_type, blemish_amount

## Key Files

| File | Purpose |
|------|---------|
| `FaceLearner.ML/Core/HierarchicalScoring/FeatureSet.cs` | Feature extraction from landmarks, contour profile |
| `FaceLearner.ML/Core/HierarchicalScoring/HierarchicalScorer.cs` | Hierarchical scoring with Tukey biweight, soft gating |
| `FaceLearner.ML/Core/HierarchicalScoring/MorphGroups.cs` | Canonical morph index→name mapping, ranges, groups |
| `FaceLearner.ML/Core/LivingKnowledge/LearningOrchestratorV3.cs` | Main learning loop, morph initialization, demographic fallback |
| `FaceLearner.ML/Core/MLAddonImplementation.cs` | Entry point, CMA-ES optimization, comparison image generation |
| `FaceLearner.ML/Core/RaceSystem/RacePresetManager.cs` | Race preset XML definitions with morph biases |
| `FaceLearner.ML/ML/GenderEnsemble.cs` | Multi-method gender detection (FairFace + Hair + Landmarks + SkinColor) |
| `FaceLearner.ML/ML/LandmarkDetector.cs` | Dlib 68-point and FaceMesh 468-point landmark detection |

## Deployment

- **Source**: `C:\Work\Sources\github\bn faces\`
- **Build**: `dotnet build FaceLearner.ML/FaceLearner.ML.csproj`
- **DLL deploys to**: `D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\FaceLearner.ML\bin\Win64_Shipping_Client\`
- **Runtime data (knowledge tree, logs)**: `D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\FaceLearner\Data\`
- **Comparisons output**: `D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\FaceLearner.ML\Data\Comparisons\`
- **Decompiled game code reference**: `C:\Work\Sources\github\Conversion\1.3\structured code\`

### Knowledge Tree Files (in FaceLearner module Data/)

- `hierarchical_knowledge.dat` — Main knowledge tree (feature→morph mappings)
- `feature_intelligence.dat` — Feature analysis data
- `feature_morph_learning.dat` — Per-feature morph learning
- `orchestrator_memory.dat` — Orchestrator state
- `phase_manager.dat` — Phase management state

**IMPORTANT**: After fixing morph index mappings or scoring changes, delete these files to force relearning from scratch. Old data trained with wrong indices will poison the optimizer.

## Version History (Recent)

- **v3.0.22**: Tukey biweight in ScoringModule, soft gating, worst-feature penalty, shape classification (5-param), female chin bias fix (morph[48]/[52] → 0.50), per-SubPhase learning, skin tone infrastructure, full morph ranges, morphospace-aware generation, race diversity
- **v3.0.23**: Tukey biweight in HierarchicalScorer (main scoring path), tightened expected ranges, contour profile (6 landmark-based width measurements), fixed race preset morph indices (all were wrong), female face_width neutralized, EyebrowThickness scale-normalized
- **v3.0.24**: **Major feature extraction & gender detection overhaul:**
  - **Feature extraction recalibration**: 11 features were saturated (always 1.0/1.0 or 0.0/0.0). Reduced multipliers: NoseWidth 5→2, NoseLength 3→2, NoseBridge 4→3, NoseTip 6→5, MouthWidth 3→1.8, MouthHeight 8→5, UpperLip 15→10, LowerLip 15→10, EyeWidth 8→4.5, EyeHeight 15→10, ChinPointedness 140/80→160/120
  - **NostrilWidth**: Was DUPLICATE of NoseWidth (identical formula). Replaced with nostril-flare ratio
  - **EyeAngle**: Was broken (Angle at point 39 always ~30° → saturated). Replaced with canthal tilt (atan2-based eye slope)
  - **BrowAngle**: Was broken (always 0.0). Replaced with brow slope (inner-to-outer gradient)
  - **EyeVertPos fix**: Was ALWAYS 1.0 for renders. Root cause: `fullHeightRaw` (chin-to-brow) is unstable between photo/render bounding boxes. Fixed by using `faceHeightRaw` (chin-to-nose-bridge) instead. Same fix applied to MouthVertPos and CheekHeight
  - **Beard detection**: New beard heuristic in HairAppearanceDetector (samples lower-center face for hair-like pixels). Beard override in GenderEnsemble (bypasses all other methods if beard detected). LandmarkGenderEstimator jaw/chin/lip weights reduced 80% when beard detected
  - **Hair analysis weight reduced**: From 2.0/3.0 to 1.0/2.0 (most unreliable method)
  - **Expected ranges adjusted** in HierarchicalScorer for recalibrated features
  - **TestDataSource added**: Fixed 49-image test dataset for reproducible debugging (in `Datasets/testing/`)
  - **MLAddonImplementation**: Currently set to use ONLY TestDataSource (UTKFace/CelebA/LFW commented out)

## Testing

### Test Dataset
49 curated stock photos in `FaceLearner.ML/Datasets/testing/` for reproducible debugging.
- `TestDataSource.cs` loads them in deterministic (alphabetical) order
- Currently the ONLY active data source (others commented out in `MLAddonImplementation.cs`)
- **Remember to re-enable UTKFace/CelebA/LFW** when done debugging!

## Known Issues / Ongoing Work

- **Knowledge tree poisoned**: Old data trained with wrong morph indices or feature formulas needs to be deleted after each round of fixes
- **Gender detection**: FairFace can be wrong on ambiguous faces. Beard detection added in v3.0.24 but may false-positive on dark-skinned faces or heavy shadows
- **Score ceiling**: Engine renders can never perfectly match photos due to texture/mesh/lighting limitations. May need a realistic score ceiling instead of theoretical 1.0
- **Landmark limitations**: Dlib 68-point landmarks don't capture 3D depth, so features like CheekProminence and ForeheadSlope are estimated/constant
- **Feature multiplier calibration**: v3.0.24 multipliers are based on estimated percentile ranges. May need further tuning after test runs with real data

## Development Notes

- Always use `MorphGroups.cs` as the canonical reference for morph index→name mapping
- The `ScoringModule.cs` scoring path is LEGACY — the active scoring path is `HierarchicalScorer.cs`
- Landmarks are normalized to face bounding box (0-1 range) by Dlib detector
- Feature values in FeatureSet are all clamped to 0-1
- Expected ranges in HierarchicalScorer define what "100% error" means for each feature
- CMA-ES optimization runs for multiple iterations per face, adjusting all 62 morphs
