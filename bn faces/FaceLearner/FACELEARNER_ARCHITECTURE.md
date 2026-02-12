# FaceLearner Architecture Documentation

**Version**: v1.0.0  
**Status**: Production Ready  
**Last Updated**: January 2026

---

## Overview

FaceLearner is a self-improving face generation system for Mount & Blade II: Bannerlord.
It learns how each morph slider affects facial landmarks and uses this knowledge to
recreate faces from photos.

**Key Insight**: Instead of brute-force ML (image → morphs), we use an evolutionary approach 
that learns **causation** - understanding what each slider DOES to face landmarks, then using 
that knowledge to achieve target faces.

---

## Core Algorithm: Hierarchical Learning + CMA-ES

```
1. Classify target face → Extract traits (gender, age, face shape, etc.)
2. Query HierarchicalKnowledge → Get trait-based starting morphs
3. Render face → Extract landmarks via ONNX
4. Compare to target → Score (0.0 to 1.0)
5. CMA-ES Evolution:
   - Sample population of morph variations
   - Evaluate fitness of each candidate
   - Update distribution towards better solutions
6. Hill Climbing Refinement:
   - Random mutations from best known state
   - Accept improvements, revert failures
   - Adaptive step size when stuck
7. Learn from experiment:
   - Store trait → morph mappings
   - Self-organize tree (split/merge/prune)
8. GOTO 3 until converged
```

### Why This Works
- **Hierarchical priors**: Start from learned trait-based knowledge
- **CMA-ES**: Efficient global search in high-dimensional morph space
- **Hill climbing**: Fine-grained local optimization
- **Self-organization**: Knowledge improves with every face learned

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         SubModule.cs                            │
│                       (Mod Entry Point)                         │
└─────────────────────────────┬───────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│                       FaceLearnerVM.cs                          │
│  - MVVM ViewModel for UI bindings                               │
│  - Camera controls (zoom, height, horizontal)                   │
│  - Status display and logging                                   │
└─────────────────────────────┬───────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│                  LearningOrchestrator.cs                        │
│  - Iterate() → apply morphs to face                             │
│  - OnRenderCaptured() → score, learn, mutate                    │
│  - GenerateNextMutation() → CMA-ES + hill climbing              │
│  - Stuck detection and adaptive exploration                     │
└────┬────────────┬───────────┬───────────┬───────────────────────┘
     │            │           │           │
┌────▼────┐ ┌─────▼─────┐ ┌───▼───┐ ┌────▼────┐
│Hierarch.│ │  Phase    │ │Session│ │Orchestr.│
│Knowledge│ │ Manager   │ │Monitor│ │ Memory  │
│         │ │           │ │       │ │         │
│Self-org │ │ 6 phases  │ │Trends │ │ History │
│  Tree   │ │ Evolution │ │Detect │ │ Stats   │
└─────────┘ └───────────┘ └───────┘ └─────────┘
```

---

## Core Components

### 1. HierarchicalKnowledge (Self-Organizing Tree)

The central knowledge store that learns from experience without external supervision.

**Structure:**
```
ROOT
├── Gender
│   ├── Male
│   │   ├── AgeGroup
│   │   │   ├── Young → {morph deltas}
│   │   │   ├── Middle → {morph deltas}
│   │   │   └── Mature → {morph deltas}
│   │   └── FaceWidth
│   │       ├── Wide → {morph deltas}
│   │       └── Narrow → {morph deltas}
│   └── Female
│       └── ...
└── ...
```

**Self-Organization:**
- **Split**: High-variance nodes split into subtypes
- **Merge**: Similar sibling nodes merge
- **Prune**: Stale, unused nodes are removed

### 2. LandmarkUtils (Static Utilities)

Pure utility class for landmark calculations - no state, single responsibility.

```csharp
// Distance calculations
float CalculateLandmarkDistance(float[] a, float[] b)
float CalculateShapeDistance(float[] a, float[] b)

// Scoring
float CalculateMatchScore(float[] current, float[] target)
(float total, float landmark, float shape) CalculateDetailedScore(...)

// Analysis
float[] CalculateFaceShapeRatios(float[] landmarks)
(float rmse, float maxDiff, int worstIdx) GetDetailedDistance(...)
```

### 3. ModuleIntegration

Coordinates ML modules for feature-level analysis:

- **ProportionsModule**: Facial proportions (thirds, fifths)
- **ScoringModule**: Per-feature match scores
- **Demographics**: Age, gender, skin tone

### 4. CMA-ES Optimizer

Covariance Matrix Adaptation Evolution Strategy for efficient morphs search:

- Population-based optimization
- Adapts search distribution
- Handles high-dimensional space (62 morphs)

### 5. PhaseManager

Adaptive learning phases:

| Phase | Description | Exit Condition |
|-------|-------------|----------------|
| Explore | Wide search | Score > 0.5 |
| Refine | Focus on promising areas | Score > 0.65 |
| Polish | Fine-tune details | Score > 0.75 |
| Exploit | Use learned knowledge | Score > 0.85 |
| Escape | Break local optima | Stuck > 100 |
| Finalize | Final convergence | Score > 0.9 |

### 6. SessionMonitor

Tracks learning progress:

- Short/medium/long term score trends
- Stagnation detection
- Volatility monitoring
- Intervention triggers

---

## Data Flow

```
┌─────────────┐     ┌──────────────┐     ┌────────────────┐
│ Data Source │────▶│  Landmark    │────▶│ Target         │
│ (UTK, LFW)  │     │  Detector    │     │ Landmarks      │
└─────────────┘     └──────────────┘     └───────┬────────┘
                                                  │
                                                  ▼
┌─────────────┐     ┌──────────────┐     ┌────────────────┐
│ Hierarchical│◀────│  Learning    │◀────│ Score          │
│ Knowledge   │     │ Orchestrator │     │ Comparison     │
└─────────────┘     └──────────────┘     └────────────────┘
       │                   │                      ▲
       │                   ▼                      │
       │            ┌──────────────┐     ┌────────────────┐
       └───────────▶│    Face      │────▶│  Current       │
                    │  Controller  │     │  Landmarks     │
                    └──────────────┘     └────────────────┘
```

---

## Scoring System

The match score combines multiple factors:

```csharp
// Landmark-based score (geometric similarity)
float landmarkScore = exp(-landmarkDistance * 10.0)

// Shape-based score (proportion similarity)  
float shapeScore = exp(-shapeDistance * 5.0)

// Combined score
float totalScore = landmarkScore * 0.5 + shapeScore * 0.5
```

With feature integration enabled:
```csharp
// Per-feature scores (eyes, nose, mouth, etc.)
foreach (feature in features)
    featureScores[feature] = ComputeFeatureMatch(...)

// Weighted combination
float finalScore = Sum(featureScores * featureWeights) / totalWeight
```

---

## File Structure

```
FaceLearner/
├── Core/
│   ├── LivingKnowledge/
│   │   ├── HierarchicalKnowledge.cs  # Self-organizing knowledge
│   │   ├── LandmarkUtils.cs          # Static utility methods
│   │   ├── LearningOrchestrator.cs   # Main learning engine
│   │   ├── PhaseManager.cs           # Adaptive phases
│   │   ├── SessionMonitor.cs         # Progress monitoring
│   │   ├── OrchestratorMemory.cs     # History storage
│   │   ├── ModuleIntegration.cs      # ML module coordination
│   │   ├── FaceTypeAnalyzer.cs       # Face classification
│   │   ├── DynamicPhase.cs           # Phase definitions
│   │   └── CmaEs.cs                  # CMA-ES optimizer
│   ├── FaceLearnerVM.cs              # ViewModel
│   ├── FaceController.cs             # Game face API
│   └── BodyEstimator.cs              # Body shape estimation
├── ML/
│   ├── DataSources/                  # Image data sources
│   ├── Modules/                      # ML analysis modules
│   └── *Detector.cs                  # Various detectors
├── Utils/
│   └── ComparisonImageCreator.cs     # Visual comparisons
└── GUI/
    └── Prefabs/                      # UI definitions
```

---

## Performance Characteristics

- **Iterations per face**: ~50-80 (with early termination)
- **Time per iteration**: ~100ms
- **Total time per face**: 5-8 seconds
- **Knowledge growth**: Self-organizing, no manual tuning
- **Memory usage**: ~50MB for knowledge tree

---

## Clean Code Principles

1. **Single Responsibility**: Each class has one job
2. **Open/Closed**: Extend via new modules, not modifications
3. **Interface Segregation**: Minimal interfaces (IFaceDataSource, etc.)
4. **DRY**: Utilities centralized in LandmarkUtils
5. **YAGNI**: Dead code removed

---

## References

- [CMA-ES Algorithm](https://en.wikipedia.org/wiki/CMA-ES)
- [MediaPipe Face Mesh](https://google.github.io/mediapipe/solutions/face_mesh.html)
- [Bannerlord Modding](https://docs.bannerlordmodding.com/)
