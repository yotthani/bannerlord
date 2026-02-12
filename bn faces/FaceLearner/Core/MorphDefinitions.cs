using System;
using System.Collections.Generic;

namespace FaceLearner.Core
{
    /// <summary>
    /// Complete mapping of Bannerlord's 62 face morph indices to human-readable names.
    /// Based on reverse engineering of the face editor and deform key data.
    /// </summary>
    public static class MorphDefinitions
    {
        /// <summary>
        /// Morph definition with index, name, and category
        /// </summary>
        public class MorphDef
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public string ShortName { get; set; }  // For UI (max 12 chars)
            public string Category { get; set; }
            
            public MorphDef(int index, string name, string shortName, string category)
            {
                Index = index;
                Name = name;
                ShortName = shortName;
                Category = category;
            }
        }
        
        /// <summary>
        /// All 62 face morphs organized by category
        /// </summary>
        public static readonly MorphDef[] AllMorphs = new MorphDef[]
        {
            // === FACE SHAPE (0-7) ===
            new MorphDef(0, "Face Width", "Width", "Face"),
            new MorphDef(1, "Face Depth", "Depth", "Face"),
            new MorphDef(2, "Face Ratio", "Ratio", "Face"),
            new MorphDef(3, "Face Weight", "Weight", "Face"),
            new MorphDef(4, "Face Center Height", "CenterH", "Face"),
            new MorphDef(5, "Face Asymmetry", "Asymmetry", "Face"),
            new MorphDef(6, "Face Softness", "Softness", "Face"),
            new MorphDef(7, "Face Tilt", "Tilt", "Face"),
            
            // === FOREHEAD (8-11) ===
            new MorphDef(8, "Forehead Width", "Width", "Forehead"),
            new MorphDef(9, "Forehead Height", "Height", "Forehead"),
            new MorphDef(10, "Forehead Depth", "Depth", "Forehead"),
            new MorphDef(11, "Forehead Position", "Position", "Forehead"),
            
            // === BROW (12-17) ===
            new MorphDef(12, "Brow Height", "Height", "Brow"),
            new MorphDef(13, "Brow Depth", "Depth", "Brow"),
            new MorphDef(14, "Brow Width", "Width", "Brow"),
            new MorphDef(15, "Brow Inner Height", "InnerH", "Brow"),
            new MorphDef(16, "Brow Outer Height", "OuterH", "Brow"),
            new MorphDef(17, "Brow Arch", "Arch", "Brow"),
            
            // === EYES (18-27) ===
            new MorphDef(18, "Eye Size", "Size", "Eyes"),
            new MorphDef(19, "Eye Width", "Width", "Eyes"),
            new MorphDef(20, "Eye Height", "Height", "Eyes"),
            new MorphDef(21, "Eye Depth", "Depth", "Eyes"),
            new MorphDef(22, "Eye Position", "Position", "Eyes"),
            new MorphDef(23, "Eye Spacing", "Spacing", "Eyes"),
            new MorphDef(24, "Eye Inner Tilt", "InnerTilt", "Eyes"),
            new MorphDef(25, "Eye Outer Tilt", "OuterTilt", "Eyes"),
            new MorphDef(26, "Eye Shape", "Shape", "Eyes"),
            new MorphDef(27, "Eyelid Height", "Eyelid", "Eyes"),
            
            // === NOSE (28-37) ===
            new MorphDef(28, "Nose Length", "Length", "Nose"),
            new MorphDef(29, "Nose Width", "Width", "Nose"),
            new MorphDef(30, "Nose Height", "Height", "Nose"),
            new MorphDef(31, "Nose Bridge Width", "BridgeW", "Nose"),
            new MorphDef(32, "Nose Bridge Height", "BridgeH", "Nose"),
            new MorphDef(33, "Nose Tip Width", "TipW", "Nose"),
            new MorphDef(34, "Nose Tip Height", "TipH", "Nose"),
            new MorphDef(35, "Nostril Width", "NostrilW", "Nose"),
            new MorphDef(36, "Nostril Height", "NostrilH", "Nose"),
            new MorphDef(37, "Nose Tilt", "Tilt", "Nose"),
            
            // === CHEEKS (38-43) ===
            new MorphDef(38, "Cheek Width", "Width", "Cheeks"),
            new MorphDef(39, "Cheek Height", "Height", "Cheeks"),
            new MorphDef(40, "Cheek Depth", "Depth", "Cheeks"),
            new MorphDef(41, "Cheekbone Width", "BoneW", "Cheeks"),
            new MorphDef(42, "Cheekbone Height", "BoneH", "Cheeks"),
            new MorphDef(43, "Cheek Fullness", "Fullness", "Cheeks"),
            
            // === MOUTH (44-51) ===
            new MorphDef(44, "Mouth Width", "Width", "Mouth"),
            new MorphDef(45, "Mouth Height", "Height", "Mouth"),
            new MorphDef(46, "Mouth Depth", "Depth", "Mouth"),
            new MorphDef(47, "Mouth Position", "Position", "Mouth"),
            new MorphDef(48, "Upper Lip Width", "UpperW", "Mouth"),
            new MorphDef(49, "Upper Lip Height", "UpperH", "Mouth"),
            new MorphDef(50, "Lower Lip Width", "LowerW", "Mouth"),
            new MorphDef(51, "Lower Lip Height", "LowerH", "Mouth"),
            
            // === JAW (52-57) ===
            new MorphDef(52, "Jaw Width", "Width", "Jaw"),
            new MorphDef(53, "Jaw Height", "Height", "Jaw"),
            new MorphDef(54, "Jaw Depth", "Depth", "Jaw"),
            new MorphDef(55, "Jaw Position", "Position", "Jaw"),
            new MorphDef(56, "Jaw Angle", "Angle", "Jaw"),
            new MorphDef(57, "Jaw Shape", "Shape", "Jaw"),
            
            // === CHIN & EARS (58-61) ===
            new MorphDef(58, "Chin Width", "Width", "Chin"),
            new MorphDef(59, "Chin Height", "Height", "Chin"),
            new MorphDef(60, "Chin Depth", "Depth", "Chin"),
            new MorphDef(61, "Chin Shape", "Shape", "Chin"),
        };
        
        /// <summary>
        /// Get all unique categories in order
        /// </summary>
        public static readonly string[] Categories = new[]
        {
            "Face", "Forehead", "Brow", "Eyes", "Nose", "Cheeks", "Mouth", "Jaw", "Chin"
        };
        
        /// <summary>
        /// Get morphs by category
        /// </summary>
        public static List<MorphDef> GetMorphsByCategory(string category)
        {
            var result = new List<MorphDef>();
            foreach (var m in AllMorphs)
            {
                if (m.Category == category)
                    result.Add(m);
            }
            return result;
        }
        
        /// <summary>
        /// Get morph by index
        /// </summary>
        public static MorphDef GetMorph(int index)
        {
            if (index >= 0 && index < AllMorphs.Length)
                return AllMorphs[index];
            return null;
        }
    }
    
    /// <summary>
    /// Body property definitions for the character editor
    /// </summary>
    public static class BodyDefinitions
    {
        public class BodyProp
        {
            public string Name { get; set; }
            public string ShortName { get; set; }
            public string Category { get; set; }
            
            public BodyProp(string name, string shortName, string category)
            {
                Name = name;
                ShortName = shortName;
                Category = category;
            }
        }
        
        public static readonly BodyProp[] AllBodyProps = new BodyProp[]
        {
            // Proportions
            new BodyProp("Height", "Height", "Proportions"),
            new BodyProp("Build", "Build", "Proportions"),
            new BodyProp("Weight", "Weight", "Proportions"),
            new BodyProp("Age", "Age", "Proportions"),
            
            // Appearance
            new BodyProp("Skin Tone", "Skin", "Appearance"),
            new BodyProp("Hair Style", "Hair", "Appearance"),
            new BodyProp("Hair Color", "HairClr", "Appearance"),
            new BodyProp("Beard Style", "Beard", "Appearance"),
            new BodyProp("Beard Color", "BeardClr", "Appearance"),
            new BodyProp("Eye Color", "EyeClr", "Appearance"),
            new BodyProp("Eyebrow", "Eyebrow", "Appearance"),
            new BodyProp("Face Texture", "FaceTex", "Appearance"),
            new BodyProp("Mouth Texture", "MouthTex", "Appearance"),
            new BodyProp("Tattoo", "Tattoo", "Appearance"),
            new BodyProp("Voice", "Voice", "Appearance"),
        };
    }
}
