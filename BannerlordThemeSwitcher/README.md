# Bannerlord Theme Switcher

A mod for Mount & Blade II: Bannerlord that enables runtime UI theme switching with a comprehensive color scheme system. Themes automatically change based on the player's kingdom.

## Features

- **Centralized Color Schemes**: Define ~30 colors once, theme the entire UI
- **Kingdom-Bound Themes**: UI automatically changes when you join a kingdom
- **Manual Override**: Select themes via MCM settings
- **No Vanilla Modification**: Uses brush interception, vanilla files untouched
- **Runtime Switching**: Themes change instantly without restart
- **Complete UI Coverage**: Character creation, popups, menus, inventory, party screen, map UI, and more

## Included Themes

| Theme | Kingdom | Primary Color | Secondary Color |
|-------|---------|---------------|-----------------|
| Vlandia | Vlandia | Royal Gold (#FFD700) | Dark Crimson (#8B0000) |
| Sturgia | Sturgia | Ice Blue (#00BFFF) | Silver (#C0C0C0) |
| Battania | Battania | Forest Green (#228B22) | Saddle Brown (#8B4513) |
| Empire | Empire (all 3) | Imperial Purple (#800080) | Gold (#FFD700) |
| Aserai | Aserai | Desert Orange (#FF8C00) | Midnight Blue (#191970) |
| Khuzait | Khuzait | Steppe Green (#9ACD32) | Sky Blue (#87CEEB) |
| Naval | Nord (DLC) | Ocean Blue (#4169E1) | Silver (#C0C0C0) |

## Color Scheme System

Each theme defines a complete color palette that can be applied in two ways:

### Two Theming Approaches

**1. XML-Based (Precise Control)**
Define brushes in XML files using `ColorRef` attributes:
```xml
<BrushLayer Name="Default" ColorRef="Primary" />
<Style Name="Default" FontColorRef="Text" />
<Style Name="Hovered" FontColorRef="TextHighlight" />
```

**2. AutoTheme (Quick & Easy)**
Enable pattern-based automatic theming:
```xml
<AutoTheme>true</AutoTheme>
```
The mod automatically applies colors based on brush name patterns (e.g., "button", "frame", "text").

### ColorRef Attributes

In brush XMLs, use these attributes to reference scheme colors:

| XML Attribute | Purpose |
|--------------|---------|
| `ColorRef` | BrushLayer color |
| `FontColorRef` | Style font color |
| `TextGlowColorRef` | Style text glow |
| `TextOutlineColorRef` | Style text outline |

### Color Categories

| Category | Colors | Purpose |
|----------|--------|---------|
| **Primary** | Primary, Secondary, Tertiary | Main theme identity colors |
| **Text** | Text, TextMuted, TextHighlight, TextTitle, TextDisabled | All text elements |
| **Background** | Background, BackgroundDark, BackgroundLight, BackgroundHover, BackgroundSelected | Panel and container fills |
| **Border** | Border, BorderMuted, BorderHighlight, BorderSecondary | Frames and outlines |
| **Button** | ButtonBackground, ButtonHover, ButtonPressed, ButtonDisabled, ButtonBorder | Interactive buttons |
| **State** | Success, Warning, Error, Info | Status indicators |
| **Special** | Gold, Experience, Health, Morale, Shadow, Glow | Game-specific elements |

### Example Color Scheme (Vlandia)

```xml
<ColorScheme>
  <Primary>#FFD700</Primary>       <!-- Royal Gold -->
  <Secondary>#8B0000</Secondary>   <!-- Dark Crimson -->
  <Text>#FFFFFFEE</Text>           <!-- White text -->
  <Background>#1A150A99</Background> <!-- Dark warm tint -->
  <Border>#FFD700AA</Border>       <!-- Gold frames -->
  <ButtonHover>#FFD70050</ButtonHover> <!-- Hover state -->
  <Success>#32CD32</Success>       <!-- Green for positive -->
  <Gold>#FFD700</Gold>             <!-- Currency color -->
</ColorScheme>
```

## Installation

1. Build the solution (requires `BANNERLORD_GAME_DIR` environment variable)
2. DLL and assets auto-deploy to Modules folder
3. Enable in launcher
4. (Optional) Install MCM for manual theme selection

## Requirements

- Bannerlord v1.3.0+
- Harmony (via NuGet)
- MCM (optional, for settings menu)

## Creating Custom Themes

### Quick Start

1. Create folder: `Themes/YourTheme/`
2. Create `ThemeManifest.xml` with your color scheme
3. The mod generates all themed brushes automatically

### Theme Manifest Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<Theme>
  <n>Your Theme Name</n>
  <Description>Theme description</Description>
  <Author>Your Name</Author>
  <Version>1.0.0</Version>
  
  <!-- Optional: Start from a base culture's colors -->
  <BaseCulture>vlandia</BaseCulture>
  
  <!-- Auto-activate when joining these kingdoms -->
  <BoundKingdoms>
    <Kingdom>vlandia</Kingdom>
  </BoundKingdoms>
  
  <!-- Your complete color palette -->
  <ColorScheme>
    <Primary>#YourColor</Primary>
    <Secondary>#YourColor</Secondary>
    <!-- ... all 30+ color slots ... -->
  </ColorScheme>
</Theme>
```

### Base Cultures

Start with a predefined palette by setting `<BaseCulture>`:
- `vlandia` - Gold & Crimson
- `sturgia` - Ice Blue & Silver
- `battania` - Forest Green & Brown
- `empire` - Purple & Gold
- `aserai` - Orange & Blue
- `khuzait` - Green & Sky Blue
- `naval` - Ocean Blue & Silver

### Documentation

See the `Examples/` folder for:
- `ColorSchemeTheme/` - Complete color scheme reference and template
- `BrushReference/` - All themeable brushes by category
- `SpriteTheming/` - How to create custom sprites

## How It Works

1. Theme manifests define color schemes
2. `BrushTemplates.cs` generates brush XML from color schemes
3. Harmony patches `BrushFactory.GetBrush()`
4. When UI requests "ButtonBrush", patch checks current theme
5. Returns "ButtonBrush.Vlandia" if Vlandia theme active
6. Falls back to vanilla if themed version doesn't exist

## MCM Settings

- **Auto-switch by Kingdom**: Enable/disable automatic switching
- **Manual Theme**: Select theme when auto-switch disabled
- **Debug options**: Show theme name, log brush lookups

## Project Structure

```
BannerlordThemeSwitcher/
├── ColorScheme.cs           # Color scheme data structure
├── DefaultColorSchemes.cs   # Predefined kingdom color palettes
├── BrushTemplates.cs        # Dynamic brush generation
├── Theme.cs                 # Theme data model
├── ThemeManager.cs          # Theme loading and switching
├── Settings.cs              # MCM settings
├── SubModule.cs             # Entry point
├── Patches/                 # Harmony patches
├── Behaviors/               # Campaign behaviors
├── Themes/[Kingdom]/        # Built-in themes
├── GUI/Brushes/             # Static brush overrides
└── Examples/                # Documentation & templates
```

## UI Elements Themed

The color scheme system themes all major UI areas:

- **Character Creation**: Culture buttons, stage titles, descriptions
- **Popups & Dialogs**: Frames, titles, buttons, info/warning messages
- **Standard UI**: Headers, tabs, scrollbars, checkboxes, sliders
- **Escape Menu**: Background, frames, buttons
- **Encyclopedia**: Links, entries, dividers
- **Conversations**: Dialogue frames, options, persuasion bars
- **Face Generation**: Panels, categories, sliders
- **Notifications**: Success/warning/error messages, chat
- **Inventory**: Item slots, gold display, weight
- **Party Screen**: Troop cards, morale, food indicators
- **Map UI**: Bar, tooltips, settlement names, time/gold display

## License

MIT
