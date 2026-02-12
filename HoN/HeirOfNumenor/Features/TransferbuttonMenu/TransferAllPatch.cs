using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Patches
{
    /// <summary>
    /// Patches ExecuteSellAllItems - The RIGHT button (player inventory side)
    /// This transfers items FROM player TO vendor/trash
    /// </summary>
    [HarmonyPatch(typeof(SPInventoryVM))]
    [HarmonyPatch("ExecuteSellAllItems")]
    public static class SellAllPatch
    {
        private static InventoryQuickActionsLogic _logic;
        private static SPInventoryVM _currentVM;

        [HarmonyPrefix]
        public static bool Prefix(SPInventoryVM __instance)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[InventoryQuickActions] Quick Actions Menu", Colors.Cyan));

                _currentVM = __instance;
                _logic = new InventoryQuickActionsLogic(__instance);

                ShowQuickActionsMenu();
                return false; // Prevent original method
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[InventoryQuickActions] Error: {ex.Message}", Colors.Red));
                return true;
            }
        }

        private static void ShowQuickActionsMenu()
        {
            var settings = Settings.Instance;
            
            var inquiryElements = new List<InquiryElement>
            {
                new InquiryElement(
                    "transfer_all",
                    new TextObject("Sell/Transfer All Items").ToString(),
                    null,
                    true,
                    "Sell or transfer all items (original behavior)"),
                new InquiryElement(
                    "sell_damaged",
                    GetSellDamagedText(settings),
                    null,
                    true,
                    GetSellDamagedHint(settings)),
                new InquiryElement(
                    "sell_low_value", 
                    GetSellLowValueText(settings),
                    null,
                    true,
                    GetSellLowValueHint(settings)),
                new InquiryElement(
                    "unequip_all",
                    new TextObject("Unequip All Items").ToString(),
                    null,
                    true,
                    "Remove all equipped items from your character")
            };

            try
            {
                var inquiryData = new MultiSelectionInquiryData(
                    new TextObject("Inventory Quick Actions").ToString(),
                    new TextObject("Select an action:").ToString(),
                    inquiryElements,
                    true,
                    1,
                    1,
                    new TextObject("Execute").ToString(),
                    new TextObject("Cancel").ToString(),
                    OnActionSelected,
                    OnActionCancelled);

                // Try MBInformationManager first (1.3.x)
                var mbInfoManager = AccessTools.TypeByName("TaleWorlds.Core.MBInformationManager");
                if (mbInfoManager != null)
                {
                    var showMethod = AccessTools.Method(mbInfoManager, "ShowMultiSelectionInquiry", 
                        new[] { typeof(MultiSelectionInquiryData), typeof(bool), typeof(bool) });
                    if (showMethod != null)
                    {
                        showMethod.Invoke(null, new object[] { inquiryData, true, false });
                        return;
                    }
                }

                // Fallback methods
                var infoManagerType = typeof(InformationManager);
                var method2 = AccessTools.Method(infoManagerType, "ShowMultiSelectionInquiry",
                    new[] { typeof(MultiSelectionInquiryData), typeof(bool) });
                if (method2 != null)
                {
                    method2.Invoke(null, new object[] { inquiryData, true });
                    return;
                }

                var method3 = AccessTools.Method(infoManagerType, "ShowMultiSelectionInquiry",
                    new[] { typeof(MultiSelectionInquiryData), typeof(bool), typeof(bool) });
                if (method3 != null)
                {
                    method3.Invoke(null, new object[] { inquiryData, true, false });
                    return;
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    "[InventoryQuickActions] Menu not available", Colors.Yellow));
                ExecuteOriginalSellAll();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[InventoryQuickActions] Menu error: {ex.Message}", Colors.Red));
                ExecuteOriginalSellAll();
            }
        }

        private static string GetSellDamagedText(Settings settings)
        {
            if (settings != null)
            {
                var threshold = settings.GetEffectiveDamageThreshold();
                var qualityName = threshold switch
                {
                    <= -0.50f => "Destroyed",
                    <= -0.30f => "Damaged",
                    <= -0.20f => "Rusty/Cracked",
                    <= -0.10f => "Worn/Battered",
                    _ => "Damaged"
                };
                return $"Sell All {qualityName} Items";
            }
            return "Sell All Damaged Items";
        }

        private static string GetSellDamagedHint(Settings settings)
        {
            if (settings != null)
            {
                var threshold = settings.GetEffectiveDamageThreshold();
                return $"Sells items with modifier at or below {threshold * 100:F0}%";
            }
            return "Sells damaged items below threshold";
        }

        private static string GetSellLowValueText(Settings settings)
        {
            if (settings != null)
            {
                return $"Sell Items ≤{settings.LowValueThreshold} Denars";
            }
            return "Sell Low Value Items";
        }

        private static string GetSellLowValueHint(Settings settings)
        {
            if (settings != null)
            {
                return $"Sells items worth {settings.LowValueThreshold} denars or less";
            }
            return "Sells items below value threshold";
        }

        private static void OnActionSelected(List<InquiryElement> selectedElements)
        {
            if (selectedElements == null || selectedElements.Count == 0)
                return;

            var selectedId = selectedElements[0].Identifier as string;

            try
            {
                switch (selectedId)
                {
                    case "transfer_all":
                        ExecuteOriginalSellAll();
                        break;
                    case "sell_damaged":
                        _logic?.ExecuteSellAllDamaged();
                        break;
                    case "sell_low_value":
                        _logic?.ExecuteSellAllLowValue();
                        break;
                    case "unequip_all":
                        _logic?.ExecuteUnequipAll();
                        break;
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[InventoryQuickActions] Action error: {ex.Message}", Colors.Red));
            }
        }

        private static void ExecuteOriginalSellAll()
        {
            if (_currentVM == null) return;

            try
            {
                // Use the private TransferAll method that the game uses
                var transferAllMethod = AccessTools.Method(typeof(SPInventoryVM), "TransferAll", new Type[] { typeof(bool) });
                if (transferAllMethod != null)
                {
                    // isBuy = false means we're selling (transferring from player to other)
                    transferAllMethod.Invoke(_currentVM, new object[] { false });
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Items transferred successfully", Colors.Green));
                    return;
                }

                // Fallback: Get items from RightItemListVM and sell each one
                var rightItemListProperty = AccessTools.Property(typeof(SPInventoryVM), "RightItemListVM");
                if (rightItemListProperty != null)
                {
                    var rightItemList = rightItemListProperty.GetValue(_currentVM) as MBBindingList<SPItemVM>;
                    if (rightItemList != null)
                    {
                        // Get inventory logic
                        var logicField = AccessTools.Field(typeof(SPInventoryVM), "_inventoryLogic");
                        var logic = logicField?.GetValue(_currentVM);
                        
                        // Get ProcessSellItem delegate
                        var processSellField = AccessTools.Field(typeof(SPItemVM), "ProcessSellItem");
                        var processSellDelegate = processSellField?.GetValue(null) as Action<SPItemVM, bool>;

                        var itemsToTransfer = rightItemList.Where(i => i != null && i.IsTransferable && !i.IsLocked).ToList();
                        int transferredCount = 0;

                        foreach (var item in itemsToTransfer)
                        {
                            try
                            {
                                if (processSellDelegate != null)
                                {
                                    // Use the delegate that SPInventoryVM sets up
                                    processSellDelegate(item, false);
                                    transferredCount++;
                                }
                                else
                                {
                                    // Fallback: Try ExecuteSellItem on the item
                                    var sellMethod = AccessTools.Method(item.GetType(), "ExecuteSellItem");
                                    if (sellMethod != null)
                                    {
                                        sellMethod.Invoke(item, null);
                                        transferredCount++;
                                    }
                                }
                            }
                            catch { /* Skip failed items */ }
                        }

                        if (transferredCount > 0)
                        {
                            // Refresh the inventory
                            var refreshMethod = AccessTools.Method(typeof(SPInventoryVM), "RefreshInformationValues");
                            refreshMethod?.Invoke(_currentVM, null);
                            
                            var removeZeroMethod = AccessTools.Method(typeof(SPInventoryVM), "ExecuteRemoveZeroCounts");
                            removeZeroMethod?.Invoke(_currentVM, null);

                            InformationManager.DisplayMessage(new InformationMessage(
                                $"Transferred {transferredCount} items", Colors.Green));
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                "No transferable items found", Colors.Yellow));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Transfer error: {ex.Message}", Colors.Red));
            }
        }

        private static void OnActionCancelled(List<InquiryElement> elements)
        {
            // Do nothing
        }
    }

    /// <summary>
    /// Debug helper to list all methods
    /// </summary>
    public static class DebugPatch
    {
        public static void ListAllMethods()
        {
            try
            {
                var methods = typeof(SPInventoryVM).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName)
                    .Select(m => m.Name)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[InventoryQuickActions] SPInventoryVM methods:", Colors.Cyan));

                // Log Execute methods specifically
                var executeMethods = methods.Where(m => m.StartsWith("Execute")).ToList();
                foreach (var method in executeMethods)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"  - {method}", Colors.White));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[InventoryQuickActions] Debug error: {ex.Message}", Colors.Red));
            }
        }
    }
}
