using TaleWorlds.Library;

namespace HeirOfNumenor.Features.EquipPresets.UI
{
    /// <summary>
    /// ViewModel for the Presets button overlay.
    /// </summary>
    public class PresetsOverlayVM : ViewModel
    {
        private string _buttonText = "Presets";

        [DataSourceProperty]
        public string ButtonText
        {
            get => _buttonText;
            set
            {
                if (_buttonText != value)
                {
                    _buttonText = value;
                    OnPropertyChanged(nameof(ButtonText));
                }
            }
        }

        /// <summary>
        /// Called when the Presets button is clicked.
        /// Command binding works by naming convention (Execute prefix).
        /// </summary>
        public void ExecuteOpenPresets()
        {
            InformationManager.DisplayMessage(new InformationMessage(
                "[EquipPresets] Presets button clicked!", Colors.Cyan));
            
            HoNEquipmentPresetPatches.OpenPresetMenu();
        }
    }
}
