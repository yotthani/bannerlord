# FaceLearner v0.26.0 Checkpoint

**Date**: January 2026  
**Status**: Living Learning System - Core Working  
**Total Experiments**: 10,000+ in test runs

---

## 🎯 What Works

### Learning System
- ✅ **Primitives Learning**: All 62 morphs learned automatically
- ✅ **Experiments Accumulating**: 10K+ experiments per session
- ✅ **Methods Discovery**: 42 methods found (morph combinations)
- ✅ **Recipes Storage**: Saves best morphs per target face
- ✅ **Knowledge Persistence**: Saves/loads between sessions

### Hill Climbing Algorithm
- ✅ **Best-First Strategy**: Always explores from best known position
- ✅ **Revert on Decline**: Returns to best when score drops
- ✅ **Adaptive Step Size**: Increases when stuck, decreases on progress
- ✅ **Stuck Counter**: Tracks consecutive non-improvements toward BEST
- ✅ **Escape Jumps**: Big random jumps when stuck >60 iterations

### UI & Integration
- ✅ **Camera Controls**: Zoom 0.82, Height -3.52 (good for full face)
- ✅ **Resume Learning**: Continues from previous best on same target
- ✅ **Real-time Logging**: Shows progress, stuck count, experiments

---

## 📊 Key Metrics (Typical Run)

```
Target 1: 500 iterations
  Best Score: 0.517
  Peaks Found: 13
  Revert Ratio: 55%
  Experiments: 8,000+

Target 2: 100 iterations  
  Best Score: 0.512
  Starting from similar recipe
```

---

## 🔧 Critical Learnings & Fixes

### 1. Learning Flow Bug (v0.25.9)
**Problem**: `Changes: 0` despite mutations happening  
**Root Cause**: `previousMorphs` was copied BEFORE render, so comparison showed no change  
**Fix**: Copy morphs AFTER learning, BEFORE next mutation

```
WRONG: Iterate() → copy → apply → Render() → learn(same,same)
RIGHT: Iterate() → apply → Render() → learn(old,new) → copy → mutate
```

### 2. Stuck Counter Reset Bug (v0.26.0)
**Problem**: Stuck counter reset on any improvement over previous score  
**Root Cause**: `0.512 > 0.510` counts as "improvement" even though best is 0.517  
**Fix**: Only reset when actually beating or matching the BEST score

```csharp
// OLD (wrong)
if (currentScore > previousScore) stuck = 0;

// NEW (correct)  
if (currentScore >= bestScore) stuck = 0;
```

### 3. Phase Bouncing (v0.26.0)
**Problem**: Phases cycling every few iterations  
**Root Cause**: SessionMonitor declared "stagnant" after only 50 iterations  
**Fix**: 
- Stagnation window: 50 → 100 iterations
- Stagnation threshold: 0.002 → 0.0005
- Phase change only after 200+ iterations

### 4. Minimum Step Size (v0.25.6)
**Problem**: Step size shrunk so small that changes were undetectable  
**Fix**: Enforce `MIN_STEP = 0.02f` (2% minimum change)

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     FaceLearnerVM                           │
│  (UI bindings, commands, status display)                    │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                 LearningOrchestrator                        │
│  - Iterate() → apply morphs to face                         │
│  - OnRenderCaptured() → score, learn, mutate                │
│  - GenerateNextMutation() → best-first hill climbing        │
└──────┬──────────────┬──────────────┬───────────────────────┘
       │              │              │
┌──────▼──────┐ ┌─────▼─────┐ ┌──────▼──────┐
│ KnowledgeTree│ │PhaseManager│ │SessionMonitor│
│ - Primitives │ │ - 6 phases │ │ - Trends     │
│ - Methods    │ │ - Evolution│ │ - Stagnation │
│ - Recipes    │ │            │ │ - Volatility │
└─────────────┘ └────────────┘ └──────────────┘
```

---

## 📁 Data Files

| File | Purpose | Location |
|------|---------|----------|
| `knowledge_tree.dat` | Primitives, Methods, Recipes | `Data/` |
| `orchestrator_memory.dat` | Step size history, success rates | `Data/` |
| `phase_manager.dat` | Phase configurations, evolution | `Data/` |

---

## 🎮 Usage

### Basic Flow
1. **Initialize** - Sets up face API, camera, landmark detector
2. **Start Learning** - Begins iterating through training images
3. **Stop** - Can resume later with same target

### Camera Defaults
```
Zoom: 0.82
Height: -3.52
Horizontal: 0
```

### Keyboard/UI
- Camera controls: ▲▼◄► buttons, zoom +/-
- Reset camera: ⟲ button

---

## 🚧 Known Issues / TODO

### Not Yet Working
- [ ] Phase Evolution (always shows `Evolved:0`)
- [ ] LFW dataset download (file exists error)
- [ ] CelebA dataset support

### Could Be Improved
- [ ] Score plateaus around 0.50-0.52 for most faces
- [ ] Tree suggestions may not be optimal
- [ ] No visual feedback of target vs current

### Future Ideas
- [ ] Side-by-side target/current preview
- [ ] Manual morph adjustment mode
- [ ] Export learned knowledge to JSON
- [ ] Multi-face batch processing
- [ ] **Animation-Sync**: Statt per-frame Frontal-Check → Loop-Timing messen und deterministisch capturen

---

## 📈 Version History

| Version | Key Changes |
|---------|-------------|
| v0.17.0 | Initial Living Learning System |
| v0.25.0 | Phase system, session monitor |
| v0.25.5 | Learning debug, min step size |
| v0.25.7 | Best score preservation on restart |
| v0.25.8 | Camera defaults 0.82/-3.52 |
| v0.25.9 | **Fixed learning flow bug** |
| v0.26.0 | **Fixed stuck counter, phase bouncing** |

---

## 💡 Key Insights

1. **Hill Climbing works** - Simple best-first strategy with reverts is effective
2. **Learning needs visible changes** - Minimum step size is critical
3. **Stuck detection is tricky** - Must compare to BEST, not just previous
4. **Phase systems add complexity** - Simple adaptive step size may be enough
5. **10K+ experiments** - System learns morph effects quickly

---

*Last updated: v0.26.0*
