# Ring UI Transfer Status

## Overview
Transfer of Ring Orbital UI from React mock to Bannerlord Gauntlet UI.

## Current Status: SPRITES DISABLED (pending compilation)

The custom ring sprites have been temporarily disabled because Bannerlord requires
sprite sheets to be compiled before use. The SpriteParts XML was causing game crashes.

**Current workaround:** Using built-in Bannerlord sprites (`MapBar\mapbar_center_circle_frame`)

### To Enable Custom Sprites Later:
1. Use Bannerlord's resource compiler to create sprite sheets from the PNG files
2. Place compiled sprite sheets in `GUI/SpriteParts/`
3. Update `OrbitalRingSystem.cs` GetSpriteNameForRing() method
4. Update `RingScreen.xml` table background sprite

### Ring Sprite Images (for future use)
The original ring PNG images should be placed in the mod's asset pipeline for compilation:
- ring_one.png
- ring_elven_narya.png, ring_elven_nenya.png, ring_elven_vilya.png
- ring_dwarf_1.png through ring_dwarf_7.png  
- ring_mortal_1.png through ring_mortal_9.png
- ring_table_with_tools.png, ring_table_clean.png

## Implementation Files

| File | Purpose |
|------|---------|
| OrbitalRingSystem.cs | Position calculations, OrbitalRingVM |
| RingScreenVM.cs | Animation state, rotation logic |
| GauntletRingScreen.cs | Screen host, OnFrameTick |
| RingScreen.xml | Gauntlet UI layout |

## Mock vs Bannerlord Comparison

| Feature | Mock (React) | Bannerlord Implementation |
|---------|--------------|--------------------------|
| Animation | requestAnimationFrame | OnFrameTick(float dt) |
| Position | transform: translate() | PositionXOffset/YOffset |
| Z-order | CSS z-index | Widget order in sorted list |
| Easing | CSS cubic-bezier | EaseInOutCubic() in C# |
| State transitions | CSS transition | VisualDefinition |
| Hover effects | CSS :hover | VisualDefinition states |
| Ring sprites | SVG circles | Built-in game sprites (placeholder) |
| Table | SVG gradients | Solid color background (placeholder) |

## Testing Checklist
- [ ] Game loads without crash
- [ ] Ring screen opens via R key on map
- [ ] Rotation animation smoothness (400ms cubic easing)
- [ ] Auto-rotate when clicking back rings (500ms)
- [ ] Z-order layering (front rings above One Ring)
- [ ] Ring positions calculated correctly
- [ ] Hover glow animation on rings
- [ ] Button press feedback
- [ ] Buttons disabled during animation
