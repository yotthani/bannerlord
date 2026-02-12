using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using HarmonyLib;
using HeirOfNumenor.Features.FormationPresets.Data;

namespace HeirOfNumenor.Features.FormationPresets
{
    /// <summary>
    /// Handles the Formation Preset context menu.
    /// Similar to Equipment Presets menu.
    /// </summary>
    public static class HoNFormationPresetMenu
    {
        private static OrderOfBattleVM _currentOobVM;

        /// <summary>
        /// Sets the current OOB ViewModel reference.
        /// </summary>
        public static void SetCurrentVM(OrderOfBattleVM oobVM)
        {
            _currentOobVM = oobVM;
        }

        /// <summary>
        /// Opens the formation preset context menu.
        /// </summary>
        public static void OpenPresetMenu(OrderOfBattleVM oobVM = null)
        {
            oobVM = oobVM ?? _currentOobVM;
            if (oobVM == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Order of Battle not available", Colors.Red));
                return;
            }

            _currentOobVM = oobVM;

            try
            {
                var presets = FormationPresetManager.Presets;
                var menuItems = new List<InquiryElement>();

                // Save current formation option
                menuItems.Add(new InquiryElement(
                    "save_new",
                    "Save Current Formation...",
                    null,
                    true,
                    "Save your current formation setup as a new preset"));

                // Add existing presets as load options
                if (presets.Count > 0)
                {
                    foreach (var preset in presets)
                    {
                        menuItems.Add(new InquiryElement(
                            $"load_{preset.Id}",
                            $"{preset.Name} ({preset.GetSummary()})",
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
                        "Overwrite a preset with current formation"));

                    menuItems.Add(new InquiryElement(
                        "delete",
                        "Delete Preset...",
                        null,
                        true,
                        "Delete a saved preset"));
                }

                ShowMultiSelectionInquiry(
                    "Formation Presets",
                    presets.Count > 0 
                        ? $"You have {presets.Count} saved preset(s). Select an action:"
                        : "No presets saved yet. Save your current formation to create one.",
                    menuItems,
                    OnPresetMenuSelected);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Error: {ex.Message}", Colors.Red));
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
                    $"Error: {ex.Message}", Colors.Red));
            }
        }

        #region Save Preset

        private static void ShowSavePresetDialog()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "Save Formation Preset",
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
                "My Formation"));
        }

        private static void OnSavePresetConfirmed(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Preset name cannot be empty", Colors.Red));
                return;
            }

            FormationPresetManager.SaveCurrentAsPreset(_currentOobVM, presetName.Trim());
        }

        #endregion

        #region Load Preset

        private static void LoadPresetById(string presetId)
        {
            var preset = FormationPresetManager.GetPresetById(presetId);

            if (preset == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Preset not found", Colors.Red));
                return;
            }

            FormationPresetManager.LoadPreset(_currentOobVM, preset);
        }

        #endregion

        #region Update Preset

        private static void ShowUpdatePresetMenu()
        {
            var presets = FormationPresetManager.Presets;

            if (presets.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "No presets to update", Colors.Yellow));
                return;
            }

            var menuItems = presets.Select(p => new InquiryElement(
                p.Id,
                $"{p.Name} ({p.GetSummary()})",
                null,
                true,
                $"Overwrite '{p.Name}' with current formation")).ToList();

            ShowMultiSelectionInquiry(
                "Update Preset",
                "Select preset to overwrite with current formation:",
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

            var preset = FormationPresetManager.GetPresetById(presetId);
            if (preset != null)
            {
                FormationPresetManager.UpdatePreset(_currentOobVM, preset);
            }
        }

        #endregion

        #region Delete Preset

        private static void ShowDeletePresetMenu()
        {
            var presets = FormationPresetManager.Presets;

            if (presets.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "No presets to delete", Colors.Yellow));
                return;
            }

            var menuItems = presets.Select(p => new InquiryElement(
                p.Id,
                $"{p.Name} ({p.GetSummary()})",
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

            var preset = FormationPresetManager.GetPresetById(presetId);
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
                    () => FormationPresetManager.DeletePreset(preset),
                    null));
            }
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

        #endregion
    }
}
