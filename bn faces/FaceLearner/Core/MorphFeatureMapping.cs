using System.Collections.Generic;

namespace FaceLearner.Core
{
    /// <summary>
    /// Maps morph indices to facial features for targeted optimization.
    /// Bannerlord face morphs grouped by facial region.
    /// 
    /// Source: Bannerlord DeformKey IDs from MBBodyProperties.GetDeformKeyData()
    /// Total: 62 face morphs (indices 0-61) + 4 body morphs (60-63)
    /// </summary>
    public static class MorphFeatureMapping
    {
        /// <summary>
        /// Morph groups by facial feature.
        /// Used for feature-specific mutation during learning.
        /// </summary>
        public static readonly Dictionary<string, int[]> FeatureGroups = new Dictionary<string, int[]>
        {
            // === OVERALL FACE SHAPE (Group 1 in Bannerlord) ===
            { "FaceShape", new[] { 
                0,  // face_width
                1,  // face_depth
                2,  // center_height
                3,  // face_ratio
                4,  // cheeks
                5,  // cheek_forward
                6,  // cheekbone_width
                7,  // cheekbone_height
                8,  // temple_width
            }},
            
            // === JAW / CHIN ===
            { "Jaw", new[] {
                9,  // jaw_line
                10, // jaw_width
                11, // jaw_height
                12, // chin_forward
                13, // chin_length
                14, // chin_width
                15, // chin_shape
                16, // mandible_width
            }},
            
            // === EYES ===
            { "Eyes", new[] {
                17, // eye_size
                18, // eye_depth
                19, // eye_shape
                20, // eye_inner_height
                21, // eye_outer_height
                22, // eye_inner_corner
                23, // eye_outer_corner
                24, // eye_position
                25, // eye_horizontal
                26, // eyelid_size
                27, // eyelid_crease
            }},
            
            // === EYEBROWS ===
            { "Eyebrows", new[] {
                28, // eyebrow_depth
                29, // eyebrow_height
                30, // eyebrow_position
                31, // eyebrow_rotation
                32, // eyebrow_concave
            }},
            
            // === NOSE ===
            { "Nose", new[] {
                33, // nose_bridge
                34, // nose_width
                35, // nose_length
                36, // nose_bumpsize
                37, // nose_tip
                38, // nose_tip_angle
                39, // nose_ridge
                40, // nose_nostril
                41, // nose_shape
                42, // nose_angle
            }},
            
            // === MOUTH / LIPS ===
            { "Mouth", new[] {
                43, // mouth_width
                44, // mouth_forward
                45, // mouth_depth
                46, // mouth_position
                47, // lip_thickness
                48, // upper_lip
                49, // lower_lip
                50, // lip_shape
                51, // lip_vertical
                52, // lip_corner
            }},
            
            // === FOREHEAD / EARS (less important for face matching) ===
            { "Forehead", new[] {
                53, // forehead_width
                54, // forehead_depth
                55, // forehead_height
            }},
            
            { "Ears", new[] {
                56, // ear_size
                57, // ear_position
                58, // ear_angle
                59, // ear_shape
            }},
        };
        
        /// <summary>
        /// Primary morphs that have the highest impact on each feature.
        /// Use these for initial adjustments.
        /// </summary>
        public static readonly Dictionary<string, int[]> PrimaryMorphs = new Dictionary<string, int[]>
        {
            { "FaceShape", new[] { 0, 3, 4 } },       // face_width, face_ratio, cheeks
            { "Jaw", new[] { 9, 10, 15 } },           // jaw_line, jaw_width, chin_shape
            { "Eyes", new[] { 17, 19, 24 } },         // eye_size, eye_shape, eye_position
            { "Eyebrows", new[] { 29, 31 } },         // eyebrow_height, eyebrow_rotation
            { "Nose", new[] { 34, 35, 37 } },         // nose_width, nose_length, nose_tip
            { "Mouth", new[] { 43, 47, 48 } },        // mouth_width, lip_thickness, upper_lip
        };
        
        /// <summary>
        /// Morph importance weights (0-1) for scoring.
        /// Higher = more important for face recognition.
        /// </summary>
        public static readonly float[] MorphImportance = new float[62]
        {
            // Face shape (0-8)
            0.9f, 0.6f, 0.7f, 0.9f, 0.8f, 0.5f, 0.7f, 0.6f, 0.4f,
            // Jaw (9-16)
            0.9f, 0.8f, 0.7f, 0.8f, 0.8f, 0.7f, 0.9f, 0.6f,
            // Eyes (17-27)
            0.9f, 0.6f, 0.9f, 0.7f, 0.7f, 0.5f, 0.5f, 0.8f, 0.6f, 0.4f, 0.3f,
            // Eyebrows (28-32)
            0.5f, 0.7f, 0.6f, 0.6f, 0.4f,
            // Nose (33-42)
            0.7f, 0.9f, 0.9f, 0.6f, 0.9f, 0.7f, 0.6f, 0.7f, 0.6f, 0.7f,
            // Mouth (43-52)
            0.8f, 0.5f, 0.4f, 0.7f, 0.8f, 0.7f, 0.7f, 0.5f, 0.4f, 0.4f,
            // Forehead (53-55)
            0.3f, 0.2f, 0.3f,
            // Ears (56-59)
            0.1f, 0.1f, 0.1f, 0.1f,
            // Reserved (60-61)
            0.0f, 0.0f
        };
        
        /// <summary>
        /// Get mutation rate multiplier for a morph based on feature scores.
        /// Boosts morphs for underperforming features.
        /// </summary>
        public static float[] GetMutationMultipliers(Dictionary<string, float> featureScores)
        {
            var multipliers = new float[62];
            for (int i = 0; i < 62; i++)
                multipliers[i] = 1.0f;
            
            if (featureScores == null) return multipliers;
            
            foreach (var kv in featureScores)
            {
                string feature = kv.Key;
                float score = kv.Value;
                
                // If feature score is low, boost its morphs
                if (score < 0.6f && FeatureGroups.TryGetValue(feature, out var morphs))
                {
                    // Boost factor: 1.0 at score=0.6, up to 2.5 at score=0
                    float boost = 1.0f + (0.6f - score) * 2.5f;
                    
                    foreach (int idx in morphs)
                    {
                        if (idx >= 0 && idx < 62)
                        {
                            multipliers[idx] *= boost;
                        }
                    }
                    
                    // Extra boost for primary morphs
                    if (PrimaryMorphs.TryGetValue(feature, out var primary))
                    {
                        foreach (int idx in primary)
                        {
                            if (idx >= 0 && idx < 62)
                            {
                                multipliers[idx] *= 1.3f;  // Additional 30% boost
                            }
                        }
                    }
                }
            }
            
            return multipliers;
        }
        
        /// <summary>
        /// Get feature name for a morph index.
        /// </summary>
        public static string GetFeatureForMorph(int morphIndex)
        {
            foreach (var kv in FeatureGroups)
            {
                foreach (int idx in kv.Value)
                {
                    if (idx == morphIndex)
                        return kv.Key;
                }
            }
            return "Unknown";
        }
    }
}
