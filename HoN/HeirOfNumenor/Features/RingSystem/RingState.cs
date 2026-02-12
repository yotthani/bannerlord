using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// GameState for the Ring System screen.
    /// Extends PlayerGameState like InventoryState.
    /// </summary>
    public class HoNRingState : PlayerGameState
    {
        public override bool IsMenuState => true;

        /// <summary>
        /// Handler interface for the screen to communicate with the state.
        /// </summary>
        public IHoNRingStateHandler Handler { get; set; }
    }

    /// <summary>
    /// Interface for screen to communicate with HoNRingState.
    /// </summary>
    public interface IHoNRingStateHandler
    {
    }
}
