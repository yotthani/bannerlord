using System;
using System.Collections.Generic;
using System.Linq;
using HeirOfNumenor.Features.EquipPresets.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace HeirOfNumenor.Features.EquipPresets
{
    /// <summary>
    /// Manages equipment preset operations including saving, loading, and lock management.
    /// </summary>
    public static class PresetManager
    {
        #region Save Preset

        /// <summary>
        /// Creates and saves a new preset from the hero's current equipment.
        /// Locks all items in the preset.
        /// </summary>
        public static HoNEquipmentPreset SavePreset(Hero hero, string presetName, SPInventoryVM inventoryVM = null)
        {
            if (hero == null || string.IsNullOrWhiteSpace(presetName))
                return null;

            var behavior = EquipmentPresetCampaignBehavior.Instance;
            if (behavior == null)
                return null;

            // Create preset from current equipment
            var equipment = hero.BattleEquipment;
            var preset = HoNEquipmentPreset.FromEquipment(presetName, equipment);

            // Save the preset
            behavior.AddPreset(hero, preset);

            // Lock items in the preset
            if (inventoryVM != null)
            {
                LockPresetItems(hero, preset, inventoryVM);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"Saved equipment preset: {presetName}",
                Colors.Green));

            return preset;
        }

        /// <summary>
        /// Overwrites an existing preset with current equipment.
        /// </summary>
        public static void OverwritePreset(Hero hero, HoNEquipmentPreset preset, SPInventoryVM inventoryVM = null)
        {
            if (hero == null || preset == null)
                return;

            var behavior = EquipmentPresetCampaignBehavior.Instance;
            if (behavior == null)
                return;

            // Unlock old items first (if not in other presets)
            if (inventoryVM != null)
            {
                UnlockPresetItems(hero, preset, inventoryVM);
            }

            // Update preset with new equipment
            var equipment = hero.BattleEquipment;
            preset.Items.Clear();
            foreach (var slot in HoNEquipmentPreset.EquipmentSlots)
            {
                preset.Items.Add(new HoNPresetItemReference(equipment[slot], slot));
            }

            behavior.UpdatePreset(hero, preset);

            // Lock new items
            if (inventoryVM != null)
            {
                LockPresetItems(hero, preset, inventoryVM);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"Updated preset: {preset.Name}",
                Colors.Green));
        }

        #endregion

        #region Load Preset

        /// <summary>
        /// Loads a preset, equipping matching items from inventory.
        /// Returns a report of what was equipped and what was missing.
        /// </summary>
        public static PresetLoadResult LoadPreset(Hero hero, HoNEquipmentPreset preset, InventoryLogic inventoryLogic)
        {
            var result = new PresetLoadResult();

            if (hero == null || preset == null || inventoryLogic == null)
                return result;

            var partyRoster = MobileParty.MainParty?.ItemRoster;
            if (partyRoster == null)
                return result;

            var equipment = hero.BattleEquipment;

            foreach (var itemRef in preset.Items)
            {
                if (itemRef.IsEmpty)
                {
                    // Unequip this slot if preset has it empty
                    if (!equipment[itemRef.Slot].IsEmpty)
                    {
                        UnequipSlot(hero, itemRef.Slot, inventoryLogic);
                    }
                    continue;
                }

                // Check if already equipped in correct slot
                if (itemRef.Matches(equipment[itemRef.Slot]))
                {
                    result.AlreadyEquipped.Add(itemRef);
                    continue;
                }

                // Find matching item in inventory
                var matchResult = FindMatchingItem(itemRef, partyRoster, equipment);
                
                if (matchResult.Found)
                {
                    // Equip the item
                    EquipItem(hero, itemRef.Slot, matchResult.Element, matchResult.FromSlot, inventoryLogic);
                    result.Equipped.Add(itemRef);
                }
                else
                {
                    result.Missing.Add(itemRef);
                }
            }

            // Display result message
            DisplayLoadResult(preset.Name, result);

            return result;
        }

        private static void DisplayLoadResult(string presetName, PresetLoadResult result)
        {
            if (result.Missing.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Loaded preset: {presetName}",
                    Colors.Green));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Loaded preset: {presetName} ({result.Missing.Count} items missing)",
                    Colors.Yellow));
            }
        }

        #endregion

        #region Delete Preset

        /// <summary>
        /// Deletes a preset and unlocks items that aren't in other presets.
        /// </summary>
        public static void DeletePreset(Hero hero, HoNEquipmentPreset preset, SPInventoryVM inventoryVM = null)
        {
            if (hero == null || preset == null)
                return;

            var behavior = EquipmentPresetCampaignBehavior.Instance;
            if (behavior == null)
                return;

            // Unlock items first
            if (inventoryVM != null)
            {
                UnlockPresetItems(hero, preset, inventoryVM);
            }

            // Remove the preset
            behavior.RemovePreset(hero, preset);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Deleted preset: {preset.Name}",
                Colors.Gray));
        }

        #endregion

        #region Lock Management

        /// <summary>
        /// Locks all items in a preset.
        /// </summary>
        public static void LockPresetItems(Hero hero, HoNEquipmentPreset preset, SPInventoryVM inventoryVM)
        {
            if (inventoryVM == null || preset == null)
                return;

            foreach (var itemId in preset.GetItemStringIds())
            {
                SetItemLockState(itemId, true, inventoryVM);
            }
        }

        /// <summary>
        /// Unlocks items from a preset that aren't in any other preset.
        /// </summary>
        public static void UnlockPresetItems(Hero hero, HoNEquipmentPreset preset, SPInventoryVM inventoryVM)
        {
            if (inventoryVM == null || preset == null)
                return;

            var behavior = EquipmentPresetCampaignBehavior.Instance;
            if (behavior == null)
                return;

            foreach (var itemId in preset.GetItemStringIds())
            {
                // Only unlock if not in another preset
                bool inOtherPreset = behavior.GetPresetsForHero(hero)
                    .Where(p => p.Id != preset.Id)
                    .Any(p => p.ContainsItemById(itemId));

                if (!inOtherPreset)
                {
                    SetItemLockState(itemId, false, inventoryVM);
                }
            }
        }

        /// <summary>
        /// Syncs lock state for all preset items (call when opening inventory).
        /// </summary>
        public static void SyncPresetLocks(Hero hero, SPInventoryVM inventoryVM)
        {
            if (inventoryVM == null || hero == null)
                return;

            var behavior = EquipmentPresetCampaignBehavior.Instance;
            if (behavior == null)
                return;

            var presetItemIds = behavior.GetAllPresetItemIds(hero);

            foreach (var itemId in presetItemIds)
            {
                SetItemLockState(itemId, true, inventoryVM);
            }
        }

        /// <summary>
        /// Sets the lock state for items matching a StringId.
        /// This hooks into the vanilla lock system.
        /// </summary>
        private static void SetItemLockState(string itemStringId, bool locked, SPInventoryVM inventoryVM)
        {
            // Lock in right panel (party inventory)
            foreach (var item in inventoryVM.RightItemListVM)
            {
                if (item.ItemRosterElement.EquipmentElement.Item?.StringId == itemStringId)
                {
                    item.IsLocked = locked;
                }
            }

            // Lock in left panel (other inventory/stash) if present
            foreach (var item in inventoryVM.LeftItemListVM)
            {
                if (item.ItemRosterElement.EquipmentElement.Item?.StringId == itemStringId)
                {
                    item.IsLocked = locked;
                }
            }
        }

        #endregion

        #region Item Finding & Equipping

        private static (bool Found, EquipmentElement Element, EquipmentIndex? FromSlot) FindMatchingItem(
            HoNPresetItemReference itemRef, 
            ItemRoster roster,
            Equipment currentEquipment)
        {
            // First check if it's already equipped in a different slot
            foreach (var slot in HoNEquipmentPreset.EquipmentSlots)
            {
                if (itemRef.Matches(currentEquipment[slot]))
                {
                    return (true, currentEquipment[slot], slot);
                }
            }

            // Then check party inventory
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (itemRef.Matches(element.EquipmentElement))
                {
                    return (true, element.EquipmentElement, null);
                }
            }

            return (false, EquipmentElement.Invalid, null);
        }

        private static void EquipItem(Hero hero, EquipmentIndex targetSlot, EquipmentElement element, 
            EquipmentIndex? fromSlot, InventoryLogic inventoryLogic)
        {
            var equipment = hero.BattleEquipment;
            
            if (fromSlot.HasValue)
            {
                // Swap from another equipment slot
                var currentInTarget = equipment[targetSlot];
                equipment[targetSlot] = element;
                equipment[fromSlot.Value] = currentInTarget;
            }
            else if (inventoryLogic != null)
            {
                // Use InventoryLogic for proper transfer from inventory to equipment
                try
                {
                    // Create a transfer command to equip the item
                    var transferMethod = typeof(TransferCommand).GetMethod("Transfer",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (transferMethod != null)
                    {
                        // Transfer from player inventory to equipment slot
                        var itemRosterElement = new ItemRosterElement(element, 1);
                        var command = transferMethod.Invoke(null, new object[] {
                            1,
                            InventoryLogic.InventorySide.PlayerInventory,
                            InventoryLogic.InventorySide.BattleEquipment,
                            itemRosterElement,
                            EquipmentIndex.None,
                            targetSlot,
                            hero.CharacterObject
                        });
                        
                        if (command != null)
                        {
                            var addMethod = typeof(InventoryLogic).GetMethod("AddTransferCommand",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            addMethod?.Invoke(inventoryLogic, new object[] { command });
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModSettings.DebugLog("PresetManager", $"Transfer command failed: {ex.Message}, using direct assignment");
                }
                
                // Fallback: Direct equipment assignment
                equipment[targetSlot] = element;
            }
            else
            {
                // No inventory logic - direct assignment only
                equipment[targetSlot] = element;
            }
        }

        private static void UnequipSlot(Hero hero, EquipmentIndex slot, InventoryLogic inventoryLogic)
        {
            var equipment = hero.BattleEquipment;
            if (equipment[slot].IsEmpty) return;
            
            var element = equipment[slot];
            
            if (inventoryLogic != null)
            {
                try
                {
                    // Transfer from equipment to player inventory
                    var transferMethod = typeof(TransferCommand).GetMethod("Transfer",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (transferMethod != null)
                    {
                        var itemRosterElement = new ItemRosterElement(element, 1);
                        var command = transferMethod.Invoke(null, new object[] {
                            1,
                            InventoryLogic.InventorySide.BattleEquipment,
                            InventoryLogic.InventorySide.PlayerInventory,
                            itemRosterElement,
                            slot,
                            EquipmentIndex.None,
                            hero.CharacterObject
                        });
                        
                        if (command != null)
                        {
                            var addMethod = typeof(InventoryLogic).GetMethod("AddTransferCommand",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            addMethod?.Invoke(inventoryLogic, new object[] { command });
                            equipment[slot] = EquipmentElement.Invalid;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModSettings.DebugLog("PresetManager", $"Unequip transfer failed: {ex.Message}");
                }
            }
            
            // Fallback: Direct unequip (item may be lost if not in inventory context)
            equipment[slot] = EquipmentElement.Invalid;
        }

        #endregion
    }

    /// <summary>
    /// Result of loading a preset, showing what was equipped and what was missing.
    /// </summary>
    public class PresetLoadResult
    {
        public List<HoNPresetItemReference> Equipped { get; } = new List<HoNPresetItemReference>();
        public List<HoNPresetItemReference> Missing { get; } = new List<HoNPresetItemReference>();
        public List<HoNPresetItemReference> AlreadyEquipped { get; } = new List<HoNPresetItemReference>();

        public bool FullyLoaded => Missing.Count == 0;
        public int TotalItems => Equipped.Count + Missing.Count + AlreadyEquipped.Count;
    }
}
