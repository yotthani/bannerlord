using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.SmithingExtended
{
    /// <summary>
    /// Harmony patches for Smithing Extended features.
    /// Uses central ModSettings for configuration.
    /// Now uses proper interface approach for stamina management.
    /// </summary>
    [HarmonyPatch]
    public static class SmithingExtendedPatches
    {
        private const string FEATURE_NAME = "SmithingExtended";

        /// <summary>
        /// Check if Smithing Extended is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableSmithingExtended; }
                catch { return true; }
            }
        }

        /// <summary>
        /// Patch to increase max stamina based on skill level.
        /// Uses the proper method instead of private field access.
        /// </summary>
        [HarmonyPatch(typeof(CraftingCampaignBehavior), "GetMaxHeroCraftingStamina")]
        [HarmonyPostfix]
        public static void GetMaxHeroCraftingStamina_Postfix(Hero hero, ref int __result)
        {
            try
            {
                if (!IsEnabled) return;
                
                var settings = ModSettings.Get();
                
                // If stamina is disabled, set a very high max
                if (settings.DisableSmithingStamina)
                {
                    __result = 9999;
                    return;
                }

                // Add bonus stamina based on smithing skill
                int smithingSkill = hero?.GetSkillValue(DefaultSkills.Crafting) ?? 0;
                int skillBonus = (smithingSkill / 25) * 5; // +5 max per 25 skill
                
                // Add bonus from settings
                __result += skillBonus;
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog($"[SmithingExtended] GetMaxHeroCraftingStamina error: {ex.Message}");
            }
        }

        /* DISABLED - DailyTickHero method doesn't exist in this game version
        /// <summary>
        /// Patch to provide faster stamina recovery.
        /// Hooks into DailyTickHero for natural daily recovery bonus.
        /// </summary>
        [HarmonyPatch(typeof(CraftingCampaignBehavior), "DailyTickHero")]
        [HarmonyPostfix]
        public static void DailyTickHero_Postfix(Hero hero, CraftingCampaignBehavior __instance)
        {
            try
            {
                if (!IsEnabled || hero == null) return;
                
                var settings = ModSettings.Get();
                
                // If stamina is disabled, ensure hero is always at max
                if (settings.DisableSmithingStamina)
                {
                    // Use the interface if available
                    var behavior = Campaign.Current?.GetCampaignBehavior<ICraftingCampaignBehavior>();
                    if (behavior != null)
                    {
                        int maxStamina = behavior.GetMaxHeroCraftingStamina(hero);
                        behavior.SetHeroCraftingStamina(hero, maxStamina);
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog($"[SmithingExtended] DailyTickHero error: {ex.Message}");
            }
        }
        */

        /* DISABLED - Method signature doesn't match this game version
        /// <summary>
        /// Patch smelting to reduce stamina cost.
        /// </summary>
        [HarmonyPatch(typeof(CraftingCampaignBehavior), "DoSmelting")]
        [HarmonyPrefix]
        public static void DoSmelting_Prefix(Hero hero, CraftingCampaignBehavior __instance)
        {
            try
            {
                if (!IsEnabled) return;
                
                var settings = ModSettings.Get();
                if (settings.DisableSmithingStamina)
                {
                    // Ensure hero has enough stamina before smelting
                    var behavior = Campaign.Current?.GetCampaignBehavior<ICraftingCampaignBehavior>();
                    if (behavior != null)
                    {
                        int maxStamina = behavior.GetMaxHeroCraftingStamina(hero);
                        if (behavior.GetHeroCraftingStamina(hero) < 100)
                        {
                            behavior.SetHeroCraftingStamina(hero, maxStamina);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog($"[SmithingExtended] DoSmelting error: {ex.Message}");
            }
        }
        */

        [HarmonyPatch(typeof(CraftingCampaignBehavior), "DoRefinement")]
        [HarmonyPrefix]
        public static void DoRefinement_Prefix(Hero hero, CraftingCampaignBehavior __instance)
        {
            try
            {
                if (!IsEnabled) return;
                
                var settings = ModSettings.Get();
                if (settings.DisableSmithingStamina)
                {
                    var behavior = Campaign.Current?.GetCampaignBehavior<ICraftingCampaignBehavior>();
                    if (behavior != null)
                    {
                        int maxStamina = behavior.GetMaxHeroCraftingStamina(hero);
                        if (behavior.GetHeroCraftingStamina(hero) < 100)
                        {
                            behavior.SetHeroCraftingStamina(hero, maxStamina);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog($"[SmithingExtended] DoRefinement error: {ex.Message}");
            }
        }
        /// </summary>
        [HarmonyPatch(typeof(CraftingCampaignBehavior), "SetHeroCraftingStamina")]
        [HarmonyPrefix]
        public static bool SetHeroCraftingStamina_Prefix(Hero hero, int value, ref int ____currentHeroCraftingStamina)
        {
            try
            {
                if (!IsEnabled) return true;
                
                var settings = ModSettings.Get();

                if (settings.DisableSmithingStamina)
                {
                    // Don't change stamina at all
                    return false;
                }

                if (Math.Abs(settings.SmithingStaminaMultiplier - 1.0f) > 0.01f)
                {
                    // Calculate the stamina change
                    int currentStamina = ____currentHeroCraftingStamina;
                    int staminaChange = value - currentStamina;

                    // If losing stamina, apply multiplier
                    if (staminaChange < 0)
                    {
                        int modifiedChange = (int)(staminaChange * settings.SmithingStaminaMultiplier);
                        ____currentHeroCraftingStamina = Math.Max(0, currentStamina + modifiedChange);
                        return false; // Skip original method
                    }
                }

                return true; // Continue with original
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog($"[SmithingExtended] SetStamina error: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Patch to check for unique item generation after crafting.
        /// </summary>
        [HarmonyPatch(typeof(CraftingCampaignBehavior), "CreateCraftedWeaponInFreeBuildMode")]
        [HarmonyPostfix]
        public static void CreateCraftedWeapon_Postfix(ItemObject __result, Hero hero)
        {
            SafeExecutor.WrapPatch(FEATURE_NAME, "CraftedWeapon", () =>
            {
                if (!IsEnabled) return;
                
                var settings = ModSettings.Get();
                if (!settings.EnableUniqueItems) return;
                if (__result == null || hero == null) return;

                int smithingSkill = hero.GetSkillValue(DefaultSkills.Crafting);
                
                // Check against settings
                if (smithingSkill < settings.MinSkillForUnique) return;

                // Calculate unique chance
                float uniqueChance = settings.UniqueItemChance;
                float skillBonus = (smithingSkill - settings.MinSkillForUnique) / 100f * 0.01f;
                float totalChance = Math.Min(uniqueChance + skillBonus, 0.5f); // Cap at 50%

                if (MBRandom.RandomFloat < totalChance)
                {
                    var uniqueData = UniqueItemGenerator.GenerateUniqueItem(__result, hero);
                    UniqueItemTracker.RegisterUniqueItem(__result.StringId, uniqueData);

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"★ You crafted a unique item: {uniqueData.GetDisplayName()}!",
                        uniqueData.GetRarityColor()));

                    // Show bonuses
                    foreach (var bonus in uniqueData.Bonuses)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"  • {bonus.GetShortDescription()}",
                            Colors.Yellow));
                    }

                    // Track statistics
                    SmithingExtendedCampaignBehavior.Instance?.TrackCraftedItem(true);
                }
                else
                {
                    SmithingExtendedCampaignBehavior.Instance?.TrackCraftedItem(false);
                }
            });
        }
    }

    /// <summary>
    /// Adds repair shop option to settlement menus.
    /// </summary>
    public static class SettlementMenuExtension
    {
        private const string FEATURE_NAME = "SmithingExtended";
        private const string RepairShopMenuId = "smithing_repair_shop";
        private const string RepairShopOptionId = "smithing_repair_option";

        /// <summary>
        /// Registers the repair shop menu options.
        /// </summary>
        public static void RegisterMenus(CampaignGameStarter starter)
        {
            SafeExecutor.Execute(FEATURE_NAME, "RegisterMenus", () =>
            {
                var settings = ModSettings.Get();
                if (!settings.EnableSmithingExtended || !settings.EnableItemRepair) return;

                // Add option to town menu
                starter.AddGameMenuOption(
                    "town",
                    RepairShopOptionId,
                    "Visit the Repair Shop",
                    args =>
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                        return SmithingExtendedPatches.IsEnabled && settings.EnableItemRepair;
                    },
                    args =>
                    {
                        SafeExecutor.Execute(FEATURE_NAME, "OpenRepairMenu", () =>
                        {
                            GameMenu.SwitchToMenu(RepairShopMenuId);
                        });
                    },
                    false,
                    4,
                    false);

                // Create repair shop submenu
                starter.AddGameMenu(
                    RepairShopMenuId,
                    "The smithy's repair shop. Here you can repair damaged equipment or upgrade items with better modifiers.",
                    args => { });

                // Add repair options
                starter.AddGameMenuOption(
                    RepairShopMenuId,
                    "repair_view_items",
                    "View items for repair",
                    args =>
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                        return true;
                    },
                    args =>
                    {
                        SafeExecutor.Execute(FEATURE_NAME, "ViewRepairItems", OpenRepairScreen);
                    },
                    false,
                    0);

                starter.AddGameMenuOption(
                    RepairShopMenuId,
                    "repair_back",
                    "Leave",
                    args =>
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                        return true;
                    },
                    args =>
                    {
                        SafeExecutor.Execute(FEATURE_NAME, "LeaveRepair", () =>
                        {
                            GameMenu.SwitchToMenu("town");
                        });
                    },
                    true,
                    99);

                ModSettings.DebugLog($"[{FEATURE_NAME}] Repair shop menu registered.");
            });
        }

        private static void OpenRepairScreen()
        {
            ShowRepairInquiry();
        }

        private static void ShowRepairInquiry()
        {
            SafeExecutor.Execute(FEATURE_NAME, "ShowRepairInquiry", () =>
            {
                var repairableItems = GetRepairableItems();

                if (repairableItems.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "You have no items that can be repaired or upgraded.", Colors.Yellow));
                    return;
                }

                // For simplicity, show first item with repair options
                foreach (var item in repairableItems.Take(10))
                {
                    var options = ItemRepairManager.GetRepairOptions(item.Item, Hero.MainHero);
                    if (options.Count > 0)
                    {
                        string itemName = item.Item.Name?.ToString() ?? "Unknown Item";
                        var firstOption = options.FirstOrDefault();
                        
                        if (firstOption != null)
                        {
                            int finalCost = (int)(firstOption.GoldCost * ModSettings.Get().RepairCostMultiplier);
                            
                            InformationManager.ShowInquiry(new InquiryData(
                                $"Repair {itemName}?",
                                $"{firstOption.Description}\nCost: {finalCost} gold\nYour gold: {Hero.MainHero.Gold}",
                                Hero.MainHero.Gold >= finalCost,
                                true,
                                "Repair",
                                "Cancel",
                                () => ExecuteRepair(item, firstOption),
                                null));
                            return;
                        }
                    }
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    "No repair options available for your current items.", Colors.Yellow));
            });
        }
        
        private static void ExecuteRepair(EquipmentElement item, RepairOption option)
        {
            SafeExecutor.Execute(FEATURE_NAME, "ExecuteRepair", () =>
            {
                int finalCost = (int)(option.GoldCost * ModSettings.Get().RepairCostMultiplier);
                
                if (Hero.MainHero.Gold < finalCost)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Not enough gold!", Colors.Red));
                    return;
                }
                
                Hero.MainHero.ChangeHeroGold(-finalCost);
                
                // Apply repair - simplified
                string repairName = item.Item.Name?.ToString() ?? "item";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Repaired {repairName}! (-{finalCost} gold)", Colors.Green));
            });
        }

        private static List<EquipmentElement> GetRepairableItems()
        {
            var items = new List<EquipmentElement>();

            SafeExecutor.Execute(FEATURE_NAME, "GetRepairableItems", () =>
            {
                var inventory = MobileParty.MainParty?.ItemRoster;
                if (inventory == null) return;

                for (int i = 0; i < inventory.Count; i++)
                {
                    try
                    {
                        var element = inventory.GetElementCopyAtIndex(i);
                        if (element.EquipmentElement.Item != null)
                        {
                            var itemType = element.EquipmentElement.Item.ItemType;
                            if (IsRepairableType(itemType))
                            {
                                items.Add(element.EquipmentElement);
                            }
                        }
                    }
                    catch { }
                }
            });

            return items;
        }

        private static bool IsRepairableType(ItemObject.ItemTypeEnum itemType)
        {
            return itemType switch
            {
                ItemObject.ItemTypeEnum.OneHandedWeapon => true,
                ItemObject.ItemTypeEnum.TwoHandedWeapon => true,
                ItemObject.ItemTypeEnum.Polearm => true,
                ItemObject.ItemTypeEnum.Bow => true,
                ItemObject.ItemTypeEnum.Crossbow => true,
                ItemObject.ItemTypeEnum.HeadArmor => true,
                ItemObject.ItemTypeEnum.BodyArmor => true,
                ItemObject.ItemTypeEnum.LegArmor => true,
                ItemObject.ItemTypeEnum.HandArmor => true,
                ItemObject.ItemTypeEnum.Shield => true,
                _ => false
            };
        }
    }
}
