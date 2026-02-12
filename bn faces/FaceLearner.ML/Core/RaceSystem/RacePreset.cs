using System;
using System.Collections.Generic;

namespace FaceLearner.ML.Core.RaceSystem
{
    /// <summary>
    /// Defines morph presets and aesthetic guidelines for a race.
    /// Used to bias face generation towards race-appropriate features.
    /// 
    /// Example usage:
    /// - Elves: Fine features, high cheekbones, slender jaw
    /// - Orcs: Harsh features, heavy brow, strong jaw
    /// - Dwarves: Stocky, broad nose, strong chin
    /// - Hobbits: Round, friendly, soft features
    /// </summary>
    public class RacePreset
    {
        /// <summary>Unique identifier matching Bannerlord race ID</summary>
        public string RaceId { get; set; }
        
        /// <summary>Display name (e.g., "High Elf", "Uruk-hai")</summary>
        public string DisplayName { get; set; }
        
        /// <summary>Race category for grouping (e.g., "Elven", "Orcish", "Mannish", "Dwarven")</summary>
        public string Category { get; set; }
        
        /// <summary>Description of race aesthetics</summary>
        public string Description { get; set; }
        
        /// <summary>Whether this race uses a custom skeleton</summary>
        public bool HasCustomSkeleton { get; set; }
        
        /// <summary>Skeleton name if custom</summary>
        public string SkeletonName { get; set; }
        
        /// <summary>Base race to inherit morph ranges from (e.g., "human" for elves)</summary>
        public string BasedOnRace { get; set; } = "human";
        
        /// <summary>
        /// Morph biases - shifts the center point of morphs
        /// Key: Morph index, Value: Bias (-1 to 1, where 0 is neutral)
        /// Example: Morph 15 (jaw width) = -0.3 means narrower jaw by default
        /// </summary>
        public Dictionary<int, float> MorphBiases { get; set; } = new Dictionary<int, float>();
        
        /// <summary>
        /// Morph range limits - restricts valid range for morphs
        /// Key: Morph index, Value: (min, max) tuple
        /// Example: Morph 15 = (0.2, 0.6) limits jaw width to narrow-medium
        /// </summary>
        public Dictionary<int, (float min, float max)> MorphRanges { get; set; } = new Dictionary<int, (float min, float max)>();
        
        /// <summary>
        /// Feature-level biases (higher level than individual morphs)
        /// Key: Feature name (Face, Eyes, Nose, Mouth, Jaw, Brows)
        /// Value: Aesthetic descriptor and bias strength
        /// </summary>
        public Dictionary<string, FeatureBias> FeatureBiases { get; set; } = new Dictionary<string, FeatureBias>();
        
        /// <summary>
        /// Skin tone range (0 = lightest, 1 = darkest)
        /// </summary>
        public (float min, float max) SkinToneRange { get; set; } = (0f, 1f);
        
        /// <summary>Default skin tone for this race</summary>
        public float DefaultSkinTone { get; set; } = 0.5f;
        
        /// <summary>Eye color range</summary>
        public (float min, float max) EyeColorRange { get; set; } = (0f, 1f);
        
        /// <summary>Hair color range</summary>
        public (float min, float max) HairColorRange { get; set; } = (0f, 1f);
        
        /// <summary>Age range suitable for this race</summary>
        public (float min, float max) AgeRange { get; set; } = (18f, 80f);
        
        /// <summary>Height bias (-1 = shorter, 0 = normal, 1 = taller)</summary>
        public float HeightBias { get; set; } = 0f;
        
        /// <summary>Build bias (-1 = slender, 0 = normal, 1 = stocky)</summary>
        public float BuildBias { get; set; } = 0f;
        
        /// <summary>Weight bias</summary>
        public float WeightBias { get; set; } = 0f;
        
        /// <summary>
        /// Named morph presets for quick application
        /// Example: "Noble", "Warrior", "Elder", "Young"
        /// </summary>
        public Dictionary<string, MorphPreset> NamedPresets { get; set; } = new Dictionary<string, MorphPreset>();
        
        /// <summary>
        /// Apply race biases to a morph array
        /// </summary>
        public float[] ApplyBiases(float[] morphs)
        {
            if (morphs == null) return morphs;
            
            var result = new float[morphs.Length];
            Array.Copy(morphs, result, morphs.Length);
            
            foreach (var bias in MorphBiases)
            {
                if (bias.Key < result.Length)
                {
                    result[bias.Key] = Clamp(result[bias.Key] + bias.Value);
                }
            }
            
            // Apply range limits
            foreach (var range in MorphRanges)
            {
                if (range.Key < result.Length)
                {
                    result[range.Key] = Clamp(result[range.Key], range.Value.min, range.Value.max);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Check if a morph value is within race-appropriate range
        /// </summary>
        public bool IsMorphInRange(int morphIndex, float value)
        {
            if (MorphRanges.TryGetValue(morphIndex, out var range))
            {
                return value >= range.min && value <= range.max;
            }
            return true; // No restriction
        }
        
        /// <summary>
        /// Get the center point for a morph (including bias)
        /// </summary>
        public float GetMorphCenter(int morphIndex)
        {
            float center = 0.5f;
            if (MorphBiases.TryGetValue(morphIndex, out float bias))
            {
                center += bias;
            }
            return Clamp(center);
        }
        
        private static float Clamp(float v, float min = 0f, float max = 1f)
        {
            return Math.Max(min, Math.Min(max, v));
        }
    }
    
    /// <summary>
    /// Feature-level bias configuration
    /// </summary>
    public class FeatureBias
    {
        /// <summary>Aesthetic descriptor (e.g., "Fine", "Harsh", "Round")</summary>
        public string Aesthetic { get; set; }
        
        /// <summary>Bias strength (-1 to 1)</summary>
        public float Strength { get; set; }
        
        /// <summary>Specific adjustments for this feature</summary>
        public Dictionary<string, float> Adjustments { get; set; } = new Dictionary<string, float>();
    }
    
    /// <summary>
    /// Named preset with specific morph values
    /// </summary>
    public class MorphPreset
    {
        /// <summary>Preset name</summary>
        public string Name { get; set; }
        
        /// <summary>Description</summary>
        public string Description { get; set; }
        
        /// <summary>Morph values to apply</summary>
        public Dictionary<int, float> Morphs { get; set; } = new Dictionary<int, float>();
        
        /// <summary>Whether to blend with existing or replace</summary>
        public bool BlendMode { get; set; } = true;
        
        /// <summary>Blend factor (0 = keep original, 1 = full preset)</summary>
        public float BlendFactor { get; set; } = 0.5f;
    }
}
