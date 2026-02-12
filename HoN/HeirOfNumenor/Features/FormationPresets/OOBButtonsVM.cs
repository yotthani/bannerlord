using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace HeirOfNumenor.Features.FormationPresets
{
    /// <summary>
    /// ViewModel for the OOB buttons overlay.
    /// Provides commands for Assign Heroes and Save/Load Presets buttons.
    /// </summary>
    public class OOBButtonsVM : ViewModel
    {
        private string _presetsButtonText;

        public OOBButtonsVM()
        {
            UpdatePresetsButtonText();
        }

        [DataSourceProperty]
        public string PresetsButtonText
        {
            get => _presetsButtonText;
            set
            {
                if (_presetsButtonText != value)
                {
                    _presetsButtonText = value;
                    OnPropertyChangedWithValue(value, "PresetsButtonText");
                }
            }
        }

        private void UpdatePresetsButtonText()
        {
            var presets = FormationPresetManager.Presets;
            if (presets.Count > 0)
            {
                PresetsButtonText = $"Presets ({presets.Count})";
            }
            else
            {
                PresetsButtonText = "Presets";
            }
        }

        /// <summary>
        /// Called when "Assign Heroes" button is clicked.
        /// Shows inquiry to ask about resetting existing assignments.
        /// </summary>
        public void ExecuteAssignCharacters()
        {
            var vm = OrderOfBattlePatches.CurrentOOBVM;
            if (vm == null) return;
            
            // Check if there are any assigned heroes
            bool hasAssignedHeroes = CheckHasAssignedHeroes(vm);
            
            if (hasAssignedHeroes)
            {
                // Ask user if they want to reset existing assignments
                InformationManager.ShowInquiry(new InquiryData(
                    "Auto-Assign Heroes",
                    "Do you want to reset existing hero assignments before auto-assigning?",
                    true, true,
                    "Reset All", "Keep Existing",
                    () => HeroAutoAssigner.AutoAssignHeroes(vm, resetExisting: true),
                    () => HeroAutoAssigner.AutoAssignHeroes(vm, resetExisting: false)
                ));
            }
            else
            {
                // No existing assignments, just auto-assign
                HeroAutoAssigner.AutoAssignHeroes(vm, resetExisting: false);
            }
        }

        /// <summary>
        /// Check if there are any heroes currently assigned to formations.
        /// </summary>
        private bool CheckHasAssignedHeroes(OrderOfBattleVM vm)
        {
            try
            {
                var unassignedProp = HarmonyLib.AccessTools.Property(
                    typeof(OrderOfBattleVM), "UnassignedHeroes");
                var unassignedHeroes = unassignedProp?.GetValue(vm) as MBBindingList<OrderOfBattleHeroItemVM>;
                
                var allHeroesField = HarmonyLib.AccessTools.Field(
                    typeof(OrderOfBattleVM), "_allHeroes");
                var allHeroes = allHeroesField?.GetValue(vm) as System.Collections.Generic.List<OrderOfBattleHeroItemVM>;

                if (allHeroes != null && unassignedHeroes != null)
                {
                    // If unassigned count is less than total, some are assigned
                    return unassignedHeroes.Count < allHeroes.Count;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Called when "Presets" button is clicked.
        /// Opens the context menu for formation presets.
        /// </summary>
        public void ExecuteManagePresets()
        {
            var vm = OrderOfBattlePatches.CurrentOOBVM;
            HoNFormationPresetMenu.SetCurrentVM(vm);
            HoNFormationPresetMenu.OpenPresetMenu(vm);
            UpdatePresetsButtonText();
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            UpdatePresetsButtonText();
        }
    }
}
