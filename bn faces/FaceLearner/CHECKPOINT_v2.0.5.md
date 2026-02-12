# FaceLearner v2.0.5 Checkpoint

## Quick Resume
```
Continue my FaceLearner Bannerlord mod. Files at `/home/claude/FaceLearner/`. 
Current version: **2.0.5**.
```

---

## Changes Since v1.7.0

### v2.0.5 - Activity Log Cleanup
- Removed iteration spam from Activity Log (was every 10 iterations)

### v2.0.4 - Gender Detection Fix
- Trust FairFace when confidence ≥0.85 (fixes men→female bug)

### v2.0.3 - Cleanup
- Removed spammy logs, cached lookups, pre-allocated lists

### v2.0.2 - Exploitation Mode
- Multi-feature revert (all features >5% drop)
- σ reduced 50% for 8 iterations after NEW BEST

### v2.0.1 - Performance
- MorphFeatureMapping.cs (62 morphs → 8 features)
- Rebalanced feature weights
- Early success detection

### v2.0.0 - Architecture
- Per-feature best tracking
- Blended 50/50 scoring

---

## Key Files

| File | Purpose |
|------|---------|
| `SubModule.cs` | VERSION = "2.0.5" |
| `CHANGELOG.md` | All version history |
| `MorphFeatureMapping.cs` | Morph→Feature mapping |
| `GenderEnsemble.cs` | Gender detection (fixed) |
| `LearningOrchestrator.cs` | Main learning loop |
| `ScoringModule.cs` | Feature weights |

---

## Test Checklist
- [ ] Men detected as Male (not Female)
- [ ] Exploitation mode triggers after NEW BEST
- [ ] Log spam reduced
- [ ] Feature highs preserved during learning
