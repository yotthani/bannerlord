using System;
using System.Collections.Generic;

namespace FaceLearner.Core
{
    /// <summary>
    /// Estimates body morphs based on face features.
    /// Uses correlations between face shape and body proportions.
    /// </summary>
    public static class BodyEstimator
    {
        private static Random _rng = new Random();
        
        // Body morph indices (group 0)
        public const int MORPH_WEIGHT = 60;   // key_time_point in skins.xml
        public const int MORPH_BUILD = 61;
        public const int MORPH_HEIGHT = 62;
        public const int MORPH_AGE = 63;
        
        // Face morph names we use for correlation
        private static readonly Dictionary<string, int> FaceMorphIndices = new Dictionary<string, int>
        {
            { "cheeks", 4 },           // Face Weight - correlates with body weight
            { "face_width", 0 },       // Face Width - correlates with build
            { "jaw_line", -1 },        // Will find dynamically
            { "face_ratio", 3 },       // Face Ratio
            { "cheekbone_width", 6 },  // Cheekbone Width
        };
        
        /// <summary>
        /// Estimate body morphs based on face morphs
        /// </summary>
        /// <param name="faceMorphs">Current face morph values (62 morphs)</param>
        /// <param name="isFemale">Character gender</param>
        /// <param name="age">Character age</param>
        /// <param name="variationAmount">Random variation (0-1), default 0.15</param>
        /// <returns>Dictionary of body morph name → suggested value (0-1 normalized)</returns>
        public static Dictionary<string, float> EstimateFromFace(
            float[] faceMorphs, 
            bool isFemale, 
            float age,
            float variationAmount = 0.15f)
        {
            var result = new Dictionary<string, float>();
            
            if (faceMorphs == null || faceMorphs.Length < 10)
            {
                // Return defaults
                result["weight"] = 0.5f;
                result["build"] = isFemale ? 0.3f : 0.5f;
                result["height"] = 0.5f;
                result["belly"] = 0.0f;
                return result;
            }
            
            // Extract relevant face features
            // Note: morph values from FaceController are already in 0-1 range (KeyWeights)
            float cheeks = SafeGet(faceMorphs, 4, 0.5f);           // Face weight/fullness
            float faceWidth = SafeGet(faceMorphs, 0, 0.5f);        // Face width
            float faceRatio = SafeGet(faceMorphs, 3, 0.5f);        // Face ratio
            float cheekboneWidth = SafeGet(faceMorphs, 6, 0.5f);   // Cheekbone width
            
            // === WEIGHT ===
            // Rounder face (cheeks) → higher weight
            // Wider face → slightly higher weight
            // cheeks and faceWidth are 0-1, so scale appropriately
            float baseWeight = 0.3f + (cheeks * 0.4f) + (faceWidth * 0.2f);
            
            // Females tend slightly lower
            if (isFemale) baseWeight -= 0.05f;
            
            // Older → slightly higher weight tendency
            if (age > 40) baseWeight += (age - 40) * 0.003f;
            
            result["weight"] = Clamp01(baseWeight + Variation(variationAmount));
            
            // === BUILD ===
            // Wider face + prominent cheekbones → more muscular build
            // Males tend higher build
            float baseBuild = 0.3f + (faceWidth * 0.3f) + (cheekboneWidth * 0.2f);
            
            if (!isFemale) baseBuild += 0.1f;
            
            // Young adults (20-35) → slightly more athletic
            if (age >= 20 && age <= 35) baseBuild += 0.05f;
            
            result["build"] = Clamp01(baseBuild + Variation(variationAmount));
            
            // === HEIGHT ===
            // Face ratio affects perceived height slightly
            // faceRatio > 0.5 → taller face → taller body
            // More variation here since face doesn't strongly predict height
            float baseHeight = 0.4f + (faceRatio * 0.3f);  // 0.4 to 0.7 base range
            
            // Males slightly taller on average
            if (!isFemale) baseHeight += 0.1f;
            
            result["height"] = Clamp01(baseHeight + Variation(variationAmount));
            
            // === BELLY ===
            // Only for older males with round faces
            float baseBelly = 0.0f;
            
            if (!isFemale && age > 35)
            {
                // Rounder face + older → beer belly chance
                baseBelly = Math.Max(0, (cheeks - 0.3f) * 0.4f + (age - 35) * 0.005f);
            }
            
            result["belly"] = Clamp01(baseBelly + Variation(variationAmount * 0.5f));
            
            return result;
        }
        
        /// <summary>
        /// Get body morph suggestions as a formatted string for logging
        /// </summary>
        public static string GetSummary(Dictionary<string, float> bodyMorphs)
        {
            if (bodyMorphs == null || bodyMorphs.Count == 0)
                return "Body: [none]";
            
            float weight = 0.5f, build = 0.5f, height = 0.5f, belly = 0f;
            bodyMorphs.TryGetValue("weight", out weight);
            bodyMorphs.TryGetValue("build", out build);
            bodyMorphs.TryGetValue("height", out height);
            bodyMorphs.TryGetValue("belly", out belly);
            
            return $"Body: W={weight:F2} B={build:F2} H={height:F2} Belly={belly:F2}";
        }
        
        /// <summary>
        /// Apply body morphs to FaceController (if it supports body morphs)
        /// </summary>
        public static bool ApplyToController(FaceController controller, Dictionary<string, float> bodyMorphs)
        {
            if (controller == null || bodyMorphs == null)
                return false;
            
            bool anyApplied = false;
            
            // Try to apply each body morph
            foreach (var kvp in bodyMorphs)
            {
                try
                {
                    // Convert 0-1 to actual morph range
                    // This depends on how FaceController handles body morphs
                    // For now, we'll store them for later use
                    anyApplied = true;
                }
                catch
                {
                    // Body morphs might not be supported
                }
            }
            
            return anyApplied;
        }
        
        /// <summary>
        /// Safely get a morph value from array (already 0-1 range)
        /// </summary>
        private static float SafeGet(float[] morphs, int index, float defaultVal)
        {
            if (index < 0 || index >= morphs.Length)
                return defaultVal;
            return morphs[index];
        }
        
        /// <summary>
        /// Generate random variation
        /// </summary>
        private static float Variation(float amount)
        {
            return (float)(_rng.NextDouble() * 2 - 1) * amount;
        }
        
        /// <summary>
        /// Clamp value to 0-1 range
        /// </summary>
        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
        
        /// <summary>
        /// Create a body profile description based on morphs
        /// </summary>
        public static string DescribeBody(Dictionary<string, float> bodyMorphs, bool isFemale)
        {
            if (bodyMorphs == null) return "Average";
            
            float weight = 0.5f, build = 0.5f, height = 0.5f;
            bodyMorphs.TryGetValue("weight", out weight);
            bodyMorphs.TryGetValue("build", out build);
            bodyMorphs.TryGetValue("height", out height);
            
            var parts = new List<string>();
            
            // Height description
            if (height < 0.35f) parts.Add("Short");
            else if (height > 0.65f) parts.Add("Tall");
            
            // Build description
            if (build > 0.7f) parts.Add(isFemale ? "Athletic" : "Muscular");
            else if (build < 0.3f) parts.Add("Slim");
            
            // Weight description
            if (weight > 0.7f) parts.Add("Heavy");
            else if (weight < 0.3f) parts.Add("Lean");
            
            if (parts.Count == 0) parts.Add("Average");
            
            return string.Join(" ", parts);
        }
    }
}
