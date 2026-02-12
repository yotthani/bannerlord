using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Navigation element for the Ring System.
    /// </summary>
    public class RingNavigationElement : INavigationElement
    {
        public string StringId => "rings";

        public NavigationPermissionItem Permission
        {
            get
            {
                if (Campaign.Current != null && !IsActive)
                    return new NavigationPermissionItem(true, null);
                return new NavigationPermissionItem(false, new TextObject("{=RingsDisabled}Not available"));
            }
        }

        // Don't lock navigation - allow switching to other screens while Ring screen is open
        public bool IsLockingNavigation => false;
        
        public bool IsActive => Game.Current?.GameStateManager?.ActiveState is HoNRingState;
        
        public TextObject Tooltip => new TextObject("{=RingsTooltip}Rings of Power (R)");
        public bool HasAlert => false;
        public TextObject AlertTooltip => new TextObject("");

        public void OpenView()
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] OpenView called, IsActive={IsActive}", Colors.Cyan));

                if (IsActive)
                {
                    // Close by popping state
                    RingScreenManager.CloseRingScreen();
                }
                else
                {
                    // Open ring screen via GameState
                    RingScreenManager.OpenRingScreen();
                }
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] OpenView error: {ex.Message}", Colors.Red));
            }
        }

        public void OpenView(params object[] parameters) => OpenView();
        public void GoToLink() { }
    }
}
