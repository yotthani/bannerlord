"""
Extract all 36 faction banners from composite images.
Strategy: Use pink composite to find exact flag boundaries per cell,
          extract from dark composite using those boundaries.
          Scale to 84x128 canvas.
"""
from PIL import Image
import numpy as np
import os

base = r'C:\Work\Sources\github\LOTRAOM_FactionMap'
outdir = os.path.join(base, 'GUI', 'SpriteData', 'FactionMap')

CANVAS_W = 84
CANVAS_H = 128
PINK_RGB = np.array([214, 92, 141], dtype=np.float32)
PINK_THRESHOLD = 80
COL_PINK_THRESHOLD = 0.30  # column is "background" if >30% of its pixels are pink
ROW_PINK_THRESHOLD = 0.30  # same for rows


def extract_banner(arr_dark, arr_pink, dark_rows, pink_rows, ri, ci, num_cols):
    """Extract banner using pink composite for boundary detection."""
    h_d, w_d = arr_dark.shape[:2]
    h_p, w_p = arr_pink.shape[:2]
    col_w_d = w_d / num_cols
    col_w_p = w_p / num_cols

    # Get cells from both composites
    dy1, dy2 = dark_rows[ri]
    py1, py2 = pink_rows[ri]
    dx1 = int(ci * col_w_d)
    dx2 = int((ci + 1) * col_w_d)
    px1 = int(ci * col_w_p)
    px2 = int((ci + 1) * col_w_p)

    cell_dark = arr_dark[dy1:dy2, dx1:dx2]
    cell_pink = arr_pink[py1:py2, px1:px2]

    ch_p, cw_p = cell_pink.shape[:2]

    # Compute pink mask on pink composite
    pink_dist = np.sqrt(np.sum(
        (cell_pink[:, :, :3].astype(np.float32) - PINK_RGB) ** 2, axis=2))
    is_pink = pink_dist < PINK_THRESHOLD

    # Per-column and per-row pink fraction
    col_pink_frac = np.mean(is_pink, axis=0)
    row_pink_frac = np.mean(is_pink, axis=1)

    # Find flag boundaries: columns/rows where pink fraction < threshold
    flag_cols = col_pink_frac < COL_PINK_THRESHOLD
    flag_rows = row_pink_frac < ROW_PINK_THRESHOLD

    if not np.any(flag_cols) or not np.any(flag_rows):
        return None

    cx1 = int(np.argmax(flag_cols))
    cx2 = int(cw_p - np.argmax(flag_cols[::-1]))
    ry1 = int(np.argmax(flag_rows))
    ry2 = int(ch_p - np.argmax(flag_rows[::-1]))

    # Map pink-composite coordinates to dark-composite coordinates
    # Both composites have same width, scale vertically
    ch_d = cell_dark.shape[0]
    scale_y = ch_d / ch_p
    scale_x = cell_dark.shape[1] / cw_p

    dcx1 = int(cx1 * scale_x)
    dcx2 = int(cx2 * scale_x)
    dry1 = int(ry1 * scale_y)
    dry2 = int(ry2 * scale_y)

    # Crop from dark composite
    cropped = cell_dark[dry1:dry2, dcx1:dcx2, :3]
    crop_h, crop_w = cropped.shape[:2]

    if crop_h < 10 or crop_w < 10:
        return None

    # Resize to exactly CANVAS_W x CANVAS_H
    img = Image.fromarray(cropped).convert('RGBA')
    img = img.resize((CANVAS_W, CANVAS_H), Image.LANCZOS)
    result = np.array(img)
    result[:, :, 3] = 255
    return result


# Load composites
img_dark = Image.open(os.path.join(base,
    'Gemini_Generated_Image_3500x03500x03500 (1).png')).convert('RGBA')
arr_dark = np.array(img_dark)

img_pink = Image.open(os.path.join(base, 'flags_small_33.PNG')).convert('RGBA')
arr_pink = np.array(img_pink)

dark_rows = [(23, 460), (530, 946), (1035, 1430)]
pink_rows = [(26, 423), (531, 935), (1037, 1427)]

main_map = {
    (0, 0): "easterlings_of_rhun",
    (0, 1): "tribes_of_harad",
    (0, 2): "variags_of_khand",
    (0, 3): "kingdom_of_gondor",
    (0, 5): "dark_lands_of_mordor",
    (0, 6): "dwarves_of_erebor",
    (0, 7): "kingdom_of_dale",
    (0, 8): "elves_of_rivendell",
    (0, 9): "orcs_of_gundabad",
    (0, 10): "dominion_of_isengard",
    (1, 0): "hill_men_of_dunland",
    (1, 1): "dwarves_of_orocarni",
    (1, 3): "realm_of_dorwinion",
    (1, 4): "clans_of_enedwaith",
    (1, 5): "realm_of_cardolan",
    (1, 6): "orcs_of_narager",
    (1, 7): "elves_of_lindon",
    (1, 8): "orcs_of_gwaer",
    (1, 9): "faithful_of_bellakar",
    (1, 10): "dwarves_of_ered_luin",
    (2, 0): "orcs_of_the_misty_mountains",
    (2, 1): "forodwaith",
    (2, 2): "kingdom_of_arthedain",
    (2, 3): "hill_men_of_rhudaur",
    (2, 4): "vale_of_anduin",
    (2, 5): "orcs_of_ered_luin",
    (2, 6): "elves_of_neldoreth",
    (2, 7): "orcs_of_forochel",
    (2, 8): "realm_of_angmar",
    (2, 9): "fangorn_forest",
    (2, 10): "iron_hills",
}

extracted = {}
for (ri, ci), name in main_map.items():
    banner = extract_banner(arr_dark, arr_pink, dark_rows, pink_rows, ri, ci, 11)
    if banner is not None:
        extracted[name] = banner
        print(f"  {name}: OK")
    else:
        print(f"  {name}: FAILED")

# Fallback: iddm9p composite for 4 missing factions
img2 = Image.open(os.path.join(base,
    'Gemini_Generated_Image_iddm9piddm9piddm.png')).convert('RGBA')
arr2 = np.array(img2)

for name, (ax1, ay1, ax2, ay2) in {
    "havens_of_umbar": (1260, 535, 1565, 940),
    "kingdom_of_rohan": (2260, 535, 2530, 940),
    "shadow_of_dol_guldur": (320, 1040, 555, 1425),
    "elves_of_lothlorien": (2260, 1040, 2530, 1425),
}.items():
    crop = arr2[ay1:ay2, ax1:ax2]
    img_c = Image.fromarray(crop[:, :, :3]).convert('RGBA')
    img_c = img_c.resize((CANVAS_W, CANVAS_H), Image.LANCZOS)
    result = np.array(img_c)
    result[:, :, 3] = 255
    extracted[name] = result
    print(f"  {name} (iddm9p): OK")

# Mirkwood placeholder
if "elves_of_neldoreth" in extracted:
    extracted["elves_of_mirkwood"] = extracted["elves_of_neldoreth"].copy()
    print("  elves_of_mirkwood: copy of neldoreth")

# Save all
os.makedirs(outdir, exist_ok=True)
for name, a in extracted.items():
    Image.fromarray(a).save(os.path.join(outdir, f"banner_{name}.png"), optimize=True)

print(f"\nSaved {len(extracted)} banners ({CANVAS_W}x{CANVAS_H}) to {outdir}")
