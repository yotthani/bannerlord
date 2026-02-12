using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace HeirOfNumenor.Features.SmithingExtended
{
    /// <summary>
    /// Types of armor that can be crafted.
    /// </summary>
    public enum ArmorCraftType
    {
        Helmet,
        BodyArmor,
        Gauntlets,
        Boots,
        Cape,
        Shield
    }

    /// <summary>
    /// Armor part categories for crafting.
    /// </summary>
    public enum ArmorPartCategory
    {
        // Helmet parts
        HelmetBase,
        Visor,
        Crest,
        NeckGuard,

        // Body armor parts
        Cuirass,
        Pauldrons,
        BackPlate,
        Tassets,

        // Gauntlet parts
        GauntletBase,
        Fingers,
        Bracers,

        // Boot parts
        BootBase,
        Greaves,
        SolePlate,

        // Shield parts
        ShieldFace,
        ShieldRim,
        ShieldBoss,
        ShieldStraps,

        // General materials
        Padding,
        Chainmail,
        PlateLayer,
        LeatherLayer
    }

    /// <summary>
    /// Definition of an armor part for crafting.
    /// </summary>
    public class ArmorPartDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ArmorPartCategory Category { get; set; }
        public ArmorCraftType ValidFor { get; set; }

        // Stats this part provides
        public int ArmorValue { get; set; }
        public float WeightModifier { get; set; }
        public int Difficulty { get; set; }

        // Material costs
        public int IronCost { get; set; }
        public int SteelCost { get; set; }
        public int LeatherCost { get; set; }
        public int ClothCost { get; set; }

        // Visual identifier
        public string MeshName { get; set; }
    }

    /// <summary>
    /// Recipe for crafting a specific armor piece.
    /// </summary>
    public class ArmorCraftingRecipe
    {
        public ArmorCraftType ArmorType { get; set; }
        public List<ArmorPartDefinition> SelectedParts { get; set; }
        public string ResultName { get; set; }
        public int TotalArmorValue { get; set; }
        public float TotalWeight { get; set; }
        public int CraftingDifficulty { get; set; }
        public Dictionary<string, int> MaterialCosts { get; set; }

        public ArmorCraftingRecipe()
        {
            SelectedParts = new List<ArmorPartDefinition>();
            MaterialCosts = new Dictionary<string, int>();
        }

        public void CalculateTotals()
        {
            TotalArmorValue = SelectedParts.Sum(p => p.ArmorValue);
            TotalWeight = SelectedParts.Sum(p => p.WeightModifier);
            CraftingDifficulty = SelectedParts.Sum(p => p.Difficulty);

            MaterialCosts.Clear();
            int totalIron = SelectedParts.Sum(p => p.IronCost);
            int totalSteel = SelectedParts.Sum(p => p.SteelCost);
            int totalLeather = SelectedParts.Sum(p => p.LeatherCost);
            int totalCloth = SelectedParts.Sum(p => p.ClothCost);

            if (totalIron > 0) MaterialCosts["Iron"] = totalIron;
            if (totalSteel > 0) MaterialCosts["Steel"] = totalSteel;
            if (totalLeather > 0) MaterialCosts["Leather"] = totalLeather;
            if (totalCloth > 0) MaterialCosts["Cloth"] = totalCloth;
        }
    }

    /// <summary>
    /// Manages armor crafting system.
    /// </summary>
    public static class ArmorSmithingManager
    {
        private static Dictionary<ArmorCraftType, List<ArmorPartDefinition>> _partsByType;
        private static bool _initialized = false;

        /// <summary>
        /// Initializes the armor parts database.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _partsByType = new Dictionary<ArmorCraftType, List<ArmorPartDefinition>>();

            // Initialize part lists for each armor type
            foreach (ArmorCraftType type in Enum.GetValues(typeof(ArmorCraftType)))
            {
                _partsByType[type] = new List<ArmorPartDefinition>();
            }

            // Add default parts
            AddDefaultParts();

            _initialized = true;
        }

        private static void AddDefaultParts()
        {
            // ═══════════════════════════════════════════════════════════
            // HELMET PARTS
            // ═══════════════════════════════════════════════════════════
            AddPart(new ArmorPartDefinition
            {
                Id = "helm_base_iron",
                Name = "Iron Helm Base",
                Category = ArmorPartCategory.HelmetBase,
                ValidFor = ArmorCraftType.Helmet,
                ArmorValue = 15,
                WeightModifier = 1.5f,
                Difficulty = 50,
                IronCost = 2,
                SteelCost = 0
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "helm_base_steel",
                Name = "Steel Helm Base",
                Category = ArmorPartCategory.HelmetBase,
                ValidFor = ArmorCraftType.Helmet,
                ArmorValue = 25,
                WeightModifier = 1.8f,
                Difficulty = 100,
                IronCost = 1,
                SteelCost = 2
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "visor_open",
                Name = "Open Face Visor",
                Category = ArmorPartCategory.Visor,
                ValidFor = ArmorCraftType.Helmet,
                ArmorValue = 0,
                WeightModifier = 0f,
                Difficulty = 20,
                IronCost = 0,
                SteelCost = 0
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "visor_full",
                Name = "Full Visor",
                Category = ArmorPartCategory.Visor,
                ValidFor = ArmorCraftType.Helmet,
                ArmorValue = 10,
                WeightModifier = 0.5f,
                Difficulty = 75,
                IronCost = 1,
                SteelCost = 1
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "neck_guard_mail",
                Name = "Mail Aventail",
                Category = ArmorPartCategory.NeckGuard,
                ValidFor = ArmorCraftType.Helmet,
                ArmorValue = 8,
                WeightModifier = 0.8f,
                Difficulty = 60,
                IronCost = 2,
                SteelCost = 0
            });

            // ═══════════════════════════════════════════════════════════
            // BODY ARMOR PARTS
            // ═══════════════════════════════════════════════════════════
            AddPart(new ArmorPartDefinition
            {
                Id = "cuirass_light",
                Name = "Light Cuirass",
                Category = ArmorPartCategory.Cuirass,
                ValidFor = ArmorCraftType.BodyArmor,
                ArmorValue = 20,
                WeightModifier = 3.0f,
                Difficulty = 80,
                IronCost = 3,
                SteelCost = 1
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "cuirass_heavy",
                Name = "Heavy Cuirass",
                Category = ArmorPartCategory.Cuirass,
                ValidFor = ArmorCraftType.BodyArmor,
                ArmorValue = 35,
                WeightModifier = 6.0f,
                Difficulty = 150,
                IronCost = 2,
                SteelCost = 4
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "pauldrons_simple",
                Name = "Simple Pauldrons",
                Category = ArmorPartCategory.Pauldrons,
                ValidFor = ArmorCraftType.BodyArmor,
                ArmorValue = 8,
                WeightModifier = 1.0f,
                Difficulty = 50,
                IronCost = 2,
                SteelCost = 0
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "pauldrons_ornate",
                Name = "Ornate Pauldrons",
                Category = ArmorPartCategory.Pauldrons,
                ValidFor = ArmorCraftType.BodyArmor,
                ArmorValue = 15,
                WeightModifier = 1.5f,
                Difficulty = 120,
                IronCost = 1,
                SteelCost = 2
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "chainmail_layer",
                Name = "Chainmail Layer",
                Category = ArmorPartCategory.Chainmail,
                ValidFor = ArmorCraftType.BodyArmor,
                ArmorValue = 12,
                WeightModifier = 4.0f,
                Difficulty = 100,
                IronCost = 5,
                SteelCost = 0
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "padding_thick",
                Name = "Thick Padding",
                Category = ArmorPartCategory.Padding,
                ValidFor = ArmorCraftType.BodyArmor,
                ArmorValue = 5,
                WeightModifier = 1.0f,
                Difficulty = 20,
                ClothCost = 3,
                LeatherCost = 1
            });

            // ═══════════════════════════════════════════════════════════
            // SHIELD PARTS
            // ═══════════════════════════════════════════════════════════
            AddPart(new ArmorPartDefinition
            {
                Id = "shield_face_wood",
                Name = "Wooden Shield Face",
                Category = ArmorPartCategory.ShieldFace,
                ValidFor = ArmorCraftType.Shield,
                ArmorValue = 10,
                WeightModifier = 2.0f,
                Difficulty = 30,
                IronCost = 0,
                LeatherCost = 1
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "shield_face_steel",
                Name = "Steel Shield Face",
                Category = ArmorPartCategory.ShieldFace,
                ValidFor = ArmorCraftType.Shield,
                ArmorValue = 25,
                WeightModifier = 5.0f,
                Difficulty = 120,
                SteelCost = 3
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "shield_rim_iron",
                Name = "Iron Rim",
                Category = ArmorPartCategory.ShieldRim,
                ValidFor = ArmorCraftType.Shield,
                ArmorValue = 5,
                WeightModifier = 1.0f,
                Difficulty = 40,
                IronCost = 2
            });

            AddPart(new ArmorPartDefinition
            {
                Id = "shield_boss_spiked",
                Name = "Spiked Boss",
                Category = ArmorPartCategory.ShieldBoss,
                ValidFor = ArmorCraftType.Shield,
                ArmorValue = 3,
                WeightModifier = 0.5f,
                Difficulty = 60,
                IronCost = 1,
                SteelCost = 1
            });
        }

        private static void AddPart(ArmorPartDefinition part)
        {
            if (_partsByType.ContainsKey(part.ValidFor))
            {
                _partsByType[part.ValidFor].Add(part);
            }
        }

        /// <summary>
        /// Gets available parts for an armor type.
        /// </summary>
        public static List<ArmorPartDefinition> GetPartsForArmorType(ArmorCraftType armorType)
        {
            if (!_initialized) Initialize();
            return _partsByType.TryGetValue(armorType, out var parts) ? parts : new List<ArmorPartDefinition>();
        }

        /// <summary>
        /// Gets parts filtered by category.
        /// </summary>
        public static List<ArmorPartDefinition> GetPartsByCategory(ArmorCraftType armorType, ArmorPartCategory category)
        {
            return GetPartsForArmorType(armorType)
                .Where(p => p.Category == category)
                .ToList();
        }

        /// <summary>
        /// Checks if the player can craft armor.
        /// </summary>
        public static bool CanCraftArmor(Hero hero)
        {
            var settings = ModSettings.Get();
            if (!settings.EnableArmorSmithing) return false;

            int smithingSkill = hero?.GetSkillValue(DefaultSkills.Crafting) ?? 0;
            return smithingSkill >= settings.MinSmithingSkillForArmor;
        }

        /// <summary>
        /// Attempts to craft an armor piece from a recipe.
        /// </summary>
        public static (bool success, string message, ItemObject resultItem) CraftArmor(
            ArmorCraftingRecipe recipe, Hero crafter)
        {
            var settings = ModSettings.Get();

            if (!CanCraftArmor(crafter))
            {
                return (false, $"Need {settings.MinSmithingSkillForArmor} smithing skill to craft armor!", null);
            }

            recipe.CalculateTotals();

            // Check materials against party inventory
            var materialCheck = CheckMaterialsInInventory(recipe, crafter);
            if (!materialCheck.hasAllMaterials)
            {
                return (false, $"Missing materials: {materialCheck.missingDescription}", null);
            }

            // Check if difficulty is too high
            int smithingSkill = crafter?.GetSkillValue(DefaultSkills.Crafting) ?? 100;
            if (recipe.CraftingDifficulty > smithingSkill * 2)
            {
                return (false, $"Recipe too difficult! Need higher smithing skill.", null);
            }

            // Calculate success chance based on skill vs difficulty
            float successChance = Math.Min(1.0f, smithingSkill / (float)(recipe.CraftingDifficulty + 50));

            // Consume materials before crafting attempt
            ConsumeMaterials(recipe, crafter);

            // Attempt craft
            var random = new Random();
            if (random.NextDouble() > successChance)
            {
                return (false, "Crafting failed! Materials lost.", null);
            }

            // Success - create the item
            // Note: Actually creating new items requires more complex game API usage
            // This is a simplified version showing the structure

            // Check for unique item
            if (settings.EnableUniqueItems && UniqueItemGenerator.ShouldGenerateUnique(smithingSkill))
            {
                // Would create unique armor here
                return (true, $"Masterwork success! Created a unique {recipe.ArmorType}!", null);
            }

            // Give XP
            int xpGain = recipe.CraftingDifficulty / 2;
            crafter?.AddSkillXp(DefaultSkills.Crafting, xpGain);

            return (true, $"Successfully crafted {recipe.ResultName}!", null);
        }

        /// <summary>
        /// Check if party has required materials.
        /// </summary>
        private static (bool hasAllMaterials, string missingDescription) CheckMaterialsInInventory(
            ArmorCraftingRecipe recipe, Hero crafter)
        {
            if (crafter == null || MobileParty.MainParty == null)
                return (false, "No party available");

            var roster = MobileParty.MainParty.ItemRoster;
            var missing = new List<string>();

            // Check each material type (calculate total from all parts)
            int ironNeeded = 0;
            int leatherNeeded = 0;
            int clothNeeded = 0;
            
            foreach (var part in recipe.SelectedParts)
            {
                ironNeeded += part.IronCost;
                leatherNeeded += part.LeatherCost;
                clothNeeded += part.ClothCost;
            }

            // Count materials in roster
            int ironHave = 0;
            int leatherHave = 0;
            int clothHave = 0;

            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var item = element.EquipmentElement.Item;
                if (item == null) continue;

                // Check for crafting materials by item type/category
                string itemId = item.StringId.ToLowerInvariant();
                
                if (itemId.Contains("iron") || itemId.Contains("steel") || itemId.Contains("ingot"))
                {
                    ironHave += element.Amount;
                }
                else if (itemId.Contains("leather") || itemId.Contains("hide"))
                {
                    leatherHave += element.Amount;
                }
                else if (itemId.Contains("cloth") || itemId.Contains("linen") || itemId.Contains("velvet"))
                {
                    clothHave += element.Amount;
                }
            }

            // Check shortages
            if (ironNeeded > 0 && ironHave < ironNeeded)
                missing.Add($"{ironNeeded - ironHave} iron");
            if (leatherNeeded > 0 && leatherHave < leatherNeeded)
                missing.Add($"{leatherNeeded - leatherHave} leather");
            if (clothNeeded > 0 && clothHave < clothNeeded)
                missing.Add($"{clothNeeded - clothHave} cloth");

            if (missing.Count > 0)
                return (false, string.Join(", ", missing));

            return (true, "");
        }

        /// <summary>
        /// Remove materials from party inventory.
        /// </summary>
        private static void ConsumeMaterials(ArmorCraftingRecipe recipe, Hero crafter)
        {
            if (MobileParty.MainParty == null) return;

            var roster = MobileParty.MainParty.ItemRoster;
            
            // Calculate total materials needed
            int ironToRemove = 0;
            int leatherToRemove = 0;
            int clothToRemove = 0;
            
            foreach (var part in recipe.SelectedParts)
            {
                ironToRemove += part.IronCost;
                leatherToRemove += part.LeatherCost;
                clothToRemove += part.ClothCost;
            }

            // Remove materials (simplified - removes from first matching items)
            for (int i = roster.Count - 1; i >= 0 && (ironToRemove > 0 || leatherToRemove > 0 || clothToRemove > 0); i--)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var item = element.EquipmentElement.Item;
                if (item == null) continue;

                string itemId = item.StringId.ToLowerInvariant();
                int toRemove = 0;

                if (ironToRemove > 0 && (itemId.Contains("iron") || itemId.Contains("steel") || itemId.Contains("ingot")))
                {
                    toRemove = Math.Min(element.Amount, ironToRemove);
                    ironToRemove -= toRemove;
                }
                else if (leatherToRemove > 0 && (itemId.Contains("leather") || itemId.Contains("hide")))
                {
                    toRemove = Math.Min(element.Amount, leatherToRemove);
                    leatherToRemove -= toRemove;
                }
                else if (clothToRemove > 0 && (itemId.Contains("cloth") || itemId.Contains("linen") || itemId.Contains("velvet")))
                {
                    toRemove = Math.Min(element.Amount, clothToRemove);
                    clothToRemove -= toRemove;
                }

                if (toRemove > 0)
                {
                    roster.AddToCounts(item, -toRemove);
                }
            }
        }

        /// <summary>
        /// Gets required parts categories for an armor type.
        /// </summary>
        public static List<ArmorPartCategory> GetRequiredCategories(ArmorCraftType armorType)
        {
            return armorType switch
            {
                ArmorCraftType.Helmet => new List<ArmorPartCategory>
                {
                    ArmorPartCategory.HelmetBase,
                    ArmorPartCategory.Visor,
                    ArmorPartCategory.NeckGuard
                },
                ArmorCraftType.BodyArmor => new List<ArmorPartCategory>
                {
                    ArmorPartCategory.Cuirass,
                    ArmorPartCategory.Pauldrons,
                    ArmorPartCategory.Padding
                },
                ArmorCraftType.Shield => new List<ArmorPartCategory>
                {
                    ArmorPartCategory.ShieldFace,
                    ArmorPartCategory.ShieldRim,
                    ArmorPartCategory.ShieldBoss
                },
                ArmorCraftType.Gauntlets => new List<ArmorPartCategory>
                {
                    ArmorPartCategory.GauntletBase,
                    ArmorPartCategory.Bracers
                },
                ArmorCraftType.Boots => new List<ArmorPartCategory>
                {
                    ArmorPartCategory.BootBase,
                    ArmorPartCategory.Greaves
                },
                ArmorCraftType.Cape => new List<ArmorPartCategory>
                {
                    ArmorPartCategory.Padding,
                    ArmorPartCategory.LeatherLayer
                },
                _ => new List<ArmorPartCategory>()
            };
        }
    }
}
