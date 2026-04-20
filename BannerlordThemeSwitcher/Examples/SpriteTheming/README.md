# Sprite Theming Howto

How to ship a theme that replaces vanilla sprites (frames, buttons, icons, panels) with your own PNGs.

## TL;DR

1. Create `Themes/MyTheme/Sprites/` and drop in PNGs whose **filename matches the vanilla sprite name**.
2. Add `<Components Brushes="true" Sprites="true"/>` to your `ThemeManifest.xml`.
3. For sprites that need scalable borders (frames, buttons), add a `SpriteConfig.xml` with `<NineRegion …/>` entries.
4. Restart the game once so the theme is discovered. Hot-switch via MCM after that.

## Folder layout

The mod looks for sprites in two places, in this order of priority:

```
Themes/MyTheme/
├── ThemeManifest.xml          ← REQUIRED
├── SpriteConfig.xml           ← optional: nine-slice borders, aliases
├── Sprites/                   ← PRIMARY: loose PNGs
│   ├── button_canvas_9.png
│   ├── frame_9.png
│   └── icons/
│       └── star.png           ← registered as "icons\star"
└── GUI/
    └── SpriteParts/           ← OPTIONAL: pre-built sprite sheets
        └── ui_atlas/
            └── atlas.png      ← only used if SpriteConfig.xml exists
```

The **loose PNG path** is the recommended one. Drop in PNGs, the mod packs them into atlases at runtime. The pre-built sheet path exists for performance-tuned themes but requires SpriteConfig.xml to define part rectangles.

Subfolders work — the relative path becomes part of the sprite name (e.g. `Sprites/icons/star.png` → `icons\star`).

## How a PNG replaces a vanilla sprite

The engine looks up sprites by name. When the loaded theme registers a sprite with the same name, the vanilla one is overridden. So:

| Filename                     | Replaces vanilla sprite |
|------------------------------|-------------------------|
| `button_canvas_9.png`        | `button_canvas_9`       |
| `frame_9.png`                | `frame_9`               |
| `BlankWhiteSquare.png`       | `BlankWhiteSquare`      |
| `icons/star.png`             | `icons\star`            |

To find the vanilla sprite name for a UI element you want to replace:
1. Enable **Log Brush Lookups** in MCM
2. Hover or open the UI element you care about
3. Find the matching brush name in the debug log
4. Open the original brush XML in `Modules/Native/GUI/Brushes/` and read the `Sprite="…"` attribute on its layers

`Examples/SpriteTheming/GUI/SPRITEDATA_REFERENCE.xml` has a starting list of common sprite names.

## SpriteConfig.xml

Optional companion file. Two purposes:

### 1. Nine-slice borders

Buttons and frames need to scale without distorting their corners. Define the inset in pixels:

```xml
<?xml version="1.0" encoding="utf-8"?>
<SpriteConfig>
  <NineRegion sprite="button_canvas_9" left="12" right="12" top="12" bottom="12" />
  <NineRegion sprite="frame_9"         left="16" right="16" top="16" bottom="16" />
  <NineRegion sprite="rounded_canvas_9" left="12" right="12" top="12" bottom="12" />
</SpriteConfig>
```

The `_9` suffix is a vanilla convention for "nine-region sprite". The numbers describe how many pixels from each edge are corner area; the middle stretches.

### 2. Aliases

Use to map a PNG to a sprite name that doesn't match its filename:

```xml
<Alias file="my_special_button.png" replaces="ButtonBrush1.Sprite" />
```

## Real example: Erebor

`Themes/Erebor/` is the cleanest reference theme to copy from:

```
Erebor/
├── ThemeManifest.xml          ← <Components Brushes="true" Sprites="true"/>
├── SpriteConfig.xml           ← 8 nine-region definitions
└── Sprites/                   ← 9 PNGs covering buttons, frames, dialogs
```

Look at `Themes/Erebor/SpriteConfig.xml` and `Themes/Erebor/Sprites/` to see exactly what a complete sprite-themed entry looks like.

## Theme switching behaviour

- On theme switch, the previous theme's sprites are **unregistered**, vanilla rules restore.
- Cached in memory per session — switching back to a previously-loaded theme is instant.
- Cache is cleared on game exit. PNGs are re-read from disk on next launch.

## Troubleshooting

**My PNG isn't replacing anything.**
- Check the filename matches the vanilla sprite name exactly (case-sensitive on the engine side).
- Confirm `<Components Sprites="true"/>` is set, OR the `Sprites/` folder contains at least one PNG (auto-detected).
- Watch the debug log for `[ThemeSwitcher] SpriteThemeManager: Loaded N parts, M textures` after a theme switch.

**My button looks distorted at large sizes.**
- It needs a `<NineRegion>` entry in `SpriteConfig.xml`.

**Switching themes is slow on first activation.**
- Expected — first activation reads PNGs from disk and packs atlases. Subsequent switches use the in-memory cache.

**The sprite shows up but the colour is wrong.**
- Sprite theming and brush colour theming are independent. Colours come from `<ColorScheme>` in the manifest. Vanilla brushes apply a tint colour over the sprite — adjust the relevant `ColorScheme` slot, or override the brush in `GUI/Brushes/` to use a fixed colour.
