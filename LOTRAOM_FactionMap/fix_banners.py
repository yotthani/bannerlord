"""
Fix all banners with v3 approach: resize cell THEN make transparent.
This ensures flags fill the full 84x128 area without dark edge gaps.
"""
from PIL import Image
import numpy as np
from scipy import ndimage as ndi
import os
import shutil

flags_dir = r'C:\Work\Sources\github\LOTRAOM_FactionMap\flags'
outdir = r'C:\Work\Sources\github\LOTRAOM_FactionMap\GUI\SpriteData\FactionMap'
gamedir = r'D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRAOM.FactionMap\GUI\SpriteData\FactionMap'

CANVAS_W = 84
CANVAS_H = 128


def crop_to_content_rgb(img):
    """Crop to non-black content area in RGB space."""
    arr = np.array(img.convert('RGB'))
    brightness = np.max(arr[:, :, :3], axis=2)
    content = brightness > 20
    rows = np.any(content, axis=1)
    cols = np.any(content, axis=0)
    if not np.any(rows) or not np.any(cols):
        return img
    y1 = int(np.argmax(rows))
    y2 = int(arr.shape[0] - np.argmax(rows[::-1]))
    x1 = int(np.argmax(cols))
    x2 = int(arr.shape[1] - np.argmax(cols[::-1]))
    return img.crop((x1, y1, x2, y2))


def make_black_transparent(img):
    """Flood-fill black from edges -> transparent."""
    arr = np.array(img.convert('RGBA'))
    brightness = np.max(arr[:, :, :3], axis=2)
    dark = brightness < 25
    seeds = np.zeros_like(dark)
    seeds[0, :] = dark[0, :]
    seeds[-1, :] = dark[-1, :]
    seeds[:, 0] = dark[:, 0]
    seeds[:, -1] = dark[:, -1]
    labeled, _ = ndi.label(dark)
    seed_labels = set(labeled[seeds].flatten()) - {0}
    bg_mask = np.zeros_like(dark)
    for lbl in seed_labels:
        bg_mask |= (labeled == lbl)
    arr[bg_mask, 3] = 0
    return Image.fromarray(arr)


def keep_largest_component(img):
    """Keep only the largest non-transparent connected region."""
    arr = np.array(img)
    content_mask = arr[:, :, 3] > 10
    labeled, num = ndi.label(content_mask)
    if num == 0:
        return img
    sizes = ndi.sum(content_mask, labeled, range(1, num + 1))
    largest = np.argmax(sizes) + 1
    mask = labeled == largest
    arr[~mask, 3] = 0
    return Image.fromarray(arr)


def process_banner_v3(cell, has_labels=True):
    """
    New approach:
    1. In RGB (black bg still intact): find the banner content area only (not the label)
       by looking at the TOP portion of the cell
    2. Crop to that content
    3. Resize to 84x128 (black bg stretches too)
    4. THEN make black transparent + largest component
    """
    w, h = cell.size

    if has_labels:
        # Find where the banner ends and text begins:
        # Scan from bottom up looking for a black gap between banner bottom and text top
        arr = np.array(cell.convert('RGB'))
        brightness = np.max(arr[:, :, :3], axis=2)

        # Per-row: what fraction of pixels are non-black?
        row_content = np.mean(brightness > 25, axis=1)

        # The banner occupies the top portion. Text is at the bottom.
        # Find the last substantial content row from the top (banner bottom)
        # Then find the gap between banner and text.

        # Strategy: scan from 60% down to find a gap (row with < 10% content)
        banner_bottom = h
        start_scan = int(h * 0.55)
        for y in range(start_scan, h):
            if row_content[y] < 0.08:
                banner_bottom = y
                break

        # Crop to banner portion only
        cell = cell.crop((0, 0, w, banner_bottom))

    # Crop to content in RGB (removes black borders around the flag)
    cropped = crop_to_content_rgb(cell)

    # Resize to 84x128 WITH black background
    resized = cropped.resize((CANVAS_W, CANVAS_H), Image.LANCZOS)

    # NOW make black transparent
    result = make_black_transparent(resized)

    # Keep largest component
    result = keep_largest_component(result)

    return result


def split_h(img, n):
    arr = np.array(img.convert('RGB'))
    h, w = arr.shape[:2]
    brt = np.max(arr[:, :, :3], axis=2)
    col_brt = np.mean(brt, axis=0)
    is_gap = col_brt < 12
    segs = []
    ss = 0
    ig = False
    for x in range(w):
        if is_gap[x] and not ig:
            if x - ss > 20:
                segs.append((ss, x))
            ig = True
        elif not is_gap[x] and ig:
            ss = x
            ig = False
    if not ig and w - ss > 20:
        segs.append((ss, w))
    if len(segs) < n:
        cw = w // n
        segs = [(i * cw, (i + 1) * cw) for i in range(n)]
    return [img.crop((x1, 0, x2, h)) for x1, x2 in segs[:n]]


def split_grid(img, rows, cols):
    w, h = img.size
    cw = w // cols
    ch = h // rows
    cells = []
    for r in range(rows):
        for c in range(cols):
            cells.append(img.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch)))
    return cells


# ═══ Source mappings ═══

composite_map = {
    "ChatGPT Image 10. Feb. 2026, 22_47_13.png":
        (["kingdom_of_gondor", "iron_hills", "realm_of_angmar"], False),
    "ChatGPT Image 10. Feb. 2026, 22_49_19.png":
        (["easterlings_of_rhun", "tribes_of_harad", "variags_of_khand", "dark_lands_of_mordor"], True),
    "ChatGPT Image 10. Feb. 2026, 22_52_32.png":
        (["vale_of_anduin", "kingdom_of_rohan", "kingdom_of_arthedain",
          "dwarves_of_erebor", "elves_of_lothlorien", "elves_of_lindon"], True),
    "ChatGPT Image 10. Feb. 2026, 22_58_26.png":
        (["orcs_of_gundabad", "orcs_of_gundabad", "dominion_of_isengard", "havens_of_umbar"], True),
    "ChatGPT Image 10. Feb. 2026, 23_02_35.png":
        (["orcs_of_the_misty_mountains", "orcs_of_ered_luin", "orcs_of_forochel"], True),
    "ChatGPT Image 10. Feb. 2026, 23_03_33.png":
        (["orcs_of_the_misty_mountains", "orcs_of_gwaer", "orcs_of_narager"], True),
    "ChatGPT Image 10. Feb. 2026, 23_05_35.png":
        (["kingdom_of_dale", "realm_of_dorwinion", "hill_men_of_dunland"], True),
    "ChatGPT Image 10. Feb. 2026, 23_06_32.png":
        (["hill_men_of_rhudaur", "faithful_of_bellakar", "realm_of_cardolan"], True),
    "ChatGPT Image 10. Feb. 2026, 23_08_03.png":
        (["elves_of_rivendell", "elves_of_mirkwood", "elves_of_neldoreth"], True),
    "ChatGPT Image 11. Feb. 2026, 11_48_30.png":
        (["dwarves_of_orocarni", "clans_of_enedwaith", "forodwaith", "shadow_of_dol_guldur"], True),
}

grid_3x3_map = {
    "ChatGPT Image 10. Feb. 2026, 22_42_38.png": [
        "realm_of_dorwinion", "tribes_of_harad", "kingdom_of_gondor",
        "dwarves_of_erebor", "kingdom_of_rohan", "kingdom_of_arthedain",
        "dwarves_of_ered_luin", "fangorn_forest", "elves_of_lindon",
    ],
}

processed = {}

print("=== Processing ALL composites with v3 ===")

for filename, (factions, has_labels) in composite_map.items():
    filepath = os.path.join(flags_dir, filename)
    if not os.path.exists(filepath):
        print(f"  SKIP {filename}: not found")
        continue
    img = Image.open(filepath)
    w, h = img.size

    if h > w:
        cells = split_grid(img, 2, 2)
    else:
        cells = split_h(img, len(factions))

    for i, faction in enumerate(factions):
        if i >= len(cells):
            break
        if faction in processed:
            continue

        result = process_banner_v3(cells[i], has_labels=has_labels)
        if result:
            result.save(os.path.join(outdir, f"banner_{faction}.png"), optimize=True)
            processed[faction] = f"{filename}[{i}]"
            print(f"  {faction} <- {filename}[{i}]")

# 3x3 grids (no labels)
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
        result = process_banner_v3(cells[i], has_labels=False)
        if result:
            result.save(os.path.join(outdir, f"banner_{faction}.png"), optimize=True)
            processed[faction] = f"{filename}[{i}]"
            print(f"  {faction} <- {filename}[{i}]")

# Report
print(f"\nProcessed {len(processed)} banners")
problems = 0
for faction in sorted(processed.keys()):
    fpath = os.path.join(outdir, f"banner_{faction}.png")
    arr = np.array(Image.open(fpath).convert('RGBA'))
    l0 = np.mean(arr[:, 0, 3])
    r83 = np.mean(arr[:, 83, 3])
    flag = ""
    if l0 < 80 or r83 < 80:
        flag = " <-- low edge"
        problems += 1
    print(f"  {faction:<35} L={l0:>5.0f}  R={r83:>5.0f}{flag}")

print(f"\n{problems} banners with low edge alpha")

# Copy all to game dir
for faction in processed:
    src = os.path.join(outdir, f"banner_{faction}.png")
    dst = os.path.join(gamedir, f"banner_{faction}.png")
    shutil.copy2(src, dst)
print("Copied to game dir")
