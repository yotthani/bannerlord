# Bannerlord Theme Switcher

A mod for Mount & Blade II: Bannerlord that re-skins the game UI at runtime — colors **and** sprites — and switches the theme automatically when the player joins a kingdom.

## Features

- **Color schemes**: define ~30 semantic color slots once, the entire menu UI re-tints
- **Sprite overrides**: drop PNGs into a theme folder, vanilla sprites are replaced at runtime (nine-slice borders supported)
- **Kingdom-bound auto-switch**: theme follows the player's current kingdom, with a culture fallback for clanless characters
- **Manual override** via MCM
- **Save-game aware**: theme re-applies after loading a save (`OnGameLoadedEvent`)
- **No vanilla files touched**: works through brush interception (Harmony) and dynamic sprite registration
- **Hot switching**: theme changes take effect immediately, no restart
- **Themes covered**: character creation, popups, menus, encyclopedia, conversations, inventory, party screen, map UI, escape menu, notifications

## Included Themes

**Vanilla cultures** (auto-bound to their kingdom):

| Theme    | Kingdom    | Primary               | Secondary             |
|----------|------------|-----------------------|-----------------------|
| Vlandia  | Vlandia    | Royal Gold (#FFD700)  | Dark Crimson (#8B0000)|
| Sturgia  | Sturgia    | Ice Blue (#00BFFF)    | Silver (#C0C0C0)      |
| Battania | Battania   | Forest Green (#228B22)| Saddle Brown (#8B4513)|
| Empire   | Empire ×3  | Imperial Purple (#800080) | Gold (#FFD700)    |
| Aserai   | Aserai     | Desert Orange (#FF8C00)| Midnight Blue (#191970)|
| Khuzait  | Khuzait    | Steppe Green (#9ACD32)| Sky Blue (#87CEEB)    |
| Naval    | Nord (DLC) | Ocean Blue (#4169E1)  | Silver (#C0C0C0)      |

**LOTR test themes** (sprite-enabled, bound to nearest vanilla culture):

| Theme       | Bound to | Sprites |
|-------------|----------|---------|
| Erebor      | sturgia  | yes (nine-slice frames + buttons) |
| Gondor      | empire   | yes |
| Rohan       | vlandia  | yes |
| Lothlorien  | battania | yes |
| Harad       | aserai   | yes |
| Rhun        | khuzait  | yes |
| Umbar       | aserai   | yes |

## Installation (for testers)

1. Copy the `Modules/BannerlordThemeSwitcher/` folder into your Bannerlord installation:
   `…/Mount & Blade II Bannerlord/Modules/BannerlordThemeSwitcher/`
2. Start the game via BLSE (Bannerlord Software Extender).
3. In the launcher, enable **Theme Switcher** below `Native`, `SandBoxCore`, `Sandbox`.
4. Optional: install **MCM** (Mod Configuration Menu) to switch themes manually.
5. Start a campaign or load a save — the UI should re-skin within ~1 second.

The mod ships as a single `BannerlordThemeSwitcher.dll` plus theme folders. No vanilla files are modified.

## Building from source

Requires:
- .NET Framework 4.7.2 SDK
- Environment variable `BANNERLORD_GAME_DIR` pointing at your Bannerlord install root
  (e.g. `D:\Steam\steamapps\common\Mount & Blade II Bannerlord`)

```
dotnet build BannerlordThemeSwitcher.csproj -c Release
```

DLL and assets auto-deploy to `$(BANNERLORD_GAME_DIR)/Modules/BannerlordThemeSwitcher/`. The game must be closed (BLSE locks the DLL).

## Requirements

- Bannerlord v1.3.0+
- BLSE
- Harmony (NuGet, bundled)
- MCM (optional, for the in-game settings panel)

## MCM Settings

| Setting | Effect |
|---------|--------|
| **Auto-switch by Kingdom** | When enabled, theme follows the player's kingdom (or culture if clanless). When disabled, the manual selection below is used. |
| **Manual Theme** | Theme dropdown — populated dynamically from the `Themes/` folder. |
| **Show Current Theme** (debug) | Prints the active theme name into the chat HUD on switch. |
| **Log Brush Lookups** (debug) | Writes every brush interception to the debug log. **Performance heavy** — leave off unless reporting a missing brush. |

## Color scheme system

Each theme defines a complete color palette that the mod applies in two ways:

1. **XML-based (precise)** — define per-brush layer colors in `GUI/Brushes/*.xml`:
   ```xml
   <BrushLayer Name="Default" ColorRef="Primary" />
   <Style Name="Default" FontColorRef="Text" />
   <Style Name="Hovered" FontColorRef="TextHighlight" />
   ```
2. **AutoTheme (pattern-based)** — set `<AutoTheme>true</AutoTheme>` in the manifest. The mod inspects every brush name and applies the right scheme color (`button*` → ButtonBackground, `frame*` → Border, etc.) plus an HSV-saturation boost to make the kingdom color visible on otherwise-grey vanilla brushes.

### ColorRef attributes

| XML Attribute        | Purpose                |
|----------------------|------------------------|
| `ColorRef`           | BrushLayer color       |
| `FontColorRef`       | Style font color       |
| `TextGlowColorRef`   | Style text glow        |
| `TextOutlineColorRef`| Style text outline     |

### Color categories

| Category    | Colors                                                                            | Purpose                          |
|-------------|-----------------------------------------------------------------------------------|----------------------------------|
| Primary     | Primary, Secondary, Tertiary                                                      | Theme identity                   |
| Text        | Text, TextMuted, TextHighlight, TextTitle, TextDisabled, TextOnPrimary            | All text                         |
| Background  | Background, BackgroundDark, BackgroundLight, BackgroundAccent, BackgroundHover, BackgroundSelected | Panels and containers |
| Border      | Border, BorderMuted, BorderHighlight, BorderSecondary                             | Frames, outlines                 |
| Button      | ButtonBackground, ButtonHover, ButtonPressed, ButtonDisabled, ButtonBorder        | Interactive elements             |
| State       | Success, Warning, Error, Info                                                     | Status indicators (menu UI only) |
| Special     | Gold, Experience, Health, Morale, Shadow, Glow                                    | Game-specific elements           |

## Sprite theming

A theme can ship its own sprites by dropping PNGs into `Themes/MyTheme/Sprites/`. They are loaded into atlases at runtime, registered in the engine's `SpriteData`, and replace the vanilla sprite of the same name. Cached per session — switching back to a previously-loaded theme is fast.

Minimal setup:

```
Themes/MyTheme/
├── ThemeManifest.xml          ← <Components Brushes="true" Sprites="true"/>
├── SpriteConfig.xml           ← optional: nine-slice border definitions
└── Sprites/
    ├── button_canvas_9.png    ← replaces vanilla "button_canvas_9"
    ├── frame_9.png
    └── icons/
        └── star.png           ← registered as "icons\star"
```

See [Examples/SpriteTheming/README.md](Examples/SpriteTheming/README.md) for the full workflow including nine-slice borders and pre-built sprite sheets.

## Creating a custom theme

### Quick start

1. Create folder `Themes/YourTheme/`
2. Add a `ThemeManifest.xml` (see template below)
3. Optionally drop PNGs into `Sprites/`
4. Restart the game (themes are discovered at module load) — the manifest itself is hot-reloadable via theme switch

### Theme manifest

```xml
<?xml version="1.0" encoding="utf-8"?>
<Theme>
  <Name>Your Theme</Name>   <!-- folder name used if omitted -->
  <Description>What it looks like</Description>
  <Author>Your Name</Author>
  <Version>1.0.0</Version>

  <!-- Start from a base culture's palette (optional) -->
  <BaseCulture>vlandia</BaseCulture>

  <!-- Auto-activate when the player joins these kingdoms -->
  <BoundKingdoms>
    <Kingdom>vlandia</Kingdom>
  </BoundKingdoms>

  <!-- Component opt-in (default: Brushes=true, Sprites=auto-detected) -->
  <Components Brushes="true" Sprites="true" />

  <!-- AutoTheme: re-tint untouched brushes via pattern + HSV boost -->
  <AutoTheme>true</AutoTheme>

  <ColorScheme>
    <Primary>#YourColor</Primary>
    <Secondary>#YourColor</Secondary>
    <!-- … all ~30 slots, see Examples/ColorSchemeTheme/ … -->
  </ColorScheme>
</Theme>
```

### Base cultures

Reuse a built-in palette via `<BaseCulture>`:

`vlandia` · `sturgia` · `battania` · `empire` · `aserai` · `khuzait` · `naval`

### Reference docs

- [Examples/ColorSchemeTheme/COLOR_SCHEME_REFERENCE.md](Examples/ColorSchemeTheme/COLOR_SCHEME_REFERENCE.md) — every color slot explained
- [Examples/BrushReference/BRUSH_REFERENCE.xml](Examples/BrushReference/BRUSH_REFERENCE.xml) — themeable vanilla brushes by category
- [Examples/SpriteTheming/README.md](Examples/SpriteTheming/README.md) — sprite workflow

## How it works

1. `ThemeManager` discovers themes from `Themes/*/ThemeManifest.xml` at load
2. `BrushTemplates` generates per-theme brush variants from each color scheme
3. Harmony patches `BrushFactory.GetBrush()` — when the UI asks for `ButtonBrush`, the patch returns `ButtonBrush.Vlandia` if Vlandia is active, else falls through to vanilla
4. `SpriteThemeManager` loads the theme's PNGs into atlases via reflection into the engine's `SpriteData`, with full unregister/re-register on switch
5. Theme activation is triggered by `OnSessionLaunched`, `OnNewGameCreated`, `OnGameLoaded`, `OnClanChangedKingdomEvent`, or manually via MCM

## Known quirks (intentional)

- **Battle/combat UI is excluded from re-tinting.** Health bars, troop cards, formation panels, morale/stamina meters and similar mission-time elements stay vanilla — re-tinting them via AutoTheme breaks readability and the engine often re-renders them outside our patch path. Filter lives in `Patches/BrushModifier.ShouldSkipBrush`.
- **Crests, sigils and icons stay vanilla** for the same reason (recognisability over theme consistency).
- **First theme switch on game start can show a 1-frame vanilla flash.** The theme is applied as soon as `SessionLaunched` fires; tints beyond that point are correct.
- **Sprite hot-reload requires switching themes** (manual via MCM, or kingdom change). PNGs are read from disk on cache miss.

## Reporting bugs

Please include:
1. Theme that was active (MCM → Show Current Theme = on)
2. Screenshot of the broken UI
3. The Bannerlord rgl_log file (`Documents/Mount and Blade II Bannerlord/logs/`)
4. Steps to reproduce — especially the screen/menu where it broke

For missing-color reports, also enable **Log Brush Lookups** for one quick repro and attach the resulting log line for the offending brush.

## Project structure

```
BannerlordThemeSwitcher/
├── ColorScheme.cs           # Color scheme data structure
├── DefaultColorSchemes.cs   # Built-in kingdom palettes
├── BrushTemplates.cs        # Dynamic brush XML generation
├── Theme.cs                 # Theme data model
├── ThemeManager.cs          # Theme discovery, loading, switching
├── Settings.cs              # MCM settings
├── SubModule.cs             # Entry point
├── Patches/                 # Harmony patches (BrushFactory, BrushModifier, …)
├── Behaviors/               # Campaign behaviors (kingdom change, save load)
├── Sprites/                 # SpriteThemeManager / Loader / Registrar / Cache
├── Themes/<Kingdom>/        # Built-in + LOTR test themes
├── Examples/                # Author-facing docs and templates
└── tools/                   # One-shot dev helper scripts (not shipped)
```

## License

MIT
