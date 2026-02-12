# FaceLearner v1.7.0 - Feature Intelligence Release

**Date:** 2026-01-22
**Version:** 1.7.0 (synchronized across all files)

## Version Locations
- `/FaceLearner/SubModule.cs` - `VERSION = "1.7.0"`
- `/FaceLearner.ML/SubModule.cs` - `VERSION = "1.7.0"`
- `/FaceLearner/SubModule.xml` - `<Version value="v1.7.0" />`
- `/FaceLearner.ML/SubModule.xml` - `<Version value="v1.7.0" />`

## Overview

Major release introducing the **Feature Intelligence System** - a comprehensive suite of four interconnected improvements to the optimization algorithm:

1. **Adaptive Feature Weights** - dynamically adjust scoring weights based on difficulty
2. **Feature Correlation Learning** - learn which morphs affect multiple features
3. **Progressive Feature Focus** - broad optimization first, then focus on weak features
4. **Feature History Tracking** - track progress over time, detect regressions

## New File: FeatureIntelligence.cs

Location: `FaceLearner.ML/Core/LivingKnowledge/FeatureIntelligence.cs`

### 1. Adaptive Feature Weights

**Problem:** All features weighted equally, but some are harder to match.

**Solution:**
```csharp
// Default weights (same as ScoringModule)
Eyes: 0.25, Nose: 0.18, Face: 0.17, Mouth: 0.15, Jaw: 0.13, Brows: 0.12

// After adaptation (example with hard Jaw):
Eyes: 0.22, Nose: 0.16, Face: 0.15, Mouth: 0.13, Jaw: 0.22, Brows: 0.11
```

**How it works:**
- Tracks difficulty per feature (0=easy, 1=hard)
- Difficulty updated based on observed scores (EMA, factor 0.1)
- Also tracks improvement success rate per feature
- Difficult features get up to 2x weight boost
- Weights normalized to sum to 1.0

**Usage:**
```csharp
// Called automatically every iteration with feature scores
_featureIntelligence.UpdateFeatureDifficulty(featureScores);

// Get current adaptive weights
var weights = _featureIntelligence.GetAdaptiveWeights();
```

### 2. Feature Correlation Learning

**Problem:** Some morphs affect multiple features, causing conflicts.

**Solution:**
```csharp
// Example correlation data learned:
Morph[12] (Nose Bridge) -> Nose: 0.45, Face: 0.12, Eyes: 0.08
Morph[23] (Jaw Width)   -> Jaw: 0.52, Face: 0.31, Mouth: 0.15
```

**How it works:**
- Records morph changes with before/after feature scores
- Learns which morphs affect which features
- Identifies "safe" morphs (affect only one feature)
- Identifies "conflicting" morphs (affect multiple)

**Usage:**
```csharp
// Record a morph change (called in LearnFromExperiment)
_featureIntelligence.RecordMorphChange(morphIndex, delta, scoresBefore, scoresAfter);

// Get morphs that are safe for a specific feature
var safeMorphs = _featureIntelligence.GetSafeMorphsForFeature("Nose");

// Check if a morph affects multiple features
bool isConflicting = _featureIntelligence.IsMorphConflicting(morphIndex);
```

### 3. Progressive Feature Focus

**Problem:** Optimizer doesn't adapt its strategy as optimization progresses.

**Solution:** Three-phase approach:
```
Phase 1 (iter 1-20):   BROAD      - Optimize all features equally
Phase 2 (iter 21-40):  NARROWING  - Start focusing on bottom 3 features
Phase 3 (iter 41+):    FOCUSED    - Heavy focus on bottom 1-2 features
```

**Focus Probability:**
- Broad: 0%
- Narrowing: 30% → 60% (gradually increases)
- Focused: 70%

**Usage:**
```csharp
// Called every iteration
_featureIntelligence.UpdateIteration(iterationNumber, currentScores);

// Check current phase
FocusPhase phase = _featureIntelligence.CurrentPhase;

// Get focused features
List<string> focused = _featureIntelligence.FocusedFeatures;

// Should we focus this iteration?
bool shouldFocus = _featureIntelligence.ShouldFocusOnWeakFeatures(random);
```

### 4. Feature History Tracking

**Problem:** No way to detect when a feature regresses (gets worse).

**Solution:**
```csharp
// Tracks last 50 iterations per feature
// Detects regression when score drops > 0.05 from peak

// Example regression alert:
RegressionInfo {
    Feature: "Jaw",
    PeakScore: 0.65,
    CurrentScore: 0.52,
    DropAmount: 0.13,
    PeakIteration: 28,
    CurrentIteration: 45
}
```

**Usage:**
```csharp
// Get active regressions
var regressions = _featureIntelligence.GetRegressionAlerts();

// Check specific feature
bool isRegressing = _featureIntelligence.IsFeatureRegressing("Jaw");

// Get trend (positive = improving, negative = declining)
float trend = _featureIntelligence.GetFeatureTrend("Eyes");
```

## Integration Points

### LearningOrchestrator Changes

1. **New Field:**
```csharp
private FeatureIntelligence _featureIntelligence;
```

2. **Initialization (constructor):**
```csharp
_featureIntelligence = new FeatureIntelligence(knowledgePath + "feature_intelligence.dat");
```

3. **Reset for new target:**
```csharp
_featureIntelligence?.ResetForNewTarget();
```

4. **Update every iteration (in comparison processing):**
```csharp
_featureIntelligence?.UpdateIteration(_iterationsOnTarget, currentFeatureScores);
```

5. **Correlation learning (in LearnFromExperiment):**
```csharp
_featureIntelligence.RecordMorphChange(index, delta, previousScores, currentScores);
```

6. **Mutation recommendation:**
```csharp
var rec = _featureIntelligence.GetRecommendation(currentScores, _random);
if (rec.Priority == MutationPriority.FixRegression) {
    // Handle regression with highest priority
}
```

7. **Progressive focus in mutation:**
```csharp
baseProbability = _featureIntelligence.GetFocusProbability();
```

8. **Save on stop and periodically:**
```csharp
_featureIntelligence?.Save();
```

### New Public Properties

```csharp
public string FeatureIntelligenceStatus => _featureIntelligence?.GetFullSummary();
public FocusPhase CurrentFocusPhase => _featureIntelligence?.CurrentPhase;
public List<string> FocusedFeatures => _featureIntelligence?.FocusedFeatures;
public bool HasActiveRegressions => _featureIntelligence?.GetRegressionAlerts()?.Count > 0;
```

## New Types

```csharp
public enum FocusPhase
{
    Broad,      // Optimize all features equally
    Narrowing,  // Start focusing on weak features
    Focused     // Heavily focus on 1-2 weakest features
}

public enum MutationPriority
{
    FixRegression,      // Fix a regressing feature (highest priority)
    FocusWeak,          // Focus on weak features
    BroadOptimization   // Optimize broadly
}

public class MutationRecommendation
{
    public MutationPriority Priority;
    public string TargetFeature;
    public string Reason;
    public List<int> RecommendedMorphs;
    public Dictionary<string, float> AdaptiveWeights;
}
```

## Log Messages

New log messages to watch for:
```
[FeatureIntelligence] Saved to ...
[FeatureIntelligence] Loaded from ...
⚠️ Regression detected: Jaw dropped 0.13
🎯 Feature focus: Nose (0.32) +8 morphs
```

## Persistence

Saves to `feature_intelligence.dat`:
- Feature difficulties
- Morph-feature correlations
- Improvement counts

## Testing

1. **Adaptive Weights:** Watch difficulty summary in logs, verify hard features get higher weights
2. **Correlations:** After 100+ iterations, check `GetCorrelationSummary()` shows learned data
3. **Progressive Focus:** Verify phase transitions at iterations 20 and 40
4. **Regressions:** Artificially worsen a feature, verify alert appears

## Migration

No migration needed. New file `feature_intelligence.dat` is created automatically.

## Version History

- **v1.6.2** - Per-feature best tracking, feature-based revert
- **v1.7.0** - Feature Intelligence System (this version)
