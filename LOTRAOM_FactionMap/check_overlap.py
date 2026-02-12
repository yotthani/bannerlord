import sys, os
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image
import numpy as np

base = r'C:\Work\Sources\github\LOTRAOM_FactionMap'
img = Image.open(os.path.join(base, 'flags_small_33.PNG')).convert('RGBA')
arr = np.array(img)

r = arr[:,:,0].astype(np.int16)
g = arr[:,:,1].astype(np.int16)
b = arr[:,:,2].astype(np.int16)
is_pink = (r > 160) & (g < 150) & ((r - g) > 50) & (b > 100) & (b < 220)

# Harad is [0,1], row 0 = y 26-423, col 1
col_w = arr.shape[1] / 11
x1 = int(1 * col_w)
x2 = int(2 * col_w)

print(f"Harad cell [0,1]: x={x1}-{x2}, width={x2-x1}")

# Check left edge of Harad cell - how many columns from the left are non-pink?
# (these would be Rhun's banner bleeding in)
for dx in range(40):
    x = x1 + dx
    col_pink_pct = np.mean(is_pink[26:423, x])
    non_pink_pct = 1.0 - col_pink_pct
    if non_pink_pct > 0.05:
        # Check what color the non-pink pixels are
        non_pink_mask = ~is_pink[26:423, x]
        if np.any(non_pink_mask):
            avg_color = np.mean(arr[26:423, x][non_pink_mask][:, :3], axis=0)
            print(f"  x={x} (offset +{dx}): non_pink={non_pink_pct:.2f}, avg_color=({avg_color[0]:.0f},{avg_color[1]:.0f},{avg_color[2]:.0f})")

# Now check Angmar [2,8] right edge
print(f"\nAngmar cell [2,8]:")
x1_a = int(8 * col_w)
x2_a = int(9 * col_w)
print(f"  x={x1_a}-{x2_a}")

# Check right edge
for dx in range(40):
    x = x2_a - 1 - dx
    col_pink_pct = np.mean(is_pink[1037:1427, x])
    non_pink_pct = 1.0 - col_pink_pct
    if non_pink_pct > 0.05:
        non_pink_mask = ~is_pink[1037:1427, x]
        if np.any(non_pink_mask):
            avg_color = np.mean(arr[1037:1427, x][non_pink_mask][:, :3], axis=0)
            print(f"  x={x} (offset -{dx} from right): non_pink={non_pink_pct:.2f}, avg_color=({avg_color[0]:.0f},{avg_color[1]:.0f},{avg_color[2]:.0f})")

# Also check: where does the pink gap between Rhun and Harad start/end?
print(f"\nPink gap between Rhun [0,0] and Harad [0,1]:")
x_rhun_end = int(1 * col_w)  # nominal boundary
for x in range(x_rhun_end - 30, x_rhun_end + 30):
    pct = np.mean(is_pink[26:423, x])
    if 0.3 < pct < 0.99:
        print(f"  x={x}: pink={pct:.2f} ({'<-- boundary' if x == x_rhun_end else ''})")
