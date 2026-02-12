using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace HeirOfNumenor.Features.SmithingExtended
{
    /// <summary>
    /// Generates unique items with random bonuses based on smith skill.
    /// Uses native ItemModifier system for better integration.
    /// </summary>
    public static class UniqueItemGenerator
    {
        private const string FEATURE_NAME = "SmithingExtended";
        private static Random _random = new Random();

        /// <summary>
        /// Checks if a unique item should be generated based on skill.
        /// </summary>
        public static bool ShouldGenerateUnique(int smithingSkill)
        {
            try
            {
                var settings = ModSettings.Get();
                if (!settings.EnableUniqueItems) return false;
                if (smithingSkill < settings.MinSkillForUnique) return false;

                // Calculate chance
                float baseChance = settings.UniqueItemChance;
                int skillAboveMin = smithingSkill - settings.MinSkillForUnique;
                float skillBonus = (skillAboveMin / 50f) * 0.01f; // 1% bonus per 50 skill above minimum
                float totalChance = baseChance + skillBonus;

                // Cap at 20%
                totalChance = Math.Min(totalChance, 0.20f);

                return _random.NextDouble() < totalChance;
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "ShouldGenerateUnique check failed", ex);
                return false;
            }
        }

        /// <summary>
        /// Creates an EquipmentElement with appropriate native ItemModifier based on quality.
        /// This integrates properly with the native item system.
        /// </summary>
        public static EquipmentElement CreateUniqueEquipment(ItemObject baseItem, Hero crafter)
        {
            try
            {
                if (baseItem == null) return EquipmentElement.Invalid;

                int smithingSkill = crafter?.GetSkillValue(DefaultSkills.Crafting) ?? 200;
                ItemQuality quality = CalculateItemQuality(smithingSkill);

                // Try to get modifier from item's native modifier group
                ItemModifier modifier = GetModifierForQuality(baseItem, quality);

                if (modifier != null)
                {
                    var element = new EquipmentElement(baseItem, modifier);
                    
                    // Notify player
                    string qualityName = GetQualityDisplayName(quality);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"★ Crafted {qualityName} {baseItem.Name}!",
                        GetQualityColor(quality)));
                    
                    return element;
                }

                // Fallback: return base item without modifier
                return new EquipmentElement(baseItem);
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, $"CreateUniqueEquipment failed: {ex.Message}");
                return new EquipmentElement(baseItem);
            }
        }

        /// <summary>
        /// Gets native ItemModifier for a quality level.
        /// </summary>
        private static ItemModifier GetModifierForQuality(ItemObject item, ItemQuality quality)
        {
            try
            {
                // First, try item's own modifier group
                var modifierGroup = item?.ItemComponent?.ItemModifierGroup;
                if (modifierGroup != null)
                {
                    var modifiers = modifierGroup.GetModifiersBasedOnQuality(quality);
                    if (modifiers != null && modifiers.Count > 0)
                    {
                        return modifiers[_random.Next(modifiers.Count)];
                    }
                }

                // Fallback: try to find a generic modifier by quality name
                string qualityModifierId = quality switch
                {
                    ItemQuality.Legendary => "legendary",
                    ItemQuality.Masterwork => "masterwork",
                    ItemQuality.Fine => "fine",
                    _ => null
                };

                if (!string.IsNullOrEmpty(qualityModifierId))
                {
                    return MBObjectManager.Instance?.GetObject<ItemModifier>(qualityModifierId);
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Calculate item quality based on smithing skill.
        /// </summary>
        private static ItemQuality CalculateItemQuality(int smithingSkill)
        {
            float roll = (float)_random.NextDouble();
            float skillFactor = smithingSkill / 300f; // 300 = max smithing

            // Higher skill = higher chance of better quality
            if (roll < 0.01f * skillFactor) return ItemQuality.Legendary;
            if (roll < 0.05f * skillFactor) return ItemQuality.Masterwork;
            if (roll < 0.15f * skillFactor) return ItemQuality.Fine;
            return ItemQuality.Common;
        }

        private static string GetQualityDisplayName(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Legendary => "Legendary",
                ItemQuality.Masterwork => "Masterwork",
                ItemQuality.Fine => "Fine",
                ItemQuality.Common => "Quality",
                _ => ""
            };
        }

        private static Color GetQualityColor(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Legendary => new Color(1f, 0.84f, 0f), // Gold
                ItemQuality.Masterwork => new Color(0.6f, 0.2f, 0.8f), // Purple
                ItemQuality.Fine => new Color(0.2f, 0.6f, 1f), // Blue
                _ => Colors.Green
            };
        }

        /// <summary>
        /// Generates a unique item from a base item (legacy method for custom tracking).
        /// </summary>
        public static HoNUniqueItemData GenerateUniqueItem(ItemObject baseItem, Hero crafter)
        {
            try
            {
                // Determine rarity
                HoNUniqueRarity rarity = DetermineRarity(crafter?.GetSkillValue(DefaultSkills.Crafting) ?? 200);

                // Create unique data
                var uniqueData = new HoNUniqueItemData
                {
                    ItemId = baseItem?.StringId ?? "unknown",
                    UniqueName = UniqueNameGenerator.GenerateUniqueName(baseItem, rarity),
                    Rarity = rarity,
                    CrafterName = crafter?.Name?.ToString() ?? "Unknown Smith",
                    CampaignTimeCrafted = GetCurrentCampaignDay(),
                    LoreText = UniqueNameGenerator.GenerateLore(rarity)
                };

                // Generate bonuses based on rarity and item type
                GenerateBonuses(uniqueData, baseItem, rarity);

                return uniqueData;
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "GenerateUniqueItem failed", ex);
                // Return a basic unique item on error
                return new HoNUniqueItemData
                {
                    ItemId = baseItem?.StringId ?? "unknown",
                    UniqueName = "Mysterious Item",
                    Rarity = HoNUniqueRarity.Uncommon,
                    CrafterName = "Unknown",
                    Bonuses = new List<HoNUniqueBonus>()
                };
            }
        }

        private static float GetCurrentCampaignDay()
        {
            try
            {
                return (float)CampaignTime.Now.GetDayOfYear + (CampaignTime.Now.GetYear * 365f);
            }
            catch
            {
                return 0f;
            }
        }

        private static HoNUniqueRarity DetermineRarity(int smithingSkill)
        {
            // Higher skill = better chance at higher rarities
            float skillFactor = Math.Min(1f, smithingSkill / 300f);
            double roll = _random.NextDouble();

            // Adjust thresholds based on skill
            float legendaryThreshold = 0.03f * skillFactor;
            float epicThreshold = 0.12f * skillFactor;
            float rareThreshold = 0.25f + (0.10f * skillFactor);

            if (roll < legendaryThreshold)
                return HoNUniqueRarity.Legendary;
            if (roll < legendaryThreshold + epicThreshold)
                return HoNUniqueRarity.Epic;
            if (roll < legendaryThreshold + epicThreshold + rareThreshold)
                return HoNUniqueRarity.Rare;

            return HoNUniqueRarity.Uncommon;
        }

        private static void GenerateBonuses(HoNUniqueItemData uniqueData, ItemObject item, HoNUniqueRarity rarity)
        {
            var settings = ModSettings.Get();
            bool isWeapon = IsWeapon(item);
            bool isArmor = IsArmor(item);

            // Determine bonus count based on rarity
            int bonusCount = rarity switch
            {
                HoNUniqueRarity.Uncommon => 1,
                HoNUniqueRarity.Rare => _random.Next(1, 3),      // 1-2
                HoNUniqueRarity.Epic => _random.Next(2, 4),      // 2-3
                HoNUniqueRarity.Legendary => _random.Next(3, settings.MaxUniqueBonuses + 1),
                _ => 1
            };

            // Get appropriate bonus pool
            var bonusPool = GetBonusPoolForItem(item, isWeapon, isArmor);
            var selectedBonuses = new HashSet<HoNUniqueBonusType>();

            for (int i = 0; i < bonusCount && selectedBonuses.Count < bonusPool.Count; i++)
            {
                // Pick a random bonus type we haven't used
                var availableBonuses = bonusPool.Where(b => !selectedBonuses.Contains(b)).ToList();
                if (availableBonuses.Count == 0) break;

                var bonusType = availableBonuses[_random.Next(availableBonuses.Count)];
                selectedBonuses.Add(bonusType);

                // Generate value based on rarity
                var bonus = GenerateBonus(bonusType, rarity);
                uniqueData.Bonuses.Add(bonus);
            }

            // Legendary items might get a special bonus
            if (rarity == HoNUniqueRarity.Legendary && _random.NextDouble() < 0.3f)
            {
                var specialBonus = GenerateSpecialBonus();
                if (specialBonus != null)
                    uniqueData.Bonuses.Add(specialBonus);
            }
        }

        private static List<HoNUniqueBonusType> GetBonusPoolForItem(ItemObject item, bool isWeapon, bool isArmor)
        {
            var pool = new List<HoNUniqueBonusType>();

            if (isWeapon)
            {
                pool.AddRange(new[]
                {
                    HoNUniqueBonusType.BonusDamage,
                    HoNUniqueBonusType.BonusSpeed,
                    HoNUniqueBonusType.ArmorPiercing,
                    HoNUniqueBonusType.CriticalChance,
                    HoNUniqueBonusType.Knockback,
                });

                // Melee weapons can have special effects
                if (item?.ItemType != ItemObject.ItemTypeEnum.Bow && 
                    item?.ItemType != ItemObject.ItemTypeEnum.Crossbow)
                {
                    pool.AddRange(new[]
                    {
                        HoNUniqueBonusType.LifeSteal,
                        HoNUniqueBonusType.Bleeding,
                        HoNUniqueBonusType.FireDamage,
                        HoNUniqueBonusType.FrostDamage,
                    });
                }
            }

            if (isArmor)
            {
                pool.AddRange(new[]
                {
                    HoNUniqueBonusType.BonusArmor,
                    HoNUniqueBonusType.DamageReduction,
                    HoNUniqueBonusType.SpeedBonus,
                    HoNUniqueBonusType.HealthRegen,
                    HoNUniqueBonusType.ResistFire,
                    HoNUniqueBonusType.ResistFrost,
                    HoNUniqueBonusType.ResistPoison,
                });
            }

            // Universal bonuses (can appear on anything)
            pool.AddRange(new[]
            {
                HoNUniqueBonusType.Charisma,
                HoNUniqueBonusType.Leadership,
                HoNUniqueBonusType.Athletics,
            });

            return pool;
        }

        private static HoNUniqueBonus GenerateBonus(HoNUniqueBonusType bonusType, HoNUniqueRarity rarity)
        {
            // Base value ranges by rarity
            float minMult = rarity switch
            {
                HoNUniqueRarity.Uncommon => 0.5f,
                HoNUniqueRarity.Rare => 0.75f,
                HoNUniqueRarity.Epic => 1.0f,
                HoNUniqueRarity.Legendary => 1.5f,
                _ => 0.5f
            };

            float maxMult = rarity switch
            {
                HoNUniqueRarity.Uncommon => 1.0f,
                HoNUniqueRarity.Rare => 1.5f,
                HoNUniqueRarity.Epic => 2.0f,
                HoNUniqueRarity.Legendary => 3.0f,
                _ => 1.0f
            };

            // Get base value for this bonus type
            var (baseValue, isPercentage) = GetBonusBaseValue(bonusType);

            // Apply rarity multiplier
            float value = baseValue * ((float)_random.NextDouble() * (maxMult - minMult) + minMult);
            value = (float)Math.Round(value, 1);

            return new HoNUniqueBonus(bonusType, value, isPercentage);
        }

        private static (float baseValue, bool isPercentage) GetBonusBaseValue(HoNUniqueBonusType bonusType)
        {
            return bonusType switch
            {
                HoNUniqueBonusType.BonusDamage => (10f, false),
                HoNUniqueBonusType.BonusSpeed => (5f, true),
                HoNUniqueBonusType.ArmorPiercing => (15f, false),
                HoNUniqueBonusType.LifeSteal => (3f, true),
                HoNUniqueBonusType.CriticalChance => (5f, true),
                HoNUniqueBonusType.Knockback => (10f, true),
                HoNUniqueBonusType.Bleeding => (2f, false),
                HoNUniqueBonusType.FireDamage => (8f, false),
                HoNUniqueBonusType.FrostDamage => (8f, false),
                HoNUniqueBonusType.BonusArmor => (5f, false),
                HoNUniqueBonusType.DamageReduction => (3f, true),
                HoNUniqueBonusType.SpeedBonus => (3f, true),
                HoNUniqueBonusType.HealthRegen => (1f, false),
                HoNUniqueBonusType.StaminaRegen => (5f, true),
                HoNUniqueBonusType.ResistFire => (10f, true),
                HoNUniqueBonusType.ResistFrost => (10f, true),
                HoNUniqueBonusType.ResistPoison => (10f, true),
                HoNUniqueBonusType.Charisma => (5f, false),
                HoNUniqueBonusType.Leadership => (5f, false),
                HoNUniqueBonusType.TradeProficiency => (5f, false),
                HoNUniqueBonusType.Athletics => (5f, false),
                HoNUniqueBonusType.Riding => (5f, false),
                _ => (5f, false)
            };
        }

        private static HoNUniqueBonus GenerateSpecialBonus()
        {
            double roll = _random.NextDouble();

            if (roll < 0.4f)
                return new HoNUniqueBonus(HoNUniqueBonusType.Legendary, 1f, false);
            if (roll < 0.6f)
                return new HoNUniqueBonus(HoNUniqueBonusType.Blessed, 1f, false);
            if (roll < 0.8f)
                return new HoNUniqueBonus(HoNUniqueBonusType.Ancient, 1f, false);

            // 20% chance for cursed (powerful but with hidden drawback)
            return new HoNUniqueBonus(HoNUniqueBonusType.Cursed, 1f, false);
        }

        private static bool IsWeapon(ItemObject item)
        {
            if (item == null) return false;
            return item.ItemType switch
            {
                ItemObject.ItemTypeEnum.OneHandedWeapon => true,
                ItemObject.ItemTypeEnum.TwoHandedWeapon => true,
                ItemObject.ItemTypeEnum.Polearm => true,
                ItemObject.ItemTypeEnum.Thrown => true,
                ItemObject.ItemTypeEnum.Bow => true,
                ItemObject.ItemTypeEnum.Crossbow => true,
                _ => false
            };
        }

        private static bool IsArmor(ItemObject item)
        {
            if (item == null) return false;
            return item.ItemType switch
            {
                ItemObject.ItemTypeEnum.HeadArmor => true,
                ItemObject.ItemTypeEnum.BodyArmor => true,
                ItemObject.ItemTypeEnum.LegArmor => true,
                ItemObject.ItemTypeEnum.HandArmor => true,
                ItemObject.ItemTypeEnum.Cape => true,
                ItemObject.ItemTypeEnum.Shield => true,
                ItemObject.ItemTypeEnum.HorseHarness => true,
                _ => false
            };
        }
    }
}
