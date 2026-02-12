# Color Scheme System Reference

## Overview

The Bannerlord Theme Switcher uses a **centralized color scheme system** that allows theme authors to define all their colors in one place. Instead of manually specifying colors in dozens of brush definitions, you define your color palette once in the `<ColorScheme>` section of your theme manifest, and all UI elements are automatically styled based on your palette.

## Benefits

- **Easy customization**: Change one color value, update entire UI
- **Consistent design**: All elements use the same coordinated palette
- **Less maintenance**: No need to update multiple brush files
- **Quick theming**: Create a complete theme by defining ~30 colors

---

## Color Slots Reference

### Primary Colors

The main colors that define your theme's identity.

| Slot | Purpose | Example Usage |
|------|---------|---------------|
| `Primary` | Main accent color | Buttons, highlights, frames, active states |
| `Secondary` | Complementary accent | Secondary buttons, alternative highlights |
| `Tertiary` | Additional variety | Info panels, tertiary actions |

### Text Colors

Colors for all text elements in the UI.

| Slot | Purpose | Example Usage |
|------|---------|---------------|
| `Text` | Primary readable text | Body text, descriptions, values |
| `TextMuted` | Less important text | Hints, footnotes, inactive labels |
| `TextHighlight` | Emphasized text | Hovered items, important info |
| `TextTitle` | Headers and titles | Panel titles, section headers |
| `TextOnPrimary` | Text on primary-colored backgrounds | Button labels, badge text |
| `TextDisabled` | Inactive/disabled text | Unavailable options |

### Background Colors

Colors for panels, cards, and container backgrounds.

| Slot | Purpose | Example Usage |
|------|---------|---------------|
| `Background` | Main panel background | Popup backgrounds, cards |
| `BackgroundDark` | Darker contrast background | Modal overlays, footers |
| `BackgroundLight` | Lighter elevated background | Floating panels, headers |
| `BackgroundAccent` | Primary-tinted background | Subtle highlighting |
| `BackgroundHover` | Hover state background | List item hover |
| `BackgroundSelected` | Selected/active background | Selected list items, active tabs |

### Border Colors

Colors for frames, dividers, and outlines.

| Slot | Purpose | Example Usage |
|------|---------|---------------|
| `Border` | Standard border color | Panel frames, input borders |
| `BorderMuted` | Subtle/dimmed border | Dividers, inactive borders |
| `BorderHighlight` | Emphasized border | Focus states, important frames |
| `BorderSecondary` | Secondary border color | Alternate sections |

### Button Colors

Dedicated colors for interactive button elements.

| Slot | Purpose | Example Usage |
|------|---------|---------------|
| `ButtonBackground` | Default button fill | Normal state buttons |
| `ButtonHover` | Hovered button fill | Mouse-over buttons |
| `ButtonPressed` | Pressed button fill | Click/active buttons |
| `ButtonDisabled` | Disabled button fill | Unavailable buttons |
| `ButtonBorder` | Button frame/border | Button outlines |

### State Colors

Colors for status indicators and feedback.

| Slot | Purpose | Example Usage |
|------|---------|---------------|
| `Success` | Positive/success state | Completed actions, positive values |
| `Warning` | Warning state | Caution messages, low resources |
| `Error` | Error/danger state | Failed actions, critical warnings |
| `Info` | Informational state | Tips, neutral information |

### Special Colors

Purpose-specific colors for game elements.

| Slot | Purpose | Example Usage |
|------|---------|---------------|
| `Gold` | Currency display | Gold amounts, trade values |
| `Experience` | Progress/XP | Skill progress, level-ups |
| `Health` | Health indicators | HP bars, damage numbers |
| `Morale` | Morale display | Party morale, troop happiness |
| `Shadow` | Shadow effects | Text shadows, drop shadows |
| `Glow` | Glow effects | Highlighted items, auras |

---

## Color Format

Colors use hexadecimal format with alpha channel:

```
#RRGGBBAA
```

- `RR` = Red (00-FF)
- `GG` = Green (00-FF)
- `BB` = Blue (00-FF)
- `AA` = Alpha/opacity (00=transparent, FF=opaque)

### Examples

```xml
<Primary>#FFD700FF</Primary>      <!-- Gold, fully opaque -->
<Background>#1A1A1A99</Background> <!-- Dark gray, 60% opaque -->
<Border>#FFD700AA</Border>        <!-- Gold, 67% opaque -->
<Shadow>#00000088</Shadow>        <!-- Black, 53% opaque -->
```

### Common Alpha Values

| Hex | Decimal | Description |
|-----|---------|-------------|
| `FF` | 255 | Fully opaque (100%) |
| `CC` | 204 | Mostly opaque (80%) |
| `AA` | 170 | Semi-opaque (67%) |
| `88` | 136 | Half-transparent (53%) |
| `66` | 102 | Mostly transparent (40%) |
| `44` | 68 | Very transparent (27%) |
| `22` | 34 | Nearly invisible (13%) |
| `00` | 0 | Fully transparent (0%) |

---

## Base Cultures

You can start with a predefined color scheme by specifying a base culture:

```xml
<BaseCulture>vlandia</BaseCulture>
```

Available base cultures:
- `vlandia` - Royal Gold & Crimson
- `sturgia` - Ice Blue & Silver
- `battania` - Forest Green & Brown
- `empire` - Imperial Purple & Gold
- `aserai` - Desert Orange & Blue
- `khuzait` - Steppe Green & Sky Blue
- `naval` - Ocean Blue & Silver

The base culture provides default values that you can then override in your `<ColorScheme>` section.

---

## Complete Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<Theme>
  <n>Crimson Legion</n>
  <Description>A dark theme with blood red accents</Description>
  <Author>ThemeAuthor</Author>
  <Version>1.0.0</Version>
  
  <ColorScheme>
    <!-- Primary - Blood Red theme -->
    <Primary>#DC143C</Primary>
    <Secondary>#8B0000</Secondary>
    <Tertiary>#CD5C5C</Tertiary>
    
    <!-- Text - Light on dark -->
    <Text>#F5F5F5EE</Text>
    <TextMuted>#A0A0A0</TextMuted>
    <TextHighlight>#FF6B6B</TextHighlight>
    <TextTitle>#DC143C</TextTitle>
    <TextOnPrimary>#FFFFFF</TextOnPrimary>
    <TextDisabled>#555555</TextDisabled>
    
    <!-- Backgrounds - Very dark with red tint -->
    <Background>#1A0A0A99</Background>
    <BackgroundDark>#0F0505BB</BackgroundDark>
    <BackgroundLight>#2A1515AA</BackgroundLight>
    <BackgroundAccent>#DC143C18</BackgroundAccent>
    <BackgroundHover>#DC143C35</BackgroundHover>
    <BackgroundSelected>#DC143C55</BackgroundSelected>
    
    <!-- Borders - Red frames -->
    <Border>#DC143CAA</Border>
    <BorderMuted>#DC143C55</BorderMuted>
    <BorderHighlight>#FF6B6BFF</BorderHighlight>
    <BorderSecondary>#8B0000AA</BorderSecondary>
    
    <!-- Buttons -->
    <ButtonBackground>#DC143C20</ButtonBackground>
    <ButtonHover>#DC143C45</ButtonHover>
    <ButtonPressed>#DC143C70</ButtonPressed>
    <ButtonDisabled>#3A2A2A44</ButtonDisabled>
    <ButtonBorder>#DC143CCC</ButtonBorder>
    
    <!-- States -->
    <Success>#32CD32</Success>
    <Warning>#FFA500</Warning>
    <Error>#FF0000</Error>
    <Info>#4169E1</Info>
    
    <!-- Special -->
    <Gold>#FFD700</Gold>
    <Experience>#9370DB</Experience>
    <Health>#DC143C</Health>
    <Morale>#32CD32</Morale>
    <Shadow>#0F050588</Shadow>
    <Glow>#DC143C66</Glow>
  </ColorScheme>
</Theme>
```

---

## Tips for Theme Authors

### 1. Start with a Base Culture
Pick the closest base culture to your vision, then customize from there.

### 2. Maintain Contrast
Ensure text is readable against backgrounds:
- Light text on dark backgrounds
- Dark text on light/bright backgrounds

### 3. Use Alpha for Layering
Use semi-transparent colors for backgrounds to allow underlying content to show through subtly.

### 4. Test All States
Remember to set distinct colors for:
- Default state
- Hover state  
- Selected/Active state
- Disabled state

### 5. Consider Accessibility
- Avoid pure red/green combinations (color blindness)
- Maintain sufficient contrast ratios
- Test with different monitor settings

### 6. Keep It Cohesive
Use variations of your primary colors throughout rather than introducing many unrelated colors.

---

## File Locations

Place your theme in:
```
Modules/BannerlordThemeSwitcher/Themes/YourThemeName/ThemeManifest.xml
```

Or in the Examples folder:
```
Modules/BannerlordThemeSwitcher/Examples/YourThemeName/ThemeManifest.xml
```
