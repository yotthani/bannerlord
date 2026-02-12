using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace HeirOfNumenor
{
    /// <summary>
    /// Core logic for inventory quick actions.
    /// Uses reflection throughout for maximum compatibility across Bannerlord versions.
    /// </summary>
    public class InventoryQuickActionsLogic
    {
        private readonly SPInventoryVM _inventoryVM;

        public InventoryQuickActionsLogic(SPInventoryVM inventoryVM)
        {
            _inventoryVM = inventoryVM;
        }

        #region Public Execute Methods

        public void ExecuteSellAllDamaged()
        {
            try
            {
                var settings = Settings.Instance;
                var itemsToSell = GetDamagedItems(settings);

                if (itemsToSell.Count == 0)
                {
                    ShowMessage("No damaged items found to sell.", Colors.Yellow);
                    return;
                }

                if (settings.ShowConfirmation)
                {
                    var totalValue = itemsToSell.Sum(x => GetItemValue(x));
                    ShowConfirmationInquiry(
                        "Sell Damaged Items",
                        $"Sell {itemsToSell.Count} damaged item(s) for approximately {totalValue} denars?",
                        () => SellItems(itemsToSell, "damaged"));
                }
                else
                {
                    SellItems(itemsToSell, "damaged");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error selling damaged items: {ex.Message}", Colors.Red);
            }
        }

        public void ExecuteSellAllLowValue()
        {
            try
            {
                var settings = Settings.Instance;
                var itemsToSell = GetLowValueItems(settings);

                if (itemsToSell.Count == 0)
                {
                    ShowMessage("No low value items found to sell.", Colors.Yellow);
                    return;
                }

                if (settings.ShowConfirmation)
                {
                    var totalValue = itemsToSell.Sum(x => GetItemValue(x));
                    ShowConfirmationInquiry(
                        "Sell Low Value Items",
                        $"Sell {itemsToSell.Count} low value item(s) for approximately {totalValue} denars?",
                        () => SellItems(itemsToSell, "low value"));
                }
                else
                {
                    SellItems(itemsToSell, "low value");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error selling low value items: {ex.Message}", Colors.Red);
            }
        }

        public void ExecuteUnequipAll()
        {
            try
            {
                int unequippedCount = 0;

                var mainHero = Hero.MainHero;
                if (mainHero == null)
                {
                    ShowMessage("No character found.", Colors.Yellow);
                    return;
                }

                var equipment = mainHero.BattleEquipment;
                if (equipment == null)
                {
                    ShowMessage("No equipment found.", Colors.Yellow);
                    return;
                }

                var slotsToUnequip = new[]
                {
                    EquipmentIndex.Head,
                    EquipmentIndex.Cape,
                    EquipmentIndex.Body,
                    EquipmentIndex.Gloves,
                    EquipmentIndex.Leg,
                    EquipmentIndex.Horse,
                    EquipmentIndex.HorseHarness,
                    EquipmentIndex.Weapon0,
                    EquipmentIndex.Weapon1,
                    EquipmentIndex.Weapon2,
                    EquipmentIndex.Weapon3
                };

                foreach (var slot in slotsToUnequip)
                {
                    try
                    {
                        if (!equipment[slot].IsEmpty)
                        {
                            if (TryUnequipSlot(slot))
                            {
                                unequippedCount++;
                            }
                        }
                    }
                    catch { /* Skip failed slots */ }
                }

                if (unequippedCount > 0)
                {
                    ShowMessage($"Unequipped {unequippedCount} item(s).", Colors.Green);
                    PlaySound();
                    RefreshInventory();
                }
                else
                {
                    ShowMessage("No items to unequip.", Colors.Yellow);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error unequipping items: {ex.Message}", Colors.Red);
            }
        }

        #endregion

        #region Item Collection Methods

        private List<SPItemVM> GetDamagedItems(Settings settings)
        {
            var damagedItems = new List<SPItemVM>();
            float threshold = settings.GetEffectiveDamageThreshold();

            var rightItemList = GetRightItemList();
            if (rightItemList == null) return damagedItems;

            foreach (var item in rightItemList)
            {
                if (item != null && IsDamagedItem(item, threshold, settings))
                {
                    damagedItems.Add(item);
                }
            }

            return damagedItems;
        }

        private List<SPItemVM> GetLowValueItems(Settings settings)
        {
            var lowValueItems = new List<SPItemVM>();
            int threshold = settings.LowValueThreshold;

            var rightItemList = GetRightItemList();
            if (rightItemList == null) return lowValueItems;

            foreach (var item in rightItemList)
            {
                if (item != null && IsLowValueItem(item, threshold, settings))
                {
                    lowValueItems.Add(item);
                }
            }

            return lowValueItems;
        }

        private IEnumerable<SPItemVM> GetRightItemList()
        {
            if (_inventoryVM == null) return Enumerable.Empty<SPItemVM>();

            try
            {
                // Try multiple property/field names for version compatibility
                string[] propertyNames = {
                    "RightItemListVM",
                    "PlayerInventoryItems", 
                    "PlayerSideItems",
                    "RightItems"
                };
                
                string[] fieldNames = {
                    "_rightItemListVM",
                    "_playerInventoryItems",
                    "_playerSideItems",
                    "_rightItems"
                };

                // Try properties first
                foreach (var propName in propertyNames)
                {
                    var prop = _inventoryVM.GetType().GetProperty(propName, 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var list = prop.GetValue(_inventoryVM);
                        if (list is IEnumerable<SPItemVM> enumerable)
                            return enumerable.ToList();
                    }
                }

                // Try fields
                foreach (var fieldName in fieldNames)
                {
                    var field = _inventoryVM.GetType().GetField(fieldName,
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var list = field.GetValue(_inventoryVM);
                        if (list is IEnumerable<SPItemVM> enumerable)
                            return enumerable.ToList();
                    }
                }
            }
            catch { }

            return Enumerable.Empty<SPItemVM>();
        }

        #endregion

        #region Item Property Access (Reflection-based)

        private bool IsDamagedItem(SPItemVM itemVM, float threshold, Settings settings)
        {
            try
            {
                var itemObject = GetItemObject(itemVM);
                if (itemObject == null) return false;

                // Check if equipped
                if (!settings.SellDamagedEquipped && GetIsEquipped(itemVM))
                    return false;

                // Check horse exclusion
                if (settings.ExcludeDamagedHorses && itemObject.IsMountable)
                    return false;

                // Check item modifier
                var modifier = GetItemModifier(itemVM);
                if (modifier != null)
                {
                    // Get PriceMultiplier via reflection
                    var priceProp = modifier.GetType().GetProperty("PriceMultiplier");
                    if (priceProp != null)
                    {
                        float priceMultiplier = (float)priceProp.GetValue(modifier);
                        float modifierValue = priceMultiplier - 1f;
                        return modifierValue < threshold;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool IsLowValueItem(SPItemVM itemVM, int threshold, Settings settings)
        {
            try
            {
                var itemObject = GetItemObject(itemVM);
                if (itemObject == null) return false;

                // Check if equipped
                if (!settings.SellLowValueEquipped && GetIsEquipped(itemVM))
                    return false;

                // Check exclusions
                if (settings.ExcludeLowValueHorses && itemObject.IsMountable)
                    return false;

                if (settings.ExcludeLowValueFood && itemObject.IsFood)
                    return false;

                if (settings.ExcludeLowValueTradeGoods && itemObject.IsTradeGood)
                    return false;

                int value = GetItemValue(itemVM);
                return value <= threshold;
            }
            catch
            {
                return false;
            }
        }

        private ItemObject GetItemObject(SPItemVM itemVM)
        {
            try
            {
                // Try ItemRosterElement.EquipmentElement.Item path
                var rosterElement = GetPropertyValue(itemVM, "ItemRosterElement");
                if (rosterElement != null)
                {
                    var equipElement = GetPropertyValue(rosterElement, "EquipmentElement");
                    if (equipElement != null)
                    {
                        var item = GetPropertyValue(equipElement, "Item");
                        if (item is ItemObject itemObj)
                            return itemObj;
                    }
                }

                // Try direct Item property
                var directItem = GetPropertyValue(itemVM, "Item");
                if (directItem is ItemObject directItemObj)
                    return directItemObj;

                // Try through StringId lookup
                var stringId = GetPropertyValue(itemVM, "StringId") as string;
                if (!string.IsNullOrEmpty(stringId))
                {
                    return Game.Current?.ObjectManager?.GetObject<ItemObject>(stringId);
                }
            }
            catch { }

            return null;
        }

        private object GetItemModifier(SPItemVM itemVM)
        {
            try
            {
                // Try ItemRosterElement.EquipmentElement.ItemModifier path
                var rosterElement = GetPropertyValue(itemVM, "ItemRosterElement");
                if (rosterElement != null)
                {
                    var equipElement = GetPropertyValue(rosterElement, "EquipmentElement");
                    if (equipElement != null)
                    {
                        return GetPropertyValue(equipElement, "ItemModifier");
                    }
                }
            }
            catch { }

            return null;
        }

        private bool GetIsEquipped(SPItemVM itemVM)
        {
            try
            {
                // Try IsEquipped property
                var isEquipped = GetPropertyValue(itemVM, "IsEquipped");
                if (isEquipped is bool b)
                    return b;

                // Try InventorySide check
                var side = GetPropertyValue(itemVM, "InventorySide");
                if (side != null)
                {
                    return side.ToString().Contains("Equipment");
                }
            }
            catch { }

            return false;
        }

        private int GetItemValue(SPItemVM itemVM)
        {
            try
            {
                // Try ItemCost
                var cost = GetPropertyValue(itemVM, "ItemCost");
                if (cost is int c)
                    return c;

                // Try ItemValue
                var value = GetPropertyValue(itemVM, "ItemValue");
                if (value is int v)
                    return v;

                // Try getting from ItemObject
                var itemObject = GetItemObject(itemVM);
                if (itemObject != null)
                {
                    return itemObject.Value;
                }
            }
            catch { }

            return 0;
        }

        private object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null) return null;

            try
            {
                var type = obj.GetType();

                // Try property
                var prop = type.GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                    return prop.GetValue(obj);

                // Try field
                var field = type.GetField(propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return field.GetValue(obj);

                // Try with underscore prefix for backing fields
                field = type.GetField("_" + char.ToLower(propertyName[0]) + propertyName.Substring(1),
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return field.GetValue(obj);
            }
            catch { }

            return null;
        }

        #endregion

        #region Item Transfer Methods

        private void SellItems(List<SPItemVM> items, string itemType)
        {
            int soldCount = 0;
            int totalValue = 0;

            foreach (var item in items.ToList())
            {
                try
                {
                    int price = GetItemValue(item);
                    if (TransferItem(item))
                    {
                        soldCount++;
                        totalValue += price;
                    }
                }
                catch { /* Skip failed items */ }
            }

            if (soldCount > 0)
            {
                ShowMessage($"Sold {soldCount} {itemType} item(s) for approximately {totalValue} denars.", Colors.Green);
                PlaySound();
                RefreshInventory();
            }
        }

        private bool TransferItem(SPItemVM itemVM)
        {
            try
            {
                // The proper way: Use ExecuteSell on the item itself
                // This calls the ProcessSellItem delegate that SPInventoryVM sets up
                
                // Method 1: Direct ExecuteSellItem (best approach)
                var executeSellMethod = itemVM.GetType().GetMethod("ExecuteSellItem",
                    BindingFlags.Public | BindingFlags.Instance);
                if (executeSellMethod != null)
                {
                    executeSellMethod.Invoke(itemVM, null);
                    return true;
                }

                // Method 2: ExecuteSell with amount
                var executeSellAmount = itemVM.GetType().GetMethod("ExecuteSell",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(int) },
                    null);
                if (executeSellAmount != null)
                {
                    int amount = itemVM.ItemCount > 0 ? itemVM.ItemCount : 1;
                    executeSellAmount.Invoke(itemVM, new object[] { amount });
                    return true;
                }

                // Method 3: Use ProcessSellItem static delegate directly
                var processSellField = typeof(SPItemVM).GetField("ProcessSellItem",
                    BindingFlags.Public | BindingFlags.Static);
                if (processSellField != null)
                {
                    var processSellDelegate = processSellField.GetValue(null) as Action<SPItemVM, bool>;
                    if (processSellDelegate != null)
                    {
                        processSellDelegate(itemVM, false);
                        return true;
                    }
                }

                // Method 4: Use TransferCommand through InventoryLogic
                var logicField = _inventoryVM.GetType().GetField("_inventoryLogic",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (logicField != null)
                {
                    var logic = logicField.GetValue(_inventoryVM) as InventoryLogic;
                    if (logic != null && itemVM.ItemRosterElement.Amount > 0)
                    {
                        // Create transfer command to move from player to other inventory (sell)
                        var transferMethod = typeof(TransferCommand).GetMethod("Transfer",
                            BindingFlags.Public | BindingFlags.Static);
                        if (transferMethod != null)
                        {
                            // Get current character
                            var currentCharField = _inventoryVM.GetType().GetField("_currentCharacter",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            var currentChar = currentCharField?.GetValue(_inventoryVM) as CharacterObject;

                            // TransferCommand.Transfer(amount, fromSide, toSide, element, fromEquipIndex, toEquipIndex, character)
                            var command = transferMethod.Invoke(null, new object[] {
                                itemVM.ItemRosterElement.Amount,
                                InventoryLogic.InventorySide.PlayerInventory,
                                InventoryLogic.InventorySide.OtherInventory,
                                itemVM.ItemRosterElement,
                                EquipmentIndex.None,
                                EquipmentIndex.None,
                                currentChar
                            });

                            if (command != null)
                            {
                                var addCommandMethod = typeof(InventoryLogic).GetMethod("AddTransferCommand",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (addCommandMethod != null)
                                {
                                    addCommandMethod.Invoke(logic, new object[] { command });
                                    return true;
                                }
                            }
                        }
                    }
                }

                // Method 5: ExecuteSellSingle as fallback
                var sellSingleMethod = itemVM.GetType().GetMethod("ExecuteSellSingle",
                    BindingFlags.Public | BindingFlags.Instance);
                if (sellSingleMethod != null)
                {
                    // Call it for each item in the stack
                    int count = itemVM.ItemCount > 0 ? itemVM.ItemCount : 1;
                    for (int i = 0; i < count; i++)
                    {
                        sellSingleMethod.Invoke(itemVM, null);
                    }
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ShowMessage($"Transfer failed: {ex.Message}", Colors.Red);
                return false;
            }
        }

        private bool TryUnequipSlot(EquipmentIndex slot)
        {
            try
            {
                var vmType = _inventoryVM.GetType();

                // Try UnequipEquipment method
                var unequipMethod = vmType.GetMethod("UnequipEquipment",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (unequipMethod != null)
                {
                    unequipMethod.Invoke(_inventoryVM, new object[] { slot });
                    return true;
                }

                // Try ProcessEquipmentSlot
                var processMethod = vmType.GetMethod("ProcessEquipmentSlot",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (processMethod != null)
                {
                    processMethod.Invoke(_inventoryVM, new object[] { slot, EquipmentElement.Invalid });
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshInventory()
        {
            try
            {
                var vmType = _inventoryVM.GetType();

                var refreshMethod = vmType.GetMethod("RefreshValues",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                refreshMethod?.Invoke(_inventoryVM, null);

                var removeZeroMethod = vmType.GetMethod("ExecuteRemoveZeroCounts",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                removeZeroMethod?.Invoke(_inventoryVM, null);
            }
            catch { }
        }

        #endregion

        #region UI Helper Methods

        private void ShowMessage(string message, Color color)
        {
            InformationManager.DisplayMessage(new InformationMessage(message, color));
        }

        private void ShowConfirmationInquiry(string title, string text, Action onConfirm)
        {
            InformationManager.ShowInquiry(
                new InquiryData(
                    title,
                    text,
                    true,
                    true,
                    "Yes",
                    "No",
                    onConfirm,
                    null),
                true);
        }

        private void PlaySound()
        {
            if (Settings.Instance?.PlaySounds != true) return;

            try
            {
                // Sound playing varies by version, try different approaches
                var soundEventMethod = typeof(InformationManager).GetMethod("AddSystemNotification",
                    BindingFlags.Public | BindingFlags.Static);
                
                // If no sound method available, just skip silently
            }
            catch { }
        }

        #endregion
    }
}
