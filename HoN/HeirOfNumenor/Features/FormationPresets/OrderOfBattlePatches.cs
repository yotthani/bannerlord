using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace HeirOfNumenor.Features.FormationPresets
{
    /// <summary>
    /// Patches to add "Assign Characters" and "Manage Presets" functionality to Order of Battle screen.
    /// The buttons are added via XML modification, this provides the command implementations.
    /// </summary>
    public static class OrderOfBattlePatches
    {
        private static OrderOfBattleVM _currentOOBVM = null;

        /// <summary>
        /// Gets the current OOB ViewModel if available.
        /// </summary>
        public static OrderOfBattleVM CurrentOOBVM => _currentOOBVM;

        /// <summary>
        /// Patch OrderOfBattleVM constructor to capture reference.
        /// </summary>
        [HarmonyPatch(typeof(OrderOfBattleVM))]
        public static class OrderOfBattleVM_Ctor_Patch
        {
            [HarmonyTargetMethods]
            public static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
            {
                foreach (var ctor in typeof(OrderOfBattleVM).GetConstructors())
                {
                    yield return ctor;
                }
            }

            [HarmonyPostfix]
            public static void Postfix(OrderOfBattleVM __instance)
            {
                _currentOOBVM = __instance;
                HoNFormationPresetMenu.SetCurrentVM(__instance);
            }
        }

        /// <summary>
        /// Patch to detect when OOB screen is closed.
        /// </summary>
        [HarmonyPatch(typeof(OrderOfBattleVM), "OnFinalize")]
        public static class OrderOfBattleVM_OnFinalize_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                _currentOOBVM = null;
                OOBButtonInjector.Reset();
            }
        }

        /// <summary>
        /// Patch Mission.OnTick to handle hotkeys during OOB screen.
        /// Hotkeys:
        /// - Ctrl+A: Auto-assign heroes
        /// - Ctrl+P: Open presets menu
        /// - Ctrl+S: Quick save preset
        /// - Ctrl+L: Load last preset
        /// </summary>
        [HarmonyPatch(typeof(Mission), "OnTick")]
        public static class Mission_OnTick_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (_currentOOBVM == null) return;

                try
                {
                    bool ctrlHeld = Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
                    
                    if (ctrlHeld)
                    {
                        if (Input.IsKeyPressed(InputKey.A))
                        {
                            ExecuteAutoAssign();
                        }
                        else if (Input.IsKeyPressed(InputKey.P))
                        {
                            ExecuteOpenPresetsMenu();
                        }
                        else if (Input.IsKeyPressed(InputKey.S))
                        {
                            ExecuteQuickSavePreset();
                        }
                        else if (Input.IsKeyPressed(InputKey.L))
                        {
                            ExecuteLoadLastPreset();
                        }
                    }
                }
                catch (Exception)
                {
                    // Silently fail input handling
                }
            }
        }

        /// <summary>
        /// Execute auto-assign command - assigns heroes to formations based on their combat role.
        /// Shows inquiry dialog to ask about resetting existing assignments.
        /// </summary>
        public static void ExecuteAutoAssign()
        {
            if (_currentOOBVM == null) return;
            
            // Check if there are assigned heroes
            bool hasAssigned = CheckHasAssignedHeroes(_currentOOBVM);
            
            if (hasAssigned)
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "Auto-Assign Heroes",
                    "Do you want to reset existing hero assignments before auto-assigning?",
                    true, true,
                    "Reset All", "Keep Existing",
                    () => HeroAutoAssigner.AutoAssignHeroes(_currentOOBVM, resetExisting: true),
                    () => HeroAutoAssigner.AutoAssignHeroes(_currentOOBVM, resetExisting: false)
                ));
            }
            else
            {
                HeroAutoAssigner.AutoAssignHeroes(_currentOOBVM, resetExisting: false);
            }
        }

        private static bool CheckHasAssignedHeroes(OrderOfBattleVM vm)
        {
            try
            {
                var unassignedProp = AccessTools.Property(typeof(OrderOfBattleVM), "UnassignedHeroes");
                var unassignedHeroes = unassignedProp?.GetValue(vm) as MBBindingList<OrderOfBattleHeroItemVM>;
                
                var allHeroesField = AccessTools.Field(typeof(OrderOfBattleVM), "_allHeroes");
                var allHeroes = allHeroesField?.GetValue(vm) as System.Collections.Generic.List<OrderOfBattleHeroItemVM>;

                if (allHeroes != null && unassignedHeroes != null)
                {
                    return unassignedHeroes.Count < allHeroes.Count;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Opens the presets context menu.
        /// </summary>
        public static void ExecuteOpenPresetsMenu()
        {
            if (_currentOOBVM == null) return;
            HoNFormationPresetMenu.OpenPresetMenu(_currentOOBVM);
        }

        /// <summary>
        /// Execute quick save preset command with text inquiry for name.
        /// </summary>
        public static void ExecuteQuickSavePreset()
        {
            if (_currentOOBVM == null) return;
            
            int presetNum = FormationPresetManager.Presets.Count + 1;
            string defaultName = $"Formation {presetNum}";
            
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "Save Formation Preset",
                "Enter a name for this preset:",
                true,
                true,
                "Save",
                "Cancel",
                (name) => {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        FormationPresetManager.SaveCurrentAsPreset(_currentOOBVM, name.Trim());
                    }
                },
                null,
                false,
                null,
                "",
                defaultName));
        }

        /// <summary>
        /// Execute load last preset command with confirmation.
        /// </summary>
        public static void ExecuteLoadLastPreset()
        {
            if (_currentOOBVM == null) return;

            var presets = FormationPresetManager.Presets;
            if (presets.Count > 0)
            {
                var lastPreset = presets[presets.Count - 1];
                
                // Check if there are assigned heroes
                if (CheckHasAssignedHeroes(_currentOOBVM))
                {
                    InformationManager.ShowInquiry(new InquiryData(
                        "Load Formation Preset",
                        $"Load preset '{lastPreset.Name}'?\nThis will override your current hero assignments.",
                        true, true,
                        "Load", "Cancel",
                        () => FormationPresetManager.LoadPreset(_currentOOBVM, lastPreset),
                        () => { }
                    ));
                }
                else
                {
                    FormationPresetManager.LoadPreset(_currentOOBVM, lastPreset);
                }
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "No saved presets. Press Ctrl+S to save or Ctrl+P for menu.", Colors.Yellow));
            }
        }
    }
}
