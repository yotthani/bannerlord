# FaceLearner v1.2.0 - Tabbed UI & Character Editor

**Date**: January 2026  
**Status**: Major UI Overhaul - Tab-Based Navigation

---

## New Feature: Tabbed User Interface

The UI has been completely redesigned with **three tabs** to serve different user needs:

```
┌─────────────────────────────────────────────────────┐
│  FACELEARNER                              v1.2.0    │
├─────────────────────────────────────────────────────┤
│  [🎭 CREATE] [📚 LEARN] [⚙ SETTINGS]               │
├─────────────────────────────────────────────────────┤
```

### Tab 1: CREATE (Default - for most users)

The main tab for **character creation** with full customization:

```
┌────────────────┐  ┌─────────────────────────────────┐
│                │  │ Gender: [Male/Female]           │
│   3D Preview   │  │                                 │
│                │  │ ─── BODY ───                    │
│                │  │ Height:    [────●────]  67%     │
│                │  │ Build:     [────●────]  50%     │
│                │  │ Weight:    [────●────]  45%     │
│                │  │ Age:       [────●────]  32y     │
├────────────────┤  │                                 │
│ Source: img.jpg│  │ ─── SKIN ───                    │
│ [Browse...]    │  │ Skin Tone: [────●────]  Medium  │
│                │  │                                 │
│ [✨ Generate]  │  │ ─── HAIR ───                    │
└────────────────┘  │ Style:   [◀] Style 3  [▶]      │
                    │ Color:   [◀] Brown    [▶]      │
                    │ Beard:   [◀] Style 1  [▶]      │
                    │                                 │
                    │ ─── EYES ───                    │
                    │ Color:   [◀] Hazel    [▶]      │
                    │                                 │
                    │ ─── FACE (GENERATED) ───        │
                    │ 🔒 Face morphs locked           │
                    │ [🔓 Unlock Face]                │
                    └─────────────────────────────────┘

[✓ Apply] [📤 Export] [📥 Import]
```

**Features:**
- Full body customization (Height, Build, Weight, Age)
- Skin tone slider with preset names
- Hair style cycling with color presets
- Beard styles (male only)
- Eye color presets
- Face locking for ML-generated faces
- Export/Import character data as XML

### Tab 2: LEARN (for power users)

The training interface for ML model improvement:

```
┌──────────────────┐  ┌─────────────────┐  ┌──────────────┐
│ Learning Controls│  │ Learning Preview│  │     Log      │
├──────────────────┤  ├─────────────────┤  ├──────────────┤
│ [Initialize]     │  │                 │  │ [scrollable  │
│                  │  │   3D Preview    │  │  log output] │
│ ─── DATASET ─── │  │                 │  │              │
│ [▶ Start]        │  │                 │  │              │
│ [■ Stop]         │  │ Score: 78.5%    │  │              │
│                  │  │                 │  │              │
│ ─── STATS ───   │  │ Status: Running │  │              │
│ 25 shared       │  │                 │  │              │
│ 45 tree nodes   │  └─────────────────┘  └──────────────┘
└──────────────────┘
```

### Tab 3: SETTINGS

Configuration and information:

- Model status and download
- Knowledge statistics and reset
- About information

---

## New: CharacterData Class

Complete character representation for export/import:

```csharp
public class CharacterData
{
    // Meta
    public string Name { get; set; }
    public bool IsFaceGenerated { get; set; }  // Locks face if true
    public float GeneratedScore { get; set; }
    
    // Face (62 morphs)
    public float[] FaceMorphs { get; set; }
    
    // Body
    public float Height { get; set; }      // 0-1
    public float Build { get; set; }       // 0-1 (slim to muscular)
    public float Weight { get; set; }      // 0-1
    public float Age { get; set; }         // 0-1 (18-68 years)
    
    // Appearance
    public float SkinTone { get; set; }    // 0-1
    public int HairStyleIndex { get; set; }
    public float HairColorR/G/B { get; set; }
    public int BeardStyleIndex { get; set; }
    public float EyeColorR/G/B { get; set; }
    
    // Export/Import
    void ExportToXml(string path);
    static CharacterData ImportFromXml(string path);
}
```

---

## Face Lock System

When a face is **ML-generated**:
- Face morphs are **locked** (cannot be manually edited)
- Shows "🔒 Face morphs locked (ML-generated)"
- User can **unlock** to allow manual editing
- Unlocking removes the "generated" status

**Why lock?**
- ML-optimized morphs work together as a system
- Manual changes could break the optimization
- Users can still customize body, hair, skin, eyes

---

## Color Presets

### Skin Tones
| Preset | Value |
|--------|-------|
| Very Light | 0.10 |
| Light | 0.25 |
| Medium Light | 0.40 |
| Medium | 0.50 |
| Medium Dark | 0.60 |
| Dark | 0.75 |
| Very Dark | 0.90 |

### Hair Colors
Black, Dark Brown, Brown, Light Brown, Auburn, Red, Ginger, Blonde, Platinum, White, Grey

### Eye Colors
Brown, Dark Brown, Hazel, Green, Blue, Light Blue, Grey, Amber

---

## Export Format

Characters are exported as XML files:

```xml
<?xml version="1.0"?>
<CharacterData>
  <Name>Aragorn_20260120_143052</Name>
  <IsFaceGenerated>true</IsFaceGenerated>
  <GeneratedScore>0.78</GeneratedScore>
  <FaceMorphs>0.42,0.35,0.18,...</FaceMorphs>
  <Height>0.67</Height>
  <Build>0.55</Build>
  <SkinTone>0.45</SkinTone>
  <HairStyleIndex>5</HairStyleIndex>
  ...
</CharacterData>
```

---

## File Structure

```
FaceLearner/
├── Core/
│   ├── CharacterData.cs         # NEW: Character data class
│   ├── FaceLearnerVM.cs         # Now partial class
│   ├── FaceLearnerVM_Tabs.cs    # NEW: Tab UI extensions
│   └── ...
├── GUI/
│   ├── Prefabs/
│   │   ├── FaceLearnerScreen.xml        # NEW: Tabbed UI
│   │   └── FaceLearnerScreen_Legacy.xml # OLD: Previous UI
│   └── Brushes/
│       └── FaceLearnerBrushes.xml       # Extended with new brushes
├── Exports/                     # NEW: Character exports folder
└── Images/                      # NEW: Source images folder
```

---

## New Brushes

Added for the tabbed UI:
- `FaceLearner.Tab` / `FaceLearner.Tab.Text`
- `FaceLearner.Slider.Filler` / `FaceLearner.Slider.Handle`
- `FaceLearner.Label` / `FaceLearner.Value`
- `FaceLearner.Toggle`
- `FaceLearner.Button.Small`
- `FaceLearner.Score.Text`
- `FaceLearner.Stats.Text`
- `FaceLearner.Text.Warning`

---

## Version History

| Version | Feature |
|---------|---------|
| v1.0.0 | Clean architecture, dead code removal |
| v1.1.0 | Two-layer knowledge system |
| **v1.2.0** | **Tabbed UI, Character Editor, Export/Import** |

---

## Usage Flow

### For Character Creation (most users):

1. Open FaceLearner → **CREATE** tab is active
2. Click **Browse Image** to select source photo
3. Click **Generate Face** → ML optimizes face morphs
4. Customize **Body**, **Skin**, **Hair**, **Eyes** as desired
5. Click **Apply** to apply to game character
6. Click **Export** to save character for later

### For ML Training (power users):

1. Switch to **LEARN** tab
2. Click **Initialize** to load models
3. Click **Start Training** to begin dataset learning
4. Monitor progress in log panel
5. Training improves future face generation quality
