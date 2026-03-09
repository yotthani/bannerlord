# Dynamic Sprite Theming Design

**Date:** 2026-03-09
**Status:** Approved
**Author:** Co-designed with user

## Problem

The ThemeSwitcher currently only supports color-based theming (32 semantic color slots applied to Brushes). All visual assets (sprites, textures, icons) remain vanilla. Theme creators cannot replace UI elements like buttons, frames, panels, or icons without hard-baking assets into the module.

## Goal

Extend the ThemeSwitcher to support **dynamically loaded sprites** per theme, with:
- Full UI skinning capability (buttons, panels, frames, icons)
- Session-based caching similar to Bannerlord's vanilla sprite system
- Convention-over-configuration for easy theme creation
- Support for both loose PNGs and pre-built sprite sheets

## Approach: Hybrid Native Registration + Custom Loader + Atlas Packer

### Architecture

```
ThemeManager.ApplyTheme()
    │
    ▼
SpriteThemeManager              ← Central sprite orchestration
    ├── SpriteLoader            ← Loads PNGs, sprite sheets, packs atlases
    ├── SpriteRegistrar         ← Registers/unregisters in native system (Reflection + Harmony fallback)
    └── SpriteCache             ← Session cache for loaded textures
```

### New Classes

| Class | Responsibility |
|-------|---------------|
| `SpriteThemeManager` | Orchestrates load/unload, called by ThemeManager |
| `SpriteLoader` | Loads PNGs and sprite sheets, builds in-memory atlases |
| `SpriteRegistrar` | Registers/deregisters sprites in native UIResourceManager via Reflection, with Harmony fallback |
| `SpriteCache` | Holds loaded textures in memory, manages lifecycle |

### Integration Point

`ThemeManager.ApplyTheme()` calls `SpriteThemeManager.LoadThemeSprites()` after brush theming. On theme switch, `UnloadThemeSprites()` of the old theme is called first.

---

## Convention-over-Configuration

### Theme Folder Structure

```
Themes/
  MyTheme/
    ThemeManifest.xml           ← only needs <Sprites enabled="true"/>
    Sprites/                    ← auto-detected loose PNGs
      button_canvas_9.png       ← filename = sprite name → replaces vanilla
      frame_9.png
      icons/
        star.png                ← registered as "icons\star"
    GUI/                        ← optional pre-built sprite sheets
      SpriteParts/
        ui_mytheme/
          1.png
    SpriteConfig.xml            ← OPTIONAL: only for 9-slice borders + aliases
```

### Conventions

1. **Filename = Sprite Name:** `button_canvas_9.png` replaces vanilla sprite `button_canvas_9`
2. **Subdirectory = Namespace:** `icons/star.png` → sprite name `icons\star`
3. **`_9` Suffix** → automatically treated as NineRegion (9-slice) sprite
4. **`GUI/SpriteParts/ui_{themeId}/`** → loaded as pre-built atlas (no packing needed)
5. **Optional `SpriteConfig.xml`** for:
   - 9-slice border overrides (when defaults don't fit)
   - Explicit name aliases (when filename != target sprite name)

### SpriteConfig.xml (Optional)

```xml
<SpriteConfig>
  <NineRegion sprite="button_canvas_9" left="16" right="16" top="16" bottom="16" />
  <Alias file="my_button.png" replaces="button_canvas_9" />
</SpriteConfig>
```

---

## Loading Pipeline

```
ThemeManager.ApplyTheme(themeId)
    │
    ▼
SpriteThemeManager.LoadThemeSprites(theme)
    │
    ├─ 1. Check: Theme has sprites? (Components.Sprites && Sprites/ dir exists)
    │     No → return (color-only theme)
    │
    ├─ 2. Unload old theme
    │     SpriteCache.UnloadTheme(previousThemeId)
    │       └─ Remove registrations from SpriteData
    │       └─ Restore vanilla sprites from backup
    │       └─ Release GPU textures
    │
    ├─ 3. Already cached?
    │     Yes → SpriteRegistrar.Register(cachedData) → done
    │
    ├─ 4. Load assets
    │     ├─ GUI/SpriteParts/ exists?
    │     │   Yes → SpriteLoader.LoadSpriteSheet(path)
    │     │         └─ PNG → TaleWorlds.Engine.Texture
    │     │
    │     └─ Sprites/ exists?
    │         Yes → SpriteLoader.LoadLoosePNGs(path)
    │               ├─ Collect all PNGs recursively
    │               ├─ Pack atlas (bin-packing, shelf-first-fit)
    │               │   └─ Generate power-of-2 atlas texture
    │               └─ Generate SpritePart definitions with UVs
    │
    ├─ 5. Read SpriteConfig.xml (if present)
    │     └─ Apply 9-slice borders + aliases
    │
    ├─ 6. Register in native system
    │     SpriteRegistrar.Register(themeId, spriteData)
    │       ├─ Create SpriteCategory for theme
    │       ├─ Register SpriteParts
    │       ├─ Register/override Sprites (Generic + NineRegion)
    │       └─ Backup overridden vanilla sprites for rollback
    │
    └─ 7. Store in cache
          SpriteCache.Store(themeId, textureData, spriteMetadata)
```

---

## Native Registration: Dual Strategy

### Primary: Reflection

Access `UIResourceManager.SpriteData` internal dictionaries directly, consistent with existing `BrushModifier` pattern that accesses `BrushFactory._brushes`.

### Fallback: Harmony Patch

If reflection fields are not found (game update changed internals), fall back to Harmony patch on sprite lookup method:

```csharp
// Fallback: Prefix on SpriteData.GetSprite() or equivalent
[HarmonyPrefix]
static bool GetSprite_Prefix(string name, ref Sprite __result)
{
    if (_themeOverrides.TryGetValue(name, out var themeSprite))
    {
        __result = themeSprite;
        return false;  // skip original
    }
    return true;  // use vanilla
}
```

The system always tries reflection first, logs a warning and activates the Harmony fallback if reflection fails. This ensures robustness across game updates.

---

## Caching Strategy

- **Session Cache:** Loaded theme textures stay in memory for the game session
- **Lazy Loading:** Only loaded on first theme activation
- **LRU Eviction:** When memory limit reached, oldest inactive themes are unloaded
- **Theme Switch:** Old theme stays cached (fast switch-back), only registration is swapped
- **Cleanup:** All textures released on module unload / game exit

---

## Atlas Packer (Loose PNGs)

**Algorithm:** Shelf-First-Fit bin packing
1. Sort PNGs by height (tallest first)
2. Pack into "shelves" left-to-right
3. When shelf full → new shelf below
4. Atlas size = next power-of-2 that fits all (max 4096x4096)
5. If exceeds 4096x4096 → create additional atlas textures

**Output:**
- One or more `Texture` objects (the atlas)
- `Dictionary<string, Rectangle>` → sprite location in atlas
- SpritePart definitions with correct UV coordinates

**Performance:** Atlas built once at theme load time and cached. Zero runtime overhead.

---

## Sprite Replacement Mechanism

When a theme provides `button_canvas_9.png`:
1. The vanilla sprite `button_canvas_9` is found in SpriteData
2. Original is backed up in `_originalSprites` dictionary
3. Theme sprite is registered under the same name, replacing vanilla
4. On unregister: original is restored from backup

New sprites (no vanilla counterpart) are simply added and removed on unregister.

---

## ThemeManifest.xml Changes

Minimal change - only the existing `<Components>` section needs the sprites flag:

```xml
<Components>
  <Brushes enabled="true" />
  <Sprites enabled="true" />    <!-- enables sprite theming -->
</Components>
```

Everything else is convention-based (folder structure, file names).

---

## Error Handling

- Missing Sprites/ folder when enabled → log warning, continue with color-only
- PNG load failure → log error, skip that sprite, continue loading others
- Atlas exceeds 4096x4096 → split into multiple atlases automatically
- Reflection failure → activate Harmony fallback, log warning
- Corrupt SpriteConfig.xml → ignore config, use defaults
- Theme switch during load → cancel current load, start new one

---

## Files to Create/Modify

### New Files
- `Sprites/SpriteThemeManager.cs` — orchestration
- `Sprites/SpriteLoader.cs` — PNG/sheet loading + atlas packing
- `Sprites/SpriteRegistrar.cs` — native registration + Harmony fallback
- `Sprites/SpriteCache.cs` — session cache management

### Modified Files
- `ThemeManager.cs` — call SpriteThemeManager on theme apply/switch
- `Theme.cs` — add HasSpriteOverrides detection from folder structure
- `SubModule.cs` — initialize SpriteThemeManager
- `BannerlordThemeSwitcher.csproj` — include new files (auto with SDK-style)
