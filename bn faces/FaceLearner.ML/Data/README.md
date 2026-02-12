# FaceLearner Models

## Required Models

### Dlib 68-point Landmark Model (AUTO-DOWNLOAD)
- **File:** `shape_predictor_68_face_landmarks.dat`
- **Size:** ~100 MB
- **Status:** Downloaded automatically on first run

### FaceMesh 468-point Landmark Model (MANUAL INSTALL)
- **File:** `face_landmark.onnx`
- **Size:** ~2.4 MB
- **Status:** Must be manually installed for 7x more landmarks

## How to Install FaceMesh

1. **Download from PINTO Model Zoo:**
   - Go to: https://github.com/PINTO0309/PINTO_model_zoo/releases/tag/032_FaceMesh
   - Download: `032_FaceMesh.tar.gz` or similar

2. **Extract and locate the ONNX file:**
   - Navigate to: `01_float32/face_landmark.onnx`

3. **Copy to this folder:**
   - Place `face_landmark.onnx` in `Modules/FaceLearner/Data/Models/`

4. **Restart the game**
   - Log should show: "✓ FaceMesh loaded: 468 landmarks"

## Landmark Comparison

| Model | Landmarks | Nose Points | Lip Points | Auto-Download |
|-------|-----------|-------------|------------|---------------|
| Dlib | 68 | 4 | 12 | ✅ Yes |
| FaceMesh | 468 | ~30 | ~40 | ❌ Manual |

FaceMesh provides 7x more facial detail for significantly better face matching!
