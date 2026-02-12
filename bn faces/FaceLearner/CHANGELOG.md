# FaceLearner Changelog

All notable changes to this project will be documented in this file.

## [3.0.14] - 2026-01-29

### Fixed
- **Scoring Formula**: Softened from k=50 to k=20
  - Old: 50% diff → score 0.0000037 (too strict!)
  - New: 50% diff → score 0.0067 (still strict but recoverable)
  - 30% diff → score 0.165 (decent)
  - 10% diff → score 0.82 (good)
  
- **SubPhase Scoring**: Prioritize worst feature (forces fixes!)
  - Formula: **40% average + 60% worst feature**
  
- **MainPhase Aggregation**: Prioritize worst sub-phase  
  - Formula: **40% average + 60% worst sub-phase**
  - Chin score 0.05 + Jaw score 0.9 → now 0.39 (was 0.48 pure avg)

- **CelebADataSource**: Quality filter
  - Skips images marked as "Blurry" in attributes
  - 50 retry attempts to find valid image
  
- **LFWDataSource**: Robustness
  - Skips files < 1KB (corrupt/placeholder)
  - 20 retry attempts for error handling

### Example Impact
Before: Target=Round Chin, Render=Pointed Chin
- Chin SubPhase: 0.007 (catastrophic)
- Structure Phase: 0.44 (hidden by good Jaw!)
- Total Score: 0.62 (misleading!)

After: Same scenario (60% weight on min)
- Chin SubPhase: ~0.15
- Structure Phase: ~0.12 (properly reflects bad)
- Total Score: ~0.30 (FORCES optimizer to fix!)

## [3.0.13] - 2026-01-29

### Added
- **Target Validation System**: New `IsValidTarget()` method filters out:
  - Children (age < 18) - detected via FairFace demographics
  - Low quality images (failed landmark detection)
  - Partial faces (landmarks clustered/too small)
  
### Changed
- **UTKFaceDataSource**: Now filters at source level
  - Skips children (age < 18 from filename)
  - Skips very old faces (age > 80, often low quality)
  - Retries up to 100 times to find valid image
- **NextTarget()**: Increased retry attempts from `sources.Count` to 50
  - Better handling when many images are invalid

### Fixed
- Children images no longer processed (safety + irrelevant for game faces)
- Low-quality images with bad landmark detection now skipped

## [3.0.12] - 2026-01-29

### Added
- **Range-Proportional Variation**: Morph variations now scale with each morph's natural range
  - `face_width` (range 1.5): sigma=0.5 → ±0.75 variation
  - `nose_bridge` (range 0.65): sigma=0.5 → ±0.325 variation
  - All 62 morphs now vary proportionally to their Bannerlord range
- **MorphGroups.cs**: Added official Bannerlord morph ranges from DeformKeyData
  - `GetMorphRange(int morphIdx)` returns (min, max, range) tuple
  - Complete mapping of all 62 morphs with KeyMin/KeyMax values

### Fixed
- **Phase Transition Bug**: Next phase now starts from BEST morphs, not last iteration
  - `OnSubPhaseComplete` now passes `bestMorphs` to reset `_currentMorphs`
  - Ensures continuous improvement across phase boundaries

### Changed
- Sigma values now interpreted as percentage of morph range:
  - Exploration: 50% of range (was absolute 0.8)
  - Refinement: 20% of range (was absolute 0.3)
  - PlateauEscape: 80% of range (was absolute 1.0)
  - LockIn: 8% of range (was absolute 0.1)

## [3.0.11] - 2026-01-29

### Fixed
- Phase transition resets to best morphs (preparation for 3.0.12)

## [3.0.10] - 2026-01-29

### Changed
- **MorphTester**: SILHOUETTE de-prioritized in primary region detection
  - Now shows actual facial region affected (NOSE, EYES, JAW_CHIN, etc.)
  - SILHOUETTE only shown when no other region has significant movement

### Updated
- **MorphGroups.cs**: Complete rewrite with official Bannerlord mapping
  - Header documentation with all 62 morph indices and names
  - `UI_Face`, `UI_Eyes`, `UI_Nose`, `UI_Mouth`, `UI_Special` arrays
  - Impact rankings from MorphTester results (2026-01-29)

## [3.0.9] - 2026-01-29

### Added
- **DumpDeformKeyData()**: Exports Bannerlord's official morph mapping
  - Creates `FaceLearner/Data/deform_key_data.csv` on learning start
  - Contains Index, Id, GroupId, GroupName, KeyMin, KeyMax for all morphs

### Fixed
- `Path` ambiguity: Changed to `System.IO.Path` (conflict with TaleWorlds.Engine.Path)

## [3.0.8] - 2026-01-28

### Fixed
- MorphTester visual capture timing issues
- Landmark calculation returning 0.0 for all values

## [3.0.5] - 2026-01-27

### Added
- Initial hierarchical optimization system
- 468 FaceMesh landmark detection
- Phase-based learning (Foundation → Structure → Features → Details)
