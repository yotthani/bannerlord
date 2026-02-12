# Project Journal - HeirOfNumenor

## Purpose
This journal tracks development progress, decisions made, and current state. Upload at session start for continuity.

---

## Current State (December 2025)

### Core Features (Stable)

#### 1. Transfer All Buttons ✅
- Quick transfer buttons in inventory screen
- Sell damaged items, low value items with configurable thresholds
- Uses native transfer patterns (TransferCommand, ProcessSellItem)

#### 2. Inventory Search ✅
- Enables native hidden search boxes in inventory screen
- Filter items by name in both player and other inventory panels
- State persists across save games (bidirectional sync)
- MCM toggle to enable/disable
- Based on InventorySearchEnabler mod, fully integrated

#### 3. Equipment Presets ✅
- Save character equipment as presets (Battle + Civilian equipment)
- Load presets with proper InventoryLogic integration
- Mount support (horse + harness slots)
- Persists across save games

#### 4. Companion Role Indicators ✅
- Shows icons on companion portraits indicating assigned roles
- Combat roles (Cavalry, Infantry, Archer, Horse Archer)
- Party roles (Quartermaster, Scout, Surgeon, Engineer)
- Works in party screen via Harmony patches

#### 5. Formation Presets (Order of Battle) ✅
- Save and load formation configurations
- Auto-assignment of heroes to formations
- Captain scoring system for optimal placement
- Integration with Order of Battle screen

### Advanced Features

#### 6. Ring System ✅
- 20 Rings of Power (3 Elven, 7 Dwarven, 9 Mortal, 1 One Ring)
- Orbital display with 3D depth effects
- Power buildup over time (days worn → skill bonuses)
- Corruption system with trait integration
- Threat system attracts enemy attention
- Single ring limit (cannot swap without decay)
- Custom MapBar button to open Ring screen

#### 7. Troop Status System ✅
- Per-troop tracking: Fear, Frustration, Bonding, BattleExperience, Loyalty, RingExposure
- Daily tick for decay and events
- Integration with native morale/speed models
- UI indicators on party screen

#### 8. Memory System (Virtual Captains) ✅
- Troops that survive 10+ battles get named captains
- Hero-like skill progression (Leadership, Tactics, Combat, Scouting)
- Trait system (Valor, Mercy, Honor, Cunning)
- Captain promotion to real companions (with user confirmation)
- Fallen captains memorial

#### 9. Custom Resource System ✅
- Culture-specific resource needs from XML config
- Terrain-based satisfaction (forest for elves, mountains for dwarves)
- Daily processing with settlement/battle hooks
- Effects on morale and combat effectiveness

#### 10. Fief Management ✅
- Remote management of all owned towns/castles
- Native VM integration (SettlementProjectSelectionVM, GovernorSelectionVM)
- Building queue management
- Carousel navigation between fiefs
- F6 hotkey integration

#### 11. Smithing Extended ✅
- Armor crafting system (helmets, body armor, gauntlets, boots, capes)
- Material checking against party inventory
- Unique item generation for high skill
- Item repair at settlements
- Stamina system with MCM controls

### Technical Infrastructure

- **ModSettings.cs**: MCM integration for all settings
- **SafeExecutor.cs**: Centralized error handling wrapper
- **CommonUtilities.cs**: Shared utility functions (widgets, heroes, math)
- **ModTexts.cs**: Localization strings
- **Harmony patches**: Proper prefix/postfix structure

### Documentation

- **BANNERLORD_KNOWLEDGE_BASE.md**: Complete API reference, patterns, and feature checklist (consolidated from all analysis docs)
- **PROJECT_JOURNAL.md**: Development history and current state
- **README.md**: Installation and usage

---

## Architecture Notes

### Save System
- Uses `CampaignBehaviorBase.SyncData` with unique IDs
- Each behavior has dedicated save ID (8675309, 8675310, etc.)
- Saveable properties marked with `[SaveableField]` or `[SaveableProperty]`

### UI Patterns
- GauntletLayer for screens
- LoadMovie for prefab binding
- ViewModel with [DataSourceProperty] bindings
- Harmony patches for existing UI modification

### Model Integration
- Custom models extend Default*Model classes
- Register via GameModelsMethod.AddModel
- Use ExplainedNumber.Add() for tooltip integration

---

## Recent Changes (Session Summary)

1. Captain promotion now requires user confirmation dialog
2. Ring system fully connected to behavior (equip/unequip, skill effects)
3. Corruption affects hero traits (Mercy, Honor, Valor penalties)
4. Terrain-based resource satisfaction implemented
5. Armor smithing checks actual party inventory for materials
6. Equipment presets use native TransferCommand
7. Cleaned up duplicate documentation files

---

## Known Issues / TODO

- [ ] XML-defined ring items (native item loading)
- [ ] Native ItemModifier for unique items
- [ ] Full RingScreenVM orbit click detection refinement

#### 5. Ring System ✅
- 20 Rings of Power (3 Elven, 7 Dwarf, 9 Mortal, 1 One Ring)
- Custom screen with 3D background
- Concentric circles layout with perspective
- MapBar navigation button with ring icon
- Right-click context menu (vanilla toolbox style)
- Equip/Unequip functionality (max 1 ring)
- Ring Power and Corruption attribute system
- Time-based effect progression
- Threat system with culture awareness
- Integration with TroopStatus (fear from corruption)

#### 6. TroopStatus System ✅
- Tracks statuses per troop type (Fear, Frustration, Bonding, Loyalty, etc.)
- Range: 0-100 scale
- Natural decay/growth rates
- Threshold events (warnings when troops become Uneasy, Frightened, etc.)
- Battle events (victory/defeat effects)
- Recruitment dilution (new troops dilute bonding)
- Desertion from high fear
- Integration with Ring System (corruption causes fear)
- **UI Integration**: Dynamic status icons in Party Screen
  - Icon mapping: Fear→Skull, Bonding→Flag, Frustration→Upgrade, Loyalty→Recruit
  - Color gradients based on value (green=good, red=bad)
  - Tooltips showing exact values and state names
  - Modified PartyTroopTuple.xml with StatusIndicators panel

#### 7. Memory System ✅
- Bonding growth over time (+0.5%/day)
- Battle survival bonuses (+2% per battle)
- Dilution when recruiting same troop type
- Virtual Captains emerge at 75% bonding + 10 troops
- Culture-specific captain names (Gondor, Rohan, Elven, Dwarf, Orc, etc.)
- Captain bonuses (morale, damage, defense, fear resistance)
- Captain death tracking (fallen heroes memorial)
- Release penalty (bonding degrades, starts fresh if re-recruited)

#### 8. Custom Resource System ✅
- XML-configurable cultural needs system
- Resource types: Consumable, Location, Action, Social, Temporal
- Satisfaction modes: Decay, DaysSince, Accumulate, Binary
- Culture profiles with inheritance (Mirkwood inherits from Lothlorien)
- Default configurations:
  - Dwarves: Need beer (Frustration if < 30%), miss mountains
  - Elves: Need forest connection, grow weary in open lands
  - Orcs: Need combat (Frustration + Fear reduction), need meat
  - Humans: Need rest, need pay (Frustration + Loyalty loss)
- Grace periods before effects trigger
- Multiple effects per requirement (status + magnitude)
- Event hooks: battles, settlement entry, terrain

#### 9. Fief Management ✅
- Remote management of all clan fiefs from map (no need to enter)
- Carousel navigation between owned towns/castles
- Stats display: Prosperity, Loyalty, Security, Food, Garrison, Income
- Building list with level and upgrade availability
- Construction queue management (add/view queue)
- Hotkey: F6 to open from campaign map
- MapBar integration planned

#### 10. Smithing Extended ✅
- **MCM Settings**: Toggle stamina usage, cost multipliers
- **Stamina Control**: Disable or reduce stamina cost for all smithing actions
- **Item Repair Shop**: Settlement menu option to repair/upgrade items
  - Basic repair (remove negative modifiers)
  - Quality upgrades (Fine, Masterwork, Legendary)
  - Specialized modifiers (Balanced, Sharp, Reinforced, Lordly)
  - Skill-based success chance
- **Armor Smithing**: Craft armor from parts (like weapon smithing)
  - Part categories: Helm, Cuirass, Pauldrons, Shield, etc.
  - Material costs: Iron, Steel, Leather, Cloth
  - Difficulty scaling based on parts selected
- **Unique Items**: High-rank smiths can create unique items
  - Rarity tiers: Uncommon → Rare → Epic → Legendary
  - Dynamic bonuses: BonusDamage, ArmorPiercing, LifeSteal, FireDamage, etc.
  - Generated names: "Blade of Flame", "Helm of the Champion"
  - Lore text generation for legendary items
  - Skill-based unique chance (2% base + skill bonus)

### Pending/Future Work

#### Ring System Enhancements
- [x] Save/load equipped rings to save game
- [x] Ring Power and Corruption attribute system
- [x] Time-based effect progression
- [x] Race-specific skills (Elven, Dwarf, Mortal)
- [x] Decay system when unequipping
- [ ] Apply actual stat bonuses from ring effects (needs Harmony patches)
- [ ] Ring acquisition system (quests, loot, merchants)
- [ ] Corruption mechanic visual effects for The One Ring
- [ ] Tooltips on context menu buttons
- [ ] Ring-specific visual effects

#### General Improvements
- [x] MCM settings integration (comprehensive ModSettings.cs)
- [x] SafeExecutor for error handling
- [ ] Localization support
- [ ] Performance optimization

### Robustness & MCM Integration Audit ✅ (December 2024)
- **ModSettings.cs**: Central MCM configuration for all 10 features
  - All features can be enabled/disabled independently
  - Feature-specific multipliers and thresholds configurable
  - Debug mode toggle, Safe mode toggle
- **SafeExecutor.cs**: Utility for robust error handling
  - `Execute()` for safe action execution
  - `WrapPatch()` for Harmony patch wrapping
  - `WrapPrefix()` for prefix patches with return values
  - Feature-aware enable checks
- All CampaignBehaviors updated with:
  - `IsEnabled` static property checking ModSettings
  - `SafeExecutor.Execute()` wrapping all event handlers
  - Null checks on all collections and objects
  - Error logging via `ModSettings.ErrorLog()`

**MCM Settings Groups:**
| Group | Settings |
|-------|----------|
| General | Debug Messages, Safe Mode |
| Equipment Presets | Enable, Max Presets Per Character |
| Formation Presets | Enable, Auto-Assign Heroes, Max Presets |
| Companion Roles | Enable, Show Role Icons |
| Ring System | Enable, Corruption, Corruption Rate, Threats, Threat Rate, Ring Screen |
| Troop Status | Enable, UI Icons, Desertion, Fear/Bonding Multipliers, Fear Threshold |
| Memory System | Enable, Bonding Threshold, Min Troops, Captain Death, Bonus Multiplier |
| Cultural Needs | Enable, Decay Multiplier, Effect Multiplier, Show Warnings |
| Fief Management | Enable, Allow Remote Building |
| Smithing Extended | Enable, Stamina, Repair, Armor, Uniques, Unique Chance, Min Skill |

---

## Technical Decisions Log

### Ring System Architecture
**Decision:** Use GameState + CreateScreen patch pattern  
**Reason:** Proper integration with game's screen stack, allows MapBar navigation to work correctly

### MapBar Button Integration
**Decision:** Override entire MapBar.xml brush file  
**Reason:** Bannerlord replaces brushes by name, doesn't merge - must include all vanilla layers plus custom

### Context Menu Style
**Decision:** Horizontal toolbox style (Inventory\toolbox)  
**Reason:** Matches vanilla inventory UI, familiar to players

### Sprite Category Loading
**Decision:** Load but never unload sprite categories  
**Reason:** Categories are shared globally, unloading breaks other UI elements

---

## Session Notes

### Session: Ring UI Implementation
- Created circular ring layout with perspective scaling
- Larger rings in front, smaller in back
- Ring colors: Silver (Mortal), Bronze (Dwarf), Unique (Elven), Gold (One Ring)
- Selection highlight with white glow ring

### Session: MapBar Integration
- StringId in NavigationElement must match brush layer name exactly
- Dual-layer injection (constructor + event) for reliability
- Icon sizing via negative Extend values on brush layer

### Session: Context Menu
- MarginLeft/MarginTop expect float, not int (crash fix)
- Horizontal toolbox pattern from InventoryEquippedItemControls.xml
- Click catcher button for closing when clicking outside

### Session: Ring Power & Corruption Attributes
- Two new attributes: Ring Power and Corruption
- Race-specific power skills: Elven (Grace, Starlight, Swiftness), Dwarf (Fortitude, Stonecraft, Goldlust), Mortal (Dominion, Command, Presence)
- Corruption skills shared: Fading, Greed, Shadow
- Time-based progression: Day 1 → Tier 1, Week → Tier 2, Month → Tier 3
- Blessings start strong (40/25/15), Curses get worse (10/20/35)
- Decay system: Bonuses decay fast, Curses decay slow
- Race affects speed: Elven = slow corruption, Mortal = fast corruption
- Ring swap prevention: Must wait for previous effects to decay
- One Ring: Instant max bonuses, accelerating corruption, very slow decay

### Session: Threat System & UI Improvements
- Single ring limit (max 1 equipped)
- Left panel: Removed equip buttons, added ownership indicators
- Threat Level system based on rings owned:
  - None: 0 rings
  - Low: 1-3 rings (15% more visible)
  - Medium: 4-7 rings (35% more visible)
  - High: 8+ rings (60% more visible)
  - EXTREME: The One Ring (100% more visible, actively hunted!)
- Daily threat events: Random ominous messages, hunter warnings
- Acquisition warnings when picking up new rings
- UI shows current threat level in effects panel
- Culture-aware aggression: Evil cultures (Mordor, Isengard, Orcs) get MASSIVE aggression bonus
- ShouldBeHostileToRingBearer(): Evil parties may attack regardless of diplomacy
- One Ring = ALL evil factions become permanent enemies (15x aggression!)
- Orcish vs Other Evil distinction - Orcs are more aggressive hunters than Umbar corsairs
- Race affinity system: Elves +50% with Elven rings, Dwarves +30% with Dwarf rings, etc.
- Accurate culture lists from LOTR:AOM mod (mordor, isengard, gundabad, dolguldur, erebor, etc.)

---

## File Quick Reference

### Core Utilities
| File | Purpose |
|------|---------|
| `ModSettings.cs` | Central MCM configuration for all features |
| `SafeExecutor.cs` | Error handling wrapper utility |
| `Settings.cs` | Original inventory quick actions settings |
| `SubModule.cs` | Mod entry point, behavior registration |

### Ring System
| File | Purpose |
|------|---------|
| `RingScreen.xml` | Ring wheel UI layout |
| `MapBar.xml` | Brush overrides for ring button |
| `GauntletRingScreen.cs` | Screen implementation |
| `RingScreenVM.cs` | ViewModel with ring data |
| `RingState.cs` | GameState for screen |
| `RingNavigationElement.cs` | MapBar button definition |
| `RingSystemPatches.cs` | Harmony patches |
| `RingItemManager.cs` | Ring item definitions |
| `RingAttributes.cs` | Skill/attribute definitions |
| `RingEffectTracker.cs` | Time tracking, buildup/decay |
| `RingSystemCampaignBehavior.cs` | Save/load, daily updates |
| `RingThreatSystem.cs` | Threat level, visibility, hunting |

### TroopStatus System
| File | Purpose |
|------|---------|
| `TroopStatusType.cs` | Status enum (Fear, Bonding, etc.) |
| `TroopStatusData.cs` | Data container per troop type |
| `TroopStatusManager.cs` | Central access/modification |
| `TroopStatusCampaignBehavior.cs` | Daily ticks, events, save/load |

### TroopStatus UI
| File | Purpose |
|------|---------|
| `UI/StatusIconMapper.cs` | Maps status types to icons/colors |
| `UI/TroopStatusIndicatorVM.cs` | ViewModels for indicator icons |
| `UI/TroopStatusUIPatches.cs` | Harmony patches for party screen |
| `PartyTroopTuple.xml` | Modified tuple with status icons |
| `TroopStatusIndicators.xml` | Reusable indicator prefab |

### Memory System
| File | Purpose |
|------|---------|
| `VirtualCaptain.cs` | Captain data, names, bonuses |
| `MemorySystemCampaignBehavior.cs` | Captain promotion, tracking |

### Custom Resource System
| File | Purpose |
|------|---------|
| `ResourceDefinition.cs` | Resource types, satisfaction sources |
| `CultureResourceRequirement.cs` | Culture needs, effects |
| `ResourceConfigLoader.cs` | XML config loading, defaults |
| `CustomResourceCampaignBehavior.cs` | Daily processing, events |
| `culture_resources.xml` | XML configuration file |

### Fief Management
| File | Purpose |
|------|---------|
| `FiefBuildingVM.cs` | ViewModel for individual buildings |
| `FiefManagementVM.cs` | Main screen ViewModel with carousel |
| `GauntletFiefManagementScreen.cs` | Screen and state classes |
| `FiefManagementPatches.cs` | MapBar/hotkey integration |
| `FiefManagement.xml` | UI layout with carousel |

### Smithing Extended
| File | Purpose |
|------|---------|
| `UniqueBonusDefinition.cs` | Unique bonus types, rarity, names |
| `UniqueItemGenerator.cs` | Generates unique items with bonuses |
| `ItemRepairManager.cs` | Repair/upgrade logic, modifiers |
| `ArmorSmithingManager.cs` | Armor crafting from parts |
| `SmithingExtendedPatches.cs` | Harmony patches, settlement menu |
| `SmithingExtendedCampaignBehavior.cs` | Save/load, initialization |

---

## Known Issues

1. **Ring spacing** - May need adjustment based on screen resolution
2. **Context menu position** - Uses mouse position calculation that may vary by resolution

---

## Contact/Credits
- Development collaboration using Claude AI
- Bannerlord modding community resources

*Last updated: December 2024*

## 2024-12-29: Ring Screen UI Polish & Localization

### Visual Effects
- **Glow System (replaced shadows):**
  - Table glow: Warm orange corona (#FFAA6640) appears when ring bobs DOWN (closer to table)
  - Ring aura: Glow around ring appears when bobbing UP (like sucking light from surface)
  - Both use `IsHitTestVisible="false"` to not block clicks
  - Subtle intensity: table glow 0.25→0.5 opacity, ring aura 0.08→0.4 opacity

- **Ring Colors Finalized:**
  - Elven: Light leaf green (#98FB98)
  - Dwarven: Light mithril blue-silver (#D0E8FF)
  - Mortal: Light orange (#FFB84D)
  - One Ring: Gold (#FFD700)

### Click/Selection Fixes
- **Anti-alignment logic:** When front rings would vertically align, they're nudged apart horizontally (~18px) so all remain clickable
- **XML declaration order for front rings:** Mortal → Dwarven → Elven (last = on top = clickable)
- Added `AcceptDrop="false"` to ring wrapper widgets
- Added `IsHitTestVisible="false"` to shadow/glow/aura widgets (40 total)

### Localization (Bannerlord Standard)
- All UI text now uses `TextObject("{=string_id}Default Text")`
- 115+ new localization strings in `std_module_strings_xml.xml`
- All 20 ring definitions (name, short, effect, lore) have string IDs
- XML binds to VM properties that return localized strings
- Translation: copy file to `Languages/DE/` etc., translate `text="..."` values

### Key Files Modified
- `RingScreenVM.cs` - 43 TextObject usages, localized labels as properties
- `RingScreen.xml` - All Text="" now use @PropertyName bindings
- `OrbitalRingSystem.cs` - Anti-alignment in UpdateAllPositions(), glow properties
- `std_module_strings_xml.xml` - All ring system strings
