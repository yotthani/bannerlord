using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Linq;
using TaleWorlds.Core;

namespace FaceLearner.Core
{
    /// <summary>
    /// Complete character data including face morphs, body, skin, hair, eyes.
    /// Supports export/import and tracks whether face was ML-generated.
    /// </summary>
    [Serializable]
    public class CharacterData
    {
        // === META ===
        public string Name { get; set; } = "Unnamed";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string SourceImage { get; set; } = "";  // Path to source image if generated
        public bool IsFaceGenerated { get; set; } = false;  // Lock face morphs if true
        public float GeneratedScore { get; set; } = 0f;  // Match score when generated
        
        // === FACE MORPHS (0-1 range, ML-generated) ===
        // Bannerlord has 62 face morphs organized in categories
        public float[] FaceMorphs { get; set; } = new float[62];

        // === BODY ===
        public float Height { get; set; } = 0.5f;      // 0-1
        public float Build { get; set; } = 0.5f;       // 0-1 (slim to muscular)
        public float Weight { get; set; } = 0.5f;      // 0-1 (lean to heavy)
        public float Age { get; set; } = 30f;          // 18-100 (actual age in years)
        public float Scale { get; set; } = 1.05f;      // 0.2-2.0 direct scale (1.05 = male human, 0.97 = female human)
        
        // === BODY MORPHS ===
        public float Shoulders { get; set; } = 0.5f;   // 0-1 (narrow to wide)
        public float Torso { get; set; } = 0.5f;       // 0-1 (short to long)
        public float Arms { get; set; } = 0.5f;        // 0-1 (thin to thick)
        public float Legs { get; set; } = 0.5f;        // 0-1 (short to long)
        
        // === SKIN ===
        public float SkinTone { get; set; } = 0.5f;    // 0-1 (light to dark)
        public int SkinTextureIndex { get; set; } = 0;
        
        // === HAIR ===
        public int HairStyleIndex { get; set; } = 0;
        public float HairColorR { get; set; } = 0.3f;
        public float HairColorG { get; set; } = 0.2f;
        public float HairColorB { get; set; } = 0.1f;
        public int BeardStyleIndex { get; set; } = 0;  // For males
        
        // === EYES ===
        public float EyeColorR { get; set; } = 0.4f;
        public float EyeColorG { get; set; } = 0.3f;
        public float EyeColorB { get; set; } = 0.2f;
        
        // === GENDER ===
        public bool IsFemale { get; set; } = false;
        
        // === TATTOOS / SCARS ===
        public int TattooIndex { get; set; } = 0;
        public int ScarIndex { get; set; } = 0;
        
        // === EQUIPMENT (optional) ===
        public string HeadArmorId { get; set; } = "";
        public string BodyArmorId { get; set; } = "";
        
        /// <summary>
        /// Create a new character with default values
        /// </summary>
        public static CharacterData CreateDefault(bool female = false)
        {
            var data = new CharacterData
            {
                IsFemale = female,
                Name = female ? "New Female" : "New Male",
                CreatedAt = DateTime.Now
            };
            
            // Initialize face morphs to 0.5 (middle position)
            for (int i = 0; i < data.FaceMorphs.Length; i++)
            {
                data.FaceMorphs[i] = 0.5f;
            }
            
            return data;
        }
        
        /// <summary>
        /// Create from ML-generated face morphs
        /// </summary>
        public static CharacterData FromGeneratedFace(float[] morphs, string sourceImage, float score, bool female)
        {
            var data = CreateDefault(female);
            data.FaceMorphs = (float[])morphs.Clone();
            data.SourceImage = sourceImage;
            data.IsFaceGenerated = true;
            data.GeneratedScore = score;
            data.Name = $"Generated_{DateTime.Now:yyyyMMdd_HHmmss}";
            return data;
        }
        
        /// <summary>
        /// Export to XML file
        /// </summary>
        public void ExportToXml(string path)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(CharacterData));
                using (var writer = new StreamWriter(path))
                {
                    serializer.Serialize(writer, this);
                }
                SubModule.Log($"Character exported to {path}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"Export error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Import from XML file
        /// </summary>
        public static CharacterData ImportFromXml(string path)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(CharacterData));
                using (var reader = new StreamReader(path))
                {
                    var data = (CharacterData)serializer.Deserialize(reader);
                    SubModule.Log($"Character imported: {data.Name}");
                    return data;
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"Import error: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Export to Bannerlord-compatible bodyproperties string
        /// </summary>
        public string ToBodyPropertiesString()
        {
            // Build the BodyProperties XML structure that Bannerlord uses
            // This is a simplified version - full implementation would need game API
            var morphString = string.Join(",", FaceMorphs.Select(m => m.ToString("F4")));
            
            return $@"<BodyProperties version=""4"" age=""{Age:F2}"" weight=""{Weight:F2}"" build=""{Build:F2}"">
  <BodyPropertiesTemplate>
    <FaceMorphs>{morphString}</FaceMorphs>
    <SkinColor>{SkinTone:F2}</SkinColor>
    <HairColor r=""{HairColorR:F2}"" g=""{HairColorG:F2}"" b=""{HairColorB:F2}""/>
    <EyeColor r=""{EyeColorR:F2}"" g=""{EyeColorG:F2}"" b=""{EyeColorB:F2}""/>
    <Hair index=""{HairStyleIndex}""/>
    <Beard index=""{BeardStyleIndex}""/>
  </BodyPropertiesTemplate>
</BodyProperties>";
        }
        
        /// <summary>
        /// Clone this character data
        /// </summary>
        public CharacterData Clone()
        {
            var clone = new CharacterData
            {
                Name = this.Name + " (Copy)",
                CreatedAt = DateTime.Now,
                SourceImage = this.SourceImage,
                IsFaceGenerated = this.IsFaceGenerated,
                GeneratedScore = this.GeneratedScore,
                Height = this.Height,
                Build = this.Build,
                Weight = this.Weight,
                Age = this.Age,
                SkinTone = this.SkinTone,
                SkinTextureIndex = this.SkinTextureIndex,
                HairStyleIndex = this.HairStyleIndex,
                HairColorR = this.HairColorR,
                HairColorG = this.HairColorG,
                HairColorB = this.HairColorB,
                BeardStyleIndex = this.BeardStyleIndex,
                EyeColorR = this.EyeColorR,
                EyeColorG = this.EyeColorG,
                EyeColorB = this.EyeColorB,
                IsFemale = this.IsFemale,
                TattooIndex = this.TattooIndex,
                ScarIndex = this.ScarIndex,
                HeadArmorId = this.HeadArmorId,
                BodyArmorId = this.BodyArmorId
            };
            clone.FaceMorphs = (float[])this.FaceMorphs.Clone();
            return clone;
        }
        
        /// <summary>
        /// Unlock face for editing (removes generated status)
        /// </summary>
        public void UnlockFace()
        {
            IsFaceGenerated = false;
            SubModule.Log("Face unlocked for manual editing");
        }
    }
    
    /// <summary>
    /// Preset skin tones with names
    /// </summary>
    public static class SkinTonePresets
    {
        public static readonly (string Name, float Value)[] Presets = new[]
        {
            ("Very Light", 0.1f),
            ("Light", 0.25f),
            ("Medium Light", 0.4f),
            ("Medium", 0.5f),
            ("Medium Dark", 0.6f),
            ("Dark", 0.75f),
            ("Very Dark", 0.9f)
        };
    }
    
    /// <summary>
    /// Preset hair colors
    /// </summary>
    public static class HairColorPresets
    {
        public static readonly (string Name, float R, float G, float B)[] Presets = new[]
        {
            ("Black", 0.05f, 0.05f, 0.05f),
            ("Dark Brown", 0.2f, 0.12f, 0.08f),
            ("Brown", 0.35f, 0.22f, 0.15f),
            ("Light Brown", 0.5f, 0.35f, 0.25f),
            ("Auburn", 0.55f, 0.25f, 0.15f),
            ("Red", 0.65f, 0.2f, 0.1f),
            ("Ginger", 0.75f, 0.4f, 0.2f),
            ("Blonde", 0.85f, 0.75f, 0.5f),
            ("Platinum", 0.95f, 0.9f, 0.8f),
            ("White", 0.95f, 0.95f, 0.95f),
            ("Grey", 0.5f, 0.5f, 0.5f)
        };
    }
    
    /// <summary>
    /// Preset eye colors
    /// </summary>
    public static class EyeColorPresets
    {
        public static readonly (string Name, float R, float G, float B)[] Presets = new[]
        {
            ("Brown", 0.35f, 0.2f, 0.1f),
            ("Dark Brown", 0.2f, 0.1f, 0.05f),
            ("Hazel", 0.5f, 0.4f, 0.2f),
            ("Green", 0.3f, 0.5f, 0.3f),
            ("Blue", 0.3f, 0.45f, 0.7f),
            ("Light Blue", 0.5f, 0.7f, 0.9f),
            ("Grey", 0.5f, 0.55f, 0.6f),
            ("Amber", 0.7f, 0.5f, 0.2f)
        };
    }
}
