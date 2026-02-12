import sys, os
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image
import numpy as np

base = r'C:\Work\Sources\github\LOTRAOM_FactionMap'
img = Image.open(os.path.join(base, 'flags_small_33.PNG')).convert('RGBA')
arr = np.array(img)
h, w = arr.shape[:2]

r, g, b = arr[:,:,0].astype(np.int16), arr[:,:,1].astype(np.int16), arr[:,:,2].astype(np.int16)
is_pink = (r > 160) & (g < 150) & ((r - g) > 50) & (b > 100) & (b < 210)

# For each row of pixels, what percentage is pink?
row_pink = np.mean(is_pink, axis=1)

# Print every row with >50% pink (these are the gaps/text rows)
print("=== Rows with >70% pink (separators) ===")
in_separator = False
sep_start = 0
for y in range(h):
    if row_pink[y] > 0.70:
        if not in_separator:
            sep_start = y
            in_separator = True
    else:
        if in_separator:
            print(f"  Pink band: y={sep_start}-{y-1} ({y-sep_start}px)")
            in_separator = False

if in_separator:
    print(f"  Pink band: y={sep_start}-{h-1} ({h-sep_start}px)")

# Now let's look at each cell more carefully
# For row 0, let's check where the actual banner content ends vs text starts
print("\n=== Detailed row analysis ===")

# For each of the 3 banner rows, find exact content bounds
# We know approximate row starts. Let's scan more carefully.
# Look at the MIDDLE column (col 5-6) since edge cols might be different
mid_col_start = int(5 * w / 11)
mid_col_end = int(6 * w / 11)

print("\nMiddle column (col 5-6) pink scan:")
for y in range(h):
    mid_pink = np.mean(is_pink[y, mid_col_start:mid_col_end])
    if mid_pink > 0.5:
        # Check if this is a text row or pure separator
        # Text rows have scattered non-pink (text) pixels
        # Pure separator is nearly 100% pink
        if mid_pink > 0.95:
            kind = "PURE SEP"
        else:
            kind = "partial"
        if y % 1 == 0 and (y < 30 or (440 < y < 540) or (940 < y < 1040) or (1440 < y < 1540)):
            print(f"  y={y:4d}: pink={mid_pink:.2f} {kind}")

# Now check specific cells for the Harad/Angmar issues
# Harad is [0,1] - check if the cell boundary has content bleeding
print("\n=== Cell [0,1] Tribes of Harad - column analysis ===")
y1, y2 = 23, 460
col_w_px = w / 11
x1 = int(1 * col_w_px)
x2 = int(2 * col_w_px)
cell = arr[y1:y2, x1:x2]
cell_pink = is_pink[y1:y2, x1:x2]

# Check first and last few columns for pink
for dx in range(0, min(30, x2-x1)):
    pct = np.mean(cell_pink[:, dx])
    if dx < 10 or pct > 0.5:
        print(f"  col {dx}: pink={pct:.2f}")

print("  ...")
cw = x2 - x1
for dx in range(max(0, cw-10), cw):
    pct = np.mean(cell_pink[:, dx])
    print(f"  col {dx}: pink={pct:.2f}")

# Check Angmar [2,8] - why does it have neighbor text?
print("\n=== Cell [2,8] Realm of Angmar - top rows ===")
y1, y2 = 965, 1460
x1 = int(8 * col_w_px)
x2 = int(9 * col_w_px)
cell = arr[y1:y2, x1:x2]
cell_pink = is_pink[y1:y2, x1:x2]

# Check top rows
for dy in range(min(80, y2-y1)):
    pct = np.mean(cell_pink[dy, :])
    if pct > 0.3:
        print(f"  row {dy} (y={y1+dy}): pink={pct:.2f}")

# Also check what's at the very top of row 2 - is there text from row 1?
print("\n=== Area around y=950-1040 (between row 1 and row 2) ===")
for y in range(940, 1050):
    pct = np.mean(is_pink[y, :])
    non_pink_count = np.sum(~is_pink[y, :])
    print(f"  y={y}: pink={pct:.2f} non_pink_pixels={non_pink_count}")
