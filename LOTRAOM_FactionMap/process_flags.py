"""
Process banner images from flags/ folder.
Handles:
- Single freigestellte PNGs (RGBA, transparent bg) from ComfyUI
- Composite grids (RGB/RGBA, black bg, labeled) from ChatGPT
Auto-detects layout, splits composites, crops to content, scales to 84x128.
"""
from PIL import Image
import numpy as np
import os

flags_dir = r'C:\Work\Sources\github\LOTRAOM_FactionMap\flags'
comfyui_dir = flags_dir  # ComfyUI files now in same folder
outdir = r'C:\Work\Sources\github\LOTRAOM_FactionMap\GUI\SpriteData\FactionMap'

CANVAS_W = 84
CANVAS_H = 128

# ── Single file mapping (ComfyUI, already cutout) ──
single_map = {
    "ComfyUI_00080_.png": "elves_of_neldoreth",
    "ComfyUI_00089_.png": "dark_lands_of_mordor",
    "ComfyUI_00116_.png": "easterlings_of_rhun",
    "ComfyUI_00127_.png": "dominion_of_isengard",
    "ComfyUI_00132_.png": "kingdom_of_rohan",
    "ComfyUI_00139_.png": "variags_of_khand",
    "ComfyUI_00168_.png": "tribes_of_harad",
    "ComfyUI_00190_.png": "orcs_of_the_misty_mountains",
}

# ── Composite mapping: filename -> list of faction names left-to-right, top-to-bottom ──
composite_map = {
    "ChatGPT Image 10. Feb. 2026, 22_47_13.png": [
        "kingdom_of_gondor", "iron_hills", "realm_of_angmar",
    ],
    "ChatGPT Image 10. Feb. 2026, 22_49_19.png": [
        "easterlings_of_rhun", "tribes_of_harad", "variags_of_khand", "dark_lands_of_mordor",
    ],
    "ChatGPT Image 10. Feb. 2026, 22_50_46.png": [
        "kingdom_of_gondor", "iron_hills", "realm_of_angmar",
    ],
    "ChatGPT Image 10. Feb. 2026, 22_52_32.png": [
        "vale_of_anduin",  # Rangers of Ithilien -> vale_of_anduin
        "kingdom_of_rohan", "kingdom_of_arthedain",  # Arnor -> Arthedain
        "dwarves_of_erebor", "elves_of_lothlorien", "elves_of_lindon",
    ],
    "ChatGPT Image 10. Feb. 2026, 22_58_26.png": [
        "orcs_of_gundabad",  # Cave Trolls -> skip or use as gundabad alt
        "orcs_of_gundabad",
        "dominion_of_isengard", "havens_of_umbar",
    ],
    "ChatGPT Image 10. Feb. 2026, 23_02_35.png": [
        "orcs_of_the_misty_mountains", "orcs_of_ered_luin", "orcs_of_forochel",
    ],
    "ChatGPT Image 10. Feb. 2026, 23_03_33.png": [
        "orcs_of_the_misty_mountains", "orcs_of_gwaer", "orcs_of_narager",
    ],
    # 23_03_45 is duplicate of 23_03_33 - skip
    "ChatGPT Image 10. Feb. 2026, 23_05_35.png": [
        "kingdom_of_dale", "realm_of_dorwinion", "hill_men_of_dunland",
    ],
    "ChatGPT Image 10. Feb. 2026, 23_06_32.png": [
        "hill_men_of_rhudaur", "faithful_of_bellakar", "realm_of_cardolan",
    ],
    "ChatGPT Image 10. Feb. 2026, 23_08_03.png": [
        "elves_of_rivendell", "elves_of_mirkwood", "elves_of_neldoreth",
    ],
    "ChatGPT Image 11. Feb. 2026, 11_48_30.png": [
        "dwarves_of_orocarni", "clans_of_enedwaith", "forodwaith", "shadow_of_dol_guldur",
    ],
}

# ── 3x3 grid: first composite (no labels) ──
grid_3x3_map = {
    "ChatGPT Image 10. Feb. 2026, 22_42_38.png": [
        # Row 0
        "realm_of_dorwinion",     # compass/star
        "tribes_of_harad",        # elephant
        "kingdom_of_gondor",      # white tree
        # Row 1
        "dwarves_of_erebor",      # crossed hammers
        "kingdom_of_rohan",       # horse
        "kingdom_of_arthedain",   # crown + star
        # Row 2
        "dwarves_of_ered_luin",   # mountains + axes
        "fangorn_forest",         # leaf
        "elves_of_lindon",        # star flower
    ],
}

# Copies (only used if no dedicated version exists)
copy_map = {
    # elves_of_mirkwood now has its own design (spider)
}


def crop_to_content(img):
    """Crop image to non-black / non-transparent content area."""
    arr = np.array(img)

    if arr.shape[2] == 4:
        # RGBA - use alpha
        content_mask = arr[:, :, 3] > 10
    else:
        # RGB - use brightness (non-black)
        brightness = np.max(arr[:, :, :3], axis=2)
        content_mask = brightness > 20

    rows = np.any(content_mask, axis=1)
    cols = np.any(content_mask, axis=0)

    if not np.any(rows) or not np.any(cols):
        return None

    y1 = int(np.argmax(rows))
    y2 = int(arr.shape[0] - np.argmax(rows[::-1]))
    x1 = int(np.argmax(cols))
    x2 = int(arr.shape[1] - np.argmax(cols[::-1]))

    return img.crop((x1, y1, x2, y2))


def remove_label_area(img):
    """Remove bottom label text area from composite cells.
    Labels sit in the bottom ~20% of the cell. Cut at 80%."""
    h = img.size[1]
    cut_y = int(h * 0.80)
    return img.crop((0, 0, img.size[0], cut_y))


def split_composite_horizontal(img, num_banners):
    """Split a horizontal composite into individual banners."""
    arr = np.array(img.convert('RGB'))
    h, w = arr.shape[:2]

    # Find vertical black separator columns
    brightness = np.max(arr[:, :, :3], axis=2)
    col_brightness = np.mean(brightness, axis=0)

    # Find gaps: columns where average brightness < threshold
    is_gap = col_brightness < 12

    # Find contiguous gap regions
    segments = []
    seg_start = 0
    in_gap = False
    for x in range(w):
        if is_gap[x] and not in_gap:
            if x - seg_start > 20:  # minimum segment width
                segments.append((seg_start, x))
            in_gap = True
        elif not is_gap[x] and in_gap:
            seg_start = x
            in_gap = False
    # Last segment
    if not in_gap and w - seg_start > 20:
        segments.append((seg_start, w))

    # If we didn't find enough segments, try equal division
    if len(segments) < num_banners:
        cell_w = w // num_banners
        segments = [(i * cell_w, (i + 1) * cell_w) for i in range(num_banners)]

    cells = []
    for x1, x2 in segments[:num_banners]:
        cell = img.crop((x1, 0, x2, h))
        cells.append(cell)

    return cells


def split_grid(img, rows, cols):
    """Split a grid composite into individual cells."""
    w, h = img.size
    cell_w = w // cols
    cell_h = h // rows

    cells = []
    for r in range(rows):
        for c in range(cols):
            x1 = c * cell_w
            y1 = r * cell_h
            cell = img.crop((x1, y1, x1 + cell_w, y1 + cell_h))
            cells.append(cell)
    return cells


def split_composite_2x2(img):
    """Split a 2x2 grid."""
    return split_grid(img, 2, 2)


def make_black_transparent(img):
    """Convert black background pixels to transparent.
    Uses edge detection: if a pixel is dark AND connected to the image border,
    it's background. Simple approach: just make all very dark pixels transparent."""
    arr = np.array(img.convert('RGBA'))
    rgb = arr[:, :, :3]
    brightness = np.max(rgb, axis=2)

    # Make very dark pixels (background) transparent
    # But be careful: dark banners (Mordor, Isengard) have dark FABRIC
    # Strategy: scan from edges inward. Background black is at the borders.
    h, w = arr.shape[:2]
    bg_mask = np.zeros((h, w), dtype=bool)

    # Flood-fill from edges: any dark pixel touching the border is background
    from scipy import ndimage

    # Seed: dark pixels on the image border
    dark = brightness < 25
    seeds = np.zeros_like(dark)
    seeds[0, :] = dark[0, :]      # top edge
    seeds[-1, :] = dark[-1, :]    # bottom edge
    seeds[:, 0] = dark[:, 0]      # left edge
    seeds[:, -1] = dark[:, -1]    # right edge

    # Flood fill: expand seeds through connected dark pixels
    labeled, _ = ndimage.label(dark)
    # Find which labels touch the seeds
    seed_labels = set(labeled[seeds].flatten()) - {0}
    for lbl in seed_labels:
        bg_mask |= (labeled == lbl)

    arr[bg_mask, 3] = 0
    return Image.fromarray(arr)


def process_to_banner(img):
    """Take a single banner image, remove black bg, remove label text,
    crop to banner only, stretch to 84x128."""
    from scipy import ndimage as ndi

    img = img.convert('RGBA')

    # Step 1: Make black background transparent
    img = make_black_transparent(img)

    # Step 2: Keep only the LARGEST connected non-transparent region (= the banner)
    # This removes isolated label text below the banner
    arr = np.array(img)
    content_mask = arr[:, :, 3] > 10
    labeled, num_features = ndi.label(content_mask)

    if num_features == 0:
        return None

    # Find largest component
    component_sizes = ndi.sum(content_mask, labeled, range(1, num_features + 1))
    largest_label = np.argmax(component_sizes) + 1
    banner_mask = labeled == largest_label

    # Zero out everything that's not the banner
    arr[~banner_mask, 3] = 0

    # Step 3: Crop to banner bbox
    rows = np.any(banner_mask, axis=1)
    cols = np.any(banner_mask, axis=0)
    y1 = int(np.argmax(rows))
    y2 = int(arr.shape[0] - np.argmax(rows[::-1]))
    x1 = int(np.argmax(cols))
    x2 = int(arr.shape[1] - np.argmax(cols[::-1]))
    cropped = Image.fromarray(arr[y1:y2, x1:x2])

    # Step 4: Stretch to exactly 84x128
    result = cropped.resize((CANVAS_W, CANVAS_H), Image.LANCZOS)
    return result


# ══════════════════════════════════════════════════════════════
os.makedirs(outdir, exist_ok=True)
processed = {}


# ── 1. Process horizontal composites (labeled) - PRIORITY ──
print("=== ChatGPT composites ===")
for filename, factions in composite_map.items():
    filepath = os.path.join(flags_dir, filename)
    if not os.path.exists(filepath):
        print(f"  SKIP {filename}: not found")
        continue

    img = Image.open(filepath)
    w, h = img.size

    # Detect layout
    if h > w:
        # Portrait = 2x2 grid
        cells = split_composite_2x2(img)
    else:
        # Landscape = horizontal strip
        cells = split_composite_horizontal(img, len(factions))

    for i, faction in enumerate(factions):
        if i >= len(cells):
            break
        if faction in processed:
            continue  # later composite already processed

        cell = cells[i]
        # No label cut — largest-component approach handles text removal
        result = process_to_banner(cell)
        if result:
            result.save(os.path.join(outdir, f"banner_{faction}.png"), optimize=True)
            processed[faction] = f"{filename}[{i}]"
            print(f"  {faction} <- {filename}[{i}]")


# ── 2. ComfyUI singles - FALLBACK for factions not in ChatGPT composites ──
print("\n=== ComfyUI singles (fallback) ===")
for filename, faction in single_map.items():
    if faction in processed:
        continue  # ChatGPT version takes priority
    filepath = os.path.join(comfyui_dir, filename)
    if not os.path.exists(filepath):
        continue
    img = Image.open(filepath).convert('RGBA')
    result = process_to_banner(img)
    if result:
        result.save(os.path.join(outdir, f"banner_{faction}.png"), optimize=True)
        processed[faction] = filename
        print(f"  {faction} <- {filename}")


# ── 3. Process 3x3 grid ──
print("\n=== 3x3 grids ===")
for filename, factions in grid_3x3_map.items():
    filepath = os.path.join(flags_dir, filename)
    if not os.path.exists(filepath):
        continue

    img = Image.open(filepath)
    cells = split_grid(img, 3, 3)

    for i, faction in enumerate(factions):
        if i >= len(cells):
            break
        if faction in processed:
            continue

        cell = cells[i]
        result = process_to_banner(cell)
        if result:
            result.save(os.path.join(outdir, f"banner_{faction}.png"), optimize=True)
            processed[faction] = f"{filename}[{i}]"
            print(f"  {faction} <- {filename}[{i}]")


# ── 4. Copies ──
print("\n=== Copies ===")
for target, source in copy_map.items():
    if target not in processed and source in processed:
        src_path = os.path.join(outdir, f"banner_{source}.png")
        if os.path.exists(src_path):
            Image.open(src_path).save(
                os.path.join(outdir, f"banner_{target}.png"), optimize=True)
            processed[target] = f"copy of {source}"
            print(f"  {target} <- copy of {source}")


# ── Summary ──
all_factions = [
    "easterlings_of_rhun", "tribes_of_harad", "variags_of_khand",
    "kingdom_of_gondor", "dark_lands_of_mordor", "dwarves_of_erebor",
    "kingdom_of_dale", "elves_of_rivendell", "orcs_of_gundabad",
    "dominion_of_isengard", "hill_men_of_dunland", "dwarves_of_orocarni",
    "realm_of_dorwinion", "clans_of_enedwaith", "realm_of_cardolan",
    "orcs_of_narager", "elves_of_lindon", "orcs_of_gwaer",
    "faithful_of_bellakar", "dwarves_of_ered_luin",
    "orcs_of_the_misty_mountains", "forodwaith", "kingdom_of_arthedain",
    "hill_men_of_rhudaur", "vale_of_anduin", "orcs_of_ered_luin",
    "elves_of_neldoreth", "orcs_of_forochel", "realm_of_angmar",
    "fangorn_forest", "iron_hills", "havens_of_umbar", "kingdom_of_rohan",
    "shadow_of_dol_guldur", "elves_of_lothlorien", "elves_of_mirkwood",
]

missing = [f for f in all_factions if f not in processed]
print(f"\n{'='*50}")
print(f"Processed: {len(processed)}/36 factions")
if missing:
    print(f"Still missing ({len(missing)}):")
    for f in missing:
        print(f"  - {f}")
else:
    print("ALL 36 factions complete!")
