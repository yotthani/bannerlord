"""
Build game assets from the claudeweb blackhat-extractor output.

Takes the 16-bit label map (kingdom_label_map.png) and region_data.json
produced by extract_kingdoms.py, then:
1. Upscales label map from extractor resolution to full map resolution (2048x1423)
2. Cuts puzzle pieces from the PLAIN map (no-colours) as textures
3. Applies dilation for gap/border coverage
4. Saves fullres + game-size PNGs with stable snake_case names
5. Updates regions.json (geometry) and factions.json (faction definitions)

Two-file architecture:
  - regions.json: region_key → { faction, norm_bbox }
  - factions.json: faction_key → { name, color, playable, game_faction, description, traits }

Usage:
    python Tools/extract_and_build.py
"""
import cv2
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
import json
import os
import sys
import colorsys
import re

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(SCRIPT_DIR)

# Input files
LABEL_MAP   = os.path.join(SCRIPT_DIR, "claudeweb", "kingdom_label_map.png")
REGION_JSON = os.path.join(SCRIPT_DIR, "claudeweb", "region_data.json")
PLAIN_MAP   = os.path.join(PROJ, "Keyforce_Map_no-colours.jpg")
COLOURS_MAP = os.path.join(PROJ, "Keyforce_Map_colours.jpg")
NAMES_MAP   = os.path.join(PROJ, "Keyforce_Map_colours-names.jpg")

# Output dirs
OUTPUT_DIR  = os.path.join(PROJ, "GUI", "SpriteData", "FactionMap")
MODULE_DATA = os.path.join(PROJ, "ModuleData")
DEBUG_DIR   = os.path.join(SCRIPT_DIR, "debug")

# Parameters
DILATION_ITERATIONS = 5
PADDING = 8
MAX_GAME_SIZE = 512

# Stable region key mapping: extractor_id → snake_case key.
# Regions with a faction get the faction name as key.
# Decoration regions (no faction) get "deco_N" keys.
# New regions not in this map get auto-generated keys.
# This map is updated at the end of each run.
EXTRACTOR_ID_TO_KEY = {
    2: "easterlings_of_rhun",
    3: "tribes_of_harad",
    4: "variags_of_khand",
    5: "kingdom_of_gondor",
    6: "iron_hills",
    7: "orcs_of_narager",
    8: "dark_lands_of_mordor",
    9: "dwarves_of_orocarni",
    10: "realm_of_dorwinion",
    11: "clans_of_enedwaith",
    12: "dwarves_of_erebor",
    13: "kingdom_of_dale",
    14: "havens_of_umbar",
    15: "realm_of_cardolan",
    16: "elves_of_lindon",
    17: "orcs_of_gwaer",
    18: "hill_men_of_dunland",
    19: "kingdom_of_rohan",
    20: "faithful_of_bellakar",
    21: "dwarves_of_ered_luin",
    22: "orcs_of_the_misty_mountains",
    23: "forodwaith",
    24: "kingdom_of_arthedain",
    25: "hill_men_of_rhudaur",
    26: "vale_of_anduin",
    27: "shadow_of_dol_guldur",
    28: "orcs_of_ered_luin",
    29: "elves_of_neldoreth",
    30: "deco_1",                      # mountain ridge near Rivendell (no faction)
    31: "orcs_of_forochel",
    32: "orcs_of_gundabad",
    33: "realm_of_angmar",
    34: "elves_of_mirkwood",
    35: "fangorn_forest",              # forest between Isengard and Lothlorien
    36: "dominion_of_isengard",        # Saruman's domain around Orthanc
    37: "elves_of_rivendell",          # Imladris / Last Homely House
    38: "elves_of_lothlorien",         # Golden Wood of Lorien
}


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    os.makedirs(MODULE_DATA, exist_ok=True)
    os.makedirs(DEBUG_DIR, exist_ok=True)
    fullres_dir = os.path.join(OUTPUT_DIR, "fullres")
    os.makedirs(fullres_dir, exist_ok=True)

    # =========================================================================
    # STEP 1: Load label map + region data from claudeweb extractor
    # =========================================================================
    print("=" * 60)
    print("STEP 1: Loading blackhat extractor output")
    print("=" * 60)

    # Load label map (16-bit, pixel value = kingdom ID)
    label_img = cv2.imread(LABEL_MAP, cv2.IMREAD_UNCHANGED)
    if label_img is None:
        raise FileNotFoundError(f"Cannot load label map: {LABEL_MAP}")
    ext_h, ext_w = label_img.shape[:2]
    print(f"  Label map: {ext_w}x{ext_h}")

    # Load region metadata
    with open(REGION_JSON, 'r') as f:
        region_data = json.load(f)
    num_kingdoms = region_data["num_kingdoms"]
    print(f"  Kingdoms: {num_kingdoms}")

    # Load target maps
    plain_img = Image.open(PLAIN_MAP)
    plain_arr = np.array(plain_img)
    full_h, full_w = plain_arr.shape[:2]
    print(f"  Target map: {full_w}x{full_h}")

    colours_arr = np.array(Image.open(COLOURS_MAP))
    names_img = Image.open(NAMES_MAP)

    # Brightness map for smart dilation
    full_brightness = colours_arr[:,:,0].astype(np.int32) + \
                      colours_arr[:,:,1].astype(np.int32) + \
                      colours_arr[:,:,2].astype(np.int32)

    # Colours map at extractor resolution (for color-distance cleanup)
    colours_ext = cv2.resize(colours_arr, (ext_w, ext_h), interpolation=cv2.INTER_LANCZOS4)

    # =========================================================================
    # STEP 1b: Density-adaptive erosion cleanup (on ORIGINAL resolution)
    # =========================================================================
    # Problem: Gap-fill in the extractor connects distant dark fragments via
    # thin pixel chains. Fix: erode to break chains, keep largest component.
    # Only apply to regions with low fill ratio (scattered fragments).
    # Compact regions (fill > 30%) are left untouched to avoid splitting
    # legitimate thin features like Orthanc.
    print("\n" + "=" * 60)
    print("STEP 1b: Density-adaptive erosion cleanup (original res)")
    print("=" * 60)

    EROSION_KERNEL = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    FILL_THRESHOLD = 0.30  # Only clean regions with fill ratio below this

    orig_unique = set(np.unique(label_img)) - {0}
    total_orphans = 0
    for kid in orig_unique:
        mask = (label_img == kid).astype(np.uint8)
        total_px = mask.sum()

        # Compute fill ratio (pixels / bbox area)
        rows = np.any(mask, axis=1)
        cols = np.any(mask, axis=0)
        if not rows.any():
            continue
        rmin, rmax = np.where(rows)[0][[0, -1]]
        cmin, cmax = np.where(cols)[0][[0, -1]]
        bbox_area = (cmax - cmin + 1) * (rmax - rmin + 1)
        fill_ratio = total_px / bbox_area

        if fill_ratio >= FILL_THRESHOLD:
            continue  # Compact region, skip erosion

        # Adaptive erosion: more iterations for sparser regions
        if fill_ratio < 0.05:
            iters = 4  # Very sparse (e.g. Misty Mountains 3.6%)
        elif fill_ratio < 0.20:
            iters = 3  # Sparse (e.g. Rhun 17.6%)
        else:
            iters = 2  # Moderately sparse

        # Erode to break thin chains
        eroded = cv2.erode(mask, EROSION_KERNEL, iterations=iters)
        eroded_px = eroded.sum()

        if eroded_px == 0:
            # Entire region eroded away = it's all thin chains / tiny blobs
            # Skip cleanup, keep as-is (will be small anyway)
            continue

        # Find connected components on eroded mask
        n_components, cc_labels = cv2.connectedComponents(eroded)
        if n_components <= 2:  # Only one component after erosion = fine
            continue

        # Find the largest eroded component
        best_cc = 0
        best_size = 0
        for cc_id in range(1, n_components):
            sz = (cc_labels == cc_id).sum()
            if sz > best_size:
                best_size = sz
                best_cc = cc_id

        # Dilate the largest component back to roughly original size
        # Use more iterations than erosion to reclaim border pixels
        kept_eroded = (cc_labels == best_cc).astype(np.uint8)
        kept_dilated = cv2.dilate(kept_eroded, EROSION_KERNEL, iterations=iters + 2)

        # Original pixels that overlap with the dilated kept component = keep
        # Original pixels that DON'T overlap = orphan fragments
        keep_mask = mask & kept_dilated
        orphan_mask = (mask > 0) & (keep_mask == 0)
        orphan_count = orphan_mask.sum()

        if orphan_count > 0:
            label_img[orphan_mask] = 0
            total_orphans += orphan_count
            print(f"  Region {kid:2d}: fill={fill_ratio:.1%}, erode={iters}x, "
                  f"{n_components-1} components, "
                  f"kept largest ({best_size:,}px eroded), "
                  f"removed {orphan_count:,}/{total_px:,} orphan px")

    # Re-assign orphan pixels to nearest neighbor via distance transform
    if total_orphans > 0:
        unassigned = (label_img == 0)
        assigned = ~unassigned
        if assigned.any() and unassigned.any():
            _, nearest_idx = ndimage.distance_transform_edt(unassigned, return_indices=True)
            label_img[unassigned] = label_img[nearest_idx[0][unassigned], nearest_idx[1][unassigned]]
        print(f"  Total: {total_orphans:,} orphan pixels reassigned to neighbors")
    else:
        print("  All regions are contiguous - no cleanup needed")

    # =========================================================================
    # STEP 1c: Color-distance cleanup (on ORIGINAL resolution)
    # =========================================================================
    # Some regions absorb gradient/transition pixels at their edges that have
    # very different colors from the region's core. Remove pixels where the
    # color on the source map differs too much from the region's average.
    print("\n" + "=" * 60)
    print("STEP 1c: Color-distance cleanup (original res)")
    print("=" * 60)

    COLOR_DIST_THRESHOLD = 60  # Euclidean RGB distance
    total_color_orphans = 0
    for kid in set(np.unique(label_img)) - {0}:
        mask = (label_img == kid)
        total_px = mask.sum()
        if total_px < 500:
            continue

        # Compute average color of this region on the source colour map
        # Use median instead of mean to be robust against the outliers themselves
        region_colors = colours_ext[mask].astype(np.float32)
        avg_color = np.median(region_colors, axis=0)

        # Distance of each pixel from the median color
        diff = region_colors - avg_color
        dists = np.sqrt((diff ** 2).sum(axis=1))

        # Find outlier pixels (far from median)
        outlier_indices = dists > COLOR_DIST_THRESHOLD
        if not outlier_indices.any():
            continue

        # Only remove outliers that are on the periphery (not interior)
        region_ys, region_xs = np.where(mask)
        outlier_ys = region_ys[outlier_indices]
        outlier_xs = region_xs[outlier_indices]

        # Erode the mask to find interior; outliers in interior are kept
        mask_uint8 = mask.astype(np.uint8)
        interior = cv2.erode(mask_uint8, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)),
                             iterations=3)
        periphery = mask_uint8 - interior

        # Only remove outliers on the periphery
        outlier_map = np.zeros_like(mask)
        outlier_map[outlier_ys, outlier_xs] = True
        remove_map = outlier_map & (periphery > 0)
        remove_count = remove_map.sum()

        if remove_count > 0 and remove_count < total_px * 0.5:
            label_img[remove_map] = 0
            total_color_orphans += remove_count
            print(f"  Region {kid:2d}: median RGB({avg_color[0]:.0f},{avg_color[1]:.0f},{avg_color[2]:.0f}), "
                  f"removed {remove_count:,}/{total_px:,} color-outlier px on periphery")

    if total_color_orphans > 0:
        unassigned = (label_img == 0)
        assigned = ~unassigned
        if assigned.any() and unassigned.any():
            _, nearest_idx = ndimage.distance_transform_edt(unassigned, return_indices=True)
            label_img[unassigned] = label_img[nearest_idx[0][unassigned], nearest_idx[1][unassigned]]
        print(f"  Total: {total_color_orphans:,} color-outlier pixels reassigned")
    else:
        print("  No color outliers found")

    # =========================================================================
    # STEP 1d: Morphological opening to remove thin protrusions
    # =========================================================================
    # Some regions have thin snake-like protrusions (3-5px wide) that extend
    # far from the main body (e.g. Mordor's snake to the bottom map edge).
    # Morphological opening (erode then dilate) removes features thinner than
    # the kernel while preserving the bulk shape.
    print("\n" + "=" * 60)
    print("STEP 1d: Morphological opening (remove thin protrusions)")
    print("=" * 60)

    OPENING_KERNEL = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
    total_protrusion_px = 0
    for kid in set(np.unique(label_img)) - {0}:
        mask = (label_img == kid).astype(np.uint8)
        total_px = mask.sum()
        if total_px < 500:
            continue

        opened = cv2.morphologyEx(mask, cv2.MORPH_OPEN, OPENING_KERNEL)
        removed = mask - opened
        removed_count = removed.sum()

        if removed_count > 0 and removed_count < total_px * 0.5:
            label_img[removed > 0] = 0
            total_protrusion_px += removed_count
            print(f"  Region {kid:2d}: removed {removed_count:,}/{total_px:,} thin-protrusion px")

    if total_protrusion_px > 0:
        unassigned = (label_img == 0)
        assigned = ~unassigned
        if assigned.any() and unassigned.any():
            _, nearest_idx = ndimage.distance_transform_edt(unassigned, return_indices=True)
            label_img[unassigned] = label_img[nearest_idx[0][unassigned], nearest_idx[1][unassigned]]
        print(f"  Total: {total_protrusion_px:,} thin-protrusion pixels reassigned")
    else:
        print("  No thin protrusions found")

    # =========================================================================
    # STEP 2: Upscale label map to full resolution
    # =========================================================================
    print("\n" + "=" * 60)
    print("STEP 2: Upscaling label map to full resolution")
    print("=" * 60)

    # Must use INTER_NEAREST to preserve integer IDs
    label_full = cv2.resize(label_img, (full_w, full_h), interpolation=cv2.INTER_NEAREST)
    print(f"  Upscaled: {ext_w}x{ext_h} -> {full_w}x{full_h}")

    # Verify kingdom IDs survived
    unique_ids = set(np.unique(label_full)) - {0}
    print(f"  Unique IDs in upscaled map: {len(unique_ids)}")

    # =========================================================================
    # STEP 3: Identify ocean + inland seas to skip
    # =========================================================================
    # Region 1 in the extractor is 23.87% of the map = ocean/background
    # Region 39 = Sea of Rhun (inland lake, not a playable faction)
    SKIP_IDS = set()
    for k in region_data["kingdoms"]:
        if k["area_percent"] > 15:
            r, g, b = k["avg_color_rgb"]
            if r > 150 and g > 120 and b < 150:
                SKIP_IDS.add(k["id"])
                print(f"\n  Ocean detected: Region {k['id']} ({k['area_percent']:.1f}%, "
                      f"RGB({r},{g},{b}))")
                break

    # Sea of Rhun: small inland lake (ext_id=39, ~0.4%, beige/tan)
    SEA_OF_RHUN_ID = 39
    SKIP_IDS.add(SEA_OF_RHUN_ID)
    print(f"  Sea of Rhun (ext_id={SEA_OF_RHUN_ID}) will be skipped")

    # =========================================================================
    # STEP 4: Cut puzzle pieces from plain map
    # =========================================================================
    print("\n" + "=" * 60)
    print("STEP 4: Cutting puzzle pieces from plain map")
    print("=" * 60)

    # Load existing regions.json and factions.json for merge-on-write
    regions_path = os.path.join(MODULE_DATA, "regions.json")
    factions_path = os.path.join(MODULE_DATA, "factions.json")
    existing_regions = {}
    existing_factions = {}
    if os.path.exists(regions_path):
        with open(regions_path, 'r', encoding='utf-8') as f:
            existing_regions = json.load(f)
    if os.path.exists(factions_path):
        with open(factions_path, 'r', encoding='utf-8') as f:
            existing_factions = json.load(f)

    regions_meta = {}   # region_key → { faction, norm_bbox }
    factions_meta = {}  # faction_key → { name, color, ... }
    deco_counter = 0
    used_keys = set(EXTRACTOR_ID_TO_KEY.values())

    for k in region_data["kingdoms"]:
        kid = k["id"]

        # Skip ocean + inland seas
        if kid in SKIP_IDS:
            continue

        # Determine stable region key
        if kid in EXTRACTOR_ID_TO_KEY:
            region_key = EXTRACTOR_ID_TO_KEY[kid]
        else:
            # New region not in mapping — auto-generate key
            deco_counter += 1
            while f"new_region_{deco_counter}" in used_keys:
                deco_counter += 1
            region_key = f"new_region_{deco_counter}"
            used_keys.add(region_key)
            print(f"  WARNING: ext_id={kid} not in EXTRACTOR_ID_TO_KEY, using '{region_key}'")

        # Extract mask at full resolution
        full_mask = (label_full == kid)

        # Find bounding box
        rows = np.any(full_mask, axis=1)
        cols = np.any(full_mask, axis=0)
        if not rows.any():
            continue

        rmin, rmax = np.where(rows)[0][[0, -1]]
        cmin, cmax = np.where(cols)[0][[0, -1]]

        x1 = max(0, cmin - PADDING)
        y1 = max(0, rmin - PADDING)
        x2 = min(full_w, cmax + 1 + PADDING)
        y2 = min(full_h, rmax + 1 + PADDING)
        bw, bh = x2 - x1, y2 - y1

        # Crop mask
        mask_crop = full_mask[y1:y2, x1:x2]

        # Dilation: cover borders and gaps
        ring1 = ndimage.binary_dilation(mask_crop, iterations=1)
        dilated = ndimage.binary_dilation(mask_crop, iterations=DILATION_ITERATIONS)

        # Only extend dilation into dark/border pixels
        bbox_brightness = full_brightness[y1:y2, x1:x2]
        border_or_dark = bbox_brightness < 200

        final_mask = mask_crop | ring1 | (dilated & border_or_dark)

        # Apply mask to PLAIN map — FULL RESOLUTION
        plain_crop = plain_arr[y1:y2, x1:x2]
        rgba = np.zeros((bh, bw, 4), dtype=np.uint8)
        rgba[:, :, :3] = plain_crop
        rgba[:, :, 3] = (final_mask * 255).astype(np.uint8)

        # Save FULL-SIZE PNG to archive
        piece = Image.fromarray(rgba)
        fullres_path = os.path.join(fullres_dir, f"region_{region_key}_full.png")
        piece.save(fullres_path, "PNG")

        # Save DOWNSCALED game version (max 512px)
        longest = max(bw, bh)
        if longest > MAX_GAME_SIZE:
            scale = MAX_GAME_SIZE / longest
            game_w = max(1, int(bw * scale))
            game_h = max(1, int(bh * scale))
            piece_game = piece.resize((game_w, game_h), Image.LANCZOS)
        else:
            piece_game = piece
        out_path = os.path.join(OUTPUT_DIR, f"region_{region_key}.png")
        piece_game.save(out_path, "PNG")

        # Save reference crop with names
        names_crop = names_img.crop((x1, y1, x2, y2))
        ref_path = os.path.join(DEBUG_DIR, f"{region_key}_ref.png")
        names_crop.save(ref_path)

        # Normalized bbox (0-1 range for PolygonWidget)
        norm_x = round(x1 / full_w, 4)
        norm_y = round(y1 / full_h, 4)
        norm_w = round(bw / full_w, 4)
        norm_h = round(bh / full_h, 4)

        r, g, b = k["avg_color_rgb"]
        hex_color = f"#{r:02x}{g:02x}{b:02x}FF"

        # Determine faction assignment from existing regions.json
        faction_id_str = None
        if region_key in existing_regions:
            faction_id_str = existing_regions[region_key].get("faction")
        elif not region_key.startswith("deco_"):
            # Default: faction key = region key (for non-decoration regions)
            faction_id_str = region_key

        # Build regions.json entry (geometry + faction link)
        regions_meta[region_key] = {
            "faction": faction_id_str,
            "norm_bbox": [norm_x, norm_y, norm_w, norm_h],
        }

        # Build/update factions.json entry (only for regions with a faction)
        if faction_id_str and faction_id_str not in factions_meta:
            if faction_id_str in existing_factions:
                # Preserve hand-curated faction data
                factions_meta[faction_id_str] = existing_factions[faction_id_str]
            else:
                # New faction — auto-generate from extractor data
                factions_meta[faction_id_str] = {
                    "name": region_key.replace("_", " ").title(),
                    "color": hex_color,
                    "playable": False,
                    "game_faction": "",
                    "description": "",
                    "traits": [],
                }

        pct = k["area_percent"]
        faction_label = f" -> {faction_id_str}" if faction_id_str else " (deco)"
        print(f"  {region_key:35s}: ext_id={kid:2d} RGB({r},{g},{b}) "
              f"bbox=({x1},{y1})-({x2},{y2}) {bw}x{bh} ({pct:.1f}%){faction_label}")

    # =========================================================================
    # STEP 5: Save regions.json + factions.json + XML
    # =========================================================================
    print("\n" + "=" * 60)
    print(f"STEP 5: Saving regions.json ({len(regions_meta)} regions) + "
          f"factions.json ({len(factions_meta)} factions)")
    print("=" * 60)

    # regions.json — geometry + faction link (overwritten each run)
    with open(regions_path, 'w', encoding='utf-8') as f:
        json.dump(regions_meta, f, indent=2, ensure_ascii=False)
    print(f"  Saved {len(regions_meta)} regions to {regions_path}")

    # factions.json — faction definitions (merge-on-write preserves curated data)
    with open(factions_path, 'w', encoding='utf-8') as f:
        json.dump(factions_meta, f, indent=2, ensure_ascii=False)
    print(f"  Saved {len(factions_meta)} factions to {factions_path}")

    # XML block (BBox no longer in XML — loaded from regions.json at runtime)
    xml_lines = []
    for key in regions_meta:
        xml_lines.append(
            f'                <PolygonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" '
            f'RegionName="{key}" '
            f'Command.Click="ExecuteSelectRegion" />'
        )

    xml_path = os.path.join(DEBUG_DIR, "region_xml_block.txt")
    with open(xml_path, 'w') as f:
        f.write('\n'.join(xml_lines))
    print(f"  XML block saved to {xml_path}")

    # =========================================================================
    # Debug visualizations
    # =========================================================================
    print("\nGenerating debug images...")

    # Half-res for debug visualization
    half_w, half_h = full_w // 2, full_h // 2
    label_half = cv2.resize(label_full, (half_w, half_h), interpolation=cv2.INTER_NEAREST)

    # Coloured overlay on names map
    names_half = names_img.resize((half_w, half_h), Image.LANCZOS)
    debug_img = names_half.copy()
    draw = ImageDraw.Draw(debug_img)
    for key, info in regions_meta.items():
        nb = info['norm_bbox']
        dx1 = int(nb[0] * half_w)
        dy1 = int(nb[1] * half_h)
        dx2 = int((nb[0] + nb[2]) * half_w)
        dy2 = int((nb[1] + nb[3]) * half_h)
        # Use faction color if available, else white
        fid = info.get('faction')
        if fid and fid in factions_meta:
            fc = factions_meta[fid].get('color', '#ffffffFF')
            cr, cg, cb = int(fc[1:3], 16), int(fc[3:5], 16), int(fc[5:7], 16)
        else:
            cr, cg, cb = 255, 255, 255
        draw.rectangle([(dx1, dy1), (dx2, dy2)], outline=(cr, cg, cb), width=2)
        # Short label: first 20 chars of key
        label = key[:20]
        draw.text((dx1 + 3, dy1 + 3), label, fill=(255, 255, 0))

    debug_path = os.path.join(DEBUG_DIR, "extract_debug.png")
    debug_img.save(debug_path)
    print(f"  Debug image: {debug_path}")

    # Kingdom-coloured overlay
    colours_half = np.array(Image.open(COLOURS_MAP).resize((half_w, half_h), Image.LANCZOS))
    overlay = colours_half.copy()
    idx = 0
    for k in region_data["kingdoms"]:
        kid = k["id"]
        if kid in SKIP_IDS:
            continue
        idx += 1
        mask = (label_half == kid)
        hue = (idx * 137) % 360
        vc = colorsys.hsv_to_rgb(hue / 360.0, 0.8, 0.9)
        vis = (int(vc[0] * 255), int(vc[1] * 255), int(vc[2] * 255))
        for ch in range(3):
            overlay[mask, ch] = np.clip(
                overlay[mask, ch].astype(np.int32) // 3 + int(vis[ch] * 2 / 3),
                0, 255
            ).astype(np.uint8)

    overlay_img = Image.fromarray(overlay)
    overlay_draw = ImageDraw.Draw(overlay_img)
    for k in region_data["kingdoms"]:
        kid = k["id"]
        if kid in SKIP_IDS:
            continue
        rkey = EXTRACTOR_ID_TO_KEY.get(kid, f"?{kid}")
        mask = (label_half == kid)
        ys, xs = np.where(mask)
        if len(ys) > 0:
            cy, cx = int(ys.mean()), int(xs.mean())
            overlay_draw.text((cx - 8, cy - 5), rkey[:15], fill=(255, 255, 255))

    overlay_path = os.path.join(DEBUG_DIR, "extract_overlay.png")
    overlay_img.save(overlay_path)
    print(f"  Overlay: {overlay_path}")

    print(f"\n{'=' * 60}")
    print(f"DONE! {len(regions_meta)} regions, {len(factions_meta)} factions")
    print(f"{'=' * 60}")


if __name__ == "__main__":
    main()
