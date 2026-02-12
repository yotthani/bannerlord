import sys, os
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image
import numpy as np

base = r'C:\Work\Sources\github\LOTRAOM_FactionMap'
outdir = os.path.join(base, '_cell_debug')
os.makedirs(outdir, exist_ok=True)

img = Image.open(os.path.join(base, 'Gemini_Generated_Image_3500x03500x03500 (1).png')).convert('RGBA')
arr = np.array(img)
h, w = arr.shape[:2]
print(f"Composite: {w}x{h}")

row_bounds = [(23, 460), (530, 946), (1035, 1430)]
num_cols = 11
col_w = w / num_cols

# For each cell, find where the actual banner content is
# by looking at brightness profiles from left and right
for ri, (y1, y2) in enumerate(row_bounds):
    for ci in range(num_cols):
        x1 = int(ci * col_w)
        x2 = int((ci + 1) * col_w)
        cell = arr[y1:y2, x1:x2]
        ch, cw = cell.shape[:2]
        
        # Save raw cell
        cell_img = Image.fromarray(cell)
        cell_img.save(os.path.join(outdir, f'cell_r{ri}_c{ci:02d}_raw.png'))
        
        # Save 4x zoomed cell (nearest neighbor to see exact pixels)
        zoomed = cell_img.resize((cw * 4, ch * 4), Image.NEAREST)
        zoomed.save(os.path.join(outdir, f'cell_r{ri}_c{ci:02d}_4x.png'))
        
        # Brightness per column
        brightness = np.mean(cell[:,:,:3], axis=(0,2))  # avg brightness per column
        
        # Find first/last column with brightness > 25 (non-background)
        bright_cols = np.where(brightness > 25)[0]
        if len(bright_cols) > 0:
            left = bright_cols[0]
            right = bright_cols[-1]
            
            # Also per row
            row_brightness = np.mean(cell[:,:,:3], axis=(1,2))
            bright_rows = np.where(row_brightness > 25)[0]
            top = bright_rows[0] if len(bright_rows) > 0 else 0
            bottom = bright_rows[-1] if len(bright_rows) > 0 else ch-1
            
            # Absolute coords in composite
            abs_x1 = x1 + left
            abs_x2 = x1 + right + 1
            abs_y1 = y1 + top
            abs_y2 = y1 + bottom + 1
            
            banner_w = right - left + 1
            banner_h = bottom - top + 1
            aspect = banner_h / banner_w if banner_w > 0 else 0
            
            print(f"[{ri},{ci}] cell=({x1},{y1})-({x2},{y2}) banner=({abs_x1},{abs_y1})-({abs_x2},{abs_y2}) size={banner_w}x{banner_h} aspect={aspect:.2f}")
        else:
            print(f"[{ri},{ci}] EMPTY")
