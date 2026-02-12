using System;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.ScreenSystem;

namespace HeirOfNumenor.Features.FormationPresets
{
    /// <summary>
    /// Injects "Assign Characters" and "Manage Presets" buttons into the Order of Battle screen
    /// using a GauntletLayer overlay, same approach as inventory presets.
    /// </summary>
    public static class OOBButtonInjector
    {
        private static GauntletLayer _buttonsLayer = null;
        private static OOBButtonsVM _buttonsVM = null;
        private static bool _layerAdded = false;

        /// <summary>
        /// Patch MissionGauntletOrderOfBattleUIHandler.OnMissionScreenInitialize to add our buttons layer.
        /// </summary>
        [HarmonyPatch(typeof(MissionGauntletOrderOfBattleUIHandler), "OnMissionScreenInitialize")]
        public static class OOBUIHandler_Init_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(MissionGauntletOrderOfBattleUIHandler __instance)
            {
                try
                {
                    _layerAdded = false;

                    // Get the MissionScreen
                    MissionScreen missionScreen = null;
                    
                    var missionScreenProp = AccessTools.Property(typeof(MissionView), "MissionScreen");
                    if (missionScreenProp != null)
                    {
                        missionScreen = missionScreenProp.GetValue(__instance) as MissionScreen;
                    }

                    if (missionScreen == null)
                    {
                        missionScreen = ScreenManager.TopScreen as MissionScreen;
                    }

                    if (missionScreen == null) return;

                    // Create ViewModel
                    _buttonsVM = new OOBButtonsVM();

                    // Create a new GauntletLayer for our overlay
                    _buttonsLayer = new GauntletLayer("OOBButtonsLayer", 200);

                    // Load our XML movie
                    _buttonsLayer.LoadMovie("OOBButtonsOverlay", _buttonsVM);

                    // Add the layer to the screen
                    missionScreen.AddLayer(_buttonsLayer);

                    // Make sure input works
                    _buttonsLayer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.All);

                    _layerAdded = true;
                }
                catch (Exception)
                {
                    // Silently fail - buttons just won't appear
                }
            }
        }

        /// <summary>
        /// Patch to clean up when OOB screen closes.
        /// </summary>
        [HarmonyPatch(typeof(MissionGauntletOrderOfBattleUIHandler), "OnMissionScreenFinalize")]
        public static class OOBUIHandler_Finalize_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(MissionGauntletOrderOfBattleUIHandler __instance)
            {
                try
                {
                    if (_layerAdded && _buttonsLayer != null)
                    {
                        var missionScreen = ScreenManager.TopScreen as MissionScreen;
                        if (missionScreen != null)
                        {
                            missionScreen.RemoveLayer(_buttonsLayer);
                        }
                    }
                }
                catch { }
                finally
                {
                    _buttonsLayer = null;
                    _buttonsVM = null;
                    _layerAdded = false;
                }
            }
        }

        /// <summary>
        /// Called from OrderOfBattlePatches tick - no longer needed with layer approach.
        /// </summary>
        public static void TryInjectButtonsFromTick()
        {
            // Not needed - buttons are added via layer in OnMissionScreenInitialize
        }

        /// <summary>
        /// Reset state.
        /// </summary>
        public static void Reset()
        {
            _buttonsLayer = null;
            _buttonsVM = null;
            _layerAdded = false;
        }
    }
}
