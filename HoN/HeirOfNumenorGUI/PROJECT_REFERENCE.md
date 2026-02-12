# HeirOfNumenor Mod - Project Reference

## Solution Structure

```
HeirOfNumenor.sln (contains both projects)
│
├── HeirOfNumenor/           <- CODE MOD (C# logic)
│   ├── Features/
│   │   ├── RingSystem/
│   │   │   ├── OrbitalRingSystem.cs    <- Ring positioning & orbit calculations
│   │   │   ├── RingScreenVM.cs         <- ViewModel + smooth animations
│   │   │   ├── GauntletRingScreen.cs   <- Screen lifecycle (OnFrameTick)
│   │   │   ├── RingItemManager.cs      <- Ring item definitions
│   │   │   └── RingSystemPatches.cs    <- Harmony patches
│   │   ├── FiefManagement/
│   │   ├── EquipPresets/
│   │   ├── TroopStatus/
│   │   ├── FormationPresets/
│   │   ├── SmithingExtended/
│   │   ├── MemorySystem/
│   │   ├── CustomResourceSystem/
│   │   ├── CompanionRoles/
│   │   └── TransferbuttonMenu/
│   ├── ModuleData/
│   │   ├── rings_of_power.xml
│   │   ├── ring_modifiers.xml
│   │   └── ring_modifier_groups.xml
│   ├── SubModule.xml
│   └── HeirOfNumenor.csproj
│
└── HeirOfNumenorGUI/        <- GUI MOD (sprites, prefabs, brushes)
    ├── GUI/
    │   ├── Prefabs/
    │   │   ├── RingScreen.xml          <- Ring UI layout
    │   │   ├── FiefManagement.xml
    │   │   ├── PartyTroopTuple.xml
    │   │   ├── TroopStatusIndicators.xml
    │   │   ├── PresetsOverlay.xml
    │   │   └── OOBButtonsOverlay.xml
    │   ├── SpriteParts/ui_ring_system/
    │   │   ├── rings/                  <- 20 ring PNG sprites
    │   │   │   ├── ring_one.png
    │   │   │   ├── ring_elven_narya.png
    │   │   │   ├── ring_elven_nenya.png
    │   │   │   ├── ring_elven_vilya.png
    │   │   │   ├── ring_dwarf_1-7.png
    │   │   │   └── ring_mortal_1-9.png
    │   │   ├── ring_table_with_tools.png  <- Table background (transparent)
    │   │   ├── ring_table_clean.png
    │   │   └── ui_ring_system.xml         <- Sprite sheet definition
    │   ├── Brushes/
    │   │   └── MapBar.xml
    │   └── HeirOfNumenorGUISpriteData.xml
    ├── Assets/GauntletUI/
    │   └── ui_ring_system_1_tex.tpac      <- Compiled texture pack
    ├── AssetSources/GauntletUI/
    │   └── ui_ring_system_1.png           <- Source sprite sheet
    ├── SubModule.xml
    └── HeirOfNumenorGUI.csproj
```

## Ring System - Technical Details

### Coordinate System
- **XML widgets use `HorizontalAlignment="Center" VerticalAlignment="Center"`**
- Ring positions are **CENTER-RELATIVE OFFSETS**, not absolute coordinates
- `PosX = 0, PosY = 0` means ring is at center of orbital container
- Positive X = right, Positive Y = down, Negative Y = up

### Key Constants (OrbitalRingSystem.cs)
```csharp
// Orbital container
CenterX = 300f;
CenterY = 200f;
ContainerWidth = 600;
ContainerHeight = 400;

// One Ring
OneRingFloatOffset = -45f;  // Floats above center (negative = up)

// Orbit radii (flat ellipses for table perspective)
ElvenOrbit:  RadiusX = 110, RadiusY = 28
DwarvenOrbit: RadiusX = 170, RadiusY = 50
MortalOrbit:  RadiusX = 240, RadiusY = 72

// Z-Index hierarchy
Back rings: 0-50
One Ring: 150
Front Elven: 200+
Front Dwarven: 300+
Front Mortal: 400+
```

### Animation System (RingScreenVM.cs)
- Uses `OnFrameTick(float dt)` for smooth rotation
- EaseInOutCubic easing function
- Manual rotation: 400ms duration
- Auto-rotate (when selecting back ring): 500ms duration

### MapBar Navbar
- Height: ~65px
- **Always add `MarginBottom="65"` to root widget** to avoid overlap

## Ring Sprite Mapping
```
Ring ID → Sprite Path
─────────────────────
hon_ring_one_ring     → ui_ring_system\\rings\\ring_one
hon_ring_narya        → ui_ring_system\\rings\\ring_elven_narya
hon_ring_nenya        → ui_ring_system\\rings\\ring_elven_nenya
hon_ring_vilya        → ui_ring_system\\rings\\ring_elven_vilya
hon_ring_dwarf_1-7    → ui_ring_system\\rings\\ring_dwarf_*
hon_ring_nazgul_1-9   → ui_ring_system\\rings\\ring_mortal_*
Fallback              → MapBar\\mapbar_center_circle_frame
```

## Common Issues & Fixes

### Rings scattered randomly
**Cause:** Position calculation adding centerX/centerY twice
**Fix:** `CalculatePosition()` should return `X = x, Y = y` (offsets), NOT `X = centerX + x`

### UI overlaps MapBar navbar
**Cause:** Root widget extends to screen bottom
**Fix:** Add `MarginBottom="65"` to root widget in RingScreen.xml

### Table shows checkerboard/white background
**Cause:** PNG missing alpha channel or wrong format
**Fix:** Ensure ring_table_with_tools.png is PNG with RGBA mode

### ListPanel positioning doesn't work
**Cause:** Bannerlord ListPanel with `StackLayout.LayoutMethod="None"` doesn't support CSS-style absolute positioning
**Fix:** Use individual Widget placement or MarginLeft/MarginTop with Left/Top alignment

## Bannerlord UI vs Web/React Differences

| Feature | React/Web | Bannerlord |
|---------|-----------|------------|
| Animation | requestAnimationFrame | OnFrameTick(float dt) |
| Position | transform: translate() | MarginLeft/MarginTop or PositionXOffset |
| Z-order | CSS z-index | Widget render order in XML |
| Easing | CSS cubic-bezier | Manual EaseInOutCubic in C# |
| State transitions | CSS transition | VisualDefinition |

## Build Notes

1. GUI mod must be built/deployed separately for sprite compilation
2. Both mods load via their respective SubModule.xml
3. HeirOfNumenorGUI depends on HeirOfNumenor (load order)
4. Sprite sheets compiled to .tpac in Assets/GauntletUI/

---
*Last updated: December 2024*
