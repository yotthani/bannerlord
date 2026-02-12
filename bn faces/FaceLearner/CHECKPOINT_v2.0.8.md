# FaceLearner v2.0.8 Checkpoint

## Quick Resume
```
Continue my FaceLearner Bannerlord mod. Files at `/home/claude/FaceLearner/`. 
Current version: **2.0.8**.
```

---

## Changes Since v1.7.0

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
| `SubModule.cs` | VERSION = "2.0.8" |
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
