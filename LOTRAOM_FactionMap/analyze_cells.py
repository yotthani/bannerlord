import sys
sys.stdout.reconfigure(encoding='utf-8')

from PIL import Image
import numpy as np

base = r'C:\Work\Sources\github\LOTRAOM_FactionMap'
img = Image.open(f'{base}\Gemini_Generated_Image_3500x03500x03500 (1).png').convert('RGBA')
arr = np.array(img)
print(f"Composite size: {arr.shape[1]}x{arr.shape[0]}")

# Grid params
num_cols = 11
row_bounds = [(23, 460), (530, 946), (1035, 1430)]
col_w = arr.shape[1] / num_cols

# For each cell, find the actual banner boundaries by looking for non-dark content
# Dark = average brightness < 40
for row_idx, (y1, y2) in enumerate(row_bounds):
    for col_idx in range(num_cols):
        x1 = int(col_idx * col_w)
        x2 = int((col_idx + 1) * col_w)
        cell = arr[y1:y2, x1:x2, :3]
        ch, cw = cell.shape[:2]
        
        # Find brightness per column (average across height)
        col_brightness = np.mean(cell, axis=(0, 2))  # shape: (cw,)
        
        # Find left and right edges where brightness > 50
        bright_cols = np.where(col_brightness > 50)[0]
        if len(bright_cols) == 0:
            print(f"[{row_idx},{col_idx}] EMPTY - no bright content")
            continue
        
        left = bright_cols[0]
        right = bright_cols[-1]
        content_width = right - left + 1
        center = (left + right) / 2
        
        # Also check row brightness for top/bottom
        row_brightness = np.mean(cell, axis=(1, 2))  # shape: (ch,)
        bright_rows = np.where(row_brightness > 50)[0]
        top = bright_rows[0] if len(bright_rows) > 0 else 0
        bottom = bright_rows[-1] if len(bright_rows) > 0 else ch-1
        content_height = bottom - top + 1
        
        # Mean brightness of center region
        cx1 = max(0, int(center - content_width*0.3))
        cx2 = min(cw, int(center + content_width*0.3))
        center_brightness = np.mean(cell[top:bottom, cx1:cx2])
        
        print(f"[{row_idx},{col_idx}] cell={cw}x{ch} content: x={left}-{right} ({content_width}px, {content_width/cw*100:.0f}%) y={top}-{bottom} ({content_height}px) center_bright={center_brightness:.0f} offset_from_center={center/cw*100-50:.1f}%")
