using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using HeirOfNumenor.Features.EquipPresets.Data;

namespace HeirOfNumenor.Features.EquipPresets.UI
{
    /// <summary>
    /// Provides Equipment Preset functionality for SPInventoryVM.
    /// Use the "Presets" button in the inventory to open the menu.
    /// </summary>
    [HarmonyPatch(typeof(SPInventoryVM))]
    public static class HoNEquipmentPresetPatches
    {
        private static SPInventoryVM _currentVM;

        /// <summary>
        /// Postfix on RefreshValues to keep track of current VM.
        /// </summary>
        [HarmonyPatch("RefreshValues")]
        [HarmonyPostfix]
        public static void RefreshValues_Postfix(SPInventoryVM __instance)
        {
            _currentVM = __instance;
        }

        /// <summary>
        /// Sync lock state for all preset items.
        /// Called by the mixin on refresh.
        /// </summary>
        public static void SyncLocks(SPInventoryVM vm)
        {
            if (vm == null) return;
            
            try
            {
                PresetManager.SyncPresetLocks(Hero.MainHero, vm);
            }
            catch (Exception)
            {
                // Silently ignore - locks are a nice-to-have
            }
        }

        /// <summary>
        /// Opens the preset context menu.
        /// Called by the mixin when Presets button is clicked.
        /// </summary>
        public static void OpenPresetMenu(SPInventoryVM vm = null)
        {
            vm = vm ?? _currentVM;
            if (vm == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[EquipPresets] Inventory not available", Colors.Red));
                return;
            }

            _currentVM = vm;

            try
            {
                var behavior = EquipmentPresetCampaignBehavior.Instance;
                var presets = behavior?.GetPresetsForHero(Hero.MainHero) ?? new List<HoNEquipmentPreset>();

                var menuItems = new List<InquiryElement>();

                // Save current equipment option
                menuItems.Add(new InquiryElement(
                    "save_new",
                    "Save Current Equipment...",
                    null,
                    true,
                    "Save your current equipment as a new preset"));

                // Add existing presets
                if (presets.Count > 0)
                {
                    // Add each preset as a load option
                    foreach (var preset in presets)
                    {
                        int itemCount = preset.Items.Count(i => !i.IsEmpty);
                        menuItems.Add(new InquiryElement(
                            $"load_{preset.Id}",
                            $"{preset.Name} ({itemCount} items)",
                            null,
                            true,
                            $"Load preset: {preset.Name}"));
                    }

                    // Management options
                    menuItems.Add(new InquiryElement(
                        "update",
                        "Update Existing Preset...",
                        null,
                        true,
                        "Overwrite a preset with current equipment"));

                    menuItems.Add(new InquiryElement(
                        "delete",
                        "Delete Preset...",
                        null,
                        true,
                        "Delete a saved preset"));
                }

                ShowMultiSelectionInquiry(
                    "Equipment Presets",
                    presets.Count > 0 
                        ? $"You have {presets.Count} saved preset(s). Select an action:"
                        : "No presets saved yet. Save your current equipment to create one.",
                    menuItems,
                    OnPresetMenuSelected);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[EquipPresets] Error: {ex.Message}", Colors.Red));
            }
        }

        private static void OnPresetMenuSelected(List<InquiryElement> selectedElements)
        {
            if (selectedElements == null || selectedElements.Count == 0)
                return;

            var selected = selectedElements[0];
            var id = selected.Identifier as string;

            if (string.IsNullOrEmpty(id))
                return;

            try
            {
                if (id == "save_new")
                {
                    ShowSavePresetDialog();
                }
                else if (id.StartsWith("load_"))
                {
                    var presetId = id.Substring(5);
                    LoadPresetById(presetId);
                }
                else if (id == "update")
                {
                    ShowUpdatePresetMenu();
                }
                else if (id == "delete")
                {
                    ShowDeletePresetMenu();
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[EquipPresets] Error: {ex.Message}", Colors.Red));
            }
        }

        #region Save Preset

        private static void ShowSavePresetDialog()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "Save Equipment Preset",
                "Enter a name for this preset:",
                true,
                true,
                "Save",
                "Cancel",
                OnSavePresetConfirmed,
                null,
                false,
                null,
                "",
                "My Preset"));
        }

        private static void OnSavePresetConfirmed(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Preset name cannot be empty", Colors.Red));
                return;
            }

            var hero = Hero.MainHero;
            PresetManager.SavePreset(hero, presetName.Trim(), _currentVM);
            SyncLocks(_currentVM);
        }

        #endregion

        #region Load Preset

        private static void LoadPresetById(string presetId)
        {
            var behavior = EquipmentPresetCampaignBehavior.Instance;
            if (behavior == null) return;

            var presets = behavior.GetPresetsForHero(Hero.MainHero);
            var preset = presets.FirstOrDefault(p => p.Id == presetId);

            if (preset == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Preset not found", Colors.Red));
                return;
            }

            var inventoryLogic = GetInventoryLogic(_currentVM);
            if (inventoryLogic != null)
            {
                PresetManager.LoadPreset(Hero.MainHero, preset, inventoryLogic);
                _currentVM?.RefreshValues();
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Could not access inventory", Colors.Red));
            }
        }

        #endregion

        #region Update Preset

        private static void ShowUpdatePresetMenu()
        {
            var behavior = EquipmentPresetCampaignBehavior.Instance;
            var presets = behavior?.GetPresetsForHero(Hero.MainHero) ?? new List<HoNEquipmentPreset>();

            if (presets.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "No presets to update", Colors.Yellow));
                return;
            }

            var menuItems = presets.Select(p => new InquiryElement(
                p.Id,
                p.Name,
                null,
                true,
                $"Overwrite '{p.Name}' with current equipment")).ToList();

            ShowMultiSelectionInquiry(
                "Update Preset",
                "Select preset to overwrite with current equipment:",
                menuItems,
                OnUpdatePresetSelected);
        }

        private static void OnUpdatePresetSelected(List<InquiryElement> selectedElements)
        {
            if (selectedElements == null || selectedElements.Count == 0)
                return;

            var presetId = selectedElements[0].Identifier as string;
            if (string.IsNullOrEmpty(presetId))
                return;

            var behavior = EquipmentPresetCampaignBehavior.Instance;
            var presets = behavior?.GetPresetsForHero(Hero.MainHero);
            var preset = presets?.FirstOrDefault(p => p.Id == presetId);

            if (preset != null)
            {
                PresetManager.OverwritePreset(Hero.MainHero, preset, _currentVM);
                SyncLocks(_currentVM);
            }
        }

        #endregion

        #region Delete Preset

        private static void ShowDeletePresetMenu()
        {
            var behavior = EquipmentPresetCampaignBehavior.Instance;
            var presets = behavior?.GetPresetsForHero(Hero.MainHero) ?? new List<HoNEquipmentPreset>();

            if (presets.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "No presets to delete", Colors.Yellow));
                return;
            }

            var menuItems = presets.Select(p => new InquiryElement(
                p.Id,
                p.Name,
                null,
                true,
                $"Delete preset '{p.Name}'")).ToList();

            ShowMultiSelectionInquiry(
                "Delete Preset",
                "Select preset to delete:",
                menuItems,
                OnDeletePresetSelected);
        }

        private static void OnDeletePresetSelected(List<InquiryElement> selectedElements)
        {
            if (selectedElements == null || selectedElements.Count == 0)
                return;

            var presetId = selectedElements[0].Identifier as string;
            if (string.IsNullOrEmpty(presetId))
                return;

            var behavior = EquipmentPresetCampaignBehavior.Instance;
            var presets = behavior?.GetPresetsForHero(Hero.MainHero);
            var preset = presets?.FirstOrDefault(p => p.Id == presetId);

            if (preset != null)
            {
                // Confirmation dialog
                InformationManager.ShowInquiry(new InquiryData(
                    "Confirm Delete",
                    $"Are you sure you want to delete '{preset.Name}'?",
                    true,
                    true,
                    "Delete",
                    "Cancel",
                    () => ConfirmDeletePreset(preset),
                    null));
            }
        }

        private static void ConfirmDeletePreset(HoNEquipmentPreset preset)
        {
            PresetManager.DeletePreset(Hero.MainHero, preset, _currentVM);
            SyncLocks(_currentVM);
        }

        #endregion

        #region Helpers

        private static void ShowMultiSelectionInquiry(string title, string description, 
            List<InquiryElement> items, Action<List<InquiryElement>> onSelect)
        {
            var inquiryData = new MultiSelectionInquiryData(
                title,
                description,
                items,
                true,
                1,
                1,
                "Select",
                "Cancel",
                onSelect,
                null);

            // Try MBInformationManager first (newer versions)
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

            // Fallback
            var method = AccessTools.Method(typeof(InformationManager), "ShowMultiSelectionInquiry",
                new[] { typeof(MultiSelectionInquiryData), typeof(bool) });
            if (method != null)
            {
                method.Invoke(null, new object[] { inquiryData, true });
            }
        }

        private static InventoryLogic GetInventoryLogic(SPInventoryVM vm)
        {
            if (vm == null) return null;
            var field = AccessTools.Field(typeof(SPInventoryVM), "_inventoryLogic");
            return field?.GetValue(vm) as InventoryLogic;
        }

        #endregion
    }
}
