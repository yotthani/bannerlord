# Examples & Author Reference

This folder ships with the mod (in `Modules/BannerlordThemeSwitcher/Examples/`) and contains everything needed to build your own theme.

## Contents

| Folder | Purpose |
|--------|---------|
| [BasicTheme/](BasicTheme/) | **Start here.** Minimal working theme template — copy the folder to `Themes/`, rename, edit colors. |
| [ColorSchemeTheme/](ColorSchemeTheme/) | Fully-annotated theme with every color slot filled in + the [COLOR_SCHEME_REFERENCE.md](ColorSchemeTheme/COLOR_SCHEME_REFERENCE.md) explaining each slot. |
| [BrushReference/](BrushReference/) | List of vanilla brush names by UI area (character creation, popups, encyclopedia, …). Use when you need precise per-brush control via `ColorRef` instead of AutoTheme. |
| [SpriteTheming/](SpriteTheming/) | How to ship custom sprites (PNG override, nine-slice borders, sprite sheets). See [README.md](SpriteTheming/README.md). |

## Workflow for theme authors

### 1. Make a copy of `BasicTheme/`

```
Modules/BannerlordThemeSwitcher/
└── Themes/
    └── MyTheme/                    ← copy of Examples/BasicTheme/
        └── ThemeManifest.xml
```

The folder name becomes the theme's `Id` (used for kingdom binding and the MCM dropdown). Spaces and special characters are fine but discouraged.

### 2. Edit colors

Open `ThemeManifest.xml`, change the `<ColorScheme>` slots. See [COLOR_SCHEME_REFERENCE.md](ColorSchemeTheme/COLOR_SCHEME_REFERENCE.md) for what each slot affects.

Alpha channel matters: `#FFD700AA` (gold, 67% opaque) reads very different from `#FFD700FF` (solid gold). Backgrounds usually want low alpha (`60`–`AA`); borders/text want full opacity.

### 3. Pick a binding

| Want | Set |
|------|-----|
| Activate when joining a specific kingdom | `<BoundKingdoms><Kingdom>vlandia</Kingdom></BoundKingdoms>` |
| Manual selection only | leave `<BoundKingdoms/>` empty |
| Replace an existing kingdom theme | use the same `<Kingdom>` ID — last theme loaded wins |

Valid kingdom/culture IDs: `vlandia`, `sturgia`, `battania`, `empire`, `empire_w`, `empire_s`, `aserai`, `khuzait`, `nord` (Nord DLC).

### 4. Reload

Either restart the game (themes are discovered at module load) or, if the folder already existed, switch themes once via MCM to force a reload of the manifest.

### 5. Iterate

Enable **Show Current Theme** in MCM to confirm the right theme is active. If the colors look off:

- Brush is grey → it's filtered by `ShouldSkipBrush` (battle/combat UI, icons, crests). Intentional.
- Brush is vanilla-coloured → AutoTheme didn't match its name. Either add an XML override in `GUI/Brushes/` using `ColorRef`, or check the brush name with **Log Brush Lookups**.
- Color is washed out → AutoTheme applies an HSV saturation boost; if your scheme color is already very saturated, try lowering its saturation.

## Adding sprites

Once colors look right, you can swap sprites too. Drop PNGs into `Themes/MyTheme/Sprites/` matching the vanilla sprite names. See [SpriteTheming/README.md](SpriteTheming/README.md) for details (nine-slice borders, sprite name discovery, troubleshooting).

[Themes/Erebor/](../Themes/Erebor/) is the cleanest live reference — 9 PNGs + a SpriteConfig.xml.

## Per-brush precision (advanced)

AutoTheme is sufficient for ~90% of brushes. For the rest, drop XML files into `Themes/MyTheme/GUI/Brushes/` using vanilla brush definitions but with `ColorRef`/`FontColorRef` attributes instead of literal hex colors:

```xml
<Brushes>
  <Brush Name="MyCustomButton">
    <Layers>
      <BrushLayer Name="Default" Sprite="button_canvas_9" ColorRef="ButtonBackground" />
    </Layers>
    <Styles>
      <Style Name="Default"><BrushLayer Name="Default" ColorRef="ButtonBackground" /></Style>
      <Style Name="Hovered"><BrushLayer Name="Default" ColorRef="ButtonHover" /></Style>
      <Style Name="Pressed"><BrushLayer Name="Default" ColorRef="ButtonPressed" /></Style>
    </Styles>
  </Brush>
</Brushes>
```

The ColorRef name must match a slot in your `<ColorScheme>`. See [BrushReference/BRUSH_REFERENCE.xml](BrushReference/BRUSH_REFERENCE.xml) for the vanilla brush names you can override.

## Reporting bugs

When something breaks (missing color, broken sprite, crash on theme switch):

1. Confirm the active theme name (MCM → Show Current Theme = on)
2. Screenshot or short clip of the issue
3. Attach the rgl_log file from `Documents/Mount and Blade II Bannerlord/logs/`
4. Steps to reproduce — especially which menu/screen, which kingdom

For a missing-color report, also enable **Log Brush Lookups** for one repro and grab the offending brush name from the log line.
