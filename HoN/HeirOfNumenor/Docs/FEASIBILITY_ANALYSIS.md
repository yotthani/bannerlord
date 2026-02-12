# Standalone Apps - Feasibility Analysis & Refinements

## Analysis Based on Project Knowledge

---

## App 1: Bannerlord UI Designer Tool

### Doability Assessment: **70% Success Rate**

#### ✅ What We Know Works
From BANNERLORD_KNOWLEDGE_BASE.md:
- Gauntlet XML structure is well-documented
- Widget types, properties, bindings are known
- VisualDefinitions for animations
- Brush system, sprite loading
- Screen/Layer lifecycle

#### ⚠️ Challenges

| Challenge | Severity | Mitigation |
|-----------|----------|------------|
| **Gauntlet not open source** | HIGH | Must use game DLLs or approximate |
| **No standalone renderer** | HIGH | Need in-game mod for accurate preview |
| **Sprite system proprietary** | MEDIUM | Can show placeholders, real sprites need game |
| **DataSource bindings** | MEDIUM | Mock data system needed |
| **Scene rendering** | HIGH | Requires full game engine |

#### 🔴 Critical Risks

1. **Accurate Preview Impossible Without Game**
   - Gauntlet renderer is tightly coupled to TaleWorlds.Engine
   - Fonts, brushes, sprites all need game resources
   - WPF approximation will have ~60-70% visual accuracy

2. **Hot-Reload Stability**
   - Game may crash on malformed XML
   - Memory leaks from repeated movie loading
   - Need robust error handling in mod

3. **Version Fragility**
   - Gauntlet internals change between versions
   - Widget properties added/removed
   - Need version detection

#### ✅ Refined Approach

**Option A: Hybrid (Recommended)**
```
┌─────────────────────────────────────────────┐
│  WPF Designer (Primary workspace)           │
│  - Fast XML editing with autocomplete       │
│  - Approximate WPF preview (instant)        │
│  - Property inspector                       │
│  - Widget palette                           │
└────────────────┬────────────────────────────┘
                 │ File save triggers
                 ▼
┌─────────────────────────────────────────────┐
│  In-Game Preview Mod (Accuracy check)       │
│  - FileSystemWatcher on XML folder          │
│  - Auto-reload on file change               │
│  - Shows REAL Gauntlet rendering            │
│  - Error reporting back to Designer         │
└─────────────────────────────────────────────┘
```

**Why This Works:**
- Fast iteration in WPF (no game restart)
- Accurate verification in-game
- Decoupled - can use either independently
- No IPC complexity needed (just file watching)

**Option B: Pure In-Game (Simpler but slower)**
- All editing inside game main menu
- Use existing TextWidget for code display
- Slower iteration but 100% accurate

#### 📊 Revised Success Estimate

| Component | Feasibility | Notes |
|-----------|-------------|-------|
| XML Editor | 95% | AvalonEdit is proven |
| WPF Approximate Preview | 75% | Some widgets hard to map |
| Property Grid | 90% | Standard tooling |
| In-Game Hot Reload | 80% | Stability concerns |
| Scene Backgrounds | 30% | Requires engine access |
| Full Accuracy | 60% | Only via game mod |

**Overall: 70%** - Useful tool, but expectations must be set that WPF preview is approximate.

---

## App 2: Balance Simulator & Analyzer

### Doability Assessment: **55% Success Rate**

#### ✅ What We Know

From our project code:
- `Agent.GetBaseArmorEffectivenessForBodyPart` - we patch this (LayeredArmorPatches.cs)
- `TroopStatusManager` - troop stat handling
- Equipment system access
- Mission/Agent lifecycle

From knowledge base:
- Combat happens in Mission (TaleWorlds.MountAndBlade)
- Agent = combat unit
- Blow = damage event

#### 🔴 Critical Problems

**1. Combat Formulas Are INTERNAL**
```csharp
// These are PRIVATE/INTERNAL in TaleWorlds.MountAndBlade:
- MissionCombatMechanicsHelper.ComputeRawDamage() // internal
- CombatStatCalculator // internal class
- Agent.HandleBlow() // complex private logic
```

**Reflection Risk:** Even with reflection:
- Obfuscation may hide method names
- IL2CPP builds break reflection
- Anti-cheat could flag it

**2. Combat Is Not Deterministic**
```csharp
// Game uses:
- Random hit zones
- Random stagger/interrupt
- AI decision randomness
- Physics engine interactions
- Animation timing
```

Our simulation would need to replicate ALL of this or accept inaccuracy.

**3. Parameter Extraction Difficult**
Many values are:
- Hardcoded in compiled IL
- Spread across multiple classes
- Context-dependent (perks, traits, items)

#### ⚠️ Honest Assessment

| What We Want | Reality |
|--------------|---------|
| Use game's exact formulas | Only via running game + capturing |
| Simulate 100k battles fast | Need simplified model OR game automation |
| Auto-balance suggestions | Need massive validated dataset first |
| AI learning | Needs consistent, reproducible data |

#### ✅ Refined Approach

**Two-Tier Architecture:**

```
┌─────────────────────────────────────────────────────────────┐
│  TIER 1: Data Capture Mod (IN-GAME)                         │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ Hooks into ACTUAL combat events:                       │  │
│  │ - OnAgentHit (real damage dealt)                       │  │
│  │ - OnAgentRemoved (death)                               │  │
│  │ - Combat context capture (weapons, armor, skills)      │  │
│  │                                                        │  │
│  │ Exports: CombatEvent[] to JSON/SQLite                  │  │
│  │ Can run automated test battles via console commands    │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ Real combat data
┌─────────────────────────────────────────────────────────────┐
│  TIER 2: Analysis App (STANDALONE)                          │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ - Import combat data from game                         │  │
│  │ - Statistical analysis (no simulation)                 │  │
│  │ - Pattern detection                                    │  │
│  │ - Claude AI for insights                               │  │
│  │ - Recommendation generation                            │  │
│  │ - Export balance patches                               │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Why This Is Better:**

1. **No Formula Replication** - We capture RESULTS, not formulas
2. **Always Accurate** - Data comes from real game
3. **Game Updates Don't Break It** - We just capture new data
4. **Smaller Scope** - Analysis tool, not full simulator

#### 📊 Revised Components

| Component | Feasibility | Notes |
|-----------|-------------|-------|
| Combat Capture Mod | 90% | OnAgentHit is public |
| Automated Battle Runner | 70% | Console commands exist |
| SQLite Database | 95% | Standard |
| Statistical Analysis | 90% | Standard math |
| Claude AI Integration | 85% | API is straightforward |
| Full Combat Simulator | 30% | Too many hidden variables |
| Auto-Apply Balance | 60% | XML editing is doable |

**Revised Success Rate: 55%** for original vision, **80%** for data-driven analysis approach.

---

## Recommendations

### App 1: UI Designer - GO AHEAD (Modified)

**Rename to:** "Bannerlord UI Workshop"

**Scope Reduction:**
1. ❌ Drop scene background feature (too complex)
2. ✅ Focus on XML editing + WPF preview + hot-reload mod
3. ✅ Add widget/brush/sprite browser
4. ⚠️ Set expectation: WPF preview = 70% accurate

**MVP Features:**
- XML editor with Gauntlet autocomplete
- Widget tree
- Property inspector
- WPF approximate preview
- File watcher mod for hot-reload

**Phase 2:**
- In-game mini-editor
- Template gallery
- Brush editor

---

### App 2: Balance Simulator - PIVOT

**Rename to:** "Bannerlord Combat Analyzer"

**Changed Approach:**
1. ❌ Drop standalone simulation (unreliable)
2. ✅ Focus on data capture from real game
3. ✅ Statistical analysis of captured data
4. ✅ AI-powered insights
5. ✅ Balance recommendation export

**MVP Features:**
- Combat capture mod (OnAgentHit hooks)
- Automated battle runner (spawn armies, fight, log)
- Data import to SQLite
- Basic statistics dashboard
- Export recommendations as mod patch

**Phase 2:**
- Claude AI analysis integration
- Historical comparison
- Community data sharing

---

## Final Feasibility Summary

| App | Original Vision | Modified Vision | Recommendation |
|-----|-----------------|-----------------|----------------|
| UI Designer | 70% | 85% | ✅ Proceed with modifications |
| Balance Simulator | 55% | 80% | ✅ Proceed as "Analyzer" |

### Key Insight

**Both apps benefit from embracing the mod requirement rather than fighting it.**

The game IS the source of truth. Our apps should:
- Capture data FROM the game
- Provide better tools AROUND the game
- NOT try to replicate the game

This pivot makes both apps:
- More reliable (no formula drift)
- More maintainable (less code to break)
- More useful (real data, not approximations)
