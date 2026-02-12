# LOTRAOM.FactionMap — Bannerlord Mod

Interactive Middle-earth faction selection map for Mount & Blade II: Bannerlord character creation.
Replaces the vanilla culture selection screen with a clickable puzzle-piece map of Middle-earth.

## Build & Deploy

```bash
dotnet build -c Release
```

- Post-build copies everything to `$(BANNERLORD_GAME_DIR)\Modules\LOTRAOM.FactionMap`
- Default game dir: `D:\Steam\steamapps\common\Mount & Blade II Bannerlord`
- Game **locks the DLL** while running — close Bannerlord before building
- Structured decompiled game code (reference): `C:\Work\Sources\github\Conversion\1.3\structured code\`
- Python: `C:\Program Files\AILocal\python\3.10\python.exe` (has Pillow, numpy, scipy)

## Architecture

### Data Flow
```
regions.json + factions.json
    → SimpleJsonParser
    → FactionRegistry (static lookup)
    → PolygonWidgets (read BBox + faction data at construction)
    → FactionSelectionVM (selection, tooltip, banner placement)
```

### Two-File Data Model
- **`ModuleData/regions.json`**: Region geometry — snake_case keys, each has `norm_bbox` [x,y,w,h] (normalized 0-1), optional `faction` reference, optional `capital_pos` [x,y] (normalized 0-1)
- **`ModuleData/factions.json`**: Faction definitions — name, color (#RRGGBBAA), playable bool, game_faction (culture ID), description, traits, bonuses, perks, special_unit, side ("free"/"evil"/"neutral"), strengths, weaknesses, difficulty (1-5)

### Region 3-Tier System
1. **Tier 1** (no faction): Inert decoration — desaturated/dark, no interaction
2. **Tier 2** (faction, not playable): Info only — minimal hover, clickable for detail panel
3. **Tier 3** (playable): Full interaction — hover lift, shadow, edge, selection, pulse, banner

### Widget Hierarchy
```
MapContainerWidget (aspect ratio 6166:4096, letterboxing)
  └─ 36x PolygonWidget (one per region puzzle piece, self-positioning via BBox)
  └─ BannerWidget (flag at selected faction's capital)
  └─ BrushWidget (ornamental frame overlay)
```

## Source Files

### Core
| File | Purpose |
|------|---------|
| `Source/SubModule.cs` | Module entry, Harmony init, file-only logging |
| `Source/FactionMapCultureStageView.cs` | Harmony patches to inject VM into character creation |
| `Source/FactionSelectionVM.cs` | ViewModel — selection, tooltip, banner, detail panel data |
| `Source/FactionRegistry.cs` | Static registry for region/faction lookups |
| `Source/RegionData.cs` | Region data class (BBox, capital pos, faction ref) |
| `Source/RegionConfig.cs` | CultureObject resolver |
| `Source/SimpleJsonParser.cs` | JSON parser using Newtonsoft.Json |
| `Source/LandmarkConfig.cs` | Hardcoded landmark definitions (cities, fortresses, etc.) |

### Widgets (`Source/Widgets/`)
| File | Purpose |
|------|---------|
| `PolygonWidget.cs` | Region puzzle piece — self-positioning, 6-pass rendering, hover/selection, pulse animation, hit-testing |
| `BannerWidget.cs` | Banner flag at capital — fade in/out, stamp-down animation, side-based glow |
| `FactionImageWidget.cs` | Faction artwork in detail panel |
| `MapContainerWidget.cs` | Aspect ratio container with letterboxing |
| `RuntimeSprite.cs` | Sprite wrapper for runtime-loaded textures |

### GUI
| Path | Purpose |
|------|---------|
| `GUI/Prefabs/CharacterCreation/CharacterCreationCultureStage.xml` | Gauntlet prefab — map layout + right detail panel |
| `GUI/Brushes/FactionMap.xml` | Custom brushes (frame, section bars, text styles) |
| `GUI/SpriteData/FactionMap/` | Region textures (`highlight_*.png`), banner textures (`banner_*.png`), emblem, faction images |

## GOLDEN RULES — Bannerlord Widget Rendering

These rules were learned through painful debugging. Violating ANY of them causes visual bugs.

1. **NEVER touch the Brush** — Any `Brush` modification on ImageWidget causes permanent darkening
2. **NEVER call `base.OnHoverBegin()`** — Triggers BrushWidget state switching which darkens
3. **Set `OverrideDefaultStateSwitchingEnabled = true`** in constructor — Locks widget in "Default" state
4. **Let base class handle OnRender for simple widgets** — Set `Sprite = _loadedSprite` and base draws it
5. **For custom multi-pass rendering** (PolygonWidget) — Override OnRender, skip base, call `drawContext.DrawSprite()` directly
6. **Static state must be reset across sessions** — Use `PolygonWidget.ResetSession()` called from Harmony Postfix before new widgets are created

## Rendering Passes (PolygonWidget.OnRender)

| Pass | What | Condition |
|------|------|-----------|
| 1 | Drop shadow | Tier 3 + hover/selected (liftT > 0) |
| 2 | Edge/thickness | Tier 3 + hover/selected (liftT > 0) |
| 3 | Main sprite | Always (with tier-based material adjustments) |
| 5 | Faction color pulse | Tier 3 + is current pulse target + not hovered/selected |
| 5b | Banner watermark overlay | Same as Pass 5 + banner loaded |
| 6 | Capital pin (deferred) | Tier 3 + has capital position |

## SimpleMaterial Parameters Cheat Sheet

| Parameter | Effect |
|-----------|--------|
| `ColorFactor = 0` | Show original texture colors |
| `ColorFactor = 1` | Force `Color` as flat tint (texture alpha = shape mask) |
| `ValueFactor > 0` | Brighten |
| `ValueFactor < 0` | Darken |
| `SaturationFactor < 0` | Desaturate |
| `AlphaFactor` | Opacity (multiply with contextAlpha) |

### What works for banner watermark overlay (Pass 5b):
```csharp
bannerMat.Color = BrightenColor(_factionColor, 0.8f);
bannerMat.ColorFactor = 1f;  // tint with faction color (alpha as mask)
bannerMat.AlphaFactor = contextAlpha * _globalPulseAlpha * 0.7f;
bannerMat.ValueFactor = 80f;
```

### What does NOT work for banner overlay:
- `ColorFactor = 0, ValueFactor = 60` → gray/washed out
- `ColorFactor = 0, ValueFactor = 0` → almost black
- `ColorFactor = 0, ValueFactor = 30` → half-transparent gray-black
- `LoadTextureFromPath` doesn't preserve alpha correctly for banner PNGs with `ColorFactor = 0`

## Bannerlord Platform Gotchas

- **Color format**: `#RRGGBBAA` (Alpha at END, not beginning)
- **System.Numerics.Vectors**: Game uses v4.1.3.0, NuGet ships v4.1.4.0 → use game's DLL with `extern alias SNV` and `using Vector2 = SNV::System.Numerics.Vector2`
- **Path conflict**: `TaleWorlds.Engine.Path` vs `System.IO.Path` → always use `System.IO.Path` fully qualified
- **Texture loading**: `Texture.CreateTextureFromPath` returns null for mod PNGs → use `Texture.LoadTextureFromPath(fileName, folder)` instead. `CreateFromMemory` works but causes memory leaks (user explicitly rejected it)
- **PNG memory**: .NET decompresses PNGs to uncompressed BMP in memory. Avoid loading many full-size (2048x1423) PNGs. Use bbox-cropped versions.
- **Sprite compiler**: Bypass entirely with `RuntimeSprite` + `LoadTextureFromPath`
- **DrawPolygon**: Does NOT exist in the actual game DLL despite appearing in decompiled code. Only `DrawSprite` available.
- **DPI/4K scaling**: `ParentWidget.Size` returns SCALED coords. `PositionXOffset`/`MarginLeft` are UNSCALED. Use `ScaledPositionXOffset` with Size-derived values or divide Margins by `_scaleToUse`. Formula: `Scale = screenHeight / 1080`
- **Widget self-positioning**: Use `OnLateUpdate` to set `ScaledPositionXOffset`/`ScaledPositionYOffset`/`ScaledSuggestedWidth`/`ScaledSuggestedHeight` based on `ParentWidget.Size`
- **Static field lifetime**: Static fields in widgets survive across character creation sessions (going to main menu and back). Must be explicitly reset when a new session starts — the Harmony Postfix in `FactionMapCultureStageView` calls `PolygonWidget.ResetSession()` before new widgets are created.

## Harmony Patching

Target: `CharacterCreationCultureStageView` (two possible namespaces searched)

| Patch | What |
|-------|------|
| `ConstructorPatch.Postfix` | Resets widget state, releases vanilla movie, loads custom prefab + FactionSelectionVM |
| `TickPatch.Postfix` | Polls hover tooltip + Enter key to confirm |
| `FinalizePatch.Prefix` | Cleanup on screen exit |

## Asset Pipeline (Python Tools)

Located in `Tools/` directory (excluded from compilation via .csproj):

- **`process_flags.py`**: Extracts banner sprites from faction flag sheets
- **`fix_banners.py`**: Post-processes banner PNGs (alpha cleanup, sizing)
- **`extract_banners.py`** / **`find_banners.py`**: Banner discovery utilities
- **`map_banners.py`**: Maps banners to faction IDs

Region texture files (`highlight_*.png`) are bbox-cropped from a 2048x1423 base map.
Banner textures (`banner_*.png`) are 84x128px with alpha transparency.

## Known Bugs / Lessons Learned

1. **Pulse stops after returning to main menu**: Static `_nextPlayableIndex` never reset → new widgets get stale indices. Fixed by `ResetSession()` in Harmony Postfix.
2. **Banner overlay shows as gray/black**: `LoadTextureFromPath` + `ColorFactor = 0` doesn't work for banner alpha. Use `ColorFactor = 1` with faction color tinting instead.
3. **Double-scaling on 4K**: Mixing `ParentWidget.Size` (scaled) with `MarginLeft` (unscaled) causes elements to be 2x offset on 4K. Always match scaled-to-scaled or unscaled-to-unscaled.
4. **Brush darkening**: Any Brush property access on custom ImageWidgets causes permanent visual artifacts. The `OverrideDefaultStateSwitchingEnabled = true` flag prevents this but you must never touch the Brush yourself either.

## C# Language Features

- **Target**: .NET Framework 4.7.2 (`net472`)
- **LangVersion**: 9.0 (supports switch expressions, pattern matching, target-typed new)
- **AllowUnsafeBlocks**: true (for potential texture manipulation)
