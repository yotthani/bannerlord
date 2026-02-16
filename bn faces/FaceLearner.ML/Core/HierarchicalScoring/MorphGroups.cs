using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Defines which Bannerlord morphs affect which facial features.
    /// 
    /// OFFICIAL BANNERLORD 1.3 MORPH MAPPING (from DeformKeyData):
    /// ═══════════════════════════════════════════════════════════════
    /// Face (GroupId 1): 0-13
    ///   0  face_width           1  face_depth          2  center_height
    ///   3  face_ratio           4  cheeks (=FaceWeight) 5  cheekbone_height
    ///   6  cheekbone_width      7  cheekbone_depth     8  face_sharpness
    ///   9  temple_width        10  eye_socket_size    11  ear_shape
    ///  12  ear_size            13  face_asymmetry
    /// 
    /// Eyes (GroupId 2): 14-27
    ///  14  eyebrow_depth       15  brow_outer_height  16  brow_middle_height
    ///  17  brow_inner_height   18  eye_position       19  eye_size
    ///  20  monolid_eyes        21  eyelid_height      22  eye_depth
    ///  23  eye_shape           24  eye_outer_corner   25  eye_inner_corner
    ///  26  eye_to_eye_distance 27  eye_asymmetry
    /// 
    /// Nose (GroupId 3): 28-39
    ///  28  nose_angle          29  nose_length        30  nose_bridge
    ///  31  nose_tip_height     32  nose_size          33  nose_width
    ///  34  nostril_height      35  nostril_scale      36  nose_bump
    ///  37  nose_definition     38  nose_shape         39  nose_asymmetry
    /// 
    /// Mouth (GroupId 4): 40-53 (includes Jaw/Chin!)
    ///  40  mouth_width         41  mouth_position     42  lips_frown
    ///  43  lip_thickness       44  lips_forward       45  lip_shape_bottom
    ///  46  lip_shape_top       47  lip_concave_convex 48  jaw_line
    ///  49  neck_slope          50  jaw_height         51  chin_forward
    ///  52  chin_shape          53  chin_length
    /// 
    /// Special (GroupId -1): 54-58
    ///  54  head_scaling (HIGHEST IMPACT!)
    ///  55  hide_ears (#2 IMPACT)
    ///  56  old_face
    ///  57  kid_face
    ///  58  eyebump
    /// 
    /// Body (GroupId 0): 59-61
    ///  59  weight              60  build              61  height
    /// </summary>
    public static class MorphGroups
    {
        // ═══════════════════════════════════════════════════════════════
        // OFFICIAL BANNERLORD UI CATEGORIES (from DeformKeyData.GroupId)
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Morphs shown in Face tab (GroupId 1)</summary>
        public static readonly int[] UI_Face = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
        
        /// <summary>Morphs shown in Eyes tab (GroupId 2)</summary>
        public static readonly int[] UI_Eyes = { 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27 };
        
        /// <summary>Morphs shown in Nose tab (GroupId 3)</summary>
        public static readonly int[] UI_Nose = { 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39 };
        
        /// <summary>Morphs shown in Mouth tab (GroupId 4) - includes Jaw/Chin!</summary>
        public static readonly int[] UI_Mouth = { 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53 };
        
        /// <summary>Hidden/Special morphs (GroupId -1)</summary>
        public static readonly int[] UI_Special = { 54, 55, 56, 57, 58 };
        
        /// <summary>Body morphs (GroupId 0) - usually controlled by sliders</summary>
        public static readonly int[] UI_Body = { 59, 60, 61 };
        
        // ═══════════════════════════════════════════════════════════════
        // PHASE 1: FOUNDATION (Overall face shape)
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Phase 1.1: Face width morphs (affects overall face width)</summary>
        /// v3.0.29: Removed morph 48 (jaw_line) — it belongs exclusively in Jaw group.
        /// Having it here AND in Jaw caused double-optimization conflicts.
        public static readonly int[] FaceWidth = {
            0,   // face_width (PRIMARY)
            1,   // face_depth (also affects apparent width)
            6,   // cheekbone_width
        };

        /// <summary>Phase 1.2: Face height/length morphs</summary>
        /// v3.0.29: Removed morphs 50 (jaw_height) and 53 (chin_length) — they belong
        /// exclusively in Jaw and Chin groups. Sharing morphs between Foundation and Structure
        /// caused the optimizer to fight itself: Foundation locked morph 50 then Jaw couldn't adjust it.
        public static readonly int[] FaceHeight = {
            2,   // center_height
            3,   // face_ratio (PRIMARY for height)
            54,  // head_scaling (affects overall proportions!)
        };
        
        /// <summary>Phase 1.3: Face shape morphs (round/oval/square)</summary>
        public static readonly int[] FaceShape = { 
            1,   // face_depth
            4,   // cheeks (Face Weight)
            8,   // face_sharpness
            13,  // face_asymmetry
        };
        
        /// <summary>ALL FOUNDATION MORPHS - for global optimization</summary>
        /// v3.0.29: Removed 48, 50, 53 — now exclusively in Structure groups
        public static readonly int[] AllFoundation = {
            0, 1, 2, 3, 4, 6, 8, 13, 54
        };
        
        // ═══════════════════════════════════════════════════════════════
        // PHASE 2: STRUCTURE (Framework elements)
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Phase 2.1: Forehead morphs (temple area, brow ridge)</summary>
        public static readonly int[] Forehead = { 
            9,   // temple_width
            10,  // eye_socket_size
            14,  // eyebrow_depth
            56,  // old_face (affects forehead wrinkles)
        };
        
        /// <summary>Phase 2.2: Jaw morphs (jaw_line is in Mouth group!)</summary>
        public static readonly int[] Jaw = { 
            48,  // jaw_line (HIGHEST JAW IMPACT)
            49,  // neck_slope
            50,  // jaw_height
        };
        
        /// <summary>Phase 2.3: Chin morphs
        /// v3.0.34: Added jaw_line (48) — engine needs jaw_line to make round chins.
        /// Without it, CMA-ES can only adjust 51/52/53 during Chin phase, which can't
        /// achieve round chins alone (engine couples chin roundness with jaw width).
        /// jaw_line is now in BOTH Jaw and Chin groups so both phases can adjust it.
        /// </summary>
        public static readonly int[] Chin = {
            48,  // jaw_line — needed for round chin shapes (shared with Jaw group)
            51,  // chin_forward
            52,  // chin_shape
            53,  // chin_length
        };
        
        /// <summary>Phase 2.4: Cheek morphs</summary>
        public static readonly int[] Cheeks = { 
            4,   // cheeks (Face Weight)
            5,   // cheekbone_height
            6,   // cheekbone_width
            7,   // cheekbone_depth
        };
        
        // ═══════════════════════════════════════════════════════════════
        // PHASE 3: MAJOR FEATURES
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Phase 3.1: Nose morphs (GroupId 3: indices 28-39)</summary>
        public static readonly int[] Nose = { 
            28,  // nose_angle
            29,  // nose_length (HIGH IMPACT)
            30,  // nose_bridge (HIGH IMPACT)
            31,  // nose_tip_height
            32,  // nose_size
            33,  // nose_width
            34,  // nostril_height (HIGH IMPACT)
            35,  // nostril_scale
            36,  // nose_bump
            37,  // nose_definition
            38,  // nose_shape
            39,  // nose_asymmetry
        };
        
        /// <summary>Phase 3.2: Eye morphs (GroupId 2: indices 14-27, minus eyebrows)</summary>
        public static readonly int[] Eyes = { 
            18,  // eye_position
            19,  // eye_size (HIGH IMPACT)
            20,  // monolid_eyes
            21,  // eyelid_height
            22,  // eye_depth
            23,  // eye_shape
            24,  // eye_outer_corner
            25,  // eye_inner_corner
            26,  // eye_to_eye_distance
            27,  // eye_asymmetry
        };
        
        /// <summary>Phase 3.3: Mouth/Lips morphs (GroupId 4: 40-47, not jaw/chin)</summary>
        public static readonly int[] Mouth = { 
            40,  // mouth_width
            41,  // mouth_position
            42,  // lips_frown
            43,  // lip_thickness
            44,  // lips_forward
            45,  // lip_shape_bottom
            46,  // lip_shape_top
            47,  // lip_concave_convex
        };
        
        // ═══════════════════════════════════════════════════════════════
        // PHASE 4: FINE DETAILS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Phase 4.1: Eyebrow (Brow) morphs</summary>
        public static readonly int[] Eyebrows = { 
            14,  // eyebrow_depth
            15,  // brow_outer_height
            16,  // brow_middle_height
            17,  // brow_inner_height
        };
        
        /// <summary>Phase 4.2: Ear morphs</summary>
        public static readonly int[] Ears = { 
            11,  // ear_shape
            12,  // ear_size
            55,  // hide_ears (HIGH IMPACT #2!)
        };
        
        /// <summary>Phase 4.3: Fine detail morphs (all remaining subtle adjustments)</summary>
        public static readonly int[] FineDetails = { 
            8,   // face_sharpness
            13,  // face_asymmetry
            57,  // kid_face
            58,  // eyebump
        };
        
        // ═══════════════════════════════════════════════════════════════
        // TEST-DERIVED IMPACT RANKINGS (from MorphTester 2026-01-29)
        // Tested with 468 FaceMesh landmarks, range -1.0 to 5.0
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// High impact morphs - these create the most visible landmark changes.
        /// Prioritize these in early learning phases for faster convergence.
        /// Test results: [54]=6.89, [55]=5.21, [3]=3.02, [10]=2.49, [1]=2.19, ...
        /// </summary>
        public static readonly int[] HighImpact = { 
            54,  // head_scaling (6.89) - HIGHEST!
            55,  // hide_ears (5.21)
            3,   // face_ratio (3.02)
            10,  // eye_socket_size (2.49)
            1,   // face_depth (2.19)
            30,  // nose_bridge (2.13)
            56,  // old_face (2.09)
            16,  // brow_middle_height (1.88)
            34,  // nostril_height (1.86)
            19,  // eye_size (1.82)
            29,  // nose_length (1.79)
            53,  // chin_length (1.77)
            33,  // nose_width (1.71)
            15,  // brow_outer_height (1.67)
            18,  // eye_position (1.65)
            4,   // cheeks (1.60)
            27,  // eye_asymmetry (1.53)
            9,   // temple_width (1.48)
            0,   // face_width (1.43)
            2,   // center_height (1.41)
        };
        
        /// <summary>
        /// Low impact morphs - these create minimal visible landmark changes.
        /// Can be de-prioritized or skipped in learning for efficiency.
        /// Test results: [36]=0.28, [25]=0.28, [20]=0.34, [50]=0.38, ...
        /// </summary>
        public static readonly int[] LowImpact = { 
            36,  // nose_bump (0.28)
            25,  // eye_inner_corner (0.28)
            20,  // monolid_eyes (0.34)
            50,  // jaw_height (0.38)
            59,  // weight (0.41)
            24,  // eye_outer_corner (0.43)
            38,  // nose_shape (0.43)
            21,  // eyelid_height (0.44)
            37,  // nose_definition (0.46)
            23,  // eye_shape (0.46)
            46,  // lip_shape_top (0.48)
            5,   // cheekbone_height (0.48)
            45,  // lip_shape_bottom (0.49)
            8,   // face_sharpness (0.49)
            22,  // eye_depth (0.53)
            14,  // eyebrow_depth (0.58)
            39,  // nose_asymmetry (0.59)
            44,  // lips_forward (0.69)
            6,   // cheekbone_width (0.68)
            7,   // cheekbone_depth (0.69)
        };
        
        /// <summary>
        /// Get morphs sorted by visual impact (high to low).
        /// Based on automated morph testing with 468 FaceMesh landmark displacement.
        /// </summary>
        public static readonly int[] ByImpact = {
            // #1-10 (Impact > 1.8)
            54, 55, 3, 10, 1, 30, 56, 16, 34, 19,
            // #11-20 (Impact 1.4-1.8)
            29, 53, 33, 15, 18, 4, 27, 9, 0, 2,
            // #21-30 (Impact 0.85-1.4)
            32, 13, 57, 48, 17, 40, 60, 51, 52, 11,
            // #31-40 (Impact 0.69-0.95)
            35, 41, 26, 61, 43, 28, 58, 42, 49, 47,
            // #41-50 (Impact 0.48-0.71)
            12, 7, 44, 6, 31, 39, 14, 22, 8, 45,
            // #51-62 (Impact < 0.48)
            5, 46, 23, 37, 21, 38, 24, 59, 50, 20, 25, 36
        };
        
        // ═══════════════════════════════════════════════════════════════
        // LOOKUP & UTILITIES
        // ═══════════════════════════════════════════════════════════════
        
        private static readonly Dictionary<SubPhase, int[]> _phaseToMorphs = new Dictionary<SubPhase, int[]>
        {
            // Foundation
            { SubPhase.FaceWidth, FaceWidth },
            { SubPhase.FaceHeight, FaceHeight },
            { SubPhase.FaceShape, FaceShape },
            // Structure
            { SubPhase.Forehead, Forehead },
            { SubPhase.Jaw, Jaw },
            { SubPhase.Chin, Chin },
            { SubPhase.Cheeks, Cheeks },
            // Features
            { SubPhase.Nose, Nose },
            { SubPhase.Eyes, Eyes },
            { SubPhase.Mouth, Mouth },
            // Details
            { SubPhase.Eyebrows, Eyebrows },
            { SubPhase.Ears, Ears },
            { SubPhase.FineDetails, FineDetails },
        };
        
        /// <summary>Get morph indices for a sub-phase</summary>
        public static int[] GetMorphsForPhase(SubPhase phase)
        {
            return _phaseToMorphs.TryGetValue(phase, out var morphs) ? morphs : Array.Empty<int>();
        }
        
        /// <summary>Get all morphs for a main phase</summary>
        public static int[] GetMorphsForMainPhase(MainPhase mainPhase)
        {
            switch (mainPhase)
            {
                case MainPhase.Foundation:
                    return FaceWidth.Concat(FaceHeight).Concat(FaceShape).Distinct().ToArray();
                case MainPhase.Structure:
                    return Forehead.Concat(Jaw).Concat(Chin).Concat(Cheeks).Distinct().ToArray();
                case MainPhase.MajorFeatures:
                    return Nose.Concat(Eyes).Concat(Mouth).Distinct().ToArray();
                case MainPhase.FineDetails:
                    // v3.0.30: Only Eyebrows — Ears and FineDetails removed (consistently 0.55, wasting iterations)
                    return Eyebrows.ToArray();
                default:
                    return Array.Empty<int>();
            }
        }
        
        /// <summary>Get total morph count for a phase</summary>
        public static int GetMorphCount(SubPhase phase)
        {
            return GetMorphsForPhase(phase).Length;
        }
        
        /// <summary>Check if a morph index belongs to a phase</summary>
        public static bool MorphBelongsToPhase(int morphIndex, SubPhase phase)
        {
            return GetMorphsForPhase(phase).Contains(morphIndex);
        }
        
        /// <summary>Get all sub-phases for a main phase</summary>
        public static SubPhase[] GetSubPhases(MainPhase mainPhase)
        {
            switch (mainPhase)
            {
                case MainPhase.Foundation:
                    return new[] { SubPhase.FaceWidth, SubPhase.FaceHeight, SubPhase.FaceShape };
                case MainPhase.Structure:
                    return new[] { SubPhase.Forehead, SubPhase.Jaw, SubPhase.Chin, SubPhase.Cheeks };
                case MainPhase.MajorFeatures:
                    return new[] { SubPhase.Nose, SubPhase.Eyes, SubPhase.Mouth };
                case MainPhase.FineDetails:
                    // v3.0.30: Only Eyebrows — Ears/FineDetails SubPhases removed (wasting iterations)
                    return new[] { SubPhase.Eyebrows };
                default:
                    return Array.Empty<SubPhase>();
            }
        }
        
        /// <summary>Get the main phase that contains a sub-phase</summary>
        public static MainPhase GetMainPhase(SubPhase subPhase)
        {
            switch (subPhase)
            {
                case SubPhase.FaceWidth:
                case SubPhase.FaceHeight:
                case SubPhase.FaceShape:
                    return MainPhase.Foundation;
                    
                case SubPhase.Forehead:
                case SubPhase.Jaw:
                case SubPhase.Chin:
                case SubPhase.Cheeks:
                    return MainPhase.Structure;
                    
                case SubPhase.Nose:
                case SubPhase.Eyes:
                case SubPhase.Mouth:
                    return MainPhase.MajorFeatures;
                    
                case SubPhase.Eyebrows:
                case SubPhase.Ears:
                case SubPhase.FineDetails:
                default:
                    return MainPhase.FineDetails;
            }
        }
        
        /// <summary>Get description of morph groups for logging</summary>
        public static string GetSummary()
        {
            return $"MorphGroups: Foundation({FaceWidth.Length + FaceHeight.Length + FaceShape.Length}) " +
                   $"Structure({Forehead.Length + Jaw.Length + Chin.Length + Cheeks.Length}) " +
                   $"Features({Nose.Length + Eyes.Length + Mouth.Length}) " +
                   $"Details({Eyebrows.Length})";
        }
        
        // ═══════════════════════════════════════════════════════════════
        // MORPH RANGES (from DeformKeyData - official Bannerlord values)
        // Used for range-proportional variation in optimization
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Official Bannerlord morph ranges from DeformKeyData.
        /// Format: (KeyMin, KeyMax) for each morph index 0-61.
        /// These vary wildly - from 0.3 to 3.0+ total range!
        /// </summary>
        private static readonly (float min, float max)[] MorphRanges = new (float, float)[]
        {
            // Face (0-13)
            (-0.60f, 0.90f),  // 0  face_width (range 1.50)
            (-0.35f, 0.40f),  // 1  face_depth (range 0.75)
            (-0.30f, 0.30f),  // 2  center_height (range 0.60)
            (0.50f, 1.10f),   // 3  face_ratio (range 0.60)
            (-0.75f, 0.35f),  // 4  cheeks (range 1.10)
            (-1.20f, 0.50f),  // 5  cheekbone_height (range 1.70)
            (-2.05f, 0.85f),  // 6  cheekbone_width (range 2.90)
            (-1.80f, 0.50f),  // 7  cheekbone_depth (range 2.30)
            (-0.80f, 0.80f),  // 8  face_sharpness (range 1.60) - NOTE: inverted in BL
            (-1.50f, 1.50f),  // 9  temple_width (range 3.00)
            (-0.80f, 0.10f),  // 10 eye_socket_size (range 0.90)
            (-0.80f, 0.70f),  // 11 ear_shape (range 1.50)
            (-1.00f, 0.80f),  // 12 ear_size (range 1.80)
            (-0.80f, 0.80f),  // 13 face_asymmetry (range 1.60)
            
            // Eyes (14-27)
            (-0.45f, 0.45f),  // 14 eyebrow_depth (range 0.90) - NOTE: inverted
            (-0.75f, 0.90f),  // 15 brow_outer_height (range 1.65)
            (0.00f, 0.90f),   // 16 brow_middle_height (range 0.90)
            (-0.25f, 0.50f),  // 17 brow_inner_height (range 0.75)
            (0.00f, 0.90f),   // 18 eye_position (range 0.90) - NOTE: inverted
            (-1.00f, 1.00f),  // 19 eye_size (range 2.00)
            (-0.50f, 0.90f),  // 20 monolid_eyes (range 1.40)
            (-0.75f, 0.95f),  // 21 eyelid_height (range 1.70)
            (-0.20f, 1.10f),  // 22 eye_depth (range 1.30) - NOTE: inverted
            (-0.50f, 0.50f),  // 23 eye_shape (range 1.00)
            (-0.80f, 0.50f),  // 24 eye_outer_corner (range 1.30)
            (-0.25f, 1.40f),  // 25 eye_inner_corner (range 1.65) - NOTE: inverted
            (-0.90f, 1.10f),  // 26 eye_to_eye_distance (range 2.00)
            (-0.90f, 0.90f),  // 27 eye_asymmetry (range 1.80)
            
            // Nose (28-39)
            (-0.80f, 1.20f),  // 28 nose_angle (range 2.00) - NOTE: inverted
            (-1.30f, 0.00f),  // 29 nose_length (range 1.30)
            (-0.45f, 0.20f),  // 30 nose_bridge (range 0.65)
            (-0.40f, 1.00f),  // 31 nose_tip_height (range 1.40)
            (-1.85f, 0.75f),  // 32 nose_size (range 2.60)
            (-0.75f, 1.20f),  // 33 nose_width (range 1.95)
            (-1.70f, 0.00f),  // 34 nostril_height (range 1.70)
            (-0.20f, 0.80f),  // 35 nostril_scale (range 1.00)
            (-0.15f, 0.70f),  // 36 nose_bump (range 0.85)
            (-1.50f, 1.10f),  // 37 nose_definition (range 2.60)
            (-1.00f, 1.00f),  // 38 nose_shape (range 2.00)
            (-0.90f, 0.90f),  // 39 nose_asymmetry (range 1.80)
            
            // Mouth (40-53, includes Jaw/Chin)
            (-0.35f, 0.15f),  // 40 mouth_width (range 0.50)
            (-0.90f, 0.20f),  // 41 mouth_position (range 1.10)
            (-0.80f, 0.30f),  // 42 lips_frown (range 1.10)
            (-0.25f, 0.90f),  // 43 lip_thickness (range 1.15)
            (-0.40f, 0.50f),  // 44 lips_forward (range 0.90)
            (-1.20f, 0.00f),  // 45 lip_shape_bottom (range 1.20) - NOTE: inverted
            (-0.50f, 0.40f),  // 46 lip_shape_top (range 0.90)
            (-1.00f, 1.20f),  // 47 lip_concave_convex (range 2.20) - NOTE: inverted
            (-2.75f, 0.00f),  // 48 jaw_line (range 2.75)
            (-0.50f, 0.80f),  // 49 neck_slope (range 1.30)
            (-1.00f, 0.60f),  // 50 jaw_height (range 1.60) - NOTE: inverted
            (-0.50f, 0.20f),  // 51 chin_forward (range 0.70)
            (-0.75f, 1.00f),  // 52 chin_shape (range 1.75)
            (-1.70f, -0.10f), // 53 chin_length (range 1.60)
            
            // Special (54-58)
            (-0.30f, 0.30f),  // 54 head_scaling (range 0.60) - NOTE: inverted
            (0.00f, 1.50f),   // 55 hide_ears (range 1.50)
            (-0.25f, 1.50f),  // 56 old_face (range 1.75)
            (-0.25f, -0.05f), // 57 kid_face (range 0.20)
            (1.00f, 1.00f),   // 58 eyebump (range 0.00 - fixed!)
            
            // Body (59-61) - controlled by DynamicBodyProperties, not morphs
            (0.00f, 1.00f),   // 59 weight (range 1.00)
            (0.00f, 1.00f),   // 60 build (range 1.00)
            (0.00f, 1.00f),   // 61 height (range 1.00)
        };
        
        /// <summary>
        /// Get the official Bannerlord range for a morph index.
        /// Returns (min, max, range) tuple.
        /// </summary>
        public static (float min, float max, float range) GetMorphRange(int morphIdx)
        {
            if (morphIdx < 0 || morphIdx >= MorphRanges.Length)
            {
                return (0f, 1f, 1f);  // Default fallback
            }
            
            var (min, max) = MorphRanges[morphIdx];
            float range = Math.Abs(max - min);
            
            // Ensure minimum range to avoid division by zero
            if (range < 0.1f) range = 0.1f;
            
            return (min, max, range);
        }
        
        /// <summary>
        /// Get normalized range (0-1 scale) for a morph.
        /// Useful for consistent variation across all morphs.
        /// </summary>
        public static float GetNormalizedRange(int morphIdx)
        {
            var (_, _, range) = GetMorphRange(morphIdx);
            // Normalize to average range (~1.5)
            return range / 1.5f;
        }
    }
}
