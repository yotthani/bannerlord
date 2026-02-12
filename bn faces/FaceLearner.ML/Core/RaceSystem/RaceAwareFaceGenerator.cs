using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.ML.Core.RaceSystem
{
    /// <summary>
    /// Integrates race presets with face generation and learning.
    /// Applies race-specific biases and constraints to morph values.
    /// </summary>
    public class RaceAwareFaceGenerator
    {
        private readonly RacePresetManager _presetManager;
        private RacePreset _currentPreset;
        private string _currentRaceId;
        private Random _random = new Random();
        
        public RacePreset CurrentPreset => _currentPreset;
        public string CurrentRaceId => _currentRaceId;
        public bool HasPreset => _currentPreset != null;
        
        public RaceAwareFaceGenerator(RacePresetManager presetManager)
        {
            _presetManager = presetManager;
        }
        
        /// <summary>
        /// Set the active race for face generation
        /// </summary>
        public bool SetRace(string raceId)
        {
            if (string.IsNullOrEmpty(raceId))
            {
                _currentPreset = null;
                _currentRaceId = null;
                return false;
            }
            
            _currentPreset = _presetManager.GetPreset(raceId);
            _currentRaceId = raceId;
            
            if (_currentPreset != null)
            {
                SubModule.Log($"[RaceGen] Set race: {_currentPreset.DisplayName}");
                return true;
            }
            
            SubModule.Log($"[RaceGen] Unknown race: {raceId}");
            return false;
        }
        
        /// <summary>
        /// Generate initial morphs biased towards the current race.
        /// v3.0.22: Added diversityFactor parameter to control how far from preset center to explore.
        /// 1.0 = normal (0.15 variance), 2.0 = double variance for more variety, etc.
        /// </summary>
        public float[] GenerateInitialMorphs(int count = 70, float diversityFactor = 1.0f)
        {
            var morphs = new float[count];

            // Start with neutral values
            for (int i = 0; i < count; i++)
            {
                morphs[i] = 0.5f;
            }

            if (_currentPreset == null) return morphs;

            // Apply race biases with diversity-scaled variance
            foreach (var bias in _currentPreset.MorphBiases)
            {
                if (bias.Key < count)
                {
                    float center = 0.5f + bias.Value;
                    // v3.0.22: variance scales with diversityFactor
                    float variance = 0.15f * diversityFactor;
                    morphs[bias.Key] = Clamp(center + (float)(_random.NextDouble() - 0.5) * variance * 2);
                }
            }

            // Ensure morphs are within race-specific ranges
            foreach (var range in _currentPreset.MorphRanges)
            {
                if (range.Key < count)
                {
                    morphs[range.Key] = Clamp(morphs[range.Key], range.Value.min, range.Value.max);
                }
            }

            return morphs;
        }
        
        /// <summary>
        /// Apply race biases to existing morphs
        /// Adds bias values and ensures results are in valid ranges
        /// </summary>
        public float[] ApplyBiases(float[] morphs)
        {
            if (_currentPreset == null || morphs == null) return morphs;
            return _currentPreset.ApplyBiases(morphs);
        }
        
        /// <summary>
        /// Apply a named preset from the current race
        /// </summary>
        public float[] ApplyNamedPreset(float[] morphs, string presetName)
        {
            if (_currentPreset == null || morphs == null) return morphs;
            
            if (!_currentPreset.NamedPresets.TryGetValue(presetName, out var preset))
            {
                SubModule.Log($"[RaceGen] Named preset not found: {presetName}");
                return morphs;
            }
            
            var result = new float[morphs.Length];
            Array.Copy(morphs, result, morphs.Length);
            
            foreach (var morph in preset.Morphs)
            {
                if (morph.Key < result.Length)
                {
                    if (preset.BlendMode)
                    {
                        // Blend between current and preset
                        result[morph.Key] = result[morph.Key] * (1 - preset.BlendFactor) + morph.Value * preset.BlendFactor;
                    }
                    else
                    {
                        // Replace
                        result[morph.Key] = morph.Value;
                    }
                }
            }
            
            SubModule.Log($"[RaceGen] Applied preset: {presetName} (blend={preset.BlendFactor:F2})");
            return result;
        }
        
        /// <summary>
        /// Constrain morphs to race-appropriate ranges during learning/optimization
        /// </summary>
        public float[] ConstrainToRace(float[] morphs)
        {
            if (_currentPreset == null || morphs == null) return morphs;
            
            var result = new float[morphs.Length];
            Array.Copy(morphs, result, morphs.Length);
            
            foreach (var range in _currentPreset.MorphRanges)
            {
                if (range.Key < result.Length)
                {
                    result[range.Key] = Clamp(result[range.Key], range.Value.min, range.Value.max);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Check if morphs are within race-appropriate ranges
        /// Returns list of out-of-range morphs
        /// </summary>
        public List<(int index, float value, float min, float max)> CheckRaceCompliance(float[] morphs)
        {
            var violations = new List<(int, float, float, float)>();
            
            if (_currentPreset == null || morphs == null) return violations;
            
            foreach (var range in _currentPreset.MorphRanges)
            {
                if (range.Key < morphs.Length)
                {
                    float value = morphs[range.Key];
                    if (value < range.Value.min || value > range.Value.max)
                    {
                        violations.Add((range.Key, value, range.Value.min, range.Value.max));
                    }
                }
            }
            
            return violations;
        }
        
        /// <summary>
        /// Get suggested body properties for the current race
        /// </summary>
        public (float height, float build, float weight) GetBodyBiases()
        {
            if (_currentPreset == null)
                return (0f, 0f, 0f);
            
            return (_currentPreset.HeightBias, _currentPreset.BuildBias, _currentPreset.WeightBias);
        }
        
        /// <summary>
        /// Get skin tone for the current race
        /// </summary>
        public float GetSkinTone(float? detected = null)
        {
            if (_currentPreset == null)
                return detected ?? 0.5f;
            
            if (detected.HasValue)
            {
                // Clamp detected value to race range
                return Clamp(detected.Value, _currentPreset.SkinToneRange.min, _currentPreset.SkinToneRange.max);
            }
            
            return _currentPreset.DefaultSkinTone;
        }
        
        /// <summary>
        /// Bias morphs towards race center during mutation
        /// Useful for keeping generated faces looking race-appropriate
        /// </summary>
        public float[] BiasTowardsRace(float[] morphs, float strength = 0.1f)
        {
            if (_currentPreset == null || morphs == null) return morphs;
            
            var result = new float[morphs.Length];
            Array.Copy(morphs, result, morphs.Length);
            
            foreach (var bias in _currentPreset.MorphBiases)
            {
                if (bias.Key < result.Length)
                {
                    float target = 0.5f + bias.Value;
                    result[bias.Key] = result[bias.Key] * (1 - strength) + target * strength;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get mutation sigma adjusted for race constraints
        /// Morphs with tight race ranges should have smaller mutation steps
        /// </summary>
        public float GetAdjustedSigma(int morphIndex, float baseSigma)
        {
            if (_currentPreset == null) return baseSigma;
            
            if (_currentPreset.MorphRanges.TryGetValue(morphIndex, out var range))
            {
                float rangeSize = range.max - range.min;
                // Tighter range = smaller sigma
                return baseSigma * Math.Max(0.3f, rangeSize);
            }
            
            return baseSigma;
        }
        
        /// <summary>
        /// Get summary of current race settings for logging
        /// </summary>
        public string GetSummary()
        {
            if (_currentPreset == null)
                return "No race preset active";
            
            return $"{_currentPreset.DisplayName} ({_currentPreset.Category}): " +
                   $"{_currentPreset.MorphBiases.Count} biases, " +
                   $"{_currentPreset.MorphRanges.Count} ranges, " +
                   $"{_currentPreset.NamedPresets.Count} presets";
        }
        
        private static float Clamp(float v, float min = 0f, float max = 1f)
        {
            return Math.Max(min, Math.Min(max, v));
        }
    }
}
