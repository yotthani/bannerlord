# FaceLearner Solution v1.3.0

Two-project solution with clean separation.

## Projects

### FaceLearner (Core) - ~50KB compiled
**Required base mod for all users**

Contains:
- Character creation UI
- Body/Face/Hair editing  
- Export/Import characters
- KnowledgeReader (read pre-trained knowledge)
- IMLAddon interface

NO ML dependencies - just Bannerlord APIs.

### FaceLearner.ML (Addon) - ~5MB compiled
**Optional addon for ML features**

Contains:
- LandmarkDetector (ONNX)
- LearningOrchestrator
- HierarchicalKnowledge (read/write)
- CMA-ES optimization
- All ML modules and data sources
- Models and Datasets folders

Requires Core as dependency.

## Building

1. Set `BANNERLORD_GAME_DIR` environment variable
2. Open `FaceLearner.sln`
3. Build:
   - Core only: Right-click FaceLearner → Build
   - Both: Build Solution

Output:
- `Modules/FaceLearner/` (Core)
- `Modules/FaceLearner.ML/` (Addon)

## For Users

**Basic install (Character creation only):**
```
Modules/FaceLearner/
```

**Full install (With ML training):**
```
Modules/FaceLearner/
Modules/FaceLearner.ML/
```

ML Addon loads after Core via DependedModules.
