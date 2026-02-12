# FaceLearner Performance & Learning Improvement Analysis

## Current Architecture Overview

### Learning Loop
```
Frame Tick → Check Warmup (2 frames) → Capture Screenshot → Process Render
           → OnRenderCaptured → Score Calculation → CMA-ES Mutation
           → Apply Morphs → Refresh Character → Next Frame
```

### Key Constants
```csharp
WARMUP_FRAMES = 2               // Frames to wait after morph change
FEATURE_SCORE_INTERVAL = 5      // Feature scoring frequency
IDENTICAL_SCORE_THRESHOLD = 3   // Iterations before forcing change
ABSOLUTE_MAX = 400              // Max iterations per target
KEEP_COMPARISONS_COUNT = 200    // Comparison images to keep
EVOLVE_EVERY_N_TARGETS = 20     // Hierarchical knowledge evolution
```

---

## Log Analysis Findings (2026-01-24)

### Typical Results
- **Final scores:** Range from 0.413 to 0.755
- **Iterations per target:** 41-61 iterations
- **Revert rate:** 48-59% of iterations result in reverts

### Feature Score Patterns
| Feature   | Typical Range | Issue |
|-----------|---------------|-------|
| Face      | 0.14-0.47     | Often LOWEST - needs focus |
| Eyes      | 0.47-0.80     | Moderate |
| Nose      | 0.22-0.76     | Inconsistent |
| Mouth     | 0.21-0.77     | Inconsistent |
| Jaw       | 0.16-0.75     | Often LOW |
| Brows     | 0.83-0.98     | Always HIGH (over-easy?) |

### Key Insight
**Face and Jaw scores are consistently the weakest features.**
The algorithm struggles with overall face shape and jawline more than individual features.

---

## Performance Analysis

### Current Bottlenecks

#### 1. Screenshot Capture & File I/O
**Impact: HIGH**
- Every iteration saves PNG to disk, waits for file existence
- File I/O is slow compared to in-memory operations

**Potential Fixes:**
- Use in-memory bitmap transfer instead of file-based
- Implement double-buffering for captures
- Consider render-to-texture if engine supports it

#### 2. Landmark Detection (ML Model)
**Impact: HIGH**
- ONNX model inference every frame
- Currently using full 468-point MediaPipe or 68-point Dlib

**Potential Fixes:**
- Cache landmark detection for similar faces
- Use smaller/faster landmark model during learning
- Batch multiple detections if possible

#### 3. Character Recreation on Gender Change
**Impact: MEDIUM**
- Full AgentVisuals recreation is expensive
- Currently happens every time gender changes

**Potential Fixes:**
- Pre-create both Male and Female visuals, swap visibility
- Pool and reuse AgentVisuals objects

#### 4. Feature Score Calculation
**Impact: MEDIUM**
- ProportionsAnalyzer runs full analysis
- Currently only cached for 5 iterations

**Potential Fixes:**
- Reduce FEATURE_SCORE_INTERVAL to 10 for exploration, 3 for refinement
- Cache more intermediate calculations
- Use simplified scoring during exploration phase

#### 5. RefreshCharacter() Calls
**Impact: MEDIUM**
- Full visual refresh every morph change
- Includes skeleton tick, resource check, visibility toggle

**Potential Fixes:**
- Batch morph changes, single refresh
- Skip visibility toggle if already visible
- Optimize skeleton update timing

---

## Learning Algorithm Improvements

### Current Scoring System

**Formula:**
```
Total Score = LandmarkScore × 0.35 + ShapeScore × 0.65
            + FeatureScore × 0.50 (blended)
            - WorstFeaturePenalty (if < 0.40)
```

**Feature Weights:**
- Eyes: 0.22
- Nose: 0.22
- FaceShape: 0.17
- Jaw: 0.15
- Mouth: 0.13
- Eyebrows: 0.11

### Improvement Opportunities

#### 1. Adaptive Feature Weighting
**Problem:** Static weights don't account for which features are most wrong

**Solution:**
```csharp
// Dynamic weight boost for worst features
if (featureScore < 0.5f)
{
    weight *= 1.0f + (0.5f - featureScore) * 2.0f;
}
```

#### 2. Multi-Objective Optimization
**Problem:** Single score hides individual feature progress

**Solution:**
- Track Pareto-optimal solutions (best combinations of features)
- Use NSGA-II or similar multi-objective algorithm
- Allow trade-offs between features

#### 3. Feature-Specific Morph Learning
**Problem:** CMA-ES doesn't know which morphs affect which features

**Solution:**
- Build morph-to-feature correlation matrix
- When Nose score is low, preferentially mutate nose-related morphs
- Pre-compute morph groupings:
```csharp
Dictionary<string, int[]> FeatureMorphGroups = {
    { "Nose", new[] { 20, 21, 22, 23, 24, 25, 26 } },  // nose_* morphs
    { "Jaw", new[] { 5, 6, 7, 8, 9, 10, 11 } },        // jaw/chin morphs
    { "Eyes", new[] { 36, 37, 38, 39, 40, 41 } },      // eye_* morphs
    // etc.
};
```

#### 4. Smarter Initial Guess
**Problem:** Starting from neutral or random wastes iterations

**Solution:**
- Use FaceType classification (already implemented) more aggressively
- Load pre-trained "prototypes" for common face types:
  - Round face + small eyes + wide nose → specific starting morphs
  - Long face + prominent chin → different starting morphs
- Train regression model: target_landmarks → predicted_morphs

#### 5. Gradient Estimation
**Problem:** CMA-ES is gradient-free, doesn't exploit structure

**Solution:**
- Estimate numerical gradients periodically:
```csharp
for each morph i:
    delta[i] = (score(morph+ε) - score(morph-ε)) / (2*ε)
```
- Use gradient to inform mutation direction
- Hybrid: CMA-ES for exploration, gradient descent for refinement

#### 6. Landmark Importance Weighting
**Problem:** All landmarks weighted equally

**Solution:**
- Weight landmarks by distinctiveness:
  - Nose tip, chin point: HIGH weight
  - Jaw contour: HIGH weight  
  - Forehead edges: LOW weight (often occluded by hair)
```csharp
float[] LandmarkWeights = ComputeFromVariance(trainingSet);
```

#### 7. Score Calibration
**Problem:** Scores may not be comparable across different face types

**Solution:**
- Normalize scores by face type baseline
- Track "expected" scores for face type and measure delta
- Adjust tolerances based on target difficulty

---

## Proposed Optimizations (Priority Order)

### Phase 1: Quick Wins (Low Risk)

1. **Reduce RefreshStats frequency** ✅ (already done: every 5 iterations)
2. **Batch morph applications** ✅ (ApplyMorphsBatch already exists)
3. **Increase FEATURE_SCORE_INTERVAL** during exploration
   - Change from 5 → 10 for first 50% of iterations
4. **Skip unnecessary logging in hot paths** ✅ (already reduced)

### Phase 2: Algorithm Improvements (Medium Risk)

5. **Feature-specific mutation weighting**
   - When worst feature is Nose, boost nose morph mutation rates
   - Implement "focused refinement" phase

6. **Better starting point selection**
   - Use face classification to pick from learned prototypes
   - Store top-5 morphs per face type combination

7. **Early termination criteria**
   - Stop if score plateaus at >0.80 for 20 iterations
   - Detect convergence more aggressively

### Phase 3: Major Architecture Changes (Higher Risk)

8. **In-memory capture pipeline**
   - Eliminate file I/O for screenshots
   - Requires engine-level changes

9. **Pre-computed morph-feature correlation**
   - Run offline analysis of morph effects
   - Use for intelligent mutation

10. **Hybrid optimization**
    - CMA-ES + local gradient descent
    - Switch methods based on phase

---

## Metrics to Track

```csharp
// Add these to LearningOrchestrator for analysis
public class LearningMetrics
{
    public float AverageTimePerIteration;
    public float TimeInLandmarkDetection;
    public float TimeInScoring;
    public float TimeInMutation;
    public float TimeInCharacterRefresh;
    
    public int IterationsToScore50;
    public int IterationsToScore70;
    public int IterationsToScore80;
    
    public Dictionary<string, float> FeatureProgressRates;
}
```

---

## Recommended Next Steps

1. **Add timing instrumentation** to identify actual bottlenecks
2. **Implement feature-specific mutation** (highest impact on quality)
3. **Improve starting point selection** using face type classification
4. **Test with diverse dataset** to find common failure modes
5. **Consider multi-resolution approach**: fast coarse search, then fine refinement

---

## Code Snippets for Implementation

### Feature-Specific Mutation Boost
```csharp
private float[] GetMutationRates(Dictionary<string, float> featureScores)
{
    float[] rates = new float[62];
    Array.Fill(rates, 1.0f);
    
    // Boost morphs for worst-scoring features
    foreach (var kv in featureScores.OrderBy(x => x.Value).Take(2))
    {
        if (FeatureMorphGroups.TryGetValue(kv.Key, out var morphs))
        {
            float boost = 1.0f + (0.5f - kv.Value) * 2.0f;  // Up to 2x boost
            foreach (int idx in morphs)
            {
                rates[idx] *= boost;
            }
        }
    }
    
    return rates;
}
```

### Adaptive Tolerance Based on Face Type
```csharp
private float GetAdaptiveTolerance(string faceType, string feature)
{
    // Some face types are harder to match
    float baseTolerance = DefaultTolerances[feature];
    
    if (faceType.Contains("Unusual") || faceType.Contains("Unique"))
    {
        return baseTolerance * 1.3f;  // More forgiving
    }
    
    return baseTolerance;
}
```

---

## Implemented Improvements (v2.0.0)

### 1. Feature-Specific Mutation Boost ✅
**File:** `MorphFeatureMapping.cs` + `LearningOrchestrator.cs`

When a feature (e.g., Face, Jaw) scores low, morphs that affect that feature 
get boosted mutation rates:
- Up to 2.5x boost for underperforming features
- Additional 1.3x for "primary" morphs (most impactful)

```csharp
// In ApplyCmaEsCandidate()
if (featureMultipliers != null && featureMultipliers[i] > 1.0f)
{
    float diff = newValue - _currentMorphs[i];
    newValue = _currentMorphs[i] + diff * featureMultipliers[i];
}
```

### 2. Morph-Feature Groupings ✅
**File:** `MorphFeatureMapping.cs`

62 morphs mapped to 8 feature groups:
- FaceShape: indices 0-8 (face_width, face_depth, face_ratio, etc.)
- Jaw: indices 9-16 (jaw_line, jaw_width, chin_shape, etc.)
- Eyes: indices 17-27
- Eyebrows: indices 28-32
- Nose: indices 33-42
- Mouth: indices 43-52
- Forehead: indices 53-55
- Ears: indices 56-59

---

## Remaining Optimization Opportunities

### High Priority (Quality Impact)

1. **Rebalance Feature Weights**
   - Eyebrows: 0.11 → 0.08 (always too high)
   - FaceShape: 0.17 → 0.22 (often too low)
   - Jaw: 0.15 → 0.18 (often too low)

2. **Adaptive Iteration Budget**
   - Easy faces (quick convergence): 30-40 iterations
   - Hard faces (slow convergence): up to 80 iterations
   - Currently fixed behavior regardless of difficulty

3. **Early Success Detection**
   - If score > 0.80 and stable for 15 iterations, stop early
   - Save time on easy targets

### Medium Priority (Speed Impact)

4. **Phase-Based Feature Scoring**
   - Exploration phase: FEATURE_SCORE_INTERVAL = 10
   - Refinement phase: FEATURE_SCORE_INTERVAL = 3

5. **Skip Comparison Image on Non-Final**
   - Only generate comparison PNG for final result
   - Skip during learning iterations

6. **Landmark Caching**
   - Cache target landmarks (already done)
   - Consider caching intermediate calculations

### Lower Priority (Architecture)

7. **In-Memory Screenshot Transfer**
   - Requires Bannerlord engine changes
   - Would eliminate file I/O bottleneck

8. **Multi-Threaded Scoring**
   - Score calculation could be parallelized
   - Feature scores are independent

---

## Quick Test Recommendations

To validate improvements, run FaceLearner on:
1. 5 easy faces (frontal, clear, typical features)
2. 5 medium faces (slight angle or unusual lighting)
3. 5 hard faces (extreme angles, unusual features)

Track:
- Average iterations to converge
- Final score distribution
- Worst feature per face type
- Time per iteration

---

## Version History

- **v2.0.0**: Feature-specific mutation, gender switch fix, consensus override
- **v1.9.x**: Basic CMA-ES optimization

---

## v2.0.3 Additions

### Weighted Landmark Distance
Distinctive features (nose, jaw) now count more in scoring:
```
Nose landmarks:     1.5x weight
Jaw contour:        1.4x weight
Eyes:               1.2x weight
Mouth:              1.1x weight
Forehead edges:     0.6x weight (often hair-covered)
Eyebrows:           0.8x weight
```

### Feature Morph Index Cache
One-time initialization, O(1) lookup thereafter.
Uses MorphFeatureMapping.FeatureGroups for accurate morph assignments.

### NOT Implemented (Memory Safety)
- ❌ In-memory bitmap caching (GDI+ memory leaks in .NET)
- ❌ Render-to-texture (engine-level changes needed)
- File-based I/O remains for all image operations
