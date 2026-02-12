using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Integrates ring corruption with the native trait system.
    /// High corruption affects hero traits and behavior.
    /// </summary>
    public static class CorruptionTraitIntegration
    {
        private const string FEATURE_NAME = "CorruptionTraits";

        // Track corruption-modified trait values (heroStringId -> traitStringId -> modifier)
        private static readonly Dictionary<string, Dictionary<string, int>> _traitModifiers = 
            new Dictionary<string, Dictionary<string, int>>();

        // Corruption thresholds
        private const float MinorCorruptionThreshold = 25f;
        private const float ModerateCorruptionThreshold = 50f;
        private const float SevereCorruptionThreshold = 75f;
        private const float CriticalCorruptionThreshold = 90f;

        /// <summary>
        /// Update trait modifiers based on current corruption level.
        /// Should be called when corruption changes significantly.
        /// </summary>
        public static void UpdateCorruptionTraitEffects(Hero hero, float corruptionLevel)
        {
            if (hero == null) return;

            SafeExecutor.Execute(FEATURE_NAME, "UpdateTraits", () =>
            {
                string heroId = hero.StringId;
                
                if (!_traitModifiers.ContainsKey(heroId))
                    _traitModifiers[heroId] = new Dictionary<string, int>();

                var modifiers = _traitModifiers[heroId];
                modifiers.Clear();

                // Calculate trait effects based on corruption level
                if (corruptionLevel >= MinorCorruptionThreshold)
                {
                    // Minor corruption: Small personality shifts
                    modifiers[DefaultTraits.Mercy.StringId] = -1;  // Less merciful
                    modifiers[DefaultTraits.Generosity.StringId] = -1;  // Less generous
                }

                if (corruptionLevel >= ModerateCorruptionThreshold)
                {
                    // Moderate corruption: Noticeable changes
                    modifiers[DefaultTraits.Mercy.StringId] = -2;
                    modifiers[DefaultTraits.Generosity.StringId] = -2;
                    modifiers[DefaultTraits.Honor.StringId] = -1;  // Less honorable
                    modifiers[DefaultTraits.Calculating.StringId] = 1;  // More calculating
                }

                if (corruptionLevel >= SevereCorruptionThreshold)
                {
                    // Severe corruption: Major personality warping
                    modifiers[DefaultTraits.Mercy.StringId] = -3;
                    modifiers[DefaultTraits.Generosity.StringId] = -3;
                    modifiers[DefaultTraits.Honor.StringId] = -2;
                    modifiers[DefaultTraits.Valor.StringId] = -1;  // Fear creeps in
                    modifiers[DefaultTraits.Calculating.StringId] = 2;
                }

                if (corruptionLevel >= CriticalCorruptionThreshold)
                {
                    // Critical corruption: Near complete personality change
                    modifiers[DefaultTraits.Mercy.StringId] = -4;
                    modifiers[DefaultTraits.Generosity.StringId] = -4;
                    modifiers[DefaultTraits.Honor.StringId] = -3;
                    modifiers[DefaultTraits.Valor.StringId] = -2;
                    modifiers[DefaultTraits.Calculating.StringId] = 3;
                }
            });
        }

        /// <summary>
        /// Get the corruption modifier for a specific trait.
        /// </summary>
        public static int GetTraitModifier(Hero hero, TraitObject trait)
        {
            if (hero == null || trait == null) return 0;

            try
            {
                string heroId = hero.StringId;
                if (_traitModifiers.TryGetValue(heroId, out var modifiers))
                {
                    if (modifiers.TryGetValue(trait.StringId, out int modifier))
                    {
                        return modifier;
                    }
                }
            }
            catch { }

            return 0;
        }

        /// <summary>
        /// Get the effective trait value including corruption effects.
        /// </summary>
        public static int GetEffectiveTraitLevel(Hero hero, TraitObject trait)
        {
            if (hero == null || trait == null) return 0;

            int baseLevel = hero.GetTraitLevel(trait);
            int modifier = GetTraitModifier(hero, trait);
            
            // Clamp to valid trait range (-2 to +2 typically)
            return Math.Max(-2, Math.Min(2, baseLevel + modifier));
        }

        /// <summary>
        /// Clear corruption effects from a hero (when corruption is cured).
        /// </summary>
        public static void ClearCorruptionEffects(Hero hero)
        {
            if (hero == null) return;

            string heroId = hero.StringId;
            if (_traitModifiers.ContainsKey(heroId))
            {
                _traitModifiers[heroId].Clear();
            }
        }

        /// <summary>
        /// Get a description of current corruption trait effects.
        /// </summary>
        public static string GetCorruptionEffectsDescription(Hero hero)
        {
            if (hero == null) return "";

            string heroId = hero.StringId;
            if (!_traitModifiers.TryGetValue(heroId, out var modifiers) || modifiers.Count == 0)
                return "No corruption effects on personality.";

            var effects = new List<string>();

            foreach (var kvp in modifiers)
            {
                string traitName = GetTraitName(kvp.Key);
                int value = kvp.Value;
                
                if (value < 0)
                    effects.Add($"{traitName} {value}");
                else if (value > 0)
                    effects.Add($"{traitName} +{value}");
            }

            return string.Join(", ", effects);
        }

        private static string GetTraitName(string traitStringId)
        {
            if (traitStringId == DefaultTraits.Mercy.StringId) return "Mercy";
            if (traitStringId == DefaultTraits.Generosity.StringId) return "Generosity";
            if (traitStringId == DefaultTraits.Honor.StringId) return "Honor";
            if (traitStringId == DefaultTraits.Valor.StringId) return "Valor";
            if (traitStringId == DefaultTraits.Calculating.StringId) return "Calculating";
            return "Unknown";
        }

        /// <summary>
        /// Check if corruption has reached a significant threshold and notify.
        /// </summary>
        public static void CheckCorruptionMilestones(Hero hero, float oldCorruption, float newCorruption)
        {
            if (hero != Hero.MainHero) return;

            SafeExecutor.Execute(FEATURE_NAME, "Milestones", () =>
            {
                // Check threshold crossings
                if (oldCorruption < MinorCorruptionThreshold && newCorruption >= MinorCorruptionThreshold)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The ring's shadow begins to touch your heart...",
                        Colors.Yellow));
                }
                else if (oldCorruption < ModerateCorruptionThreshold && newCorruption >= ModerateCorruptionThreshold)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The ring's corruption spreads. Your thoughts darken.",
                        new Color(1f, 0.5f, 0f))); // Orange
                }
                else if (oldCorruption < SevereCorruptionThreshold && newCorruption >= SevereCorruptionThreshold)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The ring consumes you! Your allies see the change in your eyes.",
                        Colors.Red));
                }
                else if (oldCorruption < CriticalCorruptionThreshold && newCorruption >= CriticalCorruptionThreshold)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "CRITICAL: You are nearly lost to the ring's power! Remove it immediately!",
                        Colors.Magenta));
                    
                    // Show warning inquiry
                    InformationManager.ShowInquiry(new InquiryData(
                        "The Ring Claims You",
                        "Your corruption has reached critical levels. The ring's power threatens to consume " +
                        "your very soul. Your personality has fundamentally changed - mercy, honor, and valor " +
                        "are fading from your heart.\n\n" +
                        "Remove the ring immediately, or risk losing yourself forever.",
                        true, false,
                        "I understand",
                        null,
                        null, null));
                }

                // Update trait modifiers
                UpdateCorruptionTraitEffects(hero, newCorruption);
            });
        }
    }

    /// <summary>
    /// Patches to integrate corruption with trait-related game systems.
    /// </summary>
    [HarmonyPatch]
    public static class CorruptionTraitPatches
    {
        /// <summary>
        /// Patch GetTraitLevel to include corruption modifiers.
        /// </summary>
        [HarmonyPatch(typeof(Hero), "GetTraitLevel")]
        [HarmonyPostfix]
        public static void GetTraitLevel_Postfix(Hero __instance, TraitObject trait, ref int __result)
        {
            try
            {
                // Early exit if campaign not fully initialized
                if (Campaign.Current == null) return;
                if (Clan.PlayerClan == null) return;
                
                // Only modify for main hero or companions
                if (__instance?.Clan != Clan.PlayerClan) return;

                int modifier = CorruptionTraitIntegration.GetTraitModifier(__instance, trait);
                if (modifier != 0)
                {
                    __result = Math.Max(-2, Math.Min(2, __result + modifier));
                }
            }
            catch { }
        }
    }
}
