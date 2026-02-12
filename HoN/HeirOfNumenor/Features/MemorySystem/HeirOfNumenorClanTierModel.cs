using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace HeirOfNumenor.Features.MemorySystem
{
    /// <summary>
    /// Custom ClanTierModel that adds bonus companion slots for promoted captains.
    /// </summary>
    public class HeirOfNumenorClanTierModel : DefaultClanTierModel
    {
        /// <summary>
        /// Override GetCompanionLimit to add our bonus from settings.
        /// </summary>
        public override int GetCompanionLimit(Clan clan)
        {
            // Get the base limit from native calculation
            int baseLimit = base.GetCompanionLimit(clan);

            // Add our bonus if this is the player clan
            if (clan == Clan.PlayerClan)
            {
                try
                {
                    baseLimit += CaptainPromotionManager.GetAdditionalCompanionLimit();
                }
                catch
                {
                    // Fail silently
                }
            }

            return baseLimit;
        }
    }
}
