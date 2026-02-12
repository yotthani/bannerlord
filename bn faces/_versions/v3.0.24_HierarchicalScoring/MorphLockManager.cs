using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Manages morph locking - once a sub-phase is optimized,
    /// its morphs are "locked" and can only vary within limits.
    /// This prevents later phases from destroying earlier work.
    /// </summary>
    public class MorphLockManager
    {
        private readonly Dictionary<int, MorphLock> _locks = new Dictionary<int, MorphLock>();
        
        /// <summary>Default allowed variation for locked morphs (±10%)</summary>
        public float DefaultAllowedVariation { get; set; } = 0.10f;
        
        /// <summary>Number of locked morphs</summary>
        public int LockedCount => _locks.Count;
        
        /// <summary>Check if a morph is locked</summary>
        public bool IsLocked(int morphIndex) => _locks.ContainsKey(morphIndex);
        
        /// <summary>Get lock info for a morph (or null if not locked)</summary>
        public MorphLock GetLock(int morphIndex)
        {
            _locks.TryGetValue(morphIndex, out var morph);
            return morph;
        }
        
        /// <summary>
        /// Lock a morph at its current value.
        /// Subsequent optimization can only vary within allowedVariation.
        /// </summary>
        public void Lock(int morphIndex, float currentValue, float allowedVariation = -1f)
        {
            if (allowedVariation < 0)
                allowedVariation = DefaultAllowedVariation;
            
            _locks[morphIndex] = new MorphLock
            {
                MorphIndex = morphIndex,
                LockedValue = currentValue,
                AllowedVariation = allowedVariation,
                LockedAt = DateTime.Now,
                MinValue = Math.Max(0f, currentValue - allowedVariation),
                MaxValue = Math.Min(1f, currentValue + allowedVariation)
            };
        }
        
        /// <summary>
        /// Lock all morphs for a sub-phase
        /// </summary>
        public void LockPhase(SubPhase phase, float[] currentMorphs, float allowedVariation = -1f)
        {
            var morphIndices = MorphGroups.GetMorphsForPhase(phase);
            foreach (var idx in morphIndices)
            {
                if (idx < currentMorphs.Length)
                {
                    Lock(idx, currentMorphs[idx], allowedVariation);
                }
            }
        }
        
        /// <summary>
        /// Unlock a morph (rarely needed, but possible for backtracking)
        /// </summary>
        public bool Unlock(int morphIndex)
        {
            return _locks.Remove(morphIndex);
        }
        
        /// <summary>
        /// Clamp a morph value to its allowed range if locked.
        /// Returns the clamped value.
        /// </summary>
        public float ClampIfLocked(int morphIndex, float value)
        {
            if (_locks.TryGetValue(morphIndex, out var morph))
            {
                return Math.Max(morph.MinValue, Math.Min(morph.MaxValue, value));
            }
            return value;
        }
        
        /// <summary>
        /// Clamp all morphs in an array to their allowed ranges.
        /// Modifies the array in place and returns it.
        /// </summary>
        public float[] ClampAllLocked(float[] morphs)
        {
            for (int i = 0; i < morphs.Length; i++)
            {
                morphs[i] = ClampIfLocked(i, morphs[i]);
            }
            return morphs;
        }
        
        /// <summary>
        /// Get the allowed range for a morph.
        /// Returns (0, 1) if not locked, or the locked range.
        /// </summary>
        public (float min, float max) GetAllowedRange(int morphIndex)
        {
            if (_locks.TryGetValue(morphIndex, out var morph))
            {
                return (morph.MinValue, morph.MaxValue);
            }
            return (0f, 1f);
        }
        
        /// <summary>
        /// Check if a proposed morph change violates any locks.
        /// Returns list of violated morph indices.
        /// </summary>
        public int[] CheckViolations(float[] proposedMorphs)
        {
            var violations = new List<int>();
            
            foreach (var kvp in _locks)
            {
                int idx = kvp.Key;
                var morph = kvp.Value;
                
                if (idx < proposedMorphs.Length)
                {
                    float proposed = proposedMorphs[idx];
                    if (proposed < morph.MinValue || proposed > morph.MaxValue)
                    {
                        violations.Add(idx);
                    }
                }
            }
            
            return violations.ToArray();
        }
        
        /// <summary>
        /// Get all locked morph indices
        /// </summary>
        public int[] GetLockedIndices() => _locks.Keys.ToArray();
        
        /// <summary>
        /// Clear all locks (for reset/restart)
        /// </summary>
        public void ClearAll() => _locks.Clear();
        
        /// <summary>
        /// Get status summary
        /// </summary>
        public string GetStatus()
        {
            if (_locks.Count == 0)
                return "No morphs locked";
            
            var byPhase = new Dictionary<MainPhase, int>();
            foreach (var idx in _locks.Keys)
            {
                foreach (SubPhase sp in Enum.GetValues(typeof(SubPhase)))
                {
                    if (MorphGroups.MorphBelongsToPhase(idx, sp))
                    {
                        var mp = MorphGroups.GetMainPhase(sp);
                        if (!byPhase.ContainsKey(mp)) byPhase[mp] = 0;
                        byPhase[mp]++;
                        break;
                    }
                }
            }
            
            var parts = byPhase.Select(kv => $"{kv.Key}:{kv.Value}");
            return $"Locked {_locks.Count} morphs ({string.Join(", ", parts)})";
        }
    }
    
    /// <summary>
    /// Information about a locked morph
    /// </summary>
    public class MorphLock
    {
        /// <summary>Index of the morph</summary>
        public int MorphIndex { get; set; }
        
        /// <summary>Value when locked</summary>
        public float LockedValue { get; set; }
        
        /// <summary>Allowed variation (±)</summary>
        public float AllowedVariation { get; set; }
        
        /// <summary>Minimum allowed value</summary>
        public float MinValue { get; set; }
        
        /// <summary>Maximum allowed value</summary>
        public float MaxValue { get; set; }
        
        /// <summary>When was it locked</summary>
        public DateTime LockedAt { get; set; }
        
        public override string ToString()
        {
            return $"Morph[{MorphIndex}] locked at {LockedValue:F3} (range: {MinValue:F3}-{MaxValue:F3})";
        }
    }
}
