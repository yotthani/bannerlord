using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using System;

namespace HeirOfNumenor.Features.EquipPresets.UI
{
    /// <summary>
    /// Harmony patch to add the Presets button overlay when inventory screen opens.
    /// </summary>
    [HarmonyPatch(typeof(GauntletInventoryScreen), "OnInitialize")]
    public static class InventoryScreenOpenPatch
    {
        private static GauntletLayer _presetsLayer;
        private static PresetsOverlayVM _presetsVM;

        [HarmonyPostfix]
        public static void Postfix(GauntletInventoryScreen __instance)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[EquipPresets] Adding presets overlay...", Colors.Yellow));

                // Create ViewModel
                _presetsVM = new PresetsOverlayVM();

                // Create a new GauntletLayer for our overlay (layerOrder 100 to be on top)
                _presetsLayer = new GauntletLayer("PresetsLayer", 100);

                // Load our XML movie
                _presetsLayer.LoadMovie("PresetsOverlay", _presetsVM);

                // Add the layer to the screen
                __instance.AddLayer(_presetsLayer);

                // Make sure input is not restricted
                _presetsLayer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.All);

                InformationManager.DisplayMessage(new InformationMessage(
                    "[EquipPresets] Overlay added successfully!", Colors.Green));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[EquipPresets] Error: {ex.Message}", Colors.Red));
            }
        }
    }

    /// <summary>
    /// Harmony patch to clean up when inventory screen closes.
    /// </summary>
    [HarmonyPatch(typeof(GauntletInventoryScreen), "OnFinalize")]
    public static class InventoryScreenClosePatch
    {
        [HarmonyPrefix]
        public static void Prefix(GauntletInventoryScreen __instance)
        {
            try
            {
                // Clean up is handled by the screen itself
            }
            catch { }
        }
    }
}
