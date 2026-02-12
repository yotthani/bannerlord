using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Race modifier - transforms target features towards race characteristics.
    /// 
    /// These are NOT learned - they are defined transformations.
    /// Learning is race-agnostic, generation applies race modifiers.
    /// 
    /// Usage:
    ///   Learning: Learn Photo → Morphs relationship (no modifier)
    ///   Generation: Photo → Morphs → Apply Race Modifier → Final Morphs
    /// </summary>
    public class RaceModifier
    {
        public string RaceId { get; set; }
        public string DisplayName { get; set; }
        
        // ═══════════════════════════════════════════════════════════════
        // EBENE 1: FACE SHAPE MODIFICATION
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Face width scale (1.0 = no change, 0.95 = narrower)</summary>
        public float FaceWidthScale { get; set; } = 1.0f;
        
        /// <summary>Face height scale (1.0 = no change, 1.05 = taller)</summary>
        public float FaceHeightScale { get; set; } = 1.0f;
        
        // ═══════════════════════════════════════════════════════════════
        // EBENE 2: STRUCTURE MODIFICATION
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Jaw angle shift in degrees (negative = sharper)</summary>
        public float JawAngleShift { get; set; } = 0f;
        
        /// <summary>Chin width scale</summary>
        public float ChinWidthScale { get; set; } = 1.0f;
        
        /// <summary>Forehead height scale</summary>
        public float ForeheadHeightScale { get; set; } = 1.0f;
        
        /// <summary>Cheekbone prominence scale</summary>
        public float CheekboneScale { get; set; } = 1.0f;
        
        // ═══════════════════════════════════════════════════════════════
        // EBENE 3: FEATURE MODIFICATION
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Nose width scale</summary>
        public float NoseWidthScale { get; set; } = 1.0f;
        
        /// <summary>Nose length scale</summary>
        public float NoseLengthScale { get; set; } = 1.0f;
        
        /// <summary>Eye width scale</summary>
        public float EyeWidthScale { get; set; } = 1.0f;
        
        /// <summary>Eye angle shift</summary>
        public float EyeAngleShift { get; set; } = 0f;
        
        /// <summary>Mouth width scale</summary>
        public float MouthWidthScale { get; set; } = 1.0f;
        
        // ═══════════════════════════════════════════════════════════════
        // GLOBAL MODIFIERS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Overall softness (0 = angular, 1 = soft/rounded)</summary>
        public float Softness { get; set; } = 0.5f;
        
        /// <summary>Age bias (-1 = younger, 0 = neutral, 1 = older)</summary>
        public float AgeBias { get; set; } = 0f;
        
        // ═══════════════════════════════════════════════════════════════
        // DIRECT MORPH BIASES (for things that can't be expressed as features)
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Direct morph value biases (morph index → bias value)</summary>
        public Dictionary<int, float> MorphBiases { get; set; } = new Dictionary<int, float>();
    }
    
    /// <summary>
    /// Predefined race modifiers for LOTR races
    /// </summary>
    public static class RaceModifiers
    {
        private static readonly Dictionary<string, RaceModifier> _modifiers = new Dictionary<string, RaceModifier>();
        
        static RaceModifiers()
        {
            // Initialize all predefined modifiers
            Register(Human);
            Register(HighElf);
            Register(WoodElf);
            Register(Dwarf);
            Register(Hobbit);
            Register(Orc);
            Register(Uruk);
            Register(Gondorian);
            Register(Rohirrim);
            Register(Haradrim);
        }
        
        /// <summary>Get modifier by race ID</summary>
        public static RaceModifier Get(string raceId)
        {
            if (string.IsNullOrEmpty(raceId))
                return null;
            
            _modifiers.TryGetValue(raceId.ToLower(), out var mod);
            return mod;
        }
        
        /// <summary>Register a custom modifier</summary>
        public static void Register(RaceModifier modifier)
        {
            if (modifier?.RaceId != null)
                _modifiers[modifier.RaceId.ToLower()] = modifier;
        }
        
        /// <summary>Get all available race IDs</summary>
        public static string[] GetAllRaceIds() => _modifiers.Keys.ToArray();
        
        // ═══════════════════════════════════════════════════════════════
        // PREDEFINED MODIFIERS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Human (neutral/baseline)</summary>
        public static RaceModifier Human = new RaceModifier
        {
            RaceId = "human",
            DisplayName = "Human",
            // All defaults (1.0 scales, 0 shifts)
        };
        
        /// <summary>High Elf - refined, ethereal, elongated features</summary>
        public static RaceModifier HighElf = new RaceModifier
        {
            RaceId = "high_elf",
            DisplayName = "High Elf",
            
            // Narrower, taller face
            FaceWidthScale = 0.94f,
            FaceHeightScale = 1.06f,
            
            // Sharper jaw, narrower chin
            JawAngleShift = -8f,
            ChinWidthScale = 0.88f,
            
            // Higher forehead, prominent cheekbones
            ForeheadHeightScale = 1.08f,
            CheekboneScale = 1.10f,
            
            // Finer nose, slightly larger eyes
            NoseWidthScale = 0.88f,
            NoseLengthScale = 1.05f,
            EyeWidthScale = 1.06f,
            EyeAngleShift = 3f,  // Slightly upward slant
            
            // Smaller mouth
            MouthWidthScale = 0.95f,
            
            // Very soft features
            Softness = 0.85f,
            AgeBias = -0.3f,  // Appear younger
        };
        
        /// <summary>Wood Elf - more rugged than High Elf but still elvish</summary>
        public static RaceModifier WoodElf = new RaceModifier
        {
            RaceId = "wood_elf",
            DisplayName = "Wood Elf",
            
            FaceWidthScale = 0.96f,
            FaceHeightScale = 1.04f,
            
            JawAngleShift = -5f,
            ChinWidthScale = 0.92f,
            
            ForeheadHeightScale = 1.04f,
            CheekboneScale = 1.08f,
            
            NoseWidthScale = 0.92f,
            NoseLengthScale = 1.02f,
            EyeWidthScale = 1.04f,
            EyeAngleShift = 2f,
            
            MouthWidthScale = 0.97f,
            
            Softness = 0.70f,
            AgeBias = -0.2f,
        };
        
        /// <summary>Dwarf - broad, sturdy, strong features</summary>
        public static RaceModifier Dwarf = new RaceModifier
        {
            RaceId = "dwarf",
            DisplayName = "Dwarf",
            
            // Wider, shorter face
            FaceWidthScale = 1.12f,
            FaceHeightScale = 0.94f,
            
            // Wide jaw, strong chin
            JawAngleShift = +8f,
            ChinWidthScale = 1.15f,
            
            // Lower forehead, prominent cheekbones
            ForeheadHeightScale = 0.92f,
            CheekboneScale = 1.12f,
            
            // Broader nose
            NoseWidthScale = 1.18f,
            NoseLengthScale = 0.95f,
            
            // Smaller eyes, wider mouth
            EyeWidthScale = 0.95f,
            MouthWidthScale = 1.05f,
            
            // Angular features
            Softness = 0.25f,
            AgeBias = 0.2f,  // Appear older/weathered
        };
        
        /// <summary>Hobbit - round, friendly, childlike proportions</summary>
        public static RaceModifier Hobbit = new RaceModifier
        {
            RaceId = "hobbit",
            DisplayName = "Hobbit",
            
            // Rounder face
            FaceWidthScale = 1.08f,
            FaceHeightScale = 0.96f,
            
            // Soft jaw, rounded chin
            JawAngleShift = +3f,
            ChinWidthScale = 1.05f,
            
            // Normal forehead, full cheeks
            ForeheadHeightScale = 0.98f,
            CheekboneScale = 0.95f,
            
            // Slightly wider nose, normal length
            NoseWidthScale = 1.08f,
            NoseLengthScale = 0.98f,
            
            // Larger eyes (childlike), normal mouth
            EyeWidthScale = 1.08f,
            MouthWidthScale = 1.02f,
            
            // Very soft, rounded features
            Softness = 0.80f,
            AgeBias = -0.4f,  // Appear younger
        };
        
        /// <summary>Orc - brutish, deformed, aggressive</summary>
        public static RaceModifier Orc = new RaceModifier
        {
            RaceId = "orc",
            DisplayName = "Orc",
            
            // Wide, flattened face
            FaceWidthScale = 1.15f,
            FaceHeightScale = 0.92f,
            
            // Heavy jaw, protruding chin
            JawAngleShift = +12f,
            ChinWidthScale = 1.20f,
            
            // Low forehead, flat cheekbones
            ForeheadHeightScale = 0.85f,
            CheekboneScale = 0.90f,
            
            // Flat, wide nose
            NoseWidthScale = 1.35f,
            NoseLengthScale = 0.80f,
            
            // Small, deep-set eyes, wide mouth
            EyeWidthScale = 0.85f,
            EyeAngleShift = -5f,  // Downward slant
            MouthWidthScale = 1.15f,
            
            // Very angular, harsh
            Softness = 0.10f,
            AgeBias = 0.3f,
            
            // Direct morph biases for orc-specific deformations
            MorphBiases = new Dictionary<int, float>
            {
                { 15, 0.30f },   // Brow ridge prominence
                { 28, -0.25f }, // Nose bridge (flatten)
            }
        };
        
        /// <summary>Uruk-hai - larger, more human-like orc</summary>
        public static RaceModifier Uruk = new RaceModifier
        {
            RaceId = "uruk",
            DisplayName = "Uruk-hai",
            
            FaceWidthScale = 1.12f,
            FaceHeightScale = 0.95f,
            
            JawAngleShift = +10f,
            ChinWidthScale = 1.18f,
            
            ForeheadHeightScale = 0.88f,
            CheekboneScale = 0.95f,
            
            NoseWidthScale = 1.25f,
            NoseLengthScale = 0.85f,
            
            EyeWidthScale = 0.90f,
            EyeAngleShift = -3f,
            MouthWidthScale = 1.10f,
            
            Softness = 0.15f,
            AgeBias = 0.2f,
        };
        
        // ═══════════════════════════════════════════════════════════════
        // HUMAN VARIANTS (for LOTR kingdoms)
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Gondorian - noble, Mediterranean features</summary>
        public static RaceModifier Gondorian = new RaceModifier
        {
            RaceId = "gondorian",
            DisplayName = "Gondorian",
            
            FaceWidthScale = 0.98f,
            FaceHeightScale = 1.02f,
            
            JawAngleShift = -2f,
            ChinWidthScale = 0.98f,
            
            NoseWidthScale = 0.96f,
            NoseLengthScale = 1.04f,  // Roman nose
            
            Softness = 0.55f,
        };
        
        /// <summary>Rohirrim - Nordic, strong features</summary>
        public static RaceModifier Rohirrim = new RaceModifier
        {
            RaceId = "rohirrim",
            DisplayName = "Rohirrim",
            
            FaceWidthScale = 1.04f,
            FaceHeightScale = 1.02f,
            
            JawAngleShift = +3f,
            ChinWidthScale = 1.05f,
            
            CheekboneScale = 1.05f,
            
            NoseWidthScale = 1.02f,
            
            Softness = 0.45f,
        };
        
        /// <summary>Haradrim - Middle Eastern/North African features</summary>
        public static RaceModifier Haradrim = new RaceModifier
        {
            RaceId = "haradrim",
            DisplayName = "Haradrim",
            
            FaceWidthScale = 0.98f,
            FaceHeightScale = 1.00f,
            
            JawAngleShift = -1f,
            
            NoseWidthScale = 1.02f,
            NoseLengthScale = 1.03f,
            
            EyeWidthScale = 1.02f,
            
            Softness = 0.50f,
        };
    }
}
