# FaceLearner Setup Guide

## AUTOMATIC SETUP (Recommended)

**Just launch the game!** The mod automatically:
1. Downloads the dlib model (~100MB)
2. Downloads LFW face dataset (~173MB, 13K faces)
3. Downloads CelebA attributes file
4. Starts learning in background

No manual setup required!

### What gets downloaded:
| File | Size | Purpose |
|------|------|---------|
| dlib 68-point model | ~100MB | Face landmark detection |
| LFW dataset | ~173MB | 13,000 labeled faces |
| CelebA attributes | ~1MB | Face attribute labels |

Downloads happen in background while you play.
Progress shown in log: `Modules/FaceLearner/Data/`

---

## MANUAL SETUP (Optional - for more data)

If you want larger datasets for better learning:

### CelebA Full Dataset (~1.4GB, 200K faces)
Best dataset - 40 attributes per face!

1. Download from: https://mmlab.ie.cuhk.edu.hk/projects/CelebA.html
2. Get: `img_align_celeba.zip` + `list_attr_celeba.txt`
3. Extract to: `Modules/FaceLearner/Data/datasets/celeba/`

### UTKFace (~200MB, 20K faces)
Has age, gender, ethnicity labels.

1. Download from: https://susanqq.github.io/UTKFace/
2. Extract to: `Modules/FaceLearner/Data/datasets/utkface/`

---

## Folder Structure
```
Modules/FaceLearner/
├── Data/
│   ├── models/       ← dlib model (auto-downloaded)
│   ├── datasets/     ← face datasets (auto-downloaded)
│   └── knowledge/    ← learned data (auto-created)
└── ...
```

## Troubleshooting

### Downloads failing?
- Check internet connection
- Firewall may block downloads
- Manual download links in SETUP.md

### Need 7-Zip?
Some archives need 7-Zip to extract:
https://www.7-zip.org/download.html

### DLL errors?
Install VC++ 2017: https://aka.ms/vs/17/release/vc_redist.x64.exe
