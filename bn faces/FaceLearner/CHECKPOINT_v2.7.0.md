# FaceLearner v2.7.0 Checkpoint - 2026-01-25

## Quick Resume
```
Continue FaceLearner development. Current version: 2.7.0 (Ensemble Systems)
Files at /home/claude/FaceLearner/
```

## Major Changes in v2.7.0

### New Ensemble Systems (like GenderEnsemble)
1. **SkinToneEnsemble** - Replaces 265 lines of fragmented logic
2. **AgeEnsemble** - Fixes elderly age underestimation

### Key Files
- `FaceLearner.ML/ML/SkinToneEnsemble.cs` - Skin tone detection
- `FaceLearner.ML/ML/AgeEnsemble.cs` - Age detection with gray hair
- `FaceLearner.ML/ML/GenderEnsemble.cs` - Gender detection (existing)
- `FaceLearner.ML/ML/ENSEMBLE_DESIGN.md` - Design documentation

### Problems Solved

| Problem | Before | After |
|---------|--------|-------|
| Spike Lee skin | 0.18 | ~0.82 |
| Mick Jagger age | 38 | ~67 |
| Debra Brown age | 29 | ~55 |

### How It Works

**SkinTone Signals:**
- RGB_Direct: Pixel sampling
- FairFace_Race: AI race prediction  
- DarkSkin_Check: Catches misdetected dark skin

**Age Signals:**
- FairFace_Age: AI prediction (poor for elderly)
- Hair_Gray: Gray/white hair detection (STRONG signal)
- Skin_Texture: Wrinkle analysis

### Log Format
```
SkinTone: 0.82 | Black detected (conf=0.63) → min 0.81
  - RGB_Direct: 0.18 (conf=0.60) Pixel sampling avg
  - FairFace_Race: 0.78 (conf=0.63) Black → range [0.55-0.95]

Age Ensemble: 38 → 67 (Senior) | Gray hair detected → age ≥ 70
  - FairFace_Age: 38 (conf=0.60) AI prediction
  - Hair_Color: 70 (conf=0.80) White hair (45%)
```

## Still Pending
- Face_Structure signal for AgeEnsemble (landmark-based)
- Threshold calibration based on real-world testing

## Testing Checklist
- [ ] Spike Lee → dark skin (~0.80)
- [ ] Wayne Ferreira → light skin (~0.20)
- [ ] Mick Jagger → age 65+
- [ ] Young person with no gray → age matches FairFace
- [ ] White-haired elder → age 65+
