using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Face characteristics derived from landmarks
    /// </summary>
    public class FaceProfile
    {
        // Proportions (0-1 normalized)
        public float FaceWidthRatio { get; set; }     // Wide vs narrow face
        public float JawlineAngle { get; set; }       // Round vs angular jaw
        public float ForeheadRatio { get; set; }      // High vs low forehead
        public float EyeSpacing { get; set; }         // Close vs wide-set eyes
        public float NoseLength { get; set; }         // Short vs long nose
        public float LipFullness { get; set; }        // Thin vs full lips
        public float ChinProminence { get; set; }     // Recessed vs prominent chin
        public float CheekboneWidth { get; set; }     // Narrow vs wide cheekbones
        
        // Metadata (from dataset if available)
        public float? Age { get; set; }
        public bool? IsFemale { get; set; }
        public int? Race { get; set; }  // 0=White, 1=Black, 2=East Asian, 3=Southeast Asian, 4=Indian, 5=Middle Eastern, 6=Latino
        
        // Computed face type
        public FaceShape Shape { get; set; }
        
        /// <summary>
        /// Get a compact hash for clustering similar faces
        /// </summary>
        public string GetTypeHash()
        {
            // Quantize to buckets for clustering
            int w = (int)(FaceWidthRatio * 3);   // 0-2
            int j = (int)(JawlineAngle * 3);
            int f = (int)(ForeheadRatio * 3);
            int e = (int)(EyeSpacing * 3);
            int g = IsFemale.HasValue ? (IsFemale.Value ? 1 : 0) : 2;
            
            return $"{(int)Shape}_{w}{j}{f}{e}_{g}";
        }
        
        /// <summary>
        /// Calculate similarity to another face profile (0-1)
        /// </summary>
        public float SimilarityTo(FaceProfile other)
        {
            float diff = 0;
            diff += Math.Abs(FaceWidthRatio - other.FaceWidthRatio);
            diff += Math.Abs(JawlineAngle - other.JawlineAngle);
            diff += Math.Abs(ForeheadRatio - other.ForeheadRatio);
            diff += Math.Abs(EyeSpacing - other.EyeSpacing);
            diff += Math.Abs(NoseLength - other.NoseLength);
            diff += Math.Abs(LipFullness - other.LipFullness);
            diff += Math.Abs(ChinProminence - other.ChinProminence);
            diff += Math.Abs(CheekboneWidth - other.CheekboneWidth);
            
            // Gender match bonus
            if (IsFemale.HasValue && other.IsFemale.HasValue && IsFemale == other.IsFemale)
                diff -= 0.2f;
            
            // Race hint bonus (only for starting recipe selection)
            // Not racist - just statistical: same ethnicity often has similar facial proportions
            // This helps find a better starting point, the actual learning is race-agnostic
            if (Race.HasValue && other.Race.HasValue && Race == other.Race)
                diff -= 0.15f;  // Smaller bonus than gender
            
            return Math.Max(0, 1f - diff / 4f);  // Normalize to 0-1
        }
    }
    
    public enum FaceShape
    {
        Oval,       // Balanced proportions
        Round,      // Wide, soft jawline
        Square,     // Wide jaw, angular
        Oblong,     // Narrow, long
        Heart,      // Wide forehead, narrow chin
        Diamond,    // Wide cheekbones, narrow forehead/chin
        Unknown
    }
    
    /// <summary>
    /// Analyzes face landmarks to extract proportions and type
    /// </summary>
    public static class FaceTypeAnalyzer
    {
        // Dlib 68 landmark indices
        private const int JAW_START = 0;
        private const int JAW_END = 16;
        private const int LEFT_BROW_START = 17;
        private const int RIGHT_BROW_END = 26;
        private const int NOSE_TOP = 27;
        private const int NOSE_TIP = 30;
        private const int NOSE_BOTTOM = 33;
        private const int LEFT_EYE_LEFT = 36;
        private const int LEFT_EYE_RIGHT = 39;
        private const int RIGHT_EYE_LEFT = 42;
        private const int RIGHT_EYE_RIGHT = 45;
        private const int MOUTH_LEFT = 48;
        private const int MOUTH_RIGHT = 54;
        private const int MOUTH_TOP = 51;
        private const int MOUTH_BOTTOM = 57;
        private const int CHIN = 8;
        
        /// <summary>
        /// Analyze landmarks to create a face profile
        /// </summary>
        public static FaceProfile Analyze(float[] landmarks, Dictionary<string, float> metadata = null)
        {
            if (landmarks == null || landmarks.Length < 136)
                return new FaceProfile { Shape = FaceShape.Unknown };
            
            // Convert 468-point FaceMesh to 68-point for analysis if needed
            float[] landmarks68 = landmarks;
            if (landmarks.Length >= 936) // 468 * 2
            {
                landmarks68 = ConvertTo68(landmarks);
            }
            
            var profile = new FaceProfile();
            
            // Extract key points (x,y pairs)
            float GetX(int idx) => landmarks68[idx * 2];
            float GetY(int idx) => landmarks68[idx * 2 + 1];
            
            // Face width at cheekbones (jaw points 1 and 15)
            float faceWidth = Math.Abs(GetX(1) - GetX(15));
            
            // Face height (top of forehead approximated by brow to chin)
            float browY = (GetY(LEFT_BROW_START) + GetY(RIGHT_BROW_END)) / 2f;
            float chinY = GetY(CHIN);
            float faceHeight = Math.Abs(chinY - browY);
            
            // Jaw width at widest point (points 4 and 12)
            float jawWidth = Math.Abs(GetX(4) - GetX(12));
            
            // Forehead width approximation (brow endpoints)
            float foreheadWidth = Math.Abs(GetX(LEFT_BROW_START) - GetX(RIGHT_BROW_END));
            
            // Eye spacing
            float leftEyeCenter = (GetX(LEFT_EYE_LEFT) + GetX(LEFT_EYE_RIGHT)) / 2f;
            float rightEyeCenter = (GetX(RIGHT_EYE_LEFT) + GetX(RIGHT_EYE_RIGHT)) / 2f;
            float eyeDistance = Math.Abs(rightEyeCenter - leftEyeCenter);
            
            // Eye width (average)
            float leftEyeWidth = Math.Abs(GetX(LEFT_EYE_RIGHT) - GetX(LEFT_EYE_LEFT));
            float rightEyeWidth = Math.Abs(GetX(RIGHT_EYE_RIGHT) - GetX(RIGHT_EYE_LEFT));
            float avgEyeWidth = (leftEyeWidth + rightEyeWidth) / 2f;
            
            // Nose length
            float noseLength = Math.Abs(GetY(NOSE_BOTTOM) - GetY(NOSE_TOP));
            
            // Lip fullness (mouth height vs width)
            float mouthWidth = Math.Abs(GetX(MOUTH_RIGHT) - GetX(MOUTH_LEFT));
            float mouthHeight = Math.Abs(GetY(MOUTH_BOTTOM) - GetY(MOUTH_TOP));
            
            // Chin prominence (chin Y relative to mouth bottom)
            float chinDistance = Math.Abs(GetY(CHIN) - GetY(MOUTH_BOTTOM));
            
            // Calculate ratios (normalize to 0-1 range)
            profile.FaceWidthRatio = Clamp(faceWidth / faceHeight, 0.5f, 1.5f, 0f, 1f);
            profile.JawlineAngle = Clamp(jawWidth / faceWidth, 0.6f, 1.0f, 0f, 1f);
            profile.ForeheadRatio = Clamp(foreheadWidth / faceWidth, 0.7f, 1.1f, 0f, 1f);
            profile.EyeSpacing = Clamp(eyeDistance / faceWidth, 0.2f, 0.5f, 0f, 1f);
            profile.NoseLength = Clamp(noseLength / faceHeight, 0.15f, 0.35f, 0f, 1f);
            profile.LipFullness = Clamp(mouthHeight / mouthWidth, 0.2f, 0.6f, 0f, 1f);
            profile.ChinProminence = Clamp(chinDistance / faceHeight, 0.1f, 0.25f, 0f, 1f);
            profile.CheekboneWidth = Clamp(faceWidth / jawWidth, 0.9f, 1.4f, 0f, 1f);
            
            // Determine face shape
            profile.Shape = DetermineFaceShape(profile);
            
            // Add metadata if available
            if (metadata != null)
            {
                if (metadata.TryGetValue("age", out float age))
                    profile.Age = age;
                if (metadata.TryGetValue("gender", out float gender))
                    profile.IsFemale = gender > 0.5f;
                if (metadata.TryGetValue("race", out float race))
                    profile.Race = (int)race;
            }
            
            return profile;
        }
        
        private static FaceShape DetermineFaceShape(FaceProfile p)
        {
            // Decision tree based on proportions
            // IMPROVED: Lower thresholds for Round to catch more round faces
            // Asian faces tend to have wider faces with softer jaws
            
            // Round: Wide face, soft jaw (LOWERED threshold from 0.6 to 0.5)
            // Also consider Asian-typical proportions: wider face + high cheekbones
            if (p.FaceWidthRatio > 0.55f && p.JawlineAngle > 0.5f)
                return FaceShape.Round;
            
            // Additional Round detection: Very soft jaw even with medium width
            if (p.JawlineAngle > 0.65f && p.ChinProminence < 0.4f)
                return FaceShape.Round;
            
            // Square: Wide face, angular jaw
            if (p.FaceWidthRatio > 0.6f && p.JawlineAngle < 0.4f)
                return FaceShape.Square;
            
            // Oblong: Narrow, long face
            if (p.FaceWidthRatio < 0.4f)
                return FaceShape.Oblong;
            
            // Heart: Wide forehead, narrow chin
            if (p.ForeheadRatio > 0.6f && p.ChinProminence < 0.4f)
                return FaceShape.Heart;
            
            // Diamond: Wide cheekbones
            if (p.CheekboneWidth > 0.7f && p.ForeheadRatio < 0.5f)
                return FaceShape.Diamond;
            
            // Default to Oval (balanced)
            return FaceShape.Oval;
        }
        
        /// <summary>
        /// Map value from one range to another
        /// </summary>
        private static float Clamp(float value, float inMin, float inMax, float outMin, float outMax)
        {
            float normalized = (value - inMin) / (inMax - inMin);
            normalized = Math.Max(0, Math.Min(1, normalized));
            return outMin + normalized * (outMax - outMin);
        }
        
        /// <summary>
        /// Get a text description of the face profile
        /// </summary>
        public static string Describe(FaceProfile profile)
        {
            var parts = new List<string>();
            
            parts.Add($"Shape: {profile.Shape}");
            
            if (profile.FaceWidthRatio > 0.6f) parts.Add("Wide");
            else if (profile.FaceWidthRatio < 0.4f) parts.Add("Narrow");
            
            if (profile.JawlineAngle > 0.6f) parts.Add("SoftJaw");
            else if (profile.JawlineAngle < 0.4f) parts.Add("AngularJaw");
            
            if (profile.IsFemale.HasValue)
                parts.Add(profile.IsFemale.Value ? "F" : "M");
            
            if (profile.Age.HasValue)
                parts.Add($"Age{(int)profile.Age}");
            
            return string.Join(" ", parts);
        }
        
        /// <summary>
        /// Adjust face shape based on detected race (Asian faces tend to be rounder)
        /// Call this AFTER Analyze() if you have race information from FairFace
        /// </summary>
        public static void AdjustForRace(FaceProfile profile, string race, float raceConfidence)
        {
            if (profile == null || string.IsNullOrEmpty(race) || raceConfidence < 0.7f)
                return;
            
            // Asian faces: Often have wider face, flatter nose bridge, softer jawline
            // If detected as Oval or Square but race is Asian with high conf, consider Round
            if ((race == "East Asian" || race == "Southeast Asian") && raceConfidence > 0.8f)
            {
                // If face has wide cheekbones and isn't already Round, nudge toward Round
                if (profile.Shape != FaceShape.Round && 
                    profile.FaceWidthRatio > 0.45f && 
                    profile.JawlineAngle > 0.4f)
                {
                    // Asian faces often appear rounder even with moderate jaw angle
                    profile.Shape = FaceShape.Round;
                }
                
                // Also adjust jaw perception - Asian jaws are often softer
                profile.JawlineAngle = Math.Min(1.0f, profile.JawlineAngle + 0.15f);
            }
            
            // Female faces: Tend to have softer features
            if (profile.IsFemale == true && profile.JawlineAngle > 0.35f && profile.JawlineAngle < 0.55f)
            {
                // Borderline cases for women should lean toward Round
                if (profile.Shape == FaceShape.Oval || profile.Shape == FaceShape.Square)
                {
                    profile.JawlineAngle += 0.1f;
                    if (profile.JawlineAngle > 0.5f && profile.FaceWidthRatio > 0.5f)
                    {
                        profile.Shape = FaceShape.Round;
                    }
                }
            }
        }
        
        /// <summary>
        /// Convert 468-point FaceMesh to 68-point Dlib format
        /// </summary>
        private static float[] ConvertTo68(float[] mesh468)
        {
            if (mesh468 == null || mesh468.Length < 936) return mesh468;
            
            // Mapping from 68-point indices to 468-point mesh indices
            int[] mapping = {
                // Jawline (0-16)
                234, 93, 132, 58, 172, 136, 150, 149, 152, 148, 176, 365, 397, 288, 361, 323, 454,
                // Right eyebrow (17-21)
                70, 63, 105, 66, 107,
                // Left eyebrow (22-26)
                336, 296, 334, 293, 300,
                // Nose bridge (27-30)
                168, 6, 197, 195,
                // Nose bottom (31-35)
                98, 97, 2, 326, 327,
                // Right eye (36-41)
                33, 160, 158, 133, 153, 144,
                // Left eye (42-47)
                362, 385, 387, 263, 373, 380,
                // Outer lips (48-59)
                61, 40, 37, 0, 267, 270, 291, 321, 314, 17, 84, 181,
                // Inner lips (60-67)
                78, 82, 13, 312, 308, 317, 14, 87
            };
            
            var result = new float[136]; // 68 * 2
            
            for (int i = 0; i < mapping.Length && i < 68; i++)
            {
                int srcIdx = mapping[i];
                if (srcIdx < 468)
                {
                    result[i * 2] = mesh468[srcIdx * 2];
                    result[i * 2 + 1] = mesh468[srcIdx * 2 + 1];
                }
            }
            
            return result;
        }
    }
}
