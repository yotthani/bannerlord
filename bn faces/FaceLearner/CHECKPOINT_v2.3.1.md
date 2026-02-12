# FaceLearner v2.3.1 Checkpoint

## Quick Resume
```
Continue my FaceLearner Bannerlord mod. Files at `/home/claude/FaceLearner/`. 
Current version: **2.3.1**.
```

---

## Changes Since v1.7.0

### v2.3.1 - JawShape Classification Fixed
- PROBLEM: JawShape always "Pointed" (ratios[17] = chin tip, too small)
- FIX: Use pointedness ratio = jawTaper / chinWidthCombined
- Thresholds: >1.5=Pointed, <0.8=Square, else=Round

### v2.3.0 - Smile Detection as Learnable Feature
- NEW: SmileLevel feature (None, Smile, BigSmile)
- Detection from landmarks: mouth aspect, corner lift, openness
- HierarchicalTree learns how to handle smiles
- NO weight adjustment - pure learning approach
- Log shows smile warning when detected

### v2.2.1 - Latino Skin Tone Fix
- Very confident threshold: 0.90 → 0.85
- Latino override → 0.40 (olive tone)
- Latino nudge when too light → 0.35

### v2.2.0 - Scoring Tolerances Rebalanced
- ROOT CAUSE: Tolerances too tight for Bannerlord's limited face range
- FaceShape WidthHeightRatio: 0.08 → 0.15
- Jaw tolerances all doubled (0.04→0.08, etc.)
- Removed hardcoded fallback morphs - pure learning
- Expected: Round face scores 0.43 → 0.55-0.65

### v2.1.3 - Round Face Starting Morphs [REVERTED]
- JawShape no longer always "Pointed" - uses chinWidth ratio
- White skin: RGB>0.18 capped to 0.15 (light tan, not Mediterranean)
- David Beckham, celeba_140243 should look better

### v2.1.1 - Round Face & Asian Skin Tone Fixes
- FaceWidth thresholds: >1.10=Narrow, <0.95=Wide (was 1.15/1.0)
- East Asian skin: High conf → 0.225, nudge dark tones to 0.22
- Round faces now properly detected as "Wide"

### v2.1.0 - Fixed Long Hair Male Override False Positives
- Women (Nicole Kidman, athletes) were wrongly detected as Male
- Made override MUCH more conservative:
  - FairFace confidence < 0.97 required
  - Landmarks must lean Male or be uncertain
  - Stricter thresholds (-0.15/-0.2 instead of 0.0/-0.1)
  - Require 3+ features OR strong evidence

### v2.0.9 - Sub-Feature Hints System
- New: Detailed sub-feature analysis (ratio, width, taper, etc.)
- New: SubFeatureHints.cs with quality ratings (Bad/Poor/Okay/Good/Great)
- Logging shows what specifically is wrong: "Face:0.25 (ratio:bad, angle:bad)"
- New API: GetDetailedHints(), GetMutationPriorities()

### v2.0.8 - Iteration Limits Rebalanced
- MIN_ITERATIONS: 15 → 60 (more time for focus phase)
- Score-based limits more generous (80/100/120/150 instead of 40/60/80/120)
- Bad scores (<0.35) still abort early

### v2.0.7 - Long Hair Male Detection
- Detects men with long hair that trick FairFace
- Checks landmarks for male facial features (jaw, brow, nose)
- Overrides FairFace if 2+ male features found

### v2.0.6 - Face Classification & Age Fix
- Fixed FaceWidth/JawShape thresholds (was too strict → always "Medium")
- Age display now shows detected age, not character age

### v2.0.5 - Activity Log Cleanup
- Removed iteration spam from Activity Log

### v2.0.4 - Gender Detection Fix
- Trust FairFace when confidence ≥0.85 (fixes men→female bug)

### v2.0.3 - Cleanup
- Removed spammy logs, cached lookups, pre-allocated lists

### v2.0.2 - Exploitation Mode
- Multi-feature revert, σ reduced after NEW BEST

### v2.0.1 - Performance
- MorphFeatureMapping.cs, rebalanced feature weights

### v2.0.0 - Architecture
- Per-feature best tracking, blended 50/50 scoring

---

## Key Files

| File | Purpose |
|------|---------|
| `SubModule.cs` | VERSION = "2.3.1" |
| `CHANGELOG.md` | All version history |
| `HierarchicalKnowledge.cs` | Face classification thresholds |
| `GenderEnsemble.cs` | Gender detection |
| `LearningOrchestrator.cs` | Main learning loop, age display |

---

## Test Checklist
- [ ] FaceWidth now varies (Wide/Medium/Narrow)
- [ ] JawShape now varies (Square/Round/Pointed)
- [ ] Age shown matches detected age from photo
- [ ] Men detected as Male
