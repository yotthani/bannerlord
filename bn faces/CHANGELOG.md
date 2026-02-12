# FaceLearner Changelog

## [3.0.4] - 2026-01-28

### Fixed
- **CRITICAL: FaceMesh 468 → Dlib 68 conversion everywhere!**
  - Problem: FaceMesh returns 468 landmarks, FeatureExtractor expects Dlib 68 format
  - All indices were wrong (Index 48 in FaceMesh ≠ Index 48 in Dlib)
  - This caused features to be compared across completely different facial points!
  
- **All DetectLandmarks() calls now convert to Dlib 68 format:**
  - Target photo landmarks (learning)
  - Rendered face landmarks (comparison)
  - GenerateFace landmarks
  - AnalyzePhoto landmarks
  - Gender estimation landmarks

- **Removed hardcoded feature values:**
  - ForeheadSlope: now calculated from brow curvature
  - CheekProminence: now calculated from cheek-to-nose distance
  - EyebrowThickness: now calculated from brow landmark spread
  - These were all 0.5f before, giving automatic 1.0 score!

### Expected Results
- Initial scores: ~0.25-0.45 (realistic)
- Features should now vary properly between faces
- Optimization should show actual progress

---

## [3.0.3] - 2026-01-28

### Fixed
- Feature-normalized scoring (divide diff by expected range)
- Version now shows correctly in logs

---

## [3.0.2] - 2026-01-28

### Fixed
- Scoring strictness k=500
- Moved detailed logging to correct location

---

## [3.0.0-3.0.1] - 2026-01-28

### Added
- Hierarchical Scoring Architecture
- All 5 Knowledge Systems
- Detailed phase progression logging

---

## Earlier versions: See full changelog in previous releases
