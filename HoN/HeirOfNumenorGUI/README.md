# Heir of Numenor GUI Module

This is a **GUI-only module** for use with the Bannerlord Mod Editor.

## Purpose

The main HeirOfNumenor module contains Harmony patches and C# code that can crash 
the mod editor. This separate GUI module contains only the visual assets:

- Sprite definitions (`HeirOfNumenorSpriteData.xml`)
- UI Prefabs (RingScreen, FiefManagement, etc.)
- Brushes
- Sprite sheet placeholders

## Usage

### For Mod Editor Work:
1. Place this `HeirOfNumenorGUI` folder in your `Modules` directory
2. Open the Bannerlord Mod Editor
3. Load only `HeirOfNumenorGUI` (not the main HeirOfNumenor)
4. Edit sprites, prefabs, and brushes without crashes

### For Playing the Game:
- Use the main `HeirOfNumenor` module (it contains its own copy of GUI files)
- You do NOT need both modules installed to play

## Workflow

1. Make GUI changes in the editor using this module
2. Copy changed files to the main `HeirOfNumenor/GUI/` folder
3. Test in-game with the main module

## File Structure

```
HeirOfNumenorGUI/
├── SubModule.xml              (No DLLs referenced)
├── GUI/
│   ├── HeirOfNumenorSpriteData.xml
│   ├── Brushes/
│   │   └── MapBar.xml
│   ├── Prefabs/
│   │   ├── RingScreen.xml
│   │   ├── FiefManagement.xml
│   │   ├── PartyTroopTuple.xml
│   │   ├── TroopStatusIndicators.xml
│   │   ├── PresetsOverlay.xml
│   │   └── OOBButtonsOverlay.xml
│   └── SpriteParts/
│       └── ui_ring_system/
│           └── (place .tpac files here)
```

## Sprite Compilation

When you compile sprites with the mod tools:
1. Work in this module's `GUI/SpriteParts/` folder
2. The tools will generate `.tpac` files
3. Copy both the `.tpac` files AND updated `HeirOfNumenorSpriteData.xml` to the main module
