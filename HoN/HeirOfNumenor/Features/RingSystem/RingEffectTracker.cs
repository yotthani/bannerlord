using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Tracks ring wearing time and manages effect buildup/decay.
    /// </summary>
    public class HoNRingEffectTracker
    {
        // Current ring being worn (null if none)
        [SaveableField(1)]
        private string _currentRingId;

        // Days spent wearing the current ring
        [SaveableField(2)]
        private float _currentRingDaysWorn;

        // Current effect levels (0-100 per skill)
        [SaveableField(3)]
        private Dictionary<string, float> _powerLevels;

        [SaveableField(4)]
        private Dictionary<string, float> _corruptionLevels;

        // Race of the effects currently active (for decay tracking) - stored as int to avoid save system enum issues
        [SaveableField(5)]
        private int _activeEffectRaceInt;
        
        private RingAttributes.RingRace _activeEffectRace 
        { 
            get => (RingAttributes.RingRace)_activeEffectRaceInt; 
            set => _activeEffectRaceInt = (int)value; 
        }

        // Is currently decaying (no ring equipped or switched rings)
        [SaveableField(6)]
        private bool _isDecaying;

        // Base decay rates (days to fully decay from 100)
        private const float BaseBonusDecayDays = 7f;    // 1 week to lose all bonuses
        private const float BaseCurseDecayDays = 21f;   // 3 weeks to lose all curses

        // Base buildup rates (days to reach tier thresholds)
        private const float BaseTier1Days = 0f;         // Immediate
        private const float BaseTier2Days = 7f;         // 1 week
        private const float BaseTier3Days = 30f;        // 1 month

        public HoNRingEffectTracker()
        {
            _powerLevels = new Dictionary<string, float>();
            _corruptionLevels = new Dictionary<string, float>();
            _activeEffectRace = RingAttributes.RingRace.None;
            _isDecaying = false;
        }

        #region Properties

        public string CurrentRingId => _currentRingId;
        public float DaysWorn => _currentRingDaysWorn;
        public bool IsDecaying => _isDecaying;
        public RingAttributes.RingRace ActiveRace => _activeEffectRace;

        public bool HasActiveEffects => _powerLevels.Count > 0 || _corruptionLevels.Count > 0;

        public bool CanEquipNewRing => !HasActiveEffects || !_isDecaying;

        #endregion

        #region Ring Equip/Unequip

        /// <summary>
        /// Attempts to equip a ring. Returns false if still decaying from previous ring.
        /// </summary>
        public bool TryEquipRing(string ringId)
        {
            var newRace = RingAttributes.GetRingRace(ringId);
            
            // If we have active effects from a different race, can't equip yet
            if (HasActiveEffects && _activeEffectRace != newRace && _activeEffectRace != RingAttributes.RingRace.None)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "The ring's power cannot take hold while shadows of another ring linger...",
                    Colors.Red));
                return false;
            }

            _currentRingId = ringId;
            _currentRingDaysWorn = 0f;
            _activeEffectRace = newRace;
            _isDecaying = false;

            // Initialize power and corruption skill tracking for this race
            InitializeSkillsForRace(newRace);

            InformationManager.DisplayMessage(new InformationMessage(
                $"You don the ring. Its power begins to flow through you...",
                Colors.Cyan));

            return true;
        }

        /// <summary>
        /// Unequips the current ring, starting decay process.
        /// </summary>
        public void UnequipRing()
        {
            if (string.IsNullOrEmpty(_currentRingId))
                return;

            _currentRingId = null;
            _currentRingDaysWorn = 0f;
            _isDecaying = true;

            InformationManager.DisplayMessage(new InformationMessage(
                "You remove the ring. Its influence begins to fade... but the shadows linger.",
                Colors.Yellow));
        }

        private void InitializeSkillsForRace(RingAttributes.RingRace race)
        {
            // Initialize power skills for this race (if not already tracked)
            foreach (var skillId in RingAttributes.GetPowerSkillsForRace(race))
            {
                if (!_powerLevels.ContainsKey(skillId))
                    _powerLevels[skillId] = 0f;
            }

            // Initialize corruption skills
            foreach (var skillId in RingAttributes.GetCorruptionSkills())
            {
                if (!_corruptionLevels.ContainsKey(skillId))
                    _corruptionLevels[skillId] = 0f;
            }
        }

        #endregion

        #region Daily Update

        /// <summary>
        /// Called each day to update effect levels.
        /// </summary>
        public void OnDayPassed()
        {
            if (_isDecaying)
            {
                ProcessDecay();
            }
            else if (!string.IsNullOrEmpty(_currentRingId))
            {
                ProcessBuildup();
            }
        }

        private void ProcessBuildup()
        {
            float oldCorruption = GetTotalCorruption();
            
            _currentRingDaysWorn += 1f;
            var race = _activeEffectRace;

            float bonusMultiplier = RingAttributes.ProgressionSpeed.GetBonusBuildup(race);
            float curseMultiplier = RingAttributes.ProgressionSpeed.GetCurseBuildup(race);

            // Special case: One Ring has accelerating corruption
            if (race == RingAttributes.RingRace.OneRing)
            {
                curseMultiplier *= (1f + (_currentRingDaysWorn * 0.1f)); // Gets worse over time
            }

            // Calculate effective days for tier unlocks
            float effectiveBonusDays = _currentRingDaysWorn * bonusMultiplier;
            float effectiveCurseDays = _currentRingDaysWorn * curseMultiplier;

            // Update power skills
            var powerSkills = RingAttributes.GetPowerSkillsForRace(race);
            UpdateSkillLevels(_powerLevels, powerSkills, effectiveBonusDays, RingAttributes.BonusTiers, true);

            // Update corruption skills
            var corruptionSkills = RingAttributes.GetCorruptionSkills();
            UpdateSkillLevels(_corruptionLevels, corruptionSkills, effectiveCurseDays, RingAttributes.CurseTiers, false);
            
            // Check corruption milestones and update trait effects
            float newCorruption = GetTotalCorruption();
            if (Hero.MainHero != null)
            {
                CorruptionTraitIntegration.CheckCorruptionMilestones(Hero.MainHero, oldCorruption, newCorruption);
            }
        }

        private void UpdateSkillLevels(Dictionary<string, float> levels, string[] skills, 
            float effectiveDays, int[] tiers, bool isPower)
        {
            for (int i = 0; i < skills.Length && i < 3; i++)
            {
                string skillId = skills[i];
                float unlockDay = RingAttributes.UnlockDays[i];
                int targetLevel = tiers[i];

                if (effectiveDays >= unlockDay)
                {
                    // Calculate progress toward this tier's max
                    float daysPastUnlock = effectiveDays - unlockDay;
                    float daysToMax = (i < 2 ? RingAttributes.UnlockDays[i + 1] : 60f) - unlockDay;
                    float progress = Math.Min(1f, daysPastUnlock / daysToMax);

                    float newLevel = targetLevel * progress;

                    // Only increase, never decrease during buildup
                    if (!levels.ContainsKey(skillId) || newLevel > levels[skillId])
                    {
                        float oldLevel = levels.ContainsKey(skillId) ? levels[skillId] : 0f;
                        levels[skillId] = newLevel;

                        // Notify on significant changes
                        if ((int)newLevel > (int)oldLevel && (int)newLevel > 0)
                        {
                            NotifySkillChange(skillId, (int)newLevel, isPower, true);
                        }
                    }
                }
            }
        }

        private void ProcessDecay()
        {
            float bonusDecayRate = RingAttributes.ProgressionSpeed.GetBonusDecay(_activeEffectRace);
            float curseDecayRate = RingAttributes.ProgressionSpeed.GetCurseDecay(_activeEffectRace);

            // Decay power skills (fast)
            float bonusDecayPerDay = 100f / BaseBonusDecayDays * bonusDecayRate;
            DecaySkills(_powerLevels, bonusDecayPerDay, true);

            // Decay corruption skills (slow)
            float curseDecayPerDay = 100f / BaseCurseDecayDays * curseDecayRate;
            DecaySkills(_corruptionLevels, curseDecayPerDay, false);

            // Check if fully decayed
            if (!HasActiveEffects)
            {
                _isDecaying = false;
                _activeEffectRace = RingAttributes.RingRace.None;

                InformationManager.DisplayMessage(new InformationMessage(
                    "The ring's influence has finally faded completely.",
                    Colors.Green));
            }
        }

        private void DecaySkills(Dictionary<string, float> levels, float decayPerDay, bool isPower)
        {
            var keysToRemove = new List<string>();

            foreach (var key in levels.Keys.ToArray())
            {
                float oldLevel = levels[key];
                float newLevel = Math.Max(0f, oldLevel - decayPerDay);
                levels[key] = newLevel;

                // Notify on significant changes
                if ((int)newLevel < (int)oldLevel)
                {
                    if (newLevel <= 0)
                    {
                        keysToRemove.Add(key);
                    }
                    else
                    {
                        NotifySkillChange(key, (int)newLevel, isPower, false);
                    }
                }
            }

            // Remove fully decayed skills
            foreach (var key in keysToRemove)
            {
                levels.Remove(key);
                NotifySkillRemoved(key, isPower);
            }
        }

        private void NotifySkillChange(string skillId, int newLevel, bool isPower, bool isIncrease)
        {
            var skillName = RingAttributes.SkillInfo.GetSkillName(skillId);
            string direction = isIncrease ? "grows" : "fades";
            Color color = isPower 
                ? (isIncrease ? Colors.Cyan : Colors.Yellow)
                : (isIncrease ? Colors.Red : Colors.Green);

            InformationManager.DisplayMessage(new InformationMessage(
                $"{skillName} {direction} to {newLevel}",
                color));
        }

        private void NotifySkillRemoved(string skillId, bool isPower)
        {
            var skillName = RingAttributes.SkillInfo.GetSkillName(skillId);
            Color color = isPower ? Colors.Yellow : Colors.Green;
            string message = isPower 
                ? $"{skillName} has faded away." 
                : $"The curse of {skillName} has lifted.";

            InformationManager.DisplayMessage(new InformationMessage(message, color));
        }

        #endregion

        #region Effect Queries

        /// <summary>
        /// Gets the current level of a power skill.
        /// </summary>
        public int GetPowerLevel(string skillId)
        {
            return _powerLevels.TryGetValue(skillId, out float level) ? (int)level : 0;
        }

        /// <summary>
        /// Gets the current level of a corruption skill.
        /// </summary>
        public int GetCorruptionLevel(string skillId)
        {
            return _corruptionLevels.TryGetValue(skillId, out float level) ? (int)level : 0;
        }

        /// <summary>
        /// Gets total corruption level for a hero (0-100 scale).
        /// </summary>
        public float GetCorruptionLevel(Hero hero)
        {
            if (hero != Hero.MainHero) return 0f;  // Currently only track main hero
            
            int total = GetTotalCorruption();
            // Normalize to 0-100 scale (max 300 from 3 corruption skills at 100 each)
            return Math.Min(100f, total / 3f);
        }

        /// <summary>
        /// Gets ring power level for a hero (0-100 scale).
        /// </summary>
        public float GetRingPower(Hero hero)
        {
            if (hero != Hero.MainHero) return 0f;
            
            int total = GetTotalRingPower();
            // Normalize to 0-100 scale (max 300 from 3 power skills at 100 each)
            return Math.Min(100f, total / 3f);
        }

        /// <summary>
        /// Gets total Ring Power (sum of all power skills).
        /// </summary>
        public int GetTotalRingPower()
        {
            int total = 0;
            foreach (var level in _powerLevels.Values)
                total += (int)level;
            return total;
        }

        /// <summary>
        /// Gets total Corruption (sum of all corruption skills).
        /// </summary>
        public int GetTotalCorruption()
        {
            int total = 0;
            foreach (var level in _corruptionLevels.Values)
                total += (int)level;
            return total;
        }

        /// <summary>
        /// Gets all active power effects.
        /// </summary>
        public Dictionary<string, int> GetActivePowerEffects()
        {
            var result = new Dictionary<string, int>();
            foreach (var kvp in _powerLevels)
            {
                if (kvp.Value > 0)
                    result[kvp.Key] = (int)kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Gets all active corruption effects.
        /// </summary>
        public Dictionary<string, int> GetActiveCorruptionEffects()
        {
            var result = new Dictionary<string, int>();
            foreach (var kvp in _corruptionLevels)
            {
                if (kvp.Value > 0)
                    result[kvp.Key] = (int)kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Gets unlock tier (1, 2, or 3) for power skills based on days worn.
        /// </summary>
        public int GetCurrentPowerTier()
        {
            if (string.IsNullOrEmpty(_currentRingId)) return 0;

            float effectiveDays = _currentRingDaysWorn * 
                RingAttributes.ProgressionSpeed.GetBonusBuildup(_activeEffectRace);

            if (effectiveDays >= RingAttributes.UnlockDays[2]) return 3;
            if (effectiveDays >= RingAttributes.UnlockDays[1]) return 2;
            if (effectiveDays >= RingAttributes.UnlockDays[0]) return 1;
            return 0;
        }

        /// <summary>
        /// Gets unlock tier for corruption skills.
        /// </summary>
        public int GetCurrentCorruptionTier()
        {
            if (string.IsNullOrEmpty(_currentRingId)) return 0;

            float effectiveDays = _currentRingDaysWorn * 
                RingAttributes.ProgressionSpeed.GetCurseBuildup(_activeEffectRace);

            if (effectiveDays >= RingAttributes.UnlockDays[2]) return 3;
            if (effectiveDays >= RingAttributes.UnlockDays[1]) return 2;
            if (effectiveDays >= RingAttributes.UnlockDays[0]) return 1;
            return 0;
        }

        #endregion

        #region Debug

        public string GetDebugStatus()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Ring: {_currentRingId ?? "None"}");
            sb.AppendLine($"Days Worn: {_currentRingDaysWorn:F1}");
            sb.AppendLine($"Race: {_activeEffectRace}");
            sb.AppendLine($"Decaying: {_isDecaying}");
            sb.AppendLine($"Power Tier: {GetCurrentPowerTier()}");
            sb.AppendLine($"Corruption Tier: {GetCurrentCorruptionTier()}");
            sb.AppendLine("--- Power Levels ---");
            foreach (var kvp in _powerLevels)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value:F1}");
            sb.AppendLine("--- Corruption Levels ---");
            foreach (var kvp in _corruptionLevels)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value:F1}");
            return sb.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Extension to make LINQ work with Dictionary
    /// </summary>
    internal static class DictionaryExtensions
    {
        public static TKey[] ToArray<TKey, TValue>(this Dictionary<TKey, TValue>.KeyCollection keys)
        {
            var result = new TKey[keys.Count];
            int i = 0;
            foreach (var key in keys)
                result[i++] = key;
            return result;
        }
    }
}
