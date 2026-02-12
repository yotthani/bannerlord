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
    /// Available item modifiers that can be applied through repair.
    /// </summary>
    public enum ItemModifierType
    {
        // Weapon modifiers
        Balanced,
        Heavy,
        Light,
        Masterwork,
        Fine,
        Rusty,
        Cracked,
        
        // Armor modifiers
        Reinforced,
        Thick,
        Lordly,
        Battered,
        Tattered,
        
        // Quality tiers
        Poor,
        Common,
        Excellent,
        Legendary
    }

    /// <summary>
    /// Repair option for an item.
    /// </summary>
    public class RepairOption
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int GoldCost { get; set; }
        public int MaterialCost { get; set; }
        public string ResultModifier { get; set; }
        public bool IsUpgrade { get; set; }
        public float SuccessChance { get; set; }

        public RepairOption()
        {
            SuccessChance = 1.0f;
        }
    }

    /// <summary>
    /// Manages item repair and modifier changes at settlements.
    /// </summary>
    public static class ItemRepairManager
    {
        private static Random _random = new Random();

        /// <summary>
        /// Gets available repair options for an item.
        /// </summary>
        public static List<RepairOption> GetRepairOptions(ItemObject item, Hero hero)
        {
            var options = new List<RepairOption>();
            var settings = ModSettings.Get();

            if (item == null || !settings.EnableItemRepair) return options;

            bool isWeapon = IsWeapon(item);
            bool isArmor = IsArmor(item);

            if (!isWeapon && !isArmor) return options;

            int smithingSkill = hero?.GetSkillValue(DefaultSkills.Crafting) ?? 100;
            int baseValue = item.Value;

            // Basic repair (remove negative modifier)
            options.Add(new RepairOption
            {
                Name = "Basic Repair",
                Description = "Remove any damage or negative modifiers",
                GoldCost = CalculateRepairCost(baseValue, 1.0f),
                MaterialCost = 0,
                ResultModifier = "",  // Remove modifier
                IsUpgrade = false,
                SuccessChance = 1.0f
            });

            // Quality upgrades based on smithing skill
            if (smithingSkill >= 50)
            {
                options.Add(new RepairOption
                {
                    Name = "Fine Quality",
                    Description = "Upgrade to Fine quality (+10% effectiveness)",
                    GoldCost = CalculateRepairCost(baseValue, 2.0f),
                    MaterialCost = 1,
                    ResultModifier = "fine",
                    IsUpgrade = true,
                    SuccessChance = Math.Min(1.0f, smithingSkill / 100f)
                });
            }

            if (smithingSkill >= 100)
            {
                options.Add(new RepairOption
                {
                    Name = "Masterwork Quality",
                    Description = "Upgrade to Masterwork quality (+20% effectiveness)",
                    GoldCost = CalculateRepairCost(baseValue, 4.0f),
                    MaterialCost = 3,
                    ResultModifier = "masterwork",
                    IsUpgrade = true,
                    SuccessChance = Math.Min(0.9f, smithingSkill / 150f)
                });
            }

            if (smithingSkill >= 200)
            {
                options.Add(new RepairOption
                {
                    Name = "Legendary Reforge",
                    Description = "Attempt to create a legendary quality item (+40% effectiveness)",
                    GoldCost = CalculateRepairCost(baseValue, 10.0f),
                    MaterialCost = 10,
                    ResultModifier = "legendary",
                    IsUpgrade = true,
                    SuccessChance = Math.Min(0.5f, (smithingSkill - 150) / 200f)
                });
            }

            // Specialized modifiers for weapons
            if (isWeapon && smithingSkill >= 75)
            {
                options.Add(new RepairOption
                {
                    Name = "Balance Weapon",
                    Description = "Improve weapon balance for faster swings",
                    GoldCost = CalculateRepairCost(baseValue, 2.5f),
                    MaterialCost = 2,
                    ResultModifier = "balanced",
                    IsUpgrade = true,
                    SuccessChance = Math.Min(1.0f, smithingSkill / 100f)
                });

                options.Add(new RepairOption
                {
                    Name = "Sharpen Edge",
                    Description = "Hone the blade for increased damage",
                    GoldCost = CalculateRepairCost(baseValue, 2.5f),
                    MaterialCost = 1,
                    ResultModifier = "sharp",
                    IsUpgrade = true,
                    SuccessChance = Math.Min(1.0f, smithingSkill / 100f)
                });
            }

            // Specialized modifiers for armor
            if (isArmor && smithingSkill >= 75)
            {
                options.Add(new RepairOption
                {
                    Name = "Reinforce Armor",
                    Description = "Add additional protection layers",
                    GoldCost = CalculateRepairCost(baseValue, 3.0f),
                    MaterialCost = 4,
                    ResultModifier = "reinforced",
                    IsUpgrade = true,
                    SuccessChance = Math.Min(1.0f, smithingSkill / 100f)
                });

                if (smithingSkill >= 150)
                {
                    options.Add(new RepairOption
                    {
                        Name = "Lordly Finish",
                        Description = "Apply noble-quality finishing for prestige and protection",
                        GoldCost = CalculateRepairCost(baseValue, 5.0f),
                        MaterialCost = 5,
                        ResultModifier = "lordly",
                        IsUpgrade = true,
                        SuccessChance = Math.Min(0.8f, smithingSkill / 200f)
                    });
                }
            }

            return options;
        }

        /// <summary>
        /// Attempts to apply a repair option to an item.
        /// </summary>
        public static (bool success, string message, EquipmentElement? newItem) ApplyRepair(
            EquipmentElement item, RepairOption option, Hero hero)
        {
            var settings = ModSettings.Get();

            // Check gold
            int finalCost = (int)(option.GoldCost * settings.RepairCostMultiplier);
            if (Hero.MainHero.Gold < finalCost)
            {
                return (false, $"Not enough gold! Need {finalCost} gold.", null);
            }

            // Check success chance
            float roll = (float)_random.NextDouble();
            if (roll > option.SuccessChance)
            {
                // Failed but still costs gold (reduced)
                int failCost = finalCost / 2;
                Hero.MainHero.ChangeHeroGold(-failCost);
                return (false, $"The repair attempt failed! Lost {failCost} gold.", null);
            }

            // Success - deduct gold
            Hero.MainHero.ChangeHeroGold(-finalCost);

            // Apply modifier
            var newItem = ApplyModifier(item, option.ResultModifier);

            // Check for unique item generation on legendary reforge
            if (option.ResultModifier == "legendary" && settings.EnableUniqueItems)
            {
                int smithingSkill = hero?.GetSkillValue(DefaultSkills.Crafting) ?? 200;
                if (UniqueItemGenerator.ShouldGenerateUnique(smithingSkill))
                {
                    // Generate unique item data (store separately)
                    var uniqueData = UniqueItemGenerator.GenerateUniqueItem(item.Item, hero);
                    UniqueItemTracker.RegisterUniqueItem(newItem.Item?.StringId ?? "", uniqueData);

                    return (true, $"Legendary success! Created unique item: {uniqueData.GetDisplayName()}", newItem);
                }
            }

            string qualityName = string.IsNullOrEmpty(option.ResultModifier) ? "repaired" : option.ResultModifier;
            return (true, $"Successfully {qualityName} the item for {finalCost} gold!", newItem);
        }

        private static EquipmentElement ApplyModifier(EquipmentElement item, string modifierName)
        {
            if (string.IsNullOrEmpty(modifierName))
            {
                // Remove modifier
                return new EquipmentElement(item.Item, null, item.CosmeticItem);
            }

            // Try to find the modifier
            var modifier = GetItemModifier(modifierName);
            
            return new EquipmentElement(item.Item, modifier, item.CosmeticItem);
        }

        private static ItemModifier GetItemModifier(string modifierName)
        {
            // Try to get existing modifier from game
            try
            {
                var modifier = MBObjectManager.Instance?.GetObject<ItemModifier>(modifierName);
                if (modifier != null) return modifier;
            }
            catch { }

            // Return null if not found (item will have no modifier)
            return null;
        }

        private static int CalculateRepairCost(int baseValue, float multiplier)
        {
            var settings = ModSettings.Get();
            int cost = (int)(settings.BaseRepairCost + (baseValue * 0.1f * multiplier));
            return (int)(cost * settings.RepairCostMultiplier);
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
                _ => false
            };
        }
    }

    /// <summary>
    /// Tracks unique items across save/load.
    /// </summary>
    public static class UniqueItemTracker
    {
        private static Dictionary<string, HoNUniqueItemData> _uniqueItems = new Dictionary<string, HoNUniqueItemData>();

        public static void RegisterUniqueItem(string itemId, HoNUniqueItemData data)
        {
            string key = GenerateKey(itemId);
            _uniqueItems[key] = data;
        }

        public static HoNUniqueItemData GetUniqueData(string itemId)
        {
            string key = GenerateKey(itemId);
            return _uniqueItems.TryGetValue(key, out var data) ? data : null;
        }

        public static bool IsUnique(string itemId)
        {
            return _uniqueItems.ContainsKey(GenerateKey(itemId));
        }

        public static Dictionary<string, HoNUniqueItemData> GetAllUniqueItems()
        {
            return _uniqueItems;
        }

        public static void LoadData(Dictionary<string, HoNUniqueItemData> data)
        {
            _uniqueItems = data ?? new Dictionary<string, HoNUniqueItemData>();
        }

        public static void Clear()
        {
            _uniqueItems.Clear();
        }

        private static string GenerateKey(string itemId)
        {
            return itemId?.ToLowerInvariant() ?? "";
        }
    }
}
