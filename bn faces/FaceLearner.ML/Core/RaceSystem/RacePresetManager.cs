using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FaceLearner.ML.Core.RaceSystem
{
    /// <summary>
    /// Manages race presets for face generation.
    /// Loads from XML configuration files and provides preset lookup.
    /// 
    /// File locations:
    /// - Built-in: FaceLearner.ML/Data/RacePresets/
    /// - Custom:   FaceLearner.ML/Data/RacePresets/Custom/
    /// </summary>
    public class RacePresetManager
    {
        private Dictionary<string, RacePreset> _presets = new Dictionary<string, RacePreset>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<RacePreset>> _presetsByCategory = new Dictionary<string, List<RacePreset>>(StringComparer.OrdinalIgnoreCase);
        private string _presetsPath;
        private bool _isLoaded;
        
        // Common LOTR race IDs for convenience
        public const string RACE_HUMAN = "human";
        public const string RACE_HIGH_ELF = "high_elf";
        public const string RACE_WOOD_ELF = "wood_elf";
        public const string RACE_SINDAR = "sindar";
        public const string RACE_NOLDOR = "noldor";
        public const string RACE_DWARF = "dwarf";
        public const string RACE_HOBBIT = "hobbit";
        public const string RACE_ORC = "orc";
        public const string RACE_URUK = "uruk_hai";
        public const string RACE_GOBLIN = "goblin";
        public const string RACE_DUNEDAIN = "dunedain";
        public const string RACE_ROHIRRIM = "rohirrim";
        public const string RACE_HARADRIM = "haradrim";
        public const string RACE_EASTERLING = "easterling";
        
        public IReadOnlyDictionary<string, RacePreset> Presets => _presets;
        public IEnumerable<string> Categories => _presetsByCategory.Keys;
        public bool IsLoaded => _isLoaded;
        
        public RacePresetManager(string basePath)
        {
            _presetsPath = Path.Combine(basePath, "Data", "RacePresets");
        }
        
        /// <summary>
        /// Load all race presets from XML files
        /// </summary>
        public void Load()
        {
            _presets.Clear();
            _presetsByCategory.Clear();
            
            try
            {
                // Create directory if needed
                if (!Directory.Exists(_presetsPath))
                {
                    Directory.CreateDirectory(_presetsPath);
                    CreateDefaultPresets();
                }
                
                // Load built-in presets
                LoadPresetsFromDirectory(_presetsPath);
                
                // Load custom presets (can override built-in)
                string customPath = Path.Combine(_presetsPath, "Custom");
                if (Directory.Exists(customPath))
                {
                    LoadPresetsFromDirectory(customPath);
                }
                
                _isLoaded = true;
                SubModule.Log($"[RacePresets] Loaded {_presets.Count} race presets in {_presetsByCategory.Count} categories");
            }
            catch (Exception ex)
            {
                SubModule.Log($"[RacePresets] Load error: {ex.Message}");
            }
        }
        
        private void LoadPresetsFromDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*.xml"))
            {
                try
                {
                    var preset = LoadPresetFromFile(file);
                    if (preset != null)
                    {
                        _presets[preset.RaceId] = preset;
                        
                        if (!_presetsByCategory.ContainsKey(preset.Category))
                            _presetsByCategory[preset.Category] = new List<RacePreset>();
                        
                        _presetsByCategory[preset.Category].Add(preset);
                        
                        SubModule.Log($"[RacePresets] Loaded: {preset.DisplayName} ({preset.RaceId})");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"[RacePresets] Failed to load {file}: {ex.Message}");
                }
            }
        }
        
        private RacePreset LoadPresetFromFile(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var root = doc.Root;
            
            if (root?.Name != "RacePreset") return null;
            
            string raceId = root.Attribute("id")?.Value ?? Path.GetFileNameWithoutExtension(filePath);
            
            var preset = new RacePreset
            {
                RaceId = raceId,
                DisplayName = root.Element("DisplayName")?.Value ?? raceId,
                Category = root.Element("Category")?.Value ?? "Other",
                Description = root.Element("Description")?.Value ?? "",
                BasedOnRace = root.Element("BasedOnRace")?.Value ?? "human",
                HasCustomSkeleton = bool.Parse(root.Element("HasCustomSkeleton")?.Value ?? "false"),
                SkeletonName = root.Element("SkeletonName")?.Value,
            };
            
            // Parse body biases
            var body = root.Element("Body");
            if (body != null)
            {
                preset.HeightBias = float.Parse(body.Element("HeightBias")?.Value ?? "0");
                preset.BuildBias = float.Parse(body.Element("BuildBias")?.Value ?? "0");
                preset.WeightBias = float.Parse(body.Element("WeightBias")?.Value ?? "0");
            }
            
            // Parse skin/color ranges
            var colors = root.Element("Colors");
            if (colors != null)
            {
                preset.SkinToneRange = ParseRange(colors.Element("SkinTone"));
                preset.DefaultSkinTone = float.Parse(colors.Element("SkinTone")?.Attribute("default")?.Value ?? "0.5");
                preset.EyeColorRange = ParseRange(colors.Element("EyeColor"));
                preset.HairColorRange = ParseRange(colors.Element("HairColor"));
            }
            
            // Parse age range
            var age = root.Element("AgeRange");
            if (age != null)
            {
                preset.AgeRange = ParseRange(age);
            }
            
            // Parse morph biases
            var morphBiases = root.Element("MorphBiases");
            if (morphBiases != null)
            {
                foreach (var morph in morphBiases.Elements("Morph"))
                {
                    int index = int.Parse(morph.Attribute("index")?.Value ?? "0");
                    float bias = float.Parse(morph.Attribute("bias")?.Value ?? "0");
                    preset.MorphBiases[index] = bias;
                }
            }
            
            // Parse morph ranges
            var morphRanges = root.Element("MorphRanges");
            if (morphRanges != null)
            {
                foreach (var morph in morphRanges.Elements("Morph"))
                {
                    int index = int.Parse(morph.Attribute("index")?.Value ?? "0");
                    float min = float.Parse(morph.Attribute("min")?.Value ?? "0");
                    float max = float.Parse(morph.Attribute("max")?.Value ?? "1");
                    preset.MorphRanges[index] = (min, max);
                }
            }
            
            // Parse feature biases
            var featureBiases = root.Element("FeatureBiases");
            if (featureBiases != null)
            {
                foreach (var feature in featureBiases.Elements("Feature"))
                {
                    string name = feature.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(name)) continue;
                    
                    var fb = new FeatureBias
                    {
                        Aesthetic = feature.Attribute("aesthetic")?.Value ?? "Normal",
                        Strength = float.Parse(feature.Attribute("strength")?.Value ?? "0")
                    };
                    
                    foreach (var adj in feature.Elements("Adjust"))
                    {
                        string adjName = adj.Attribute("name")?.Value;
                        float adjValue = float.Parse(adj.Attribute("value")?.Value ?? "0");
                        if (!string.IsNullOrEmpty(adjName))
                        {
                            fb.Adjustments[adjName] = adjValue;
                        }
                    }
                    
                    preset.FeatureBiases[name] = fb;
                }
            }
            
            // Parse named presets
            var namedPresets = root.Element("NamedPresets");
            if (namedPresets != null)
            {
                foreach (var np in namedPresets.Elements("Preset"))
                {
                    string name = np.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(name)) continue;
                    
                    var morphPreset = new MorphPreset
                    {
                        Name = name,
                        Description = np.Element("Description")?.Value ?? "",
                        BlendMode = bool.Parse(np.Attribute("blend")?.Value ?? "true"),
                        BlendFactor = float.Parse(np.Attribute("factor")?.Value ?? "0.5")
                    };
                    
                    foreach (var morph in np.Elements("Morph"))
                    {
                        int index = int.Parse(morph.Attribute("index")?.Value ?? "0");
                        float value = float.Parse(morph.Attribute("value")?.Value ?? "0.5");
                        morphPreset.Morphs[index] = value;
                    }
                    
                    preset.NamedPresets[name] = morphPreset;
                }
            }
            
            return preset;
        }
        
        private (float min, float max) ParseRange(XElement element)
        {
            if (element == null) return (0f, 1f);
            
            float min = float.Parse(element.Attribute("min")?.Value ?? "0");
            float max = float.Parse(element.Attribute("max")?.Value ?? "1");
            return (min, max);
        }
        
        /// <summary>
        /// Get preset by race ID
        /// </summary>
        public RacePreset GetPreset(string raceId)
        {
            if (_presets.TryGetValue(raceId, out var preset))
                return preset;
            
            // Try common aliases
            raceId = raceId.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
            if (_presets.TryGetValue(raceId, out preset))
                return preset;
            
            return null;
        }
        
        /// <summary>
        /// Get all presets in a category
        /// </summary>
        public IEnumerable<RacePreset> GetPresetsByCategory(string category)
        {
            if (_presetsByCategory.TryGetValue(category, out var presets))
                return presets;
            return Enumerable.Empty<RacePreset>();
        }
        
        /// <summary>
        /// Create default LOTR race presets
        /// </summary>
        private void CreateDefaultPresets()
        {
            CreateElvenPresets();
            CreateDwarvenPresets();
            CreateOrcishPresets();
            CreateMannishPresets();
            CreateHobbitPresets();
        }
        
        private void CreateElvenPresets()
        {
            // High Elf preset - most refined and beautiful
            string highElfXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""high_elf"">
  <DisplayName>High Elf (Noldor/Sindar)</DisplayName>
  <Category>Elven</Category>
  <Description>Fair and beautiful, with fine features, high cheekbones, and an ethereal quality.</Description>
  <BasedOnRace>human</BasedOnRace>
  <HasCustomSkeleton>false</HasCustomSkeleton>
  
  <Body>
    <HeightBias>0.2</HeightBias>
    <BuildBias>-0.3</BuildBias>
    <WeightBias>-0.2</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.1"" max=""0.4"" default=""0.25"" />
    <EyeColor min=""0.0"" max=""0.7"" />
    <HairColor min=""0.0"" max=""0.8"" />
  </Colors>
  
  <AgeRange min=""20"" max=""35"" />
  
  <FeatureBiases>
    <Feature name=""Face"" aesthetic=""Refined"" strength=""0.4"">
      <Adjust name=""width"" value=""-0.2"" />
      <Adjust name=""length"" value=""0.1"" />
    </Feature>
    <Feature name=""Eyes"" aesthetic=""Large"" strength=""0.3"">
      <Adjust name=""size"" value=""0.2"" />
      <Adjust name=""spacing"" value=""0.1"" />
    </Feature>
    <Feature name=""Nose"" aesthetic=""Fine"" strength=""0.4"">
      <Adjust name=""width"" value=""-0.3"" />
      <Adjust name=""length"" value=""0.1"" />
    </Feature>
    <Feature name=""Jaw"" aesthetic=""Narrow"" strength=""0.3"">
      <Adjust name=""width"" value=""-0.25"" />
      <Adjust name=""definition"" value=""0.2"" />
    </Feature>
    <Feature name=""Brows"" aesthetic=""Arched"" strength=""0.2"">
      <Adjust name=""arch"" value=""0.3"" />
    </Feature>
  </FeatureBiases>
  
  <!-- v3.0.23: FIXED morph indices to match MorphGroups.cs canonical mapping.
       Old code had completely wrong indices (6=jaw, 15=face, 22=nose, etc.)
       which were actually cheekbone_width, brow_outer_height, eye_depth. -->
  <MorphBiases>
    <Morph index=""0"" bias=""-0.2"" />   <!-- face_width narrower -->
    <Morph index=""5"" bias=""0.2"" />    <!-- cheekbone_height higher -->
    <Morph index=""6"" bias=""-0.15"" />  <!-- cheekbone_width narrower (elven) -->
    <Morph index=""33"" bias=""-0.25"" /> <!-- nose_width narrower -->
    <Morph index=""30"" bias=""0.1"" />   <!-- nose_bridge higher -->
    <Morph index=""19"" bias=""0.15"" />  <!-- eye_size larger -->
    <Morph index=""48"" bias=""-0.15"" /> <!-- jaw_line narrower -->
  </MorphBiases>

  <MorphRanges>
    <Morph index=""0"" min=""0.25"" max=""0.6"" />   <!-- Restrict face width -->
    <Morph index=""33"" min=""0.2"" max=""0.55"" />  <!-- Restrict nose width -->
    <Morph index=""48"" min=""0.2"" max=""0.55"" />  <!-- Restrict jaw width -->
  </MorphRanges>

  <NamedPresets>
    <Preset name=""Noble"" blend=""true"" factor=""0.6"">
      <Description>Aristocratic elven features</Description>
      <Morph index=""5"" value=""0.7"" />
      <Morph index=""0"" value=""0.35"" />
      <Morph index=""33"" value=""0.3"" />
    </Preset>
    <Preset name=""Warrior"" blend=""true"" factor=""0.5"">
      <Description>Slightly stronger features for elven warriors</Description>
      <Morph index=""48"" value=""0.45"" />
      <Morph index=""52"" value=""0.55"" />
    </Preset>
  </NamedPresets>
</RacePreset>";
            
            File.WriteAllText(Path.Combine(_presetsPath, "high_elf.xml"), highElfXml);
            
            // Wood Elf - slightly more rugged but still elegant
            string woodElfXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""wood_elf"">
  <DisplayName>Wood Elf (Silvan)</DisplayName>
  <Category>Elven</Category>
  <Description>More rustic than High Elves, but still fair with a wilder beauty.</Description>
  <BasedOnRace>human</BasedOnRace>
  
  <Body>
    <HeightBias>0.1</HeightBias>
    <BuildBias>-0.15</BuildBias>
    <WeightBias>-0.1</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.2"" max=""0.5"" default=""0.35"" />
    <EyeColor min=""0.2"" max=""0.8"" />
    <HairColor min=""0.3"" max=""0.9"" />
  </Colors>
  
  <AgeRange min=""22"" max=""40"" />
  
  <FeatureBiases>
    <Feature name=""Face"" aesthetic=""Natural"" strength=""0.3"" />
    <Feature name=""Eyes"" aesthetic=""Alert"" strength=""0.25"" />
    <Feature name=""Nose"" aesthetic=""Refined"" strength=""0.3"" />
    <Feature name=""Jaw"" aesthetic=""Defined"" strength=""0.2"" />
  </FeatureBiases>
  
  <!-- v3.0.23: Fixed morph indices -->
  <MorphBiases>
    <Morph index=""48"" bias=""-0.1"" />  <!-- jaw_line narrower -->
    <Morph index=""0"" bias=""-0.15"" />  <!-- face_width narrower -->
    <Morph index=""33"" bias=""-0.15"" /> <!-- nose_width narrower -->
    <Morph index=""19"" bias=""0.1"" />   <!-- eye_size larger -->
  </MorphBiases>
</RacePreset>";

            File.WriteAllText(Path.Combine(_presetsPath, "wood_elf.xml"), woodElfXml);
        }
        
        private void CreateDwarvenPresets()
        {
            string dwarfXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""dwarf"">
  <DisplayName>Dwarf</DisplayName>
  <Category>Dwarven</Category>
  <Description>Stocky and strong, with broad features, prominent brows, and sturdy builds.</Description>
  <BasedOnRace>human</BasedOnRace>
  
  <Body>
    <HeightBias>-0.4</HeightBias>
    <BuildBias>0.5</BuildBias>
    <WeightBias>0.3</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.25"" max=""0.55"" default=""0.4"" />
    <EyeColor min=""0.3"" max=""0.8"" />
    <HairColor min=""0.4"" max=""1.0"" />
  </Colors>
  
  <AgeRange min=""30"" max=""70"" />
  
  <FeatureBiases>
    <Feature name=""Face"" aesthetic=""Broad"" strength=""0.5"">
      <Adjust name=""width"" value=""0.3"" />
    </Feature>
    <Feature name=""Nose"" aesthetic=""Strong"" strength=""0.4"">
      <Adjust name=""width"" value=""0.25"" />
      <Adjust name=""length"" value=""0.1"" />
    </Feature>
    <Feature name=""Jaw"" aesthetic=""Square"" strength=""0.5"">
      <Adjust name=""width"" value=""0.35"" />
      <Adjust name=""definition"" value=""0.3"" />
    </Feature>
    <Feature name=""Brows"" aesthetic=""Heavy"" strength=""0.4"">
      <Adjust name=""prominence"" value=""0.3"" />
    </Feature>
  </FeatureBiases>
  
  <!-- v3.0.23: Fixed morph indices -->
  <MorphBiases>
    <Morph index=""48"" bias=""0.25"" />  <!-- jaw_line wider -->
    <Morph index=""0"" bias=""0.2"" />    <!-- face_width wider -->
    <Morph index=""33"" bias=""0.2"" />   <!-- nose_width broader -->
    <Morph index=""14"" bias=""0.25"" />  <!-- eyebrow_depth heavier brow -->
    <Morph index=""52"" bias=""0.2"" />   <!-- chin_shape stronger chin -->
  </MorphBiases>

  <MorphRanges>
    <Morph index=""48"" min=""0.5"" max=""0.85"" />  <!-- Wide jaw required -->
    <Morph index=""0"" min=""0.45"" max=""0.8"" />   <!-- Wide face required -->
  </MorphRanges>

  <NamedPresets>
    <Preset name=""Smith"" blend=""true"" factor=""0.6"">
      <Description>Working dwarf with strong features</Description>
      <Morph index=""48"" value=""0.75"" />
      <Morph index=""52"" value=""0.7"" />
    </Preset>
    <Preset name=""Lord"" blend=""true"" factor=""0.5"">
      <Description>Noble dwarf with distinguished features</Description>
      <Morph index=""5"" value=""0.6"" />
      <Morph index=""14"" value=""0.65"" />
    </Preset>
  </NamedPresets>
</RacePreset>";
            
            File.WriteAllText(Path.Combine(_presetsPath, "dwarf.xml"), dwarfXml);
        }
        
        private void CreateOrcishPresets()
        {
            string orcXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""orc"">
  <DisplayName>Orc</DisplayName>
  <Category>Orcish</Category>
  <Description>Brutish and harsh, with heavy brows, flat noses, and cruel features.</Description>
  <BasedOnRace>human</BasedOnRace>
  
  <Body>
    <HeightBias>-0.1</HeightBias>
    <BuildBias>0.3</BuildBias>
    <WeightBias>0.1</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.4"" max=""0.8"" default=""0.6"" />
    <EyeColor min=""0.5"" max=""0.9"" />
    <HairColor min=""0.6"" max=""1.0"" />
  </Colors>
  
  <AgeRange min=""25"" max=""55"" />
  
  <FeatureBiases>
    <Feature name=""Face"" aesthetic=""Harsh"" strength=""0.6"">
      <Adjust name=""asymmetry"" value=""0.2"" />
    </Feature>
    <Feature name=""Nose"" aesthetic=""Flat"" strength=""0.5"">
      <Adjust name=""width"" value=""0.4"" />
      <Adjust name=""bridge"" value=""-0.3"" />
    </Feature>
    <Feature name=""Jaw"" aesthetic=""Heavy"" strength=""0.5"">
      <Adjust name=""width"" value=""0.3"" />
      <Adjust name=""protrusion"" value=""0.2"" />
    </Feature>
    <Feature name=""Brows"" aesthetic=""Prominent"" strength=""0.6"">
      <Adjust name=""ridge"" value=""0.4"" />
    </Feature>
  </FeatureBiases>
  
  <!-- v3.0.23: Fixed morph indices -->
  <MorphBiases>
    <Morph index=""48"" bias=""0.2"" />   <!-- jaw_line wider -->
    <Morph index=""33"" bias=""0.35"" />  <!-- nose_width wide flat nose -->
    <Morph index=""30"" bias=""-0.3"" /> <!-- nose_bridge flat -->
    <Morph index=""14"" bias=""0.4"" />  <!-- eyebrow_depth heavy brow ridge -->
    <Morph index=""19"" bias=""-0.15"" /> <!-- eye_size smaller eyes -->
  </MorphBiases>

  <MorphRanges>
    <Morph index=""33"" min=""0.5"" max=""0.9"" />  <!-- Wide nose required -->
    <Morph index=""14"" min=""0.55"" max=""0.95"" /> <!-- Heavy brows required -->
  </MorphRanges>
</RacePreset>";
            
            File.WriteAllText(Path.Combine(_presetsPath, "orc.xml"), orcXml);
            
            // Uruk-hai - larger, more formidable orcs
            string urukXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""uruk_hai"">
  <DisplayName>Uruk-hai</DisplayName>
  <Category>Orcish</Category>
  <Description>Large fighting orcs bred by Saruman, stronger and more fearsome than common orcs.</Description>
  <BasedOnRace>human</BasedOnRace>
  
  <Body>
    <HeightBias>0.2</HeightBias>
    <BuildBias>0.5</BuildBias>
    <WeightBias>0.3</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.5"" max=""0.85"" default=""0.7"" />
  </Colors>
  
  <AgeRange min=""25"" max=""45"" />
  
  <!-- v3.0.23: Fixed morph indices -->
  <MorphBiases>
    <Morph index=""48"" bias=""0.3"" />   <!-- jaw_line wider -->
    <Morph index=""33"" bias=""0.4"" />   <!-- nose_width wider -->
    <Morph index=""30"" bias=""-0.35"" /> <!-- nose_bridge flat -->
    <Morph index=""14"" bias=""0.5"" />   <!-- eyebrow_depth heavy brow -->
    <Morph index=""52"" bias=""0.35"" />  <!-- chin_shape stronger chin -->
  </MorphBiases>
</RacePreset>";

            File.WriteAllText(Path.Combine(_presetsPath, "uruk_hai.xml"), urukXml);
            
            // Goblin - smaller, more wretched
            string goblinXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""goblin"">
  <DisplayName>Goblin</DisplayName>
  <Category>Orcish</Category>
  <Description>Small, wretched creatures with sharp features and cunning eyes.</Description>
  <BasedOnRace>human</BasedOnRace>
  
  <Body>
    <HeightBias>-0.35</HeightBias>
    <BuildBias>-0.2</BuildBias>
    <WeightBias>-0.15</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.45"" max=""0.75"" default=""0.55"" />
  </Colors>
  
  <AgeRange min=""20"" max=""50"" />
  
  <!-- v3.0.23: Fixed morph indices -->
  <MorphBiases>
    <Morph index=""33"" bias=""0.2"" />   <!-- nose_width wide nose -->
    <Morph index=""19"" bias=""0.2"" />   <!-- eye_size larger eyes -->
    <Morph index=""48"" bias=""-0.15"" /> <!-- jaw_line narrow jaw -->
    <Morph index=""52"" bias=""-0.2"" />  <!-- chin_shape weak chin -->
    <Morph index=""14"" bias=""0.25"" />  <!-- eyebrow_depth prominent brow -->
  </MorphBiases>
</RacePreset>";

            File.WriteAllText(Path.Combine(_presetsPath, "goblin.xml"), goblinXml);
        }
        
        private void CreateMannishPresets()
        {
            string dunedainXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""dunedain"">
  <DisplayName>Dúnedain</DisplayName>
  <Category>Mannish</Category>
  <Description>Noble men of Númenorean descent, tall and fair with keen grey eyes.</Description>
  <BasedOnRace>human</BasedOnRace>
  
  <Body>
    <HeightBias>0.25</HeightBias>
    <BuildBias>0.1</BuildBias>
    <WeightBias>0.0</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.2"" max=""0.45"" default=""0.3"" />
    <EyeColor min=""0.3"" max=""0.6"" />
    <HairColor min=""0.5"" max=""0.9"" />
  </Colors>
  
  <AgeRange min=""25"" max=""70"" />
  
  <FeatureBiases>
    <Feature name=""Face"" aesthetic=""Noble"" strength=""0.3"" />
    <Feature name=""Nose"" aesthetic=""Aquiline"" strength=""0.25"" />
    <Feature name=""Jaw"" aesthetic=""Strong"" strength=""0.2"" />
  </FeatureBiases>
  
  <!-- v3.0.23: Fixed morph indices -->
  <MorphBiases>
    <Morph index=""5"" bias=""0.15"" />   <!-- cheekbone_height defined cheekbones -->
    <Morph index=""30"" bias=""0.15"" />  <!-- nose_bridge higher -->
    <Morph index=""52"" bias=""0.1"" />   <!-- chin_shape stronger chin -->
  </MorphBiases>
</RacePreset>";

            File.WriteAllText(Path.Combine(_presetsPath, "dunedain.xml"), dunedainXml);
            
            string rohirrimXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""rohirrim"">
  <DisplayName>Rohirrim</DisplayName>
  <Category>Mannish</Category>
  <Description>Horse-lords of Rohan, tall and fair-haired with open, honest faces.</Description>
  <BasedOnRace>human</BasedOnRace>
  
  <Body>
    <HeightBias>0.15</HeightBias>
    <BuildBias>0.15</BuildBias>
    <WeightBias>0.05</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.2"" max=""0.45"" default=""0.35"" />
    <HairColor min=""0.0"" max=""0.5"" />  <!-- Blonde to brown -->
  </Colors>
  
  <AgeRange min=""20"" max=""60"" />
  
  <FeatureBiases>
    <Feature name=""Face"" aesthetic=""Open"" strength=""0.2"" />
    <Feature name=""Jaw"" aesthetic=""Square"" strength=""0.25"" />
  </FeatureBiases>
  
  <!-- v3.0.23: Fixed morph indices -->
  <MorphBiases>
    <Morph index=""48"" bias=""0.1"" />   <!-- jaw_line wider -->
    <Morph index=""0"" bias=""0.05"" />   <!-- face_width slightly wider -->
  </MorphBiases>
</RacePreset>";

            File.WriteAllText(Path.Combine(_presetsPath, "rohirrim.xml"), rohirrimXml);
        }
        
        private void CreateHobbitPresets()
        {
            string hobbitXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RacePreset id=""hobbit"">
  <DisplayName>Hobbit</DisplayName>
  <Category>Hobbit</Category>
  <Description>Small folk with round, friendly faces, curly hair, and cheerful dispositions.</Description>
  <BasedOnRace>human</BasedOnRace>
  
  <Body>
    <HeightBias>-0.5</HeightBias>
    <BuildBias>0.2</BuildBias>
    <WeightBias>0.25</WeightBias>
  </Body>
  
  <Colors>
    <SkinTone min=""0.25"" max=""0.5"" default=""0.4"" />
    <EyeColor min=""0.3"" max=""0.7"" />
    <HairColor min=""0.4"" max=""0.9"" />
  </Colors>
  
  <AgeRange min=""25"" max=""65"" />
  
  <FeatureBiases>
    <Feature name=""Face"" aesthetic=""Round"" strength=""0.5"">
      <Adjust name=""roundness"" value=""0.4"" />
    </Feature>
    <Feature name=""Nose"" aesthetic=""Button"" strength=""0.3"">
      <Adjust name=""length"" value=""-0.2"" />
      <Adjust name=""upturn"" value=""0.2"" />
    </Feature>
    <Feature name=""Jaw"" aesthetic=""Soft"" strength=""0.3"">
      <Adjust name=""definition"" value=""-0.2"" />
    </Feature>
    <Feature name=""Eyes"" aesthetic=""Bright"" strength=""0.2"">
      <Adjust name=""roundness"" value=""0.15"" />
    </Feature>
  </FeatureBiases>
  
  <!-- v3.0.23: Fixed morph indices -->
  <MorphBiases>
    <Morph index=""0"" bias=""0.15"" />   <!-- face_width rounder face -->
    <Morph index=""4"" bias=""0.2"" />    <!-- cheeks fuller -->
    <Morph index=""33"" bias=""-0.1"" />  <!-- nose_width smaller nose -->
    <Morph index=""31"" bias=""0.15"" />  <!-- nose_tip_height upturned nose -->
    <Morph index=""48"" bias=""-0.1"" />  <!-- jaw_line softer jaw -->
    <Morph index=""19"" bias=""0.1"" />   <!-- eye_size rounder eyes -->
  </MorphBiases>

  <MorphRanges>
    <Morph index=""4"" min=""0.45"" max=""0.8"" />  <!-- Must have some cheek fullness -->
  </MorphRanges>

  <NamedPresets>
    <Preset name=""Baggins"" blend=""true"" factor=""0.5"">
      <Description>Respectable hobbit from a good family</Description>
      <Morph index=""4"" value=""0.55"" />
      <Morph index=""0"" value=""0.6"" />
    </Preset>
    <Preset name=""Took"" blend=""true"" factor=""0.5"">
      <Description>Adventurous hobbit with keen eyes</Description>
      <Morph index=""19"" value=""0.65"" />
      <Morph index=""4"" value=""0.5"" />
    </Preset>
  </NamedPresets>
</RacePreset>";
            
            File.WriteAllText(Path.Combine(_presetsPath, "hobbit.xml"), hobbitXml);
        }
    }
}
