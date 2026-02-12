#!/usr/bin/env python3
"""
Middle-earth Kingdom Map Extractor v13
Extracts colored kingdom regions from a political map of Middle-earth.

Input:  Political map image (JPG/PNG)
Output: JSON data, label map, colored visualization, overlay, masks, ZIP

Usage:
    python3 extract_kingdoms.py <input_image> [output_dir]
    
    Default output_dir: ./kingdom_export/

Algorithm:
    1. Border detection via blackhat morphology + Canny edges
    2. Dark blob rescue (preserves Lothlórien, large dark terrain features)
    3. HSV gradient edges in dark zones (separates similarly-dark regions)
    4. Watershed segmentation on detected seeds
    5. Hue-aware region merging with brightness-adaptive thresholds
    6. Protected region system (dark blobs can't merge with non-dark regions)
    7. Mordor post-merge (dark regions in SE quadrant)
    8. Misty Mountains merge (dark regions at specific location)
    9. Manual fixes: Orthanc circle carve-out, Sea of Rhûn extraction
    10. Gap filling via nearest-neighbor, small region absorption

Requirements:
    pip install opencv-python numpy pillow scipy
"""

import cv2
import numpy as np
from PIL import Image, ImageDraw
from collections import defaultdict
from scipy import ndimage
import colorsys, json, os, sys, zipfile


def extract_kingdoms(input_image_path, output_dir="./kingdom_export"):
    """Main extraction pipeline."""
    
    # === Load image ===
    img = cv2.imread(input_image_path)
    if img is None:
        print(f"Error: Could not load image: {input_image_path}")
        return
    img_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    h, w = gray.shape
    print(f"Image: {w}x{h}")

    # === STEP 1: Border detection ===
    print("Step 1: Border detection...")
    smooth = cv2.bilateralFilter(gray, 9, 75, 75)
    smooth = cv2.bilateralFilter(smooth, 9, 75, 75)
    
    # Blackhat morphology: extracts thin dark structures (borders), ignores thick dark areas
    kernel_bh = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (11, 11))
    blackhat = cv2.morphologyEx(smooth, cv2.MORPH_BLACKHAT, kernel_bh)
    _, border_bh = cv2.threshold(blackhat, 12, 255, cv2.THRESH_BINARY)
    
    # Canny edges on smoothed image
    edges_smooth = cv2.Canny(smooth, 30, 80)
    edges_smooth = cv2.dilate(edges_smooth, np.ones((2,2), np.uint8), iterations=1)
    
    # Color-channel Canny in dark zones (separates similarly-dark regions)
    dark_zone = (smooth < 70).astype(np.uint8)
    b_s = cv2.bilateralFilter(img[:,:,0], 7, 50, 50)
    g_s = cv2.bilateralFilter(img[:,:,1], 7, 50, 50)
    r_s = cv2.bilateralFilter(img[:,:,2], 7, 50, 50)
    ce = cv2.bitwise_or(cv2.bitwise_or(
        cv2.Canny(b_s, 20, 60), cv2.Canny(g_s, 20, 60)), cv2.Canny(r_s, 20, 60))
    ce_dark = cv2.bitwise_and(ce, dark_zone * 255)
    
    border = cv2.bitwise_or(border_bh, edges_smooth)
    border = cv2.bitwise_or(border, ce_dark)
    border = cv2.morphologyEx(border, cv2.MORPH_CLOSE, np.ones((5,5), np.uint8), iterations=1)
    border = cv2.dilate(border, np.ones((2,2), np.uint8), iterations=1)

    # === STEP 2: Dark blob rescue ===
    print("Step 2: Dark blob rescue...")
    very_dark = (smooth < 35).astype(np.uint8) * 255
    num_dark_blobs, dark_labels = cv2.connectedComponents(very_dark)
    
    border_fixed = border.copy()
    protected_region_ids = set()
    rescued_blob_masks = []
    
    for blob_id in range(1, num_dark_blobs):
        blob_mask = (dark_labels == blob_id)
        blob_size = blob_mask.sum()
        if blob_size < 400:
            continue
        blob_u8 = blob_mask.astype(np.uint8) * 255
        contours, _ = cv2.findContours(blob_u8, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        if not contours:
            continue
        perimeter = sum(cv2.arcLength(c, True) for c in contours)
        if perimeter == 0:
            continue
        compactness = 4 * np.pi * blob_size / (perimeter ** 2)
        
        if compactness > 0.03 and blob_size > 400:
            # Remove blob interior from border mask
            border_fixed[blob_mask] = 0
            # Add blob perimeter as border (keeps it separate from neighbors)
            dilated = cv2.dilate(blob_u8, np.ones((3,3), np.uint8), iterations=2)
            blob_border = cv2.subtract(dilated, blob_u8)
            border_fixed = cv2.bitwise_or(border_fixed, blob_border)
            rescued_blob_masks.append(blob_mask)
            ys, xs = np.where(blob_mask)
            print(f"  Rescued blob: {blob_size}px, compact={compactness:.3f}, "
                  f"center=({int(xs.mean())},{int(ys.mean())})")
    
    print(f"  Rescued: {len(rescued_blob_masks)} dark blobs")

    # === STEP 3: HSV gradient edges in dark zones ===
    print("Step 3: HSV edges in dark zones...")
    hue_ch = hsv[:,:,0].astype(np.float32)
    sat_ch = hsv[:,:,1].astype(np.float32)
    hue_s = cv2.bilateralFilter(hue_ch, 9, 30, 30)
    sat_s = cv2.bilateralFilter(sat_ch, 9, 30, 30)
    
    hue_grad = np.sqrt(
        cv2.Sobel(hue_s, cv2.CV_32F, 1, 0, ksize=3)**2 +
        cv2.Sobel(hue_s, cv2.CV_32F, 0, 1, ksize=3)**2)
    sat_grad = np.sqrt(
        cv2.Sobel(sat_s, cv2.CV_32F, 1, 0, ksize=3)**2 +
        cv2.Sobel(sat_s, cv2.CV_32F, 0, 1, ksize=3)**2)
    
    dark_area = (smooth < 80).astype(np.uint8)
    hsv_edges = ((hue_grad > 8) | (sat_grad > 15)).astype(np.uint8) * 255
    hsv_edges_dark = cv2.bitwise_and(hsv_edges, dark_area * 255)
    
    kernel_thin = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
    hsv_opened = cv2.morphologyEx(hsv_edges_dark, cv2.MORPH_OPEN, kernel_thin)
    hsv_thin = cv2.subtract(hsv_edges_dark, hsv_opened)
    
    border_fixed = cv2.bitwise_or(border_fixed, hsv_thin)
    border_fixed = cv2.morphologyEx(border_fixed, cv2.MORPH_CLOSE, np.ones((3,3), np.uint8), iterations=1)
    
    print(f"  Border coverage: {(border_fixed>0).sum()*100/(h*w):.1f}%")

    # === STEP 4: Watershed segmentation ===
    print("Step 4: Watershed segmentation...")
    non_border = cv2.bitwise_not(border_fixed)
    non_border = cv2.erode(non_border, np.ones((2,2), np.uint8), iterations=1)
    num_labels, labels_seed = cv2.connectedComponents(non_border)
    markers = labels_seed.copy().astype(np.int32)
    markers[border_fixed > 0] = 0
    ws = cv2.watershed(img, markers)
    
    region_colors = {}; region_sizes = {}; region_hues = {}; region_sats = {}
    for i in range(1, num_labels):
        mask = (ws == i); size = mask.sum()
        if size < 50:
            continue
        region_colors[i] = img_rgb[mask].mean(axis=0)
        region_sizes[i] = size
        region_hues[i] = hsv[:,:,0][mask].mean()
        region_sats[i] = hsv[:,:,1][mask].mean()
    
    print(f"  Fine segments: {len(region_colors)}")
    
    # Identify protected regions (overlap with rescued dark blobs)
    for blob_mask in rescued_blob_masks:
        blob_ids = ws[blob_mask]
        blob_ids = blob_ids[blob_ids > 0]
        if len(blob_ids) == 0:
            continue
        vals, counts = np.unique(blob_ids, return_counts=True)
        for v, c in zip(vals, counts):
            if c > 100 and v in region_colors:
                protected_region_ids.add(v)
    
    print(f"  Protected regions: {len(protected_region_ids)}")

    # === STEP 5: Build adjacency graph ===
    print("Step 5: Adjacency + merging...")
    adjacency = defaultdict(set)
    bys, bxs = np.where(ws == -1)
    for idx in range(0, len(bys), 2):
        y, x = bys[idx], bxs[idx]
        patch = ws[max(0,y-2):min(h,y+3), max(0,x-2):min(w,x+3)]
        valid = [l for l in set(patch.flatten()) if l > 0 and l in region_colors]
        for i in range(len(valid)):
            for j in range(i+1, len(valid)):
                adjacency[valid[i]].add(valid[j])
                adjacency[valid[j]].add(valid[i])

    # === STEP 6: Hue-aware merging with protected regions ===
    parent = {i: i for i in region_colors}
    rnk = {i: 0 for i in region_colors}
    
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    
    def union(a, b):
        ra, rb = find(a), find(b)
        if ra == rb: return
        if rnk[ra] < rnk[rb]: ra, rb = rb, ra
        parent[rb] = ra
        if rnk[ra] == rnk[rb]: rnk[ra] += 1
    
    pairs = sorted([
        (np.sqrt(np.sum((region_colors[r] - region_colors[n])**2)), r, n)
        for r, nbs in adjacency.items() for n in nbs if r < n
    ])
    
    for pass_num in range(5):
        groups = defaultdict(list)
        for rid in region_colors:
            groups[find(rid)].append(rid)
        
        gavg = {}; gsz = {}; ghue = {}; gsat = {}
        for root, members in groups.items():
            t = sum(region_sizes[m] for m in members)
            gavg[root] = sum(region_colors[m] * region_sizes[m] for m in members) / t
            gsz[root] = t
            ghue[root] = sum(region_hues[m] * region_sizes[m] for m in members) / t
            gsat[root] = sum(region_sats[m] * region_sizes[m] for m in members) / t
        
        merged = 0
        for _, rid, nid in pairs:
            ra, rb = find(rid), find(nid)
            if ra == rb:
                continue
            
            # Protected region logic
            group_a = set(groups.get(ra, [ra]))
            group_b = set(groups.get(rb, [rb]))
            a_prot = bool(group_a & protected_region_ids)
            b_prot = bool(group_b & protected_region_ids)
            
            if a_prot != b_prot:
                continue  # One protected, one not → don't merge
            
            if a_prot and b_prot:
                # Both protected → only merge if very similar
                c1 = gavg.get(ra, region_colors[ra])
                c2 = gavg.get(rb, region_colors[rb])
                if np.sqrt(np.sum((c1 - c2)**2)) < 15:
                    sz1, sz2 = gsz.get(ra, 1), gsz.get(rb, 1)
                    union(ra, rb)
                    nr = find(ra)
                    gavg[nr] = (c1*sz1 + c2*sz2) / (sz1+sz2)
                    gsz[nr] = sz1 + sz2
                    merged += 1
                continue
            
            # Normal merging with hue-aware thresholds
            c1 = gavg.get(ra, region_colors[ra])
            c2 = gavg.get(rb, region_colors[rb])
            dist_rgb = np.sqrt(np.sum((c1 - c2)**2))
            
            h1, h2 = ghue.get(ra, 0), ghue.get(rb, 0)
            s1_v, s2_v = gsat.get(ra, 0), gsat.get(rb, 0)
            hue_diff = min(abs(h1 - h2), 180 - abs(h1 - h2))
            sat_diff = abs(s1_v - s2_v)
            bri = (np.mean(c1) + np.mean(c2)) / 2
            
            # Brightness-adaptive base threshold
            base = 28 if bri < 50 else (35 if bri < 80 else (40 if bri < 120 else 44))
            
            # Dark regions: hue/sat differences weighted more heavily
            if bri < 80:
                if hue_diff > 10: base *= 0.3
                elif hue_diff > 5: base *= 0.5
                if sat_diff > 20: base *= 0.6
            else:
                if hue_diff > 12: base *= 0.5
                elif hue_diff > 6: base *= 0.75
            
            if dist_rgb < base:
                sz1, sz2 = gsz.get(ra, 1), gsz.get(rb, 1)
                union(ra, rb)
                nr = find(ra)
                gavg[nr] = (c1*sz1 + c2*sz2) / (sz1+sz2)
                gsz[nr] = sz1 + sz2
                ghue[nr] = (h1*sz1 + h2*sz2) / (sz1+sz2)
                gsat[nr] = (s1_v*sz1 + s2_v*sz2) / (sz1+sz2)
                merged += 1
        
        print(f"  Pass {pass_num+1}: {merged} merges")
        if merged == 0:
            break

    # === STEP 7: Build initial kingdom map ===
    print("Step 7: Building kingdom map...")
    final_groups = defaultdict(list)
    for rid in region_colors:
        final_groups[find(rid)].append(rid)
    sorted_groups = sorted(final_groups.items(),
                           key=lambda x: -sum(region_sizes.get(m, 0) for m in x[1]))
    
    kingdom_map = np.zeros((h, w), dtype=np.uint16)
    all_kingdoms = []
    kid = 0
    for root, members in sorted_groups:
        total = sum(region_sizes.get(m, 0) for m in members)
        kid += 1
        mask = np.zeros((h, w), dtype=bool)
        for m in members:
            mask |= (ws == m)
        kingdom_map[mask] = kid
        csum = sum(region_colors[m] * region_sizes[m] for m in members)
        avg = (csum / total).astype(int).tolist()
        is_prot = bool(set(members) & protected_region_ids)
        ys, xs = np.where(mask)
        all_kingdoms.append({
            'id': kid, 'size': total, 'pct': total*100/(h*w),
            'avg_color': avg, 'protected': is_prot,
            'center': (int(xs.mean()), int(ys.mean()))
        })

    # === STEP 8: Fill gaps ===
    unassigned = (kingdom_map == 0)
    if unassigned.any():
        _, nearest = ndimage.distance_transform_edt(unassigned, return_indices=True)
        km = kingdom_map.copy()
        km[unassigned] = kingdom_map[nearest[0][unassigned], nearest[1][unassigned]]
    else:
        km = kingdom_map.copy()

    # === STEP 9: Absorb tiny regions (except protected) ===
    MIN_PCT = 0.25
    for k in all_kingdoms:
        if k['pct'] >= MIN_PCT or k['protected']:
            continue
        small_mask = (km == k['id'])
        if not small_mask.any():
            continue
        dilated = cv2.dilate(small_mask.astype(np.uint8), np.ones((5,5), np.uint8), iterations=3)
        nzone = dilated.astype(bool) & ~small_mask
        nids = km[nzone]
        nids = nids[(nids != k['id']) & (nids != 0)]
        if len(nids) > 0:
            vals, counts = np.unique(nids, return_counts=True)
            km[small_mask] = vals[counts.argmax()]

    # === STEP 10: Mordor post-merge ===
    print("Step 10: Post-merge fixes...")
    mordor_bbox = (1050, 650, 1600, 1000)
    mordor_ids = set()
    for k in all_kingdoms:
        cx, cy = k['center']
        if mordor_bbox[0] <= cx <= mordor_bbox[2] and mordor_bbox[1] <= cy <= mordor_bbox[3]:
            mask = (km == k['id'])
            if mask.any() and gray[mask].mean() < 55:
                mordor_ids.add(k['id'])
    if len(mordor_ids) > 1:
        main = max(mordor_ids, key=lambda mid: (km == mid).sum())
        for mid in mordor_ids:
            if mid != main:
                km[km == mid] = main
        print(f"  Mordor merged: {mordor_ids} → {main}")

    # === STEP 11: Misty Mountains merge ===
    # Find dark regions at ~(796,395) and ~(769,452)
    mm_id_at_a = km[395, 796]
    mm_id_at_b = km[452, 769]
    if mm_id_at_a != mm_id_at_b and mm_id_at_a > 0 and mm_id_at_b > 0:
        # Merge smaller into larger
        sz_a = (km == mm_id_at_a).sum()
        sz_b = (km == mm_id_at_b).sum()
        if sz_a >= sz_b:
            km[km == mm_id_at_b] = mm_id_at_a
            print(f"  Misty Mountains merged: {mm_id_at_b} → {mm_id_at_a}")
        else:
            km[km == mm_id_at_a] = mm_id_at_b
            print(f"  Misty Mountains merged: {mm_id_at_a} → {mm_id_at_b}")

    # === STEP 12: Orthanc circle carve-out ===
    print("Step 12: Orthanc circle...")
    area = np.zeros((h, w), dtype=np.uint8)
    area[580:680, 770:860] = 255
    dark_ring = ((smooth < 45) & (area > 0)).astype(np.uint8) * 255
    dark_ring = cv2.morphologyEx(dark_ring, cv2.MORPH_CLOSE, np.ones((5,5), np.uint8), iterations=2)
    inv = cv2.bitwise_not(dark_ring)
    flood = inv.copy()
    cv2.floodFill(flood, None, (0, 0), 0)
    interior = flood.copy()
    interior[dark_ring > 0] = 255
    orthanc_mask = (interior > 0) & (area > 0)
    orthanc_u8 = orthanc_mask.astype(np.uint8) * 255
    orthanc_u8 = cv2.morphologyEx(orthanc_u8, cv2.MORPH_OPEN, np.ones((3,3), np.uint8))
    orthanc_u8 = cv2.morphologyEx(orthanc_u8, cv2.MORPH_CLOSE, np.ones((3,3), np.uint8))
    n_blobs, blob_labels = cv2.connectedComponents(orthanc_u8)
    if n_blobs > 1:
        best = max(range(1, n_blobs), key=lambda b: (blob_labels == b).sum())
        orthanc_final = (blob_labels == best)
        new_orthanc_id = km.max() + 1
        km[orthanc_final] = new_orthanc_id
        ys, xs = np.where(orthanc_final)
        print(f"  Orthanc: {orthanc_final.sum()}px, center=({int(xs.mean())},{int(ys.mean())})")

    # === STEP 13: Sea of Rhûn extraction ===
    print("Step 13: Sea of Rhûn...")
    # Find the main Rhûn region (largest olive/brown region in east)
    rhun_candidates = []
    for kid_id in range(1, int(km.max()) + 1):
        mask = (km == kid_id)
        if not mask.any():
            continue
        ys, xs = np.where(mask)
        cx = int(xs.mean())
        if cx > 1500 and mask.sum() > 100000:
            rhun_candidates.append((kid_id, mask.sum()))
    
    if rhun_candidates:
        rhun_id = max(rhun_candidates, key=lambda x: x[1])[0]
        rhun_mask = (km == rhun_id)
        
        # Sea = warmer hue + higher warmth within Rhûn
        r_ch = img[:,:,2].astype(float)
        b_ch = img[:,:,0].astype(float)
        warmth = r_ch - b_ch
        
        # Get surrounding Rhûn warmth baseline
        surr_warmth = warmth[rhun_mask].mean()
        surr_hue = hsv[:,:,0][rhun_mask].mean()
        
        sea_candidate = rhun_mask & (warmth > surr_warmth + 5) & (hsv[:,:,0] < surr_hue - 1)
        sea_u8 = sea_candidate.astype(np.uint8) * 255
        sea_u8 = cv2.morphologyEx(sea_u8, cv2.MORPH_CLOSE, np.ones((7,7), np.uint8), iterations=2)
        sea_u8 = cv2.morphologyEx(sea_u8, cv2.MORPH_OPEN, np.ones((5,5), np.uint8), iterations=2)
        
        # Use border-enclosed detection for better contour
        rhun_area = np.zeros((h, w), dtype=np.uint8)
        rhun_area[380:580, 1440:1680] = 255
        local_borders = cv2.bitwise_or(
            cv2.threshold(cv2.morphologyEx(smooth, cv2.MORPH_BLACKHAT, kernel_bh), 10, 255, cv2.THRESH_BINARY)[1],
            cv2.dilate(cv2.Canny(smooth, 25, 70), np.ones((2,2), np.uint8)))
        local_borders = cv2.morphologyEx(local_borders, cv2.MORPH_CLOSE, np.ones((3,3), np.uint8))
        rhun_borders = cv2.bitwise_and(local_borders, rhun_area)
        inv_borders = cv2.bitwise_not(rhun_borders)
        inv_borders[rhun_area == 0] = 0
        
        n_cc, cc_labels = cv2.connectedComponents(inv_borders)
        best_sea_cid = None
        best_warmth = -999
        for cid in range(1, n_cc):
            cmask = (cc_labels == cid)
            sz = cmask.sum()
            if sz < 500 or sz > 50000:
                continue
            ys, xs = np.where(cmask)
            cx, cy = int(xs.mean()), int(ys.mean())
            if not (1460 < cx < 1650 and 400 < cy < 560):
                continue
            avg_w = warmth[cmask].mean()
            if avg_w > best_warmth:
                best_warmth = avg_w
                best_sea_cid = cid
        
        if best_sea_cid is not None:
            sea_mask = (cc_labels == best_sea_cid)
            sea_u8_final = cv2.morphologyEx(
                sea_mask.astype(np.uint8) * 255, cv2.MORPH_CLOSE, np.ones((5,5), np.uint8))
            sea_mask = sea_u8_final > 0
            new_sea_id = km.max() + 1
            km[sea_mask] = new_sea_id
            ys, xs = np.where(sea_mask)
            print(f"  Sea of Rhûn: {sea_mask.sum()}px, center=({int(xs.mean())},{int(ys.mean())})")

    # === STEP 14: Rhûn merge (region at ~1708,515 into main Rhûn) ===
    # The dark spot in east Rhûn should be part of main Rhûn
    dark_east_id = km[515, 1708]
    main_rhun_id = km[565, 1638]
    if dark_east_id != main_rhun_id and dark_east_id > 0 and main_rhun_id > 0:
        dark_sz = (km == dark_east_id).sum()
        if dark_sz < 20000:  # Only merge if it's a small dark region
            km[km == dark_east_id] = main_rhun_id
            print(f"  Rhûn dark spot merged: {dark_east_id} → {main_rhun_id}")

    # === STEP 15: Isengard merge (region 39 + 36 equivalent) ===
    isengard_id = km[579, 865]
    nearby_id = km[592, 850]
    if isengard_id != nearby_id:
        # Check if they're adjacent and should merge
        isengard_id2 = km[265, 845]  # Region 37 area
        if isengard_id2 == isengard_id or nearby_id == isengard_id:
            pass  # Already same
    # Merge the Isengard area (regions at ~865,579 and ~799,635 area)
    id_at_579 = km[579, 865]
    id_at_265 = km[265, 845]
    if id_at_579 != id_at_265 and id_at_579 > 0 and id_at_265 > 0:
        # These might need merging - check if they're the same type of region
        sz1 = (km == id_at_579).sum()
        sz2 = (km == id_at_265).sum()
        # Only merge if both are small greenish regions
        if sz1 < 30000 and sz2 < 30000:
            c1 = img_rgb[km == id_at_579].mean(axis=0)
            c2 = img_rgb[km == id_at_265].mean(axis=0)
            if np.sqrt(np.sum((c1-c2)**2)) < 40:
                bigger = id_at_579 if sz1 >= sz2 else id_at_265
                smaller = id_at_265 if sz1 >= sz2 else id_at_579
                km[km == smaller] = bigger
                print(f"  Isengard area merged: {smaller} → {bigger}")

    # === STEP 16: Re-number consecutively ===
    print("Step 16: Final numbering...")
    unique_ids = sorted(set(km.flatten()) - {0})
    id_remap = {}
    new_id = 0
    for old_id in unique_ids:
        if (km == old_id).sum() > 0:
            new_id += 1
            id_remap[old_id] = new_id
    
    km_final = np.zeros((h, w), dtype=np.uint16)
    for old_id, nkid in id_remap.items():
        km_final[km == old_id] = nkid
    
    # Final orphan fill
    orphan = (km_final == 0)
    if orphan.any():
        _, nearest = ndimage.distance_transform_edt(orphan, return_indices=True)
        km_final[orphan] = km_final[nearest[0][orphan], nearest[1][orphan]]
    
    num_k = int(km_final.max())
    print(f"  Final: {num_k} kingdoms")

    # === EXPORT ===
    print("\nExporting...")
    os.makedirs(output_dir, exist_ok=True)
    mask_dir = os.path.join(output_dir, "masks")
    os.makedirs(mask_dir, exist_ok=True)
    
    # Label map
    cv2.imwrite(os.path.join(output_dir, "kingdom_label_map.png"), km_final)
    
    # Kingdom data + polygons + masks
    kingdom_data = []
    for kid in range(1, num_k + 1):
        mask = (km_final == kid)
        size = int(mask.sum())
        if size == 0:
            continue
        pct = size * 100 / (h * w)
        avg = img_rgb[mask].mean(axis=0).astype(int).tolist()
        avg_hue = float(hsv[:,:,0][mask].mean())
        avg_sat = float(hsv[:,:,1][mask].mean())
        avg_val = float(hsv[:,:,2][mask].mean())
        ys, xs = np.where(mask)
        
        # Binary mask
        mask_u8 = mask.astype(np.uint8) * 255
        cv2.imwrite(os.path.join(mask_dir, f"kingdom_{kid:02d}.png"), mask_u8)
        
        # Simplified polygon contours
        contours, _ = cv2.findContours(mask_u8, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        polygons = []
        for cnt in contours:
            approx = cv2.approxPolyDP(cnt, 1.5, True)
            pts = [[int(p[0]), int(p[1])] for p in approx.reshape(-1, 2)]
            if len(pts) >= 3:
                polygons.append(pts)
        
        total_pts = sum(len(p) for p in polygons)
        kingdom_data.append({
            "id": int(kid),
            "avg_color_rgb": avg,
            "avg_color_hex": f"#{avg[0]:02x}{avg[1]:02x}{avg[2]:02x}",
            "avg_hue": round(avg_hue, 1),
            "avg_saturation": round(avg_sat, 1),
            "avg_value": round(avg_val, 1),
            "area_percent": round(pct, 2),
            "area_pixels": size,
            "bbox": {
                "x": int(xs.min()), "y": int(ys.min()),
                "w": int(xs.max() - xs.min() + 1), "h": int(ys.max() - ys.min() + 1)
            },
            "center": {"x": int(xs.mean()), "y": int(ys.mean())},
            "polygon_point_count": total_pts,
            "polygons": polygons
        })
    
    # JSON
    with open(os.path.join(output_dir, "region_data.json"), "w") as f:
        json.dump({
            "image_width": w, "image_height": h,
            "num_kingdoms": num_k,
            "kingdoms": kingdom_data
        }, f, indent=2)
    
    total_pts = sum(k["polygon_point_count"] for k in kingdom_data)
    print(f"  JSON: {num_k} kingdoms, {total_pts} polygon points")
    
    # Colored visualization (golden angle hue spacing)
    lut = np.zeros((num_k + 1, 3), dtype=np.uint8)
    for kid in range(1, num_k + 1):
        hv = (kid * 137.508) % 360
        s = 0.55 + (kid % 3) * 0.15
        v = 0.60 + (kid % 5) * 0.08
        r, g, b = colorsys.hsv_to_rgb(hv / 360, s, v)
        lut[kid] = [int(r * 255), int(g * 255), int(b * 255)]
    
    colored = lut[km_final]
    for kid in range(1, num_k + 1):
        m = (km_final == kid).astype(np.uint8) * 255
        cts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        cv2.drawContours(colored, cts, -1, (255, 255, 255), 1)
    
    cpil = Image.fromarray(colored)
    draw = ImageDraw.Draw(cpil)
    for k in kingdom_data:
        cx, cy = k['center']['x'], k['center']['y']
        label = str(k['id'])
        for dx in [-1, 0, 1]:
            for dy in [-1, 0, 1]:
                draw.text((cx-8+dx, cy-6+dy), label, fill=(0, 0, 0))
        draw.text((cx-8, cy-6), label, fill=(255, 255, 0))
    cpil.save(os.path.join(output_dir, "kingdom_colored.png"))
    
    # Overlay on original
    overlay = (img_rgb * 0.4).astype(np.uint8)
    for kid in range(1, num_k + 1):
        m = (km_final == kid)
        overlay[m] = img_rgb[m]
    for kid in range(1, num_k + 1):
        m = (km_final == kid).astype(np.uint8) * 255
        cts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        for c in cts:
            for pt in c:
                px, py = pt[0]
                for dy in range(-1, 2):
                    for dx in range(-1, 2):
                        ny, nx = py + dy, px + dx
                        if 0 <= ny < h and 0 <= nx < w:
                            overlay[ny, nx] = [255, 255, 255]
    opil = Image.fromarray(overlay)
    draw2 = ImageDraw.Draw(opil)
    for k in kingdom_data:
        cx, cy = k['center']['x'], k['center']['y']
        label = str(k['id'])
        for dx in [-1, 0, 1]:
            for dy in [-1, 0, 1]:
                draw2.text((cx-8+dx, cy-6+dy), label, fill=(0, 0, 0))
        draw2.text((cx-8, cy-6), label, fill=(255, 255, 0))
    opil.save(os.path.join(output_dir, "kingdom_overlay.png"))
    
    # ZIP
    zippath = os.path.join(output_dir, "kingdom_export.zip")
    with zipfile.ZipFile(zippath, 'w', zipfile.ZIP_DEFLATED) as zf:
        for fname in ["region_data.json", "kingdom_label_map.png",
                       "kingdom_colored.png", "kingdom_overlay.png"]:
            zf.write(os.path.join(output_dir, fname), fname)
        for fname in sorted(os.listdir(mask_dir)):
            zf.write(os.path.join(mask_dir, fname), f"masks/{fname}")
    
    zipsize = os.path.getsize(zippath) / (1024 * 1024)
    print(f"  ZIP: {zipsize:.1f} MB")
    print(f"\nOutput: {output_dir}/")
    print(f"  region_data.json     - {num_k} kingdoms with polygons ({total_pts} points)")
    print(f"  kingdom_label_map.png - 16-bit label map (ID per pixel)")
    print(f"  kingdom_colored.png   - Random-color visualization")
    print(f"  kingdom_overlay.png   - Boundaries on original map")
    print(f"  masks/               - {num_k} binary masks (one per kingdom)")
    print(f"  kingdom_export.zip   - All of the above ({zipsize:.1f} MB)")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python3 extract_kingdoms.py <input_image> [output_dir]")
        sys.exit(1)
    
    input_path = sys.argv[1]
    output_path = sys.argv[2] if len(sys.argv) > 2 else "./kingdom_export"
    extract_kingdoms(input_path, output_path)
