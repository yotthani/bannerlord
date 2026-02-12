# New Features Plan

## Overview
Five new features planned for HeirOfNumenor mod:

1. **Siege Auto-Dismount** - Smart horse handling for sieges
2. **Layered Armor System** - Multi-layer armor simulation
3. **Mixed Formation Layouts** - Custom inner formation positioning
4. **Battle Action Bar** - Manual context-bound troop orders
5. **Smart Cavalry AI** - Improved mounted unit behavior

---

## Feature 1: Siege Auto-Dismount

### Problem
When entering battle while mounted, player spawns on horse. This is fine for field battles but makes no sense for sieges where horses can't be used effectively.

### Solution
MCM-configurable behavior for siege battles:

### MCM Settings
```
SiegeMountBehavior (enum):
  - Vanilla (no change)
  - AutoDismountKeepOnMap (dismount, horse stays in battle as entity)
  - AutoDismountToInventory (dismount, horse packed away - won't spawn)
  - AutoRemountAfterSiege (restore mount state after siege ends)
```

### Implementation Approach
1. **Patch `MissionAgentSpawnLogic`** or `Mission.SpawnAgent` to intercept player spawn
2. **Detect siege mission type** via `Mission.Mode` or `Mission.CombatType`
3. **Before spawn**: Check if mounted, apply behavior setting
4. **After battle**: If AutoRemount, restore previous mount state

### Key Classes
- `SiegeDismountBehavior.cs` - Main campaign behavior
- `SiegeDismountPatches.cs` - Harmony patches for spawn logic

### Hooks
- `OnMissionStarted` - Detect siege, store mount state
- `OnPlayerSpawn` - Apply dismount behavior
- `OnMissionEnded` - Restore mount if configured

---

## Feature 2: Layered Armor System

### Problem
Realistic armor would have multiple layers (gambeson → chainmail → plate), but game only uses single equipment slot.

### Solution
Use hidden equipment slots (Battle/Civilian/Roguery presets exist in vanilla) to store armor layers, then calculate combined protection.

### Concept
```
Visual Layer (what player sees in inventory):
  - Plate Armor (shown)
  
Hidden Layers (stored in alternate equipment sets):
  - Layer 1: Gambeson (padding) - stored in Civilian slot
  - Layer 2: Chainmail - stored in Roguery slot
  - Layer 3: Plate (actual) - stored in Battle slot
  
Combined Calculation:
  - ArmorValue = Layer1 * 0.3 + Layer2 * 0.5 + Layer3 * 1.0
  - Or: Use highest + % bonus from under-layers
```

### MCM Settings
```
EnableLayeredArmor: bool
LayerCalculationMode: enum (Additive, HighestPlusBonus, Weighted)
UnderArmorBonusPercent: float (0.1-0.5)
```

### Implementation Approach
1. **Custom Equipment UI** - Show layer slots in inventory
2. **Store layers** in hidden equipment indices (game has 0-4: Battle, Civilian, etc.)
3. **Patch `Agent.GetBaseArmorEffectivenessForBodyPart`** to calculate combined armor
4. **On battle start** - Apply combined armor stats to agent

### Key Classes
- `LayeredArmorManager.cs` - Manages layer storage/retrieval
- `LayeredArmorCalculator.cs` - Calculates combined armor values
- `LayeredArmorPatches.cs` - Patches for armor calculation
- `LayeredArmorVM.cs` - UI for layer management

### Challenges
- Need to ensure vanilla equipment logic doesn't break
- Save/load layer configuration
- Visual representation in UI

---

## Feature 3: Mixed Formation Layouts

### Problem
Mixed formations (infantry + ranged) don't allow control over internal arrangement (who's in front vs back).

### Solution
Add formation layout presets that control internal unit positioning.

### Layout Options
```
MixedFormationLayout (enum):
  - Default (vanilla behavior)
  - RangedFrontInfantryBack (ranged as screen, infantry behind)
  - InfantryFrontRangedBack (traditional - melee shields ranged)
  - RangedWingsInfantryCenter (ranged on flanks)
  - Checkerboard (alternating rows)
  - Custom (user-defined ratio)
```

### MCM Settings
```
DefaultMixedLayout: enum
RangedRowDepth: int (1-3) - how many rows of ranged
InfantryRowDepth: int (1-5) - how many rows of infantry
EnableFormationLayoutUI: bool
```

### Implementation Approach
1. **Patch `Formation.ArrangementOrder`** or position calculation
2. **Override unit positioning** within formation based on layout
3. **Sort agents by type** (ranged/melee) before positioning
4. **UI widget** to select layout during battle

### Key Classes
- `MixedFormationLayoutBehavior.cs` - Mission behavior
- `FormationLayoutPatches.cs` - Harmony patches
- `FormationLayoutVM.cs` - UI for layout selection

### Hooks
- Formation arrangement recalculation
- Agent position assignment within formation

---

## Feature 4: Battle Action Bar

### Problem
AI can use contextual orders (hold fire, etc.) but player lacks quick access to specialized unit orders like pike anti-cavalry stance.

### Solution
Add action bar UI during battle with context-sensitive orders based on unit types.

### Action Types
```
For Ranged Units:
  - Hold Fire / Free Fire (existing)
  - Volley Fire (coordinated)
  - Skirmish Mode (fire and retreat)

For Pike/Spear Units:
  - Anti-Cavalry Stance (brace for charge) - LOCKED until cancelled
  - Pike Wall (dense formation)
  - Normal Stance

For Shield Units:
  - Shield Wall (existing)
  - Testudo (if applicable)
  - Loose Formation

For Cavalry:
  - Charge Mode (stay in line, full charge)
  - Skirmish Mode (hit and run)
  - Disengage (pull back)
```

### MCM Settings
```
EnableBattleActionBar: bool
ActionBarPosition: enum (Top, Bottom, Left, Right)
ShowActionBarHotkeys: bool
AutoCancelStanceOnMove: bool
```

### Implementation Approach
1. **Create UI widget** - Horizontal/vertical bar with action buttons
2. **Detect selected formation's unit types** to show relevant actions
3. **Apply special behaviors** via Formation.ArrangementOrder or custom AI
4. **Track stance state** per formation
5. **Lock stance** until explicitly cancelled or auto-cancel triggers

### Key Classes
- `BattleActionBarVM.cs` - ViewModel for action bar
- `BattleActionBarMissionView.cs` - Mission view component
- `TroopStanceManager.cs` - Manages active stances
- `SpecialOrderBehavior.cs` - Implements custom orders

### UI Layout
```
┌────────────────────────────────────────┐
│ [Pike Wall] [Anti-Cav] [Hold Fire] ... │
└────────────────────────────────────────┘
```

---

## Feature 5: Smart Cavalry AI

### Problem
Cavalry units cluster up instead of staying in formation during charge, get stuck on friendly units, and don't properly navigate around shield walls.

### Solution
Improve cavalry charge behavior with better formation cohesion and pathfinding.

### Improvements
```
1. Line Charge:
   - Maintain formation line during charge
   - Synchronized charge timing
   - Don't break formation until contact
   
2. Collision Avoidance:
   - Detect friendly units in path
   - Route around shield walls
   - Avoid clustering at destination
   
3. Charge Coordination:
   - Wait for line to form before charging
   - Re-form after pass-through
   - Disengage and reform behavior
```

### MCM Settings
```
EnableSmartCavalryAI: bool
ChargeFormationStrictness: float (0.0-1.0)
MinReformDistanceAfterCharge: float
EnableFriendlyCollisionAvoidance: bool
ChargeLineSpacing: float
```

### Implementation Approach
1. **Patch cavalry agent AI** (TacticComponent or AgentAI)
2. **Override charge behavior** when charge order given
3. **Add formation cohesion check** before/during charge
4. **Implement pathfinding enhancement** for friendly obstacle avoidance
5. **Add reformation logic** after charge completes

### Key Classes
- `SmartCavalryAIBehavior.cs` - Mission behavior
- `CavalryChargeController.cs` - Manages charge coordination
- `CavalryPathfinding.cs` - Enhanced pathfinding
- `SmartCavalryPatches.cs` - Harmony patches

### Challenges
- Native AI is complex, patches may conflict
- Performance impact of enhanced pathfinding
- Need to detect "charge" order vs normal movement

---

## Implementation Priority

### Phase 1 (Simpler, less invasive)
1. **Siege Auto-Dismount** - Straightforward spawn patch
2. **Battle Action Bar** - UI addition, uses existing order system

### Phase 2 (Medium complexity)
3. **Mixed Formation Layouts** - Formation patches
4. **Layered Armor** - Equipment system extension

### Phase 3 (Complex, AI changes)
5. **Smart Cavalry AI** - Deep AI modifications

---

## File Structure
```
Features/
├── SiegeDismount/
│   ├── SiegeDismountBehavior.cs
│   └── SiegeDismountPatches.cs
├── LayeredArmor/
│   ├── LayeredArmorManager.cs
│   ├── LayeredArmorCalculator.cs
│   ├── LayeredArmorPatches.cs
│   └── LayeredArmorVM.cs
├── MixedFormations/
│   ├── MixedFormationLayoutBehavior.cs
│   ├── FormationLayoutPatches.cs
│   └── FormationLayoutVM.cs
├── BattleActionBar/
│   ├── BattleActionBarVM.cs
│   ├── BattleActionBarMissionView.cs
│   ├── TroopStanceManager.cs
│   └── SpecialOrderBehavior.cs
└── SmartCavalryAI/
    ├── SmartCavalryAIBehavior.cs
    ├── CavalryChargeController.cs
    ├── CavalryPathfinding.cs
    └── SmartCavalryPatches.cs
```

---

## MCM Settings Summary

Add to `Settings.cs`:
```csharp
// Siege Dismount
public SiegeMountBehavior SiegeMountBehaviorSetting { get; set; }

// Layered Armor
public bool EnableLayeredArmor { get; set; }
public LayerCalculationMode ArmorLayerCalculation { get; set; }
public float UnderArmorBonusPercent { get; set; }

// Mixed Formations
public MixedFormationLayout DefaultMixedLayout { get; set; }
public int RangedRowDepth { get; set; }
public int InfantryRowDepth { get; set; }

// Battle Action Bar
public bool EnableBattleActionBar { get; set; }
public ActionBarPosition ActionBarPosition { get; set; }
public bool AutoCancelStanceOnMove { get; set; }

// Smart Cavalry AI
public bool EnableSmartCavalryAI { get; set; }
public float ChargeFormationStrictness { get; set; }
public bool EnableFriendlyCollisionAvoidance { get; set; }
```
