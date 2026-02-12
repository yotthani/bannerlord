# FaceLearner v1.3.0 - Split Architecture

**Date**: January 2026  
**Status**: Major Architecture Split - Core + ML Addon

---

## Overview: Two-Mod Architecture

The mod has been split into two separate mods:

| Mod | Size | Purpose | Required |
|-----|------|---------|----------|
| **FaceLearner** (Core) | ~200KB | Character creation & editing | ✅ Yes |
| **FaceLearner.ML** (Addon) | ~5MB+ | ML training & generation | ❌ Optional |

### Why Split?

1. **Most users don't need ML** - They just want to create characters
2. **Heavy dependencies** - ONNX runtime, models, etc. slow down loading
3. **Modular approach** - Install what you need
4. **Faster iteration** - Core updates don't require re-downloading ML

---

## FaceLearner (Core)

The lightweight base mod that everyone needs.

### Features
- ✅ Tabbed UI (CREATE, LEARN*, SETTINGS)
- ✅ Character editor (body, skin, hair, eyes)
- ✅ Face preview
- ✅ Export/Import characters (XML)
- ✅ Read pre-trained knowledge
- ✅ Simple face generation (using saved knowledge)

### What it CANNOT do (without ML addon)
- ❌ ML-based face generation from images
- ❌ Landmark detection
- ❌ Training from datasets
- ❌ CMA-ES optimization

### Files
```
FaceLearner/
├── Core/
│   ├── CharacterData.cs        # Character data model
│   ├── FaceLearnerVM.cs        # Main ViewModel
│   ├── FaceLearnerVM_Tabs.cs   # Tab UI extensions
│   ├── FaceController.cs       # Game face API
│   ├── IMLAddon.cs             # ML addon interface
│   ├── KnowledgeReader.cs      # Read-only knowledge
│   └── ...
├── GUI/
│   └── Prefabs/FaceLearnerScreen.xml
├── SubModule.cs
└── SubModule.xml
```

---

## FaceLearner.ML (Addon)

Optional addon for ML capabilities.

### Features
- ✅ Full ML-based face generation
- ✅ Landmark detection (MediaPipe)
- ✅ LEARN tab functionality
- ✅ Training from datasets
- ✅ Knowledge writing/training
- ✅ CMA-ES optimization
- ✅ Per-feature scoring

### Dependencies
- FaceLearner (Core) - Required
- Microsoft.ML.OnnxRuntime
- System.Drawing.Common
- SixLabors.ImageSharp

### Files
```
FaceLearner.ML/
├── Core/
│   ├── MLAddonImplementation.cs   # IMLAddon implementation
│   └── LivingKnowledge/           # Full knowledge system
│       ├── HierarchicalKnowledge.cs
│       ├── LearningOrchestrator.cs
│       └── ...
├── ML/
│   ├── LandmarkDetector.cs
│   ├── Modules/
│   └── DataSources/
├── SubModule.cs
└── SubModule.xml
```

---

## How the Split Works

### Interface: IMLAddon

```csharp
public interface IMLAddon
{
    bool IsReady { get; }
    bool Initialize();
    
    (float[] morphs, float score) GenerateFace(
        string imagePath, 
        bool isFemale, 
        Action<float> progress = null);
    
    void StartLearning();
    void StopLearning();
    bool IsLearning { get; }
    
    string GetStatusText();
    string GetKnowledgeStats();
    
    void Tick(float dt);
    void Dispose();
}
```

### Registry: MLAddonRegistry

```csharp
public static class MLAddonRegistry
{
    public static bool HasAddon { get; }
    public static IMLAddon Addon { get; }
    public static void Register(IMLAddon addon);
    public static void Unregister();
}
```

### Flow

1. Core loads → UI available
2. If ML addon present:
   - ML addon loads after Core
   - Calls `MLAddonRegistry.Register(this)`
   - LEARN tab becomes functional
   - "Generate Face" uses ML
3. If ML addon NOT present:
   - LEARN tab shows "Install ML addon" message
   - "Generate Face" uses simple knowledge-based generation

---

## KnowledgeReader (Core)

Lightweight, read-only knowledge access:

```csharp
public class KnowledgeReader
{
    public bool Load();
    public float[] GetStartingMorphs(Dictionary<string, string> features);
    public float[] GetSimpleMorphs(bool isFemale, float age);
}
```

- Reads shared feature knowledge from HKNOW04 files
- Falls back to built-in defaults if no file exists
- Does NOT write or train

---

## UI Changes

### LEARN Tab Behavior

**With ML Addon:**
```
┌──────────────────┐
│ [Initialize]     │
│ [▶ Start Train]  │
│ [■ Stop]         │
│                  │
│ Stats: 25 shared │
└──────────────────┘
```

**Without ML Addon:**
```
┌──────────────────────────────┐
│   ML Addon not installed     │
│                              │
│   Install FaceLearner.ML     │
│   for:                       │
│   • ML-based face generation │
│   • Training from datasets   │
│   • Better face matching     │
└──────────────────────────────┘
```

### Generate Face Behavior

**With ML Addon:**
- Full landmark detection
- CMA-ES optimization
- Per-feature scoring
- High-quality results

**Without ML Addon:**
- Uses pre-trained knowledge
- Basic morph combination
- No image matching
- Acceptable for simple use

---

## Installation

### Core Only (Minimal)
```
Modules/
└── FaceLearner/
    ├── bin/FaceLearner.dll
    ├── GUI/
    ├── Data/
    │   └── hierarchical_knowledge.dat  (pre-trained)
    └── SubModule.xml
```

### Core + ML (Full)
```
Modules/
├── FaceLearner/
│   └── ...
└── FaceLearner.ML/
    ├── bin/
    │   ├── FaceLearner.ML.dll
    │   ├── Microsoft.ML.OnnxRuntime.dll
    │   └── ...
    ├── Data/Models/
    │   ├── face_landmarker.task
    │   └── ...
    └── SubModule.xml
```

---

## Version History

| Version | Feature |
|---------|---------|
| v1.0.0 | Clean architecture |
| v1.1.0 | Two-layer knowledge |
| v1.2.0 | Tabbed UI, Character Editor |
| **v1.3.0** | **Split: Core + ML Addon** |

---

## Migration Notes

### For Users
- If you only create characters: Install Core only
- If you want ML training: Install Core + ML addon
- Pre-trained knowledge files work with Core alone

### For Developers
- Core references: TaleWorlds.* only
- ML references: Core + ONNX + ImageSharp
- ML addon must load AFTER Core (via DependedModules)
