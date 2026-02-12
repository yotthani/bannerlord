import sys, os
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image
import numpy as np

base = r'C:\Work\Sources\github\LOTRAOM_FactionMap'
img = Image.open(os.path.join(base, 'flags_small_33.PNG')).convert('RGBA')
arr = np.array(img)
h, w = arr.shape[:2]
print(f"Image size: {w}x{h}")

# Detect pink pixels: R > 200, G < 100, B > 150 (magenta range)
r, g, b = arr[:,:,0], arr[:,:,1], arr[:,:,2]
is_pink = (r > 180) & (g < 120) & (b > 140)

# Find rows that are mostly pink (>80% pink) - these are the gaps between banner rows
row_pink_pct = np.mean(is_pink, axis=1)
print("\nRow pink percentage (sampled):")
for y in range(0, h, 10):
    bar = '#' * int(row_pink_pct[y] * 50)
    print(f"  y={y:4d}: {row_pink_pct[y]:.2f} {bar}")

# Find horizontal separators (rows that are >90% pink)
separator_rows = np.where(row_pink_pct > 0.90)[0]
if len(separator_rows) > 0:
    # Find gaps in separator rows to identify row boundaries
    gaps = np.where(np.diff(separator_rows) > 1)[0]
    print(f"\nSeparator row ranges:")
    start = separator_rows[0]
    for gap_idx in gaps:
        end = separator_rows[gap_idx]
        print(f"  y={start}-{end} (pink band)")
        start = separator_rows[gap_idx + 1]
    end = separator_rows[-1]
    print(f"  y={start}-{end} (pink band)")

# Similarly for columns
col_pink_pct = np.mean(is_pink, axis=0)
print("\nColumn pink percentage (sampled):")
for x in range(0, w, 20):
    bar = '#' * int(col_pink_pct[x] * 50)
    print(f"  x={x:4d}: {col_pink_pct[x]:.2f} {bar}")

# Find vertical separators (columns that are >80% pink) 
separator_cols = np.where(col_pink_pct > 0.70)[0]
if len(separator_cols) > 0:
    gaps = np.where(np.diff(separator_cols) > 1)[0]
    print(f"\nSeparator column ranges:")
    start = separator_cols[0]
    for gap_idx in gaps:
        end = separator_cols[gap_idx]
        print(f"  x={start}-{end} (pink band, width={end-start+1})")
        start = separator_cols[gap_idx + 1]
    end = separator_cols[-1]
    print(f"  x={start}-{end} (pink band, width={end-start+1})")

print("\nDone")
