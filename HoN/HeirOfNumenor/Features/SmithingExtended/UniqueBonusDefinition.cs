using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.SmithingExtended
{
    /// <summary>
    /// Types of unique bonuses that can be applied to items.
    /// </summary>
    public enum HoNUniqueBonusType
    {
        // Weapon bonuses
        BonusDamage,
        BonusSpeed,
        ArmorPiercing,
        LifeSteal,
        CriticalChance,
        Knockback,
        Bleeding,
        FireDamage,
        FrostDamage,
        
        // Armor bonuses
        BonusArmor,
        DamageReduction,
        SpeedBonus,
        HealthRegen,
        StaminaRegen,
        ResistFire,
        ResistFrost,
        ResistPoison,
        
        // Universal bonuses
        Charisma,
        Leadership,
        TradeProficiency,
        Athletics,
        Riding,
        
        // Special/Rare
        Legendary,      // Multiple small bonuses
        Cursed,         // Powerful but with drawback
        Blessed,        // Divine protection
        Ancient,        // Historic/lore bonus
    }

    /// <summary>
    /// Rarity tiers for unique items.
    /// </summary>
    public enum HoNUniqueRarity
    {
        Uncommon,       // 60% - 1 bonus, small values
        Rare,           // 25% - 1-2 bonuses, medium values
        Epic,           // 12% - 2-3 bonuses, high values
        Legendary,      // 3% - 3+ bonuses, very high values
    }

    /// <summary>
    /// Defines a single unique bonus on an item.
    /// </summary>
    public class HoNUniqueBonus
    {
        [SaveableField(1)]
        public HoNUniqueBonusType BonusType;

        [SaveableField(2)]
        public float Value;

        [SaveableField(3)]
        public string Description;

        [SaveableField(4)]
        public bool IsPercentage;

        public HoNUniqueBonus() { }

        public HoNUniqueBonus(HoNUniqueBonusType type, float value, bool isPercentage = false)
        {
            BonusType = type;
            Value = value;
            IsPercentage = isPercentage;
            Description = GenerateDescription();
        }

        private string GenerateDescription()
        {
            string valueStr = IsPercentage ? $"+{Value:F0}%" : $"+{Value:F0}";
            
            return BonusType switch
            {
                HoNUniqueBonusType.BonusDamage => $"{valueStr} Damage",
                HoNUniqueBonusType.BonusSpeed => $"{valueStr} Speed",
                HoNUniqueBonusType.ArmorPiercing => $"{valueStr} Armor Piercing",
                HoNUniqueBonusType.LifeSteal => $"{valueStr} Life Steal",
                HoNUniqueBonusType.CriticalChance => $"{valueStr} Critical Chance",
                HoNUniqueBonusType.Knockback => $"{valueStr} Knockback",
                HoNUniqueBonusType.Bleeding => $"Causes Bleeding ({valueStr} damage/s)",
                HoNUniqueBonusType.FireDamage => $"{valueStr} Fire Damage",
                HoNUniqueBonusType.FrostDamage => $"{valueStr} Frost Damage",
                HoNUniqueBonusType.BonusArmor => $"{valueStr} Armor",
                HoNUniqueBonusType.DamageReduction => $"{valueStr} Damage Reduction",
                HoNUniqueBonusType.SpeedBonus => $"{valueStr} Movement Speed",
                HoNUniqueBonusType.HealthRegen => $"{valueStr} Health Regen/day",
                HoNUniqueBonusType.StaminaRegen => $"{valueStr} Stamina Regen",
                HoNUniqueBonusType.ResistFire => $"{valueStr} Fire Resistance",
                HoNUniqueBonusType.ResistFrost => $"{valueStr} Frost Resistance",
                HoNUniqueBonusType.ResistPoison => $"{valueStr} Poison Resistance",
                HoNUniqueBonusType.Charisma => $"{valueStr} Charisma",
                HoNUniqueBonusType.Leadership => $"{valueStr} Leadership",
                HoNUniqueBonusType.TradeProficiency => $"{valueStr} Trade",
                HoNUniqueBonusType.Athletics => $"{valueStr} Athletics",
                HoNUniqueBonusType.Riding => $"{valueStr} Riding",
                HoNUniqueBonusType.Legendary => "Legendary Item",
                HoNUniqueBonusType.Cursed => "Cursed (powerful but dangerous)",
                HoNUniqueBonusType.Blessed => "Blessed (divine protection)",
                HoNUniqueBonusType.Ancient => "Ancient Power",
                _ => $"Unknown Bonus ({valueStr})"
            };
        }

        public string GetShortDescription()
        {
            return Description;
        }
    }

    /// <summary>
    /// Complete unique item data attached to an equipment.
    /// </summary>
    public class HoNUniqueItemData
    {
        [SaveableField(1)]
        public string ItemId;

        [SaveableField(2)]
        public string UniqueName;

        [SaveableField(3)]
        public HoNUniqueRarity Rarity;

        [SaveableField(4)]
        public List<HoNUniqueBonus> Bonuses;

        [SaveableField(5)]
        public string CrafterName;

        [SaveableField(6)]
        public float CampaignTimeCrafted;

        [SaveableField(7)]
        public string LoreText;

        public HoNUniqueItemData()
        {
            Bonuses = new List<HoNUniqueBonus>();
        }

        public HoNUniqueItemData(string itemId, string uniqueName, HoNUniqueRarity rarity) : this()
        {
            ItemId = itemId;
            UniqueName = uniqueName;
            Rarity = rarity;
        }

        /// <summary>
        /// Gets the full display name with rarity color.
        /// </summary>
        public string GetDisplayName()
        {
            return $"{GetRarityPrefix()}{UniqueName}";
        }

        public string GetRarityPrefix()
        {
            return Rarity switch
            {
                HoNUniqueRarity.Uncommon => "[Uncommon] ",
                HoNUniqueRarity.Rare => "[Rare] ",
                HoNUniqueRarity.Epic => "[Epic] ",
                HoNUniqueRarity.Legendary => "[Legendary] ",
                _ => ""
            };
        }

        public Color GetRarityColor()
        {
            return Rarity switch
            {
                HoNUniqueRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),   // Green
                HoNUniqueRarity.Rare => new Color(0.2f, 0.4f, 1.0f),       // Blue
                HoNUniqueRarity.Epic => new Color(0.6f, 0.2f, 0.8f),       // Purple
                HoNUniqueRarity.Legendary => new Color(1.0f, 0.6f, 0.0f), // Orange
                _ => Colors.White
            };
        }

        /// <summary>
        /// Generates tooltip text for the unique item.
        /// </summary>
        public string GetTooltipText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"═══ {GetDisplayName()} ═══");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(LoreText))
            {
                sb.AppendLine($"\"{LoreText}\"");
                sb.AppendLine();
            }

            sb.AppendLine("Unique Bonuses:");
            foreach (var bonus in Bonuses)
            {
                sb.AppendLine($"  • {bonus.GetShortDescription()}");
            }

            if (!string.IsNullOrEmpty(CrafterName))
            {
                sb.AppendLine();
                sb.AppendLine($"Crafted by: {CrafterName}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Static generator for unique item names and bonuses.
    /// </summary>
    public static class UniqueNameGenerator
    {
        // Weapon name prefixes by type
        private static readonly string[] SwordPrefixes = { "Blade of", "Edge of", "Fang of", "Claw of", "Talon of" };
        private static readonly string[] AxePrefixes = { "Cleaver of", "Hewer of", "Render of", "Splitter of" };
        private static readonly string[] MacePrefixes = { "Crusher of", "Smasher of", "Breaker of", "Hammer of" };
        private static readonly string[] PolearmPrefixes = { "Lance of", "Spear of", "Pike of", "Glaive of" };
        private static readonly string[] BowPrefixes = { "Bow of", "Longbow of", "Arc of", "String of" };

        // Armor name prefixes
        private static readonly string[] ArmorPrefixes = { "Plate of", "Mail of", "Guard of", "Shield of", "Bastion of" };
        private static readonly string[] HelmPrefixes = { "Helm of", "Crown of", "Visage of", "Mask of" };

        // Name suffixes (themes)
        private static readonly string[] ElementalSuffixes = { "Flame", "Frost", "Storm", "Shadow", "Light" };
        private static readonly string[] WarriorSuffixes = { "the Warrior", "the Champion", "the Conqueror", "the Vanquisher" };
        private static readonly string[] MythicSuffixes = { "the Dragon", "the Phoenix", "the Titan", "the Ancient" };
        private static readonly string[] VirtueSuffixes = { "Valor", "Honor", "Justice", "Mercy", "Wrath" };

        // Lore text templates
        private static readonly string[] LoreTemplates =
        {
            "Forged in the fires of {0}, this weapon has tasted the blood of countless foes.",
            "An ancient relic from the days of {0}, its power remains undiminished.",
            "The masterwork of {0}, the greatest smith of their age.",
            "Blessed by the priests of {0}, this item carries divine protection.",
            "Recovered from the ruins of {0}, its origins shrouded in mystery.",
            "Crafted for the legendary {0}, who fell in the great war.",
            "The last creation of {0}, imbued with their dying breath.",
        };

        private static readonly string[] LorePlaces = { "Mount Doom", "the Northern Wastes", "the Crystal Caves", "the Dragon's Lair", "the Sunken City", "Vaegir lands", "the Empire's fall" };
        private static readonly string[] LoreNames = { "Aldric the Bold", "Sigrun Ironhand", "Master Vorn", "the High King", "Lady Aethel", "the Last Emperor" };

        /// <summary>
        /// Generates a unique name for an item.
        /// </summary>
        public static string GenerateUniqueName(ItemObject item, HoNUniqueRarity rarity)
        {
            var random = new Random();

            string[] prefixes = GetPrefixesForItem(item);
            string[] suffixes = GetSuffixesForRarity(rarity);

            string prefix = prefixes[random.Next(prefixes.Length)];
            string suffix = suffixes[random.Next(suffixes.Length)];

            return $"{prefix} {suffix}";
        }

        private static string[] GetPrefixesForItem(ItemObject item)
        {
            if (item == null) return SwordPrefixes;

            var itemType = item.ItemType;
            
            return itemType switch
            {
                ItemObject.ItemTypeEnum.OneHandedWeapon => SwordPrefixes,
                ItemObject.ItemTypeEnum.TwoHandedWeapon => SwordPrefixes,
                ItemObject.ItemTypeEnum.Polearm => PolearmPrefixes,
                ItemObject.ItemTypeEnum.Thrown => PolearmPrefixes,
                ItemObject.ItemTypeEnum.Bow => BowPrefixes,
                ItemObject.ItemTypeEnum.Crossbow => BowPrefixes,
                ItemObject.ItemTypeEnum.HeadArmor => HelmPrefixes,
                ItemObject.ItemTypeEnum.BodyArmor => ArmorPrefixes,
                ItemObject.ItemTypeEnum.LegArmor => ArmorPrefixes,
                ItemObject.ItemTypeEnum.HandArmor => ArmorPrefixes,
                ItemObject.ItemTypeEnum.Cape => ArmorPrefixes,
                ItemObject.ItemTypeEnum.Shield => ArmorPrefixes,
                _ => SwordPrefixes
            };
        }

        private static string[] GetSuffixesForRarity(HoNUniqueRarity rarity)
        {
            return rarity switch
            {
                HoNUniqueRarity.Uncommon => VirtueSuffixes,
                HoNUniqueRarity.Rare => ElementalSuffixes,
                HoNUniqueRarity.Epic => WarriorSuffixes,
                HoNUniqueRarity.Legendary => MythicSuffixes,
                _ => VirtueSuffixes
            };
        }

        /// <summary>
        /// Generates lore text for an item.
        /// </summary>
        public static string GenerateLore(HoNUniqueRarity rarity)
        {
            var random = new Random();

            // Higher rarity = more likely to have lore
            float loreChance = rarity switch
            {
                HoNUniqueRarity.Uncommon => 0.2f,
                HoNUniqueRarity.Rare => 0.5f,
                HoNUniqueRarity.Epic => 0.8f,
                HoNUniqueRarity.Legendary => 1.0f,
                _ => 0.1f
            };

            if (random.NextDouble() > loreChance)
                return "";

            string template = LoreTemplates[random.Next(LoreTemplates.Length)];
            string[] names = random.NextDouble() > 0.5 ? LorePlaces : LoreNames;
            string name = names[random.Next(names.Length)];

            return string.Format(template, name);
        }
    }
}
