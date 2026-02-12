# FaceLearner v1.6.0 Checkpoint

**Date:** 2025-01-22
**Version:** 1.6.2

## Overview

This checkpoint represents a major improvement to the optimization algorithm with **per-feature best tracking** and **feature-based revert logic**.

## Key Changes Since v1.4.7

### v1.5.0 - Face Shape Detection Improvements
- **Round Jaw Detection:** Better detection for round jaws vs angular
- **Asian Face Shape:** Improved handling of rounder face shapes common in East Asian faces
- **Latino/White Skin Tone:** Fixed skin tone detection for ambiguous cases

### v1.5.2 - Better Feature Prioritization
- Dynamic feature-focus probability based on worst feature score:
  - Score < 0.30 → 70% chance to focus (very aggressive)
  - Score < 0.40 → 50% chance to focus
  - Score < 0.50 → 30% chance to focus
- Extra +15% when stuck for 8+ iterations
- Extra +15% when stuck for 15+ iterations
- Larger step size (1.5x) for very bad features (<0.3)

### v1.5.3 - CMA-ES Early Generation Abort
- If 6+ consecutive candidates worse than best, abort generation early
- Prevents wasting 16 iterations on bad directions
- Fills remaining candidates with worst fitness so CMA-ES learns

### v1.6.0 - Per-Feature Best Tracking
**The Core Innovation:**
- Track best morphs for EACH feature independently
- `_bestMorphsPerFeature["Face"]` = morphs that gave best Face score
- `_bestMorphsPerFeature["Jaw"]` = morphs that gave best Jaw score
- At end of target, try combining best features

### v1.6.1 - Feature-Based Revert
- Every 5th revert uses combined best features instead of best overall
- Creates potentially better baseline from best individual features
- Then continues optimizing from this new baseline

### v1.6.2 - Build Fixes
- Fixed compilation errors for proper API usage
- Simplified TryCombineBestFeatures (no inline evaluation)
- Uses MorphDefinitions.AllMorphs correctly

## Architecture

### Key Classes Modified:
- `LearningOrchestrator.cs` - Main optimization loop
  - Per-feature tracking fields
  - Feature-based revert logic
  - CMA-ES early abort
  - Feature combination methods

### New Methods:
- `CreateCombinedFeatureMorphs()` - Blends best morphs per feature
- `GetMorphIndicesForFeature()` - Maps feature names to morph indices
- `TryCombineBestFeatures()` - Tries feature combination at target end

### Data Structures:
```csharp
// Per-feature best tracking
private Dictionary<string, float[]> _bestMorphsPerFeature;
private Dictionary<string, float> _bestScorePerFeature;
```

## The Problem Solved

**Before:**
```
Iteration 20: Overall=0.58, Face=0.20 ← BEST overall (saved)
Iteration 35: Overall=0.55, Face=0.44 ← Better face, DISCARDED!
```

**After:**
- Both iterations tracked per-feature
- Best Face morphs saved separately from best overall
- At end/stuck: combine best Face + best Jaw + best Eyes...
- Result: potentially better overall from combining individual bests

## File Structure

```
FaceLearner/
├── Core/
│   ├── FaceController.cs
│   ├── FaceLearnerVM.cs
│   ├── MorphDefinitions.cs      # 62 morph definitions
│   └── ...
├── CHECKPOINT_v1.3.0.md
├── CHECKPOINT_v1.6.0.md         # This file
└── ...

FaceLearner.ML/
├── Core/
│   └── LivingKnowledge/
│       ├── LearningOrchestrator.cs  # Main changes here
│       ├── CmaEs.cs
│       ├── FaceTypeAnalyzer.cs
│       └── ...
├── ML/
│   └── Modules/
│       └── Scoring/
└── ...
```

## Testing Notes

- Test with faces that have one very bad feature (e.g., Face=0.2)
- Should see "🔀 Feature focus" and "🔀 Trying feature combination" in logs
- CMA-ES should abort early when generation is bad
- Feature-based revert every 5th revert when stuck

## Next Steps / Ideas

1. **Adaptive Feature Weights:** Weight features by difficulty (some features harder to match)
2. **Feature Correlation Learning:** Learn which morphs affect multiple features
3. **Progressive Feature Focus:** Start broad, then focus on worst features
4. **Feature History:** Track how features improve over time, detect regressions
