// v3.0.27: Native FaceMesh 468 — Dlib removed
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
    /// Analyzes face landmarks to extract proportions and type.
    /// Uses native FaceMesh 468-point landmarks (936 floats: x,y pairs).
    /// </summary>
    public static class FaceTypeAnalyzer
    {
        // FaceMesh 468 landmark indices (native, no Dlib conversion)
        // Jaw contour
        private const int JAW_1 = 93;           // Upper jaw left (was Dlib 1)
        private const int JAW_4 = 172;          // Mid jaw left (was Dlib 4)
        private const int CHIN = 152;           // Chin bottom (was Dlib 8)
        private const int JAW_12 = 397;         // Mid jaw right (was Dlib 12)
        private const int JAW_15 = 323;         // Upper jaw right (was Dlib 15)

        // Eyebrows
        private const int LEFT_BROW_OUTER = 70;   // Left brow outer (was Dlib 17)
        private const int RIGHT_BROW_OUTER = 300; // Right brow outer (was Dlib 26)

        // Nose
        private const int NOSE_BRIDGE_TOP = 168;  // Nose bridge top (was Dlib 27)
        private const int NOSE_TIP = 195;         // Nose tip (was Dlib 30)
        private const int NOSE_BOTTOM = 2;        // Nose bottom center (was Dlib 33)

        // Right eye (from viewer's perspective — anatomical left eye)
        private const int RIGHT_EYE_INNER = 33;   // Right eye inner corner (was Dlib 36)
        private const int RIGHT_EYE_OUTER = 133;  // Right eye outer corner (was Dlib 39)

        // Left eye (from viewer's perspective — anatomical right eye)
        private const int LEFT_EYE_INNER = 362;   // Left eye inner corner (was Dlib 42)
        private const int LEFT_EYE_OUTER = 263;   // Left eye outer corner (was Dlib 45)

        // Mouth
        private const int MOUTH_LEFT = 61;        // Mouth left corner (was Dlib 48)
        private const int MOUTH_TOP = 0;          // Mouth top center (was Dlib 51)
        private const int MOUTH_RIGHT = 291;      // Mouth right corner (was Dlib 54)
        private const int MOUTH_BOTTOM = 17;      // Mouth bottom center (was Dlib 57)

        /// <summary>
        /// Get X coordinate for a FaceMesh 468 landmark index from the raw array
        /// </summary>
        private static float GetX(float[] landmarks, int meshIndex) => landmarks[meshIndex * 2];

        /// <summary>
        /// Get Y coordinate for a FaceMesh 468 landmark index from the raw array
        /// </summary>
        private static float GetY(float[] landmarks, int meshIndex) => landmarks[meshIndex * 2 + 1];

        /// <summary>
        /// Analyze landmarks to create a face profile.
        /// Input must be FaceMesh 468-point landmarks (936 floats).
        /// </summary>
        public static FaceProfile Analyze(float[] landmarks, Dictionary<string, float> metadata = null)
        {
            if (landmarks == null || landmarks.Length < 936)
                return new FaceProfile { Shape = FaceShape.Unknown };

            var profile = new FaceProfile();

            // Face width at cheekbones (jaw points 1 and 15 equivalent)
            float faceWidth = Math.Abs(GetX(landmarks, JAW_1) - GetX(landmarks, JAW_15));

            // Face height (top of forehead approximated by brow to chin)
            float browY = (GetY(landmarks, LEFT_BROW_OUTER) + GetY(landmarks, RIGHT_BROW_OUTER)) / 2f;
            float chinY = GetY(landmarks, CHIN);
            float faceHeight = Math.Abs(chinY - browY);

            // Jaw width at widest point (points 4 and 12 equivalent)
            float jawWidth = Math.Abs(GetX(landmarks, JAW_4) - GetX(landmarks, JAW_12));

            // Forehead width approximation (brow endpoints)
            float foreheadWidth = Math.Abs(GetX(landmarks, LEFT_BROW_OUTER) - GetX(landmarks, RIGHT_BROW_OUTER));

            // Eye spacing
            float rightEyeCenter = (GetX(landmarks, RIGHT_EYE_INNER) + GetX(landmarks, RIGHT_EYE_OUTER)) / 2f;
            float leftEyeCenter = (GetX(landmarks, LEFT_EYE_INNER) + GetX(landmarks, LEFT_EYE_OUTER)) / 2f;
            float eyeDistance = Math.Abs(leftEyeCenter - rightEyeCenter);

            // Eye width (average)
            float rightEyeWidth = Math.Abs(GetX(landmarks, RIGHT_EYE_OUTER) - GetX(landmarks, RIGHT_EYE_INNER));
            float leftEyeWidth = Math.Abs(GetX(landmarks, LEFT_EYE_OUTER) - GetX(landmarks, LEFT_EYE_INNER));
            float avgEyeWidth = (rightEyeWidth + leftEyeWidth) / 2f;

            // Nose length
            float noseLength = Math.Abs(GetY(landmarks, NOSE_BOTTOM) - GetY(landmarks, NOSE_BRIDGE_TOP));

            // Lip fullness (mouth height vs width)
            float mouthWidth = Math.Abs(GetX(landmarks, MOUTH_RIGHT) - GetX(landmarks, MOUTH_LEFT));
            float mouthHeight = Math.Abs(GetY(landmarks, MOUTH_BOTTOM) - GetY(landmarks, MOUTH_TOP));

            // Chin prominence (chin Y relative to mouth bottom)
            float chinDistance = Math.Abs(GetY(landmarks, CHIN) - GetY(landmarks, MOUTH_BOTTOM));

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
    }
}
