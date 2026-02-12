import sys, os
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image
import numpy as np

base = r'C:\Work\Sources\github\LOTRAOM_FactionMap'
img = Image.open(os.path.join(base, 'flags_small_33.PNG')).convert('RGBA')
arr = np.array(img)
h, w = arr.shape[:2]
print(f"Image: {w}x{h}")

# Create pink mask - generous range for the magenta background
r, g, b = arr[:,:,0], arr[:,:,1], arr[:,:,2]
is_pink = (r.astype(int) - b.astype(int)).astype(float)  # pink has R~=B, high
# Better: check if pixel is close to magenta (#FF69B4 range or #FF00FF range)
# The pink in the image looks like a hot pink / magenta
# Let's check actual pink pixel values
pink_samples = []
for y in [5, 10, h-5]:
    for x in [5, 10, w-5]:
        if True:  # edges should be pink
            pink_samples.append((arr[y,x,0], arr[y,x,1], arr[y,x,2]))
print(f"Pink samples (corners): {pink_samples}")

# Pink detection: R > 180, G < 150, B > 130, and R-G > 80
is_pink = (arr[:,:,0].astype(int) > 180) & (arr[:,:,1].astype(int) < 150) & (arr[:,:,2].astype(int) > 130) & ((arr[:,:,0].astype(int) - arr[:,:,1].astype(int)) > 60)
not_pink = ~is_pink

# Also exclude the text labels (they're between banner bottom and next row)
# The labels are dark text on pink, so they'd be detected as non-pink
# Let's ignore that for now and focus on the larger objects

# Use scipy.ndimage.label for connected component analysis
from scipy import ndimage

# Label connected non-pink regions
labeled, num_features = ndimage.label(not_pink)
print(f"Found {num_features} connected non-pink regions")

# Get bounding box for each region
objects = ndimage.find_objects(labeled)

# Filter: only keep objects that are banner-sized (at least 30x50 pixels)
banners = []
for i, slc in enumerate(objects):
    if slc is None:
        continue
    y_slice, x_slice = slc
    obj_h = y_slice.stop - y_slice.start
    obj_w = x_slice.stop - x_slice.start
    obj_area = np.sum(labeled[slc] == (i+1))
    if obj_h > 50 and obj_w > 30 and obj_area > 2000:
        banners.append({
            'idx': i+1,
            'x1': x_slice.start, 'y1': y_slice.start,
            'x2': x_slice.stop, 'y2': y_slice.stop,
            'w': obj_w, 'h': obj_h,
            'area': obj_area
        })

# Sort by y then x
banners.sort(key=lambda b: (b['y1'], b['x1']))

print(f"\nFound {len(banners)} banner-sized objects:")
for i, b in enumerate(banners):
    print(f"  #{i:2d}: x={b['x1']:4d}-{b['x2']:4d} y={b['y1']:4d}-{b['y2']:4d}  size={b['w']}x{b['h']}  area={b['area']}")

# Group into rows based on y position
rows = []
current_row = [banners[0]] if banners else []
for b in banners[1:]:
    if b['y1'] > current_row[-1]['y1'] + 100:  # new row if >100px gap
        rows.append(current_row)
        current_row = [b]
    else:
        current_row.append(b)
if current_row:
    rows.append(current_row)

print(f"\n{len(rows)} rows detected:")
for ri, row in enumerate(rows):
    print(f"  Row {ri}: {len(row)} banners, y range: {row[0]['y1']}-{row[0]['y2']}")
    for b in row:
        print(f"    x={b['x1']:4d}-{b['x2']:4d}  {b['w']}x{b['h']}")
