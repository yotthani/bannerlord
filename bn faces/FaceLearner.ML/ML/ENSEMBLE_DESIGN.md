# Demographics Ensemble System Design

## Overview

Three ensemble systems for robust demographic detection:
1. **GenderEnsemble** (already exists) - Male/Female detection
2. **SkinToneEnsemble** (new) - Skin color 0.0-1.0
3. **AgeEnsemble** (new) - Age estimation with elder correction

All follow the same pattern: Multiple signals → Weighted voting → Conflict resolution → Final value

---

## Common Structure

```csharp
public struct Signal
{
    public string Source;      // e.g., "FairFace", "RGB_Direct", "HairGray"
    public float Value;        // The estimated value
    public float Confidence;   // 0-1, how confident is this signal?
    public float Weight;       // Base weight for voting
    public string Reasoning;   // Human-readable explanation
}

public struct EnsembleResult
{
    public float Value;           // Final computed value
    public float Confidence;      // Overall confidence
    public List<Signal> Signals;  // All signals for debugging
    public string Decision;       // Why this value was chosen
}
```

---

## SkinToneEnsemble

### Signal Sources

#### 1. RGB_Direct (Weight: 1.0)
- Sample skin pixels from face landmarks
- Direct mapping: avgRGB → skinTone
  - 200+ → 0.05-0.10 (very pale)
  - 180  → 0.12-0.18 (pale)
  - 150  → 0.20-0.30 (light)
  - 120  → 0.35-0.45 (medium)
  - 90   → 0.50-0.60 (tan)
  - 60   → 0.65-0.80 (dark)
  - 30   → 0.85-0.95 (very dark)
- NO "underexposed" heuristics!
- Confidence: Based on color variance (consistent = high conf)

#### 2. FairFace_Race (Weight: 0.8)
- Get race from FairFace model
- Map to expected range:
  - White: 0.08-0.30
  - Black: 0.55-0.95
  - East Asian: 0.12-0.35
  - Southeast Asian: 0.25-0.55
  - Indian: 0.35-0.70
  - Latino: 0.20-0.55
  - Middle Eastern: 0.20-0.50
- Value = middle of range
- Confidence: From FairFace model

#### 3. RGB_Undertone (Weight: 0.3)
- Analyze R/G/B ratios
- Warm undertone (R>G>B high ratio) → suggests warmer skin
- Cool undertone → suggests cooler/paler skin
- Very low weight - just a hint

#### 4. Hair_Eye_Correlation (Weight: 0.2)
- Dark hair + dark eyes → hint toward darker skin
- Light hair + light eyes → hint toward lighter skin
- Very low weight - weak correlation

### Conflict Resolution

```
Priority order (highest to lowest):

1. IF FairFace says "Black" with conf >= 0.50:
   → Minimum = 0.55 (never lighter for confident Black detection)
   → Final = MAX(RGB_Direct, 0.55 + conf * 0.30)

2. IF FairFace says "White" with conf >= 0.75:
   → Maximum = 0.35 (never darker for confident White detection)
   → Final = MIN(weighted_avg, 0.35)

3. IF FairFace says "East Asian" with conf >= 0.70:
   → Clamp to range [0.12, 0.40]

4. IF all signals agree (max deviation < 0.15):
   → Final = weighted average
   → High confidence

5. ELSE (conflict or low confidence):
   → Trust RGB_Direct as primary
   → Small nudge toward FairFace range if conf > 0.60
```

---

## AgeEnsemble

### The Problem

AI models systematically underestimate age for older people:
- Training data bias toward younger faces
- Mick Jagger: Estimated 38, Actual ~70
- Debra Brown: Estimated 29, Actual ~55

### Signal Sources

#### 1. FairFace_Age (Weight: 0.6 for young, 0.3 for old)
- Raw age prediction from FairFace
- Good accuracy for 20-45
- Systematically underestimates 50+
- Weight decreases when other signals suggest older

#### 2. Hair_Gray (Weight: 0.8)
- Detect gray/white hair percentage
- Very strong signal for age!
- Mapping:
  - 0% gray → no age adjustment
  - 10-30% gray → suggests 45-55
  - 30-60% gray → suggests 55-70
  - 60%+ gray/white → suggests 65+
- Confidence: Based on hair visibility

#### 3. Skin_Texture (Weight: 0.5)
- Analyze wrinkle patterns from landmarks
- Forehead lines, crow's feet, nasolabial folds
- Smooth skin → younger
- Visible texture/lines → older
- Confidence: Based on image quality

#### 4. Face_Sag (Weight: 0.4)
- Detect sagging from landmarks
- Jowls, drooping cheeks
- Lower face wider than expected → older
- Confidence: Based on landmark reliability

### Conflict Resolution

```
Priority order:

1. IF Hair_Gray shows significant gray (>30%):
   → This is a STRONG signal - override FairFace
   → Minimum age = 50 + (gray_percent - 30) * 0.5
   → e.g., 60% gray → minimum 65

2. IF Hair_Gray shows white/silver hair (>70%):
   → Minimum age = 65
   → Typical range: 65-80

3. IF Face_Sag detected AND Skin_Texture shows wrinkles:
   → Add 10-20 years to FairFace estimate
   → These are hard to fake

4. IF FairFace says < 40 BUT hair/skin suggest older:
   → This is the common failure mode!
   → Trust hair/skin signals over FairFace
   → Final = MAX(FairFace + 15, hair_suggested_age)

5. IF all signals agree (young face, no gray, smooth skin):
   → Trust FairFace
   → Final = FairFace age

6. CLAMP final to reasonable range:
   → Minimum: 18 (Bannerlord adult)
   → Maximum: 70 (Bannerlord visual limit)
```

### Gray Hair Detection Algorithm

```csharp
float DetectGrayHairPercent(Bitmap img, float[] landmarks)
{
    // Sample hair region (above forehead)
    var hairPixels = SampleHairRegion(img, landmarks);
    
    int grayCount = 0;
    foreach (var pixel in hairPixels)
    {
        // Gray/white: R≈G≈B with high brightness
        float maxDiff = Max(Abs(R-G), Abs(G-B), Abs(R-B));
        float brightness = (R + G + B) / 3f;
        
        // Gray: low saturation (maxDiff < 30) + medium-high brightness (> 120)
        // White: very low saturation + very high brightness (> 200)
        if (maxDiff < 30 && brightness > 120)
            grayCount++;
    }
    
    return (float)grayCount / hairPixels.Count;
}
```

---

## Integration

Replace scattered code in LearningOrchestrator with clean calls:

```csharp
// OLD (scattered, 50+ lines of overrides)
float targetSkinTone = _skinToneDetector.DetectSkinTone(imagePath);
if (ffRace == "Black" && ffRaceConf >= 0.85f) { ... }
else if (ffRace == "Latino" && ...) { ... }
// ... 20 more branches

// NEW (clean, single call)
var skinResult = _skinToneEnsemble.Detect(imagePath, landmarks, fairFaceResult);
_faceController.SkinColor = skinResult.Value;
SubModule.Log($"SkinTone: {skinResult.Value:F2} | {skinResult.Decision}");
foreach (var s in skinResult.Signals)
    SubModule.Log($"  - {s.Source}: {s.Value:F2} (conf={s.Confidence:F2})");
```

Same for age:

```csharp
// OLD
float age = _demographicDetector.DetectAge(imagePath);
// No correction for elderly...

// NEW
var ageResult = _ageEnsemble.Detect(imagePath, landmarks, fairFaceResult);
targetAge = ageResult.Value;
SubModule.Log($"Age: {ageResult.Value:F0} | {ageResult.Decision}");
```

---

## Testing Checklist

### SkinTone
- [ ] Spike Lee (Black, dark skin) → Should be 0.70+
- [ ] Wayne Ferreira (White, tanned) → Should be 0.15-0.25
- [ ] Generic White person → Should be 0.10-0.20
- [ ] East Asian → Should be 0.15-0.30
- [ ] Latino → Should be 0.25-0.45
- [ ] Dark photo of White person → Should still be light

### Age
- [ ] Mick Jagger (gray hair, wrinkles) → Should be 60+
- [ ] Debra Brown (blonde/gray, older) → Should be 55+
- [ ] Young person, no gray → Should match FairFace
- [ ] Middle-aged, some gray → Should be 45-55
- [ ] White-haired elder → Should be 65+
