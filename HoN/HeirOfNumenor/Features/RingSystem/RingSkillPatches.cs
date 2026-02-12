using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Manages custom ring-based skills that the player cannot manually modify.
    /// Skills progress based on ring usage and decay when ring is removed.
    /// Uses native SkillObject system but patches UI to prevent player modification.
    /// </summary>
    public static class RingSkillPatches
    {
        private const string FEATURE_NAME = "RingSkills";

        /// <summary>
        /// Custom skill IDs that should not be modifiable by player.
        /// These are displayed but focus cannot be added.
        /// </summary>
        public static readonly HashSet<string> LockedSkillIds = new HashSet<string>
        {
            RingAttributes.ElvenGraceId,
            RingAttributes.ElvenStarlightId,
            RingAttributes.ElvenSwiftnessId,
            RingAttributes.DwarfFortitudeId,
            RingAttributes.DwarfStonecraftId,
            RingAttributes.DwarfGoldlustId,
            RingAttributes.MortalDominionId,
            RingAttributes.MortalCommandId,
            RingAttributes.MortalPresenceId,
            RingAttributes.CorruptionFadingId,
            RingAttributes.CorruptionGreedId,
            RingAttributes.CorruptionShadowId
        };

        /// <summary>
        /// Tracks virtual focus points for ring skills per hero.
        /// This allows us to show progress without using actual game focus points.
        /// </summary>
        public static class VirtualFocusTracker
        {
            // Hero StringId -> Skill StringId -> Virtual Focus Level (0-5)
            [SaveableField(1)]
            private static Dictionary<string, Dictionary<string, int>> _virtualFocus = 
                new Dictionary<string, Dictionary<string, int>>();

            /// <summary>
            /// Get virtual focus level for a hero's skill.
            /// </summary>
            public static int GetVirtualFocus(Hero hero, string skillId)
            {
                if (hero == null || string.IsNullOrEmpty(skillId))
                    return 0;

                string heroId = hero.StringId;
                if (_virtualFocus.TryGetValue(heroId, out var skills))
                {
                    if (skills.TryGetValue(skillId, out int level))
                        return level;
                }
                return 0;
            }

            /// <summary>
            /// Set virtual focus level for a hero's skill.
            /// </summary>
            public static void SetVirtualFocus(Hero hero, string skillId, int level)
            {
                if (hero == null || string.IsNullOrEmpty(skillId))
                    return;

                level = (int)MathF.Clamp(level, 0, 5); // Max 5 focus points per skill

                string heroId = hero.StringId;
                if (!_virtualFocus.ContainsKey(heroId))
                    _virtualFocus[heroId] = new Dictionary<string, int>();

                _virtualFocus[heroId][skillId] = level;
            }

            /// <summary>
            /// Add virtual focus based on ring power progression.
            /// </summary>
            public static void UpdateFromRingPower(Hero hero, float ringPower, RingAttributes.RingRace race)
            {
                if (hero == null) return;

                // Power 0-100 maps to 0-5 focus points
                int focusLevel = (int)(ringPower / 20f);
                focusLevel = (int)MathF.Clamp(focusLevel, 0, 5);

                // Apply to race-specific skills
                foreach (string skillId in RingAttributes.GetPowerSkillsForRace(race))
                {
                    SetVirtualFocus(hero, skillId, focusLevel);
                }
            }

            /// <summary>
            /// Add virtual focus to corruption skills based on corruption level.
            /// </summary>
            public static void UpdateFromCorruption(Hero hero, float corruption)
            {
                if (hero == null) return;

                // Corruption 0-100 maps to 0-5 focus points
                int focusLevel = (int)(corruption / 20f);
                focusLevel = (int)MathF.Clamp(focusLevel, 0, 5);

                foreach (string skillId in RingAttributes.GetCorruptionSkills())
                {
                    SetVirtualFocus(hero, skillId, focusLevel);
                }
            }

            /// <summary>
            /// Decay all virtual focus for a hero (when ring is removed).
            /// </summary>
            public static void DecayFocus(Hero hero, float decayRate)
            {
                if (hero == null) return;

                string heroId = hero.StringId;
                if (!_virtualFocus.TryGetValue(heroId, out var skills))
                    return;

                var toUpdate = new List<KeyValuePair<string, int>>();
                foreach (var kvp in skills)
                {
                    if (kvp.Value > 0)
                    {
                        // Decay based on rate
                        int newLevel = MathF.Max(0, kvp.Value - (int)decayRate);
                        toUpdate.Add(new KeyValuePair<string, int>(kvp.Key, newLevel));
                    }
                }

                foreach (var kvp in toUpdate)
                {
                    skills[kvp.Key] = kvp.Value;
                }
            }

            /// <summary>
            /// Clear all virtual focus data (for save/load).
            /// </summary>
            public static void Clear()
            {
                _virtualFocus.Clear();
            }

            /// <summary>
            /// Get all data for saving.
            /// </summary>
            public static Dictionary<string, Dictionary<string, int>> GetSaveData()
            {
                return new Dictionary<string, Dictionary<string, int>>(_virtualFocus);
            }

            /// <summary>
            /// Load data from save.
            /// </summary>
            public static void LoadSaveData(Dictionary<string, Dictionary<string, int>> data)
            {
                _virtualFocus = data ?? new Dictionary<string, Dictionary<string, int>>();
            }
        }

        /// <summary>
        /// Patch to disable focus point addition for ring skills.
        /// </summary>
        [HarmonyPatch(typeof(HeroDeveloper), "CanAddFocusToSkill")]
        public static class HeroDeveloper_CanAddFocusToSkill_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(SkillObject skill, ref bool __result)
            {
                // If this is one of our locked skills, prevent focus addition
                if (skill != null && LockedSkillIds.Contains(skill.StringId))
                {
                    __result = false;
                    return false; // Skip original method
                }

                return true; // Continue to original method
            }
        }

        /// <summary>
        /// Patch SkillVM to show custom hint for locked skills.
        /// </summary>
        [HarmonyPatch(typeof(SkillVM))]
        public static class SkillVM_Patches
        {
            [HarmonyPatch("RefreshValues")]
            [HarmonyPostfix]
            public static void RefreshValues_Postfix(SkillVM __instance)
            {
                SafeExecutor.WrapPatch(FEATURE_NAME, "RefreshValues", () =>
                {
                    if (__instance.Skill == null)
                        return;

                    if (LockedSkillIds.Contains(__instance.Skill.StringId))
                    {
                        // Mark as unable to add focus
                        __instance.CanAddFocus = false;

                        // Update hint to explain why
                        try
                        {
                            var hint = new TaleWorlds.Core.ViewModelCollection.Information.HintViewModel(
                                new TextObject("{=ring_skill_locked}This skill is controlled by Ring Power and cannot be manually improved."));
                            __instance.AddFocusHint = hint;
                        }
                        catch { }
                    }
                });
            }
        }

        /// <summary>
        /// Check if a skill ID is a locked ring skill.
        /// </summary>
        public static bool IsLockedRingSkill(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && LockedSkillIds.Contains(skillId);
        }

        /// <summary>
        /// Check if a SkillObject is a locked ring skill.
        /// </summary>
        public static bool IsLockedRingSkill(SkillObject skill)
        {
            return skill != null && LockedSkillIds.Contains(skill.StringId);
        }

        /// <summary>
        /// Get bonus for a ring skill based on virtual focus level.
        /// </summary>
        public static float GetSkillBonus(Hero hero, string skillId)
        {
            if (hero == null || string.IsNullOrEmpty(skillId))
                return 0f;

            int focusLevel = VirtualFocusTracker.GetVirtualFocus(hero, skillId);
            
            // Each focus level gives a bonus
            // This is used in our custom models (morale, speed, etc.)
            return focusLevel * 5f; // 0-25 bonus
        }
    }
}
