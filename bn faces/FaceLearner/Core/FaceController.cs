using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace FaceLearner.Core
{
    public class FaceController
    {
        private FaceGenerationParams _params;
        private bool _isFemale;
        private int _race = 0;
        private int _numDeformKeys = 62;
        private bool _apiTested = false;
        private bool _nativeApiWorks = false;
        
        public bool NativeApiWorks => _nativeApiWorks;
        public int NumMorphKeys => _numDeformKeys;
        public bool ApiTested => _apiTested;
        
        public FaceController()
        {
            _params = FaceGenerationParams.Create();
            _params.CurrentAge = 25f;
            _params.CurrentWeight = 0.5f;
            _params.CurrentBuild = 0.5f;
            _params.CurrentRace = 0;
            _params.CurrentGender = 0;
            
            // Initialize all morphs to 0.5 (middle position)
            for (int i = 0; i < 320; i++)
            {
                _params.KeyWeights[i] = 0.5f;
            }
        }
        
        public void TestApi(bool force = false)
        {
            if (_apiTested && !force) return;
            _apiTested = true;
            
            try
            {
                SubModule.Log($"TestApi: Starting... race={_race}, female={_isFemale}, age={_params.CurrentAge}");
                
                _numDeformKeys = MBBodyProperties.GetNumEditableDeformKeys(_race, _isFemale, (int)_params.CurrentAge);
                SubModule.Log($"TestApi: {_numDeformKeys} deform keys available");
                
                // Get DeformKeyData to see actual min/max values!
                for (int i = 0; i < Math.Min(_numDeformKeys, 5); i++)  // Log first 5 keys
                {
                    try
                    {
                        var keyData = MBBodyProperties.GetDeformKeyData(i, _race, _isFemale ? 1 : 0, (int)_params.CurrentAge);
                        SubModule.Log($"DeformKey[{i}]: Id={keyData.Id}, Min={keyData.KeyMin:F2}, Max={keyData.KeyMax:F2}, Grp={keyData.GroupId}");
                    }
                    catch (Exception ex)
                    {
                        SubModule.Log($"DeformKey[{i}]: Error - {ex.Message}");
                    }
                }
                
                // If we got deform keys, API is working
                // The actual morph changes happen through the HeroVisualCode binding
                _nativeApiWorks = _numDeformKeys > 0;
                SubModule.Log($"TestApi: {(_nativeApiWorks ? "OK" : "FAIL")}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"TestApi EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                _nativeApiWorks = false;
            }
        }
        
        // Get the real min/max ranges for all deform keys
        public (float min, float max)[] GetDeformKeyRanges()
        {
            var ranges = new (float min, float max)[_numDeformKeys];
            for (int i = 0; i < _numDeformKeys; i++)
            {
                try
                {
                    var keyData = MBBodyProperties.GetDeformKeyData(i, _race, _isFemale ? 1 : 0, (int)_params.CurrentAge);
                    ranges[i] = (keyData.KeyMin, keyData.KeyMax);
                }
                catch
                {
                    ranges[i] = (0f, 1f);  // Fallback
                }
            }
            return ranges;
        }
        
        public bool IsFemale
        {
            get => _isFemale;
            set { _isFemale = value; _params.CurrentGender = value ? 1 : 0; }
        }
        
        public int Race
        {
            get => _race;
            set { _race = value; _params.CurrentRace = value; }
        }
        
        public float Age
        {
            get => _params.CurrentAge;
            set => _params.CurrentAge = MBMath.ClampFloat(value, 18f, 100f);
        }
        
        public float Weight
        {
            get => _params.CurrentWeight;
            set => _params.CurrentWeight = MBMath.ClampFloat(value, 0f, 1f);
        }
        
        public float Build
        {
            get => _params.CurrentBuild;
            set => _params.CurrentBuild = MBMath.ClampFloat(value, 0f, 1f);
        }
        
        public float Height
        {
            get => _params.HeightMultiplier;
            set => _params.HeightMultiplier = MBMath.ClampFloat(value, 0f, 1f);
        }
        
        public float SkinColor
        {
            get => _params.CurrentSkinColorOffset;
            set => _params.CurrentSkinColorOffset = MBMath.ClampFloat(value, 0f, 1f);
        }
        
        public float EyeColor
        {
            get => _params.CurrentEyeColorOffset;
            set => _params.CurrentEyeColorOffset = MBMath.ClampFloat(value, 0f, 1f);
        }
        
        public float HairColor
        {
            get => _params.CurrentHairColorOffset;
            set => _params.CurrentHairColorOffset = MBMath.ClampFloat(value, 0f, 1f);
        }
        
        public int Hair
        {
            get => _params.CurrentHair;
            set => _params.CurrentHair = value;
        }
        
        public int Beard
        {
            get => _params.CurrentBeard;
            set => _params.CurrentBeard = value;
        }
        
        public void SetMorph(int index, float value)
        {
            if (index >= 0 && index < 320)
                _params.KeyWeights[index] = value;  // No clamp - allow any value for testing
        }
        
        public float GetMorph(int index)
        {
            return (index >= 0 && index < 320) ? _params.KeyWeights[index] : 0f;
        }
        
        public float[] GetAllMorphs()
        {
            var result = new float[_numDeformKeys];
            for (int i = 0; i < _numDeformKeys; i++)
                result[i] = _params.KeyWeights[i];
            return result;
        }
        
        public void SetAllMorphs(float[] morphs)
        {
            if (morphs == null) return;
            for (int i = 0; i < Math.Min(morphs.Length, _numDeformKeys); i++)
                _params.KeyWeights[i] = morphs[i];  // No clamp
        }
        
        /// <summary>
        /// Set all morphs to a single value
        /// </summary>
        public void SetAllMorphs(float value)
        {
            for (int i = 0; i < _numDeformKeys; i++)
                _params.KeyWeights[i] = value;
        }
        
        /// <summary>
        /// Alias for RandomizeFace
        /// </summary>
        public void Randomize(Random rng) => RandomizeFace(rng);
        
        /// <summary>
        /// Log all morph ranges to file
        /// </summary>
        public void LogMorphRanges()
        {
            SubModule.Log("=== MORPH RANGES ===");
            var ranges = GetDeformKeyRanges();
            for (int i = 0; i < ranges.Length; i++)
            {
                SubModule.Log($"Morph[{i}]: min={ranges[i].min:F2}, max={ranges[i].max:F2}");
            }
        }
        
        /// <summary>
        /// Get DNA code string
        /// </summary>
        public string ToDnaCode() => GetKeyString();
        
        /// <summary>
        /// Load from DNA code string
        /// </summary>
        public bool FromDnaCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            
            try
            {
                // Try to parse as raw key string first
                if (code.Length == 128)
                {
                    var staticProps = ParseStaticBodyPropertiesFromKey(code);
                    if (!staticProps.Equals(default(StaticBodyProperties)))
                    {
                        var dynamicProps = new DynamicBodyProperties(Age, Weight, Build);
                        var bp = new BodyProperties(dynamicProps, staticProps);
                        MBBodyProperties.GetParamsFromKey(ref _params, bp, _isFemale, false);
                        return true;
                    }
                }
                
                // Try as XML
                if (code.Contains("<BodyProperties"))
                {
                    return LoadFromBodyPropertiesXml(code);
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        public void RandomizeMorphs(Random rng, float magnitude = 1f)
        {
            for (int i = 0; i < _numDeformKeys; i++)
                _params.KeyWeights[i] = (float)rng.NextDouble() * magnitude;
        }
        
        // Log ALL fields of FaceGenerationParams to understand what we have
        public void LogAllParams()
        {
            SubModule.Log("--- FaceGenerationParams Fields ---");
            SubModule.Log($"CurrentAge: {_params.CurrentAge}");
            SubModule.Log($"CurrentWeight: {_params.CurrentWeight}");
            SubModule.Log($"CurrentBuild: {_params.CurrentBuild}");
            SubModule.Log($"CurrentRace: {_params.CurrentRace}");
            SubModule.Log($"CurrentGender: {_params.CurrentGender}");
            SubModule.Log($"CurrentHair: {_params.CurrentHair}");
            SubModule.Log($"CurrentBeard: {_params.CurrentBeard}");
            SubModule.Log($"CurrentFaceTattoo: {_params.CurrentFaceTattoo}");
            SubModule.Log($"CurrentSkinColorOffset: {_params.CurrentSkinColorOffset}");
            SubModule.Log($"CurrentHairColorOffset: {_params.CurrentHairColorOffset}");
            SubModule.Log($"CurrentEyeColorOffset: {_params.CurrentEyeColorOffset}");
            SubModule.Log($"CurrentVoice: {_params.CurrentVoice}");
            SubModule.Log($"CurrentFaceTexture: {_params.CurrentFaceTexture}");
            SubModule.Log($"CurrentMouthTexture: {_params.CurrentMouthTexture}");
            SubModule.Log($"CurrentEyebrow: {_params.CurrentEyebrow}");
            
            // Log KeyWeights array info
            SubModule.Log($"KeyWeights array length: {_params.KeyWeights?.Length ?? 0}");
            SubModule.Log($"NumEditableDeformKeys: {_numDeformKeys}");
            
            // Sample some KeyWeights
            if (_params.KeyWeights != null && _params.KeyWeights.Length > 0)
            {
                SubModule.Log($"KeyWeights[0..4]: {_params.KeyWeights[0]:F3}, {_params.KeyWeights[1]:F3}, {_params.KeyWeights[2]:F3}, {_params.KeyWeights[3]:F3}, {_params.KeyWeights[4]:F3}");
            }
            
            // Try to find if there are any other arrays or properties via reflection
            try
            {
                var type = _params.GetType();
                SubModule.Log($"--- All Fields via Reflection ---");
                foreach (var field in type.GetFields())
                {
                    if (field.Name != "KeyWeights")  // Skip KeyWeights, we logged it
                    {
                        var val = field.GetValue(_params);
                        if (val is Array arr)
                            SubModule.Log($"{field.Name}: Array[{arr.Length}]");
                        else
                            SubModule.Log($"{field.Name}: {val}");
                    }
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"Reflection error: {ex.Message}");
            }
        }
        
        public void RandomizeFace(Random rng)
        {
            // Randomize morphs with moderate variation
            RandomizeMorphs(rng, 0.8f);
            
            // Randomize body params
            Age = 20f + (float)rng.NextDouble() * 35f;
            Weight = 0.3f + (float)rng.NextDouble() * 0.4f;
            Build = 0.3f + (float)rng.NextDouble() * 0.4f;
            
            // Keep hair/beard at 0 for clean face learning
            Hair = 0;
            Beard = 0;
        }
        
        public void ResetToDefault()
        {
            for (int i = 0; i < 320; i++)
                _params.KeyWeights[i] = 0.5f;
            
            Age = 25f;
            Weight = 0.5f;
            Build = 0.5f;
            Hair = 0;
            Beard = 0;
        }
        
        public BodyProperties ToBodyProperties()
        {
            BodyProperties bp = default;
            try { MBBodyProperties.ProduceNumericKeyWithParams(_params, false, false, ref bp); }
            catch { }
            return bp;
        }
        
        public string GetBodyPropertiesKey()
        {
            return GetKeyString();
        }
        
        public string GetKeyString()
        {
            try
            {
                var sp = ToBodyProperties().StaticProperties;
                return $"{sp.KeyPart1:X16}{sp.KeyPart2:X16}{sp.KeyPart3:X16}{sp.KeyPart4:X16}{sp.KeyPart5:X16}{sp.KeyPart6:X16}{sp.KeyPart7:X16}{sp.KeyPart8:X16}";
            }
            catch { return new string('0', 128); }
        }
        
        public string ToBodyPropertiesXml()
        {
            // DEBUG: Log the params we're using
            SubModule.Log($"ToBodyPropertiesXml: Weight={_params.CurrentWeight:F3}, Build={_params.CurrentBuild:F3}, Height={_params.HeightMultiplier:F3}");
            // Note: Bannerlord BodyProperties format includes age, weight, build - height is in the key
            return $"<BodyProperties version=\"4\" age=\"{Age:F1}\" weight=\"{Weight:F2}\" build=\"{Build:F2}\" key=\"{GetKeyString()}\" />";
        }
        
        /// <summary>
        /// Load face from BodyProperties XML string
        /// </summary>
        public bool LoadFromBodyPropertiesXml(string xml)
        {
            try
            {
                // Clean up input
                xml = xml.Trim();
                
                // Extract attributes using simple parsing
                float age = ExtractFloat(xml, "age");
                float weight = ExtractFloat(xml, "weight");
                float build = ExtractFloat(xml, "build");
                string key = ExtractString(xml, "key");
                
                if (string.IsNullOrEmpty(key))
                    return false;
                
                // Parse the key string into StaticBodyProperties
                // Key is a hex string encoding 8 ulongs (KeyPart1-8)
                var staticProps = ParseStaticBodyPropertiesFromKey(key);
                if (staticProps.Equals(default(StaticBodyProperties)))
                    return false;
                
                // Create BodyProperties from components
                var dynamicProps = new DynamicBodyProperties(age, weight, build);
                var bp = new BodyProperties(dynamicProps, staticProps);
                
                // Apply to our params
                _params.CurrentAge = age;
                Weight = weight;
                Build = build;
                
                // Decode the static properties into morph values
                // GetParamsFromKey signature: (ref FaceGenerationParams, BodyProperties, bool isFemale, bool mouthHidden)
                MBBodyProperties.GetParamsFromKey(ref _params, bp, _isFemale, false);
                
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"LoadFromBodyPropertiesXml error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Parse a key string into StaticBodyProperties
        /// Key format: hex string encoding 8 ulongs
        /// </summary>
        private StaticBodyProperties ParseStaticBodyPropertiesFromKey(string key)
        {
            try
            {
                // Key should be 128 hex chars (8 ulongs * 16 hex chars each)
                if (string.IsNullOrEmpty(key) || key.Length < 128)
                    return default;
                
                // Parse 8 ulongs from hex string
                ulong[] parts = new ulong[8];
                for (int i = 0; i < 8; i++)
                {
                    string hexPart = key.Substring(i * 16, 16);
                    parts[i] = Convert.ToUInt64(hexPart, 16);
                }
                
                // Create StaticBodyProperties using constructor
                return new StaticBodyProperties(
                    parts[0], parts[1], parts[2], parts[3],
                    parts[4], parts[5], parts[6], parts[7]
                );
            }
            catch
            {
                return default;
            }
        }
        
        private float ExtractFloat(string xml, string attr)
        {
            string search = $"{attr}=\"";
            int start = xml.IndexOf(search);
            if (start < 0) return 0;
            start += search.Length;
            int end = xml.IndexOf("\"", start);
            if (end < 0) return 0;
            float.TryParse(xml.Substring(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
            return result;
        }
        
        private string ExtractString(string xml, string attr)
        {
            string search = $"{attr}=\"";
            int start = xml.IndexOf(search);
            if (start < 0) return null;
            start += search.Length;
            int end = xml.IndexOf("\"", start);
            if (end < 0) return null;
            return xml.Substring(start, end - start);
        }
        
        /// <summary>
        /// Export current face data as JSON for ML training
        /// </summary>
        public string ToJson()
        {
            var morphs = GetAllMorphs();
            var morphStr = string.Join(",", Array.ConvertAll(morphs, m => m.ToString("F4", CultureInfo.InvariantCulture)));
            
            return $@"{{
  ""age"": {Age.ToString("F1", CultureInfo.InvariantCulture)},
  ""weight"": {Weight.ToString("F3", CultureInfo.InvariantCulture)},
  ""build"": {Build.ToString("F3", CultureInfo.InvariantCulture)},
  ""isFemale"": {IsFemale.ToString().ToLower()},
  ""race"": {Race},
  ""hair"": {Hair},
  ""beard"": {Beard},
  ""morphCount"": {_numDeformKeys},
  ""morphs"": [{morphStr}],
  ""bodyPropertiesKey"": ""{GetKeyString()}""
}}";
        }
    }
}
