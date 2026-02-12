# LOTRAOM Faction Map - Bannerlord 1.3.x Mod

## What this does

Replaces the vanilla culture selection screen during character creation
with an interactive Middle-earth map. Players click a region on the map
to choose their starting faction.

```
+-----------------------------------------------------------+
|              CHOOSE YOUR REALM                            |
|  +------------------------+--------------------------+    |
|  |                        |                          |    |
|  |    [Interactive Map]   |   Gondor                 |    |
|  |                        |                          |    |
|  |    Hover -> highlight  |   Das Sudkonigreich      |    |
|  |    Click -> select     |   der Dunedain...        |    |
|  |                        |                          |    |
|  |    +---------------+   |   * Turmwache            |    |
|  |    |   Gondor      |   |   * Schwanenritter       |    |
|  |    +---------------+   |   * Numenorisches Erbe   |    |
|  +------------------------+--------------------------+    |
|              [ Auswahl bestatigen ]                        |
+-----------------------------------------------------------+
```

## 18 Factions of Middle-Earth

| ID | Region | Description |
|----|--------|-------------|
| 2 | Dunland & Nebelberge | Misty Mountains and wild Dunlendings |
| 3 | Rhovanion & Rhun | Wilderlands from Dale to Sea of Rhun |
| 4 | Dusterwald | Thranduil's woodland realm |
| 5 | Haradwaith | Southern lands - Mumakil and desert warriors |
| 6 | Eryan | Far eastern realm beyond the maps |
| 7 | Asamaniya | Northeastern steppes - horse nomads |
| 8 | Mordor | The Black Land - Barad-dur and Gorgoroth |
| 9 | Rohan | Riddermark - land of the horse lords |
| 10 | Khand | Land of the Variags |
| 11 | Gondor | South Kingdom - Minas Tirith |
| 12 | Eisenberge | Iron Hills - Dain's dwarven fortress |
| 13 | Imladris | Rivendell - Elrond's hidden valley |
| 14 | Mithlond | Grey Havens - gateway to the West |
| 15 | Lindon | Elven coastal realm west of the Blue Mountains |
| 16 | Dale | Rebuilt trading city at Erebor's foot |
| 17 | Dol Guldur | Necromancer's fortress in southern Mirkwood |
| 18 | Gundabad | Orc stronghold under northern Misty Mountains |
| 19 | Umbar | Corsair city - Black Numenoreans and pirates |

## Architecture

```
Mouse Position
    |
    v
Convert to map UV coordinates (normalized 0..1)
    |
    v
HitMapLoader.SampleAtUV(u, v)
    |  reads map_hitmap_512.bin or map_hitmap_1024.bin
    |  returns: 0 = ocean/border, 2-19 = faction ID
    |
    v
FactionSelectionVM.HoveredRegionId
    |
    +-> UI: show region name, highlight
    |
    v (on click)
FactionSelectionVM.SelectedRegionId
    |
    +-> UI: show faction info panel + landmarks
    |
    v (on confirm)
RegionConfig -> CultureObject
    |
    v
CharacterCreation.SetCulture() + GoToNextStage()
```

## File Structure

```
Modules/LOTRAOM.FactionMap/
+-- SubModule.xml
+-- LOTRAOM.FactionMap.csproj
|
+-- bin/Win64_Shipping_Client/
|   +-- LOTRAOM.FactionMap.dll        <- compiled output
|
+-- GUI/
|   +-- Prefabs/
|   |   +-- FactionSelectionMap.xml   <- Gauntlet layout
|   |
|   +-- SpriteData/
|       +-- ui_factionmap/
|       |   +-- ui_factionmap.xml     <- sprite registration
|       +-- FactionMap/
|           +-- map_base.png          <- 2048x1423 visible map
|           +-- map_hitmap_512.bin    <- hit detection (binary, 512x356)
|           +-- map_hitmap_1024.bin   <- hit detection (binary, 1024x712)
|           +-- highlight_*.png       <- hover overlays
|           +-- selected_*.png        <- selection overlays
|
+-- Source/
|   +-- SubModule.cs                  <- entry point, Harmony init
|   +-- HitMapLoader.cs               <- loads binary hit map
|   +-- RegionConfig.cs               <- region ID -> culture mapping (18 factions)
|   +-- LandmarkConfig.cs             <- city/landmark markers
|   +-- FactionSelectionVM.cs         <- ViewModel (hover, selection, traits, landmarks)
|   +-- FactionMapLayer.cs            <- GauntletLayer overlay + mouse handling
|   +-- CharacterCreationPatches.cs   <- Harmony patches for BL 1.3.x
|
+-- Tools/
    +-- generate_hitmap.py            <- generates binary hit maps from HTML coords
    +-- GenerateHitMap.cs             <- C# version (standalone)
```

## Building

### Prerequisites
- Visual Studio 2022 or Rider
- Bannerlord 1.3.x installed
- Bannerlord.Harmony mod installed

### Environment
Set one of:
```
set BANNERLORD_GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord
```
Or edit `GameDir` in the .csproj directly.

### Build
```
dotnet build LOTRAOM.FactionMap.csproj -c Release
```
Output DLL is auto-copied to `Modules/LOTRAOM.FactionMap/bin/Win64_Shipping_Client/`.

## Regenerating Hit Maps

The hit maps define clickable regions for each faction. To regenerate after
changing faction boundaries:

```bash
cd Tools
python generate_hitmap.py
```

This generates:
- `map_hitmap_512.bin` (182 KB) - low resolution
- `map_hitmap_1024.bin` (729 KB) - medium resolution

## Sprite Compilation

The map texture needs to be compiled into a sprite sheet for Gauntlet.

### Option A: TaleWorlds Sprite Compiler
1. Place `map_base.png` in `GUI/SpriteData/FactionMap/`
2. Run the sprite compiler (part of BL modding tools)
3. It generates the sprite sheet in `GUI/SpriteParts/`

### Option B: Manual (for quick testing)
Replace the `Sprite="FactionMap_Base"` in the XML with any existing
vanilla sprite to verify the hit detection works, then add the proper
sprite later.

## Culture Mapping

The `RegionConfig.cs` maps hit map region IDs to Bannerlord culture IDs.

For **vanilla testing**, regions are mapped to vanilla cultures:

| Faction | Vanilla Fallback | LOTR Culture (TODO) |
|---------|-----------------|---------------------|
| Gondor | vlandia | gondor |
| Rohan | vlandia | rohan |
| Mordor | battania | mordor |
| Dol Guldur | battania | dol_guldur |
| Gundabad | battania | gundabad |
| Haradwaith | aserai | harad |
| Umbar | aserai | umbar |
| Khand | aserai | khand |
| Eryan | aserai | eryan |
| Dusterwald | empire | woodland_realm |
| Imladris | empire | imladris |
| Lindon | empire | lindon |
| Mithlond | empire | mithlond |
| Rhovanion | khuzait | rhovanion |
| Asamaniya | khuzait | asamaniya |
| Eisenberge | sturgia | iron_hills |
| Dale | sturgia | dale |
| Dunland | battania | dunland |

When integrating with LOTRAOM, replace the `CultureId` values in
`RegionConfig.BuildRegions()` with your actual culture StringIds
from `spcultures.xml`.

## Landmarks

The mod includes a landmark system showing important locations on the map:
- **Capitals**: Minas Tirith, Edoras, Barad-dur, Erebor, etc.
- **Fortresses**: Helm's Deep, Minas Morgul, Orthanc
- **Ruins**: Moria, Osgiliath, Weathertop
- **Other**: Lothlórien, Fangorn, Dead Marshes

Configure in `LandmarkConfig.cs`. Landmarks can be displayed on the map
and in the faction info panel.

## Known Limitations / TODO

1. **Sprite compilation** - The map texture needs sprite sheet compilation
   for proper display. Without it, the map image won't show (but hit
   detection still works).

2. **Map area bounds** - The mouse->UV conversion in `FactionMapLayer.cs`
   uses approximate screen coordinates. Needs calibration to your actual
   screen resolution / UI scale.

3. **Faction border refinement** - Umbar, Dale, Dol Guldur, and Gundabad
   borders are approximate. May need fine-tuning based on actual LOTR maps.

4. **Per-region overlays** - Highlight/selected PNG overlays are generated
   but need wiring into Gauntlet XML (requires sprite sheet compilation).

## Version History

| Version | Changes |
|---------|---------|
| v0.2.0 | 18 factions, landmark system, BL 1.3 patches, hit map generator |
| v0.1.0 | Initial PoC: hit detection, VM, Harmony patches, text UI |
