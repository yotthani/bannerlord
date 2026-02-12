# FaceLearner Module System

## Architecture Overview

```
                    ┌─────────────────────────────────────┐
                    │       IFaceAnalyzer (PUBLIC)        │
                    │                                     │
                    │  • Analyze(image) → FaceAnalysisResult
                    │  • Compare(a, b)  → FeatureScoreResult
                    └─────────────────┬───────────────────┘
                                      │
    ┌──────────────┬──────────────────┼──────────────────┬──────────────┐
    │              │                  │                  │              │
    ▼              ▼                  ▼                  ▼              ▼
┌────────┐  ┌───────────┐    ┌─────────────┐    ┌────────────┐   ┌─────────┐
│LANDMARKS│  │FACE PARSE │    │ PROPORTIONS │    │DEMOGRAPHICS│   │ SCORING │
│ MODULE  │  │  MODULE   │    │   MODULE    │    │   MODULE   │   │ MODULE  │
├────────┤  ├───────────┤    ├─────────────┤    ├────────────┤   ├─────────┤
│MediaPipe│  │ BiSeNet   │    │FaceGeometry │    │Gender:     │   │Per-feat │
│ dlib   │  │ 19 regions│    │EyeAnalyzer  │    │ •FairFace  │   │scores:  │
└────────┘  └───────────┘    │NoseAnalyzer │    │ •Landmarks │   │ •Face   │
                              │MouthAnalyzer│    │ •Beard     │   │ •Eyes   │
                              │JawAnalyzer  │    │Age:        │   │ •Nose   │
                              │EyebrowAnalyz│    │ •FairFace  │   │ •Mouth  │
                              └─────────────┘    │SkinTone:   │   │ •Jaw    │
                                                 │ •ITA-based │   │ •Brows  │
                                                 └────────────┘   └─────────┘
```

## Key Principles

### 1. Blackbox API
```csharp
// Users only see this:
IFaceAnalyzer analyzer = FaceAnalyzerFactory.Create(basePath);
FaceAnalysisResult result = analyzer.Analyze(image);

// That's it. Internal complexity is hidden.
```

### 2. Specialized Modules
Each module does ONE thing well:
- **Landmarks**: Detect facial landmarks (MediaPipe primary, dlib fallback)
- **FaceParsing**: Segment face into regions (BiSeNet)
- **Proportions**: Measure face features (6 specialized analyzers)
- **Scoring**: Compare faces feature-by-feature

### 3. Feature-Based Scoring
Instead of one opaque number:
```
OLD:  Score: 0.72 (what's wrong? 🤷)

NEW:  Face Shape: 0.82  ✓
      Eyes:       0.65
      Nose:       0.31  ← PROBLEM!
      Mouth:      0.89  ✓
      Overall:    0.71
```

### 4. Calibrated Confidence
```csharp
// Raw softmax 0.57 means 57% female, 43% male
// That's basically a coin flip, NOT "57% confident"!

// We calibrate: 0.50 → 0, 0.70 → 0.40, 0.90 → 0.80
float calibrated = ConfidenceCalibrator.FromProbability(0.57f);
// Result: 0.14 (correctly shows uncertainty)
```

## Directory Structure

```
/ML/Modules/
├── Core/
│   └── Interfaces.cs           # Base interfaces, enums, ConfidenceCalibrator
│
├── FaceParsing/
│   ├── FaceParsingModule.cs    # Orchestrates parsing
│   ├── BiSeNetDetector.cs      # BiSeNet implementation
│   └── FaceParsingResult.cs    # 19-region mask result
│
├── Proportions/
│   ├── ProportionsModule.cs    # Orchestrates all analyzers
│   ├── ProportionsResult.cs    # Per-feature measurements
│   └── (analyzers inline)      # FaceGeometry, Eye, Nose, Mouth, Jaw, Eyebrow
│
├── Scoring/
│   └── ScoringModule.cs        # Feature-by-feature comparison
│
├── Demographics/
│   ├── DemographicsModule.cs   # Orchestrates Gender/Age/SkinTone
│   ├── Gender/
│   │   └── GenderModule.cs     # Multi-signal voting
│   ├── Age/
│   │   └── AgeModule.cs        # FairFace with bias correction
│   └── SkinTone/
│       └── SkinToneModule.cs   # ITA-based (industry standard)
│
├── Infrastructure/
│   └── ModuleLogger.cs         # Centralized logging
│
└── FaceAnalyzer.cs             # PUBLIC API (IFaceAnalyzer)
```

## Required Models

| Model | Purpose | Size | Status |
|-------|---------|------|--------|
| MediaPipe FaceMesh | Landmarks | 2MB | ✅ Existing |
| dlib shape_predictor | Fallback landmarks | 95MB | ✅ Existing |
| **BiSeNet (resnet18)** | Face Parsing | 43MB | ✅ Download below |
| FairFace | Demographics | 85MB | ✅ Existing |

### BiSeNet Face Parsing Model

**Source:** https://github.com/yakhyo/face-parsing (MIT License)

**Direct Download:**
```bash
# ResNet18 (recommended - faster, smaller)
curl -L -o resnet18.onnx https://github.com/yakhyo/face-parsing/releases/download/v0.0.1/resnet18.onnx

# ResNet34 (optional - more accurate)
curl -L -o resnet34.onnx https://github.com/yakhyo/face-parsing/releases/download/v0.0.1/resnet34.onnx
```

**Installation:**
```
Place in: Modules/FaceLearner/Data/Models/resnet18.onnx
      or: Modules/FaceLearner/Models/resnet18.onnx
```

**Model Specs:**
- Input: 512×512 RGB (NCHW format, ImageNet normalized)
- Output: 512×512 segmentation mask
- 19 Classes: background, skin, l_brow, r_brow, l_eye, r_eye, glasses, l_ear, r_ear, earring, nose, mouth, u_lip, l_lip, neck, necklace, cloth, hair, hat

## Usage Example

```csharp
// Initialize
var analyzer = FaceAnalyzerFactory.Create("path/to/FaceLearner");

// Analyze target image
var target = analyzer.Analyze("target.jpg");

// Analyze current render
var current = analyzer.Analyze(screenshotBitmap);

// Get per-feature scores
var scores = analyzer.Compare(target, current);

// Find problem areas
if (scores.Nose < 0.5f)
{
    Console.WriteLine("Nose is the main problem!");
    // Focus mutations on nose sliders
}

// Get detailed breakdown
foreach (var (feature, score) in scores.GetFeaturesByScore())
{
    Console.WriteLine($"{feature}: {score:F2}");
}
```

## Migration from Old System

The old `LearningOrchestrator` mixed everything together. The new system separates concerns:

| Old | New |
|-----|-----|
| `ApplyTargetAttributesToCharacter()` | `analyzer.Analyze()` → Demographics |
| `CalculateMatchScore()` | `analyzer.Compare()` → FeatureScoreResult |
| `CalculateFaceShapeRatios()` | `ProportionsModule` → ProportionsResult |
| Scattered confidence logic | `ConfidenceCalibrator` (centralized) |

## Next Steps

1. ✅ Core interfaces and calibration
2. ✅ Face Parsing module (BiSeNet)
3. ✅ Proportions module (6 analyzers)
4. ✅ Scoring module (per-feature)
5. ✅ Public API (IFaceAnalyzer)
6. ✅ Demographics module (Gender/Age/SkinTone)
7. ✅ Integration with LearningOrchestrator (ModuleIntegration adapter)
8. ✅ BiSeNet model support (resnet18.onnx, resnet34.onnx)

## Integration Usage

The `ModuleIntegration` adapter connects the new module system to `LearningOrchestrator`:

```csharp
// In LearningOrchestrator constructor
_moduleIntegration = new ModuleIntegration();
_moduleIntegration.Initialize(_basePath);

// Analyze target with enhanced demographics
var demographics = _moduleIntegration.AnalyzeTarget(imagePath);
if (demographics.HasFacialHair)
{
    // Override FairFace's uncertain gender - beards mean male!
}

// Get per-feature score breakdown
float score = _moduleIntegration.CalculateFeatureScore(renderBitmap);
var worstFeature = _moduleIntegration.WorstFeature;  // e.g., "Nose"

// Guided mutation based on problem areas
var guidance = _moduleIntegration.GetMutationGuidance();
foreach (int morphIdx in guidance.GetPrioritizedIndices())
{
    if (guidance.ShouldMutate(morphIdx, random))
    {
        // Mutate this morph with higher probability
    }
}
```
