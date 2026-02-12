using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.TroopStatus
{
    /// <summary>
    /// Custom party speed model that integrates TroopStatus and Ring System effects.
    /// Effects are visible in the native speed tooltip!
    /// </summary>
    public class HeirOfNumenorPartySpeedModel : PartySpeedModel
    {
        // Text objects for tooltip display
        private static readonly TextObject _fearSlowText = new TextObject("{=hon_fear_slow}Fearful Troops");
        private static readonly TextObject _veteranSpeedText = new TextObject("{=hon_veteran_speed}Battle-Hardened Veterans");
        private static readonly TextObject _highBondingText = new TextObject("{=hon_bonding_speed}Unit Cohesion");
        private static readonly TextObject _ringCorruptionSlowText = new TextObject("{=hon_ring_slow}Ring's Burden");
        private static readonly TextObject _ringSpeedBonusText = new TextObject("{=hon_ring_speed}Ring's Swiftness");

        public override float BaseSpeed => BaseModel.BaseSpeed;

        public override float MinimumSpeed => BaseModel.MinimumSpeed;

        public override ExplainedNumber CalculateBaseSpeed(
            MobileParty mobileParty, 
            bool includeDescriptions = false, 
            int additionalTroopOnFootCount = 0, 
            int additionalTroopOnHorseCount = 0)
        {
            // Get base calculation
            ExplainedNumber result = BaseModel.CalculateBaseSpeed(
                mobileParty, includeDescriptions, 
                additionalTroopOnFootCount, additionalTroopOnHorseCount);

            // Only apply to player party
            if (mobileParty != MobileParty.MainParty) return result;

            try
            {
                ApplyTroopStatusSpeedEffects(mobileParty, ref result);
                ApplyRingSystemSpeedEffects(ref result);
            }
            catch
            {
                // Silently fail if systems aren't loaded
            }

            return result;
        }

        public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
        {
            return BaseModel.CalculateFinalSpeed(mobileParty, finalSpeed);
        }

        private void ApplyTroopStatusSpeedEffects(MobileParty party, ref ExplainedNumber result)
        {
            if (!TroopStatusCampaignBehavior.IsEnabled) return;

            var statusManager = TroopStatusManager.Instance;
            if (statusManager == null) return;

            // Calculate weighted averages
            float totalFear = 0f;
            float totalExperience = 0f;
            float totalBonding = 0f;
            int troopCount = 0;

            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                if (element.Character == null || element.Character.IsHero) continue;

                var status = statusManager.GetTroopStatus(element.Character.StringId);
                if (status == null) continue;

                int count = element.Number;
                totalFear += status.Fear * count;
                totalExperience += status.BattleExperience * count;
                totalBonding += status.Bonding * count;
                troopCount += count;
            }

            if (troopCount == 0) return;

            float avgFear = totalFear / troopCount;
            float avgExperience = totalExperience / troopCount;
            float avgBonding = totalBonding / troopCount;

            // High fear slows the party (troops hesitate, look over shoulders)
            // -5% max speed at 100 fear
            if (avgFear > 30f)
            {
                float fearPenalty = -((avgFear - 30f) / 70f) * 0.05f;
                result.AddFactor(fearPenalty, _fearSlowText);
            }

            // Battle veterans move more efficiently
            // +3% max speed at 100 experience
            if (avgExperience > 60f)
            {
                float expBonus = ((avgExperience - 60f) / 40f) * 0.03f;
                result.AddFactor(expBonus, _veteranSpeedText);
            }

            // High bonding = better coordination = faster movement
            // +2% max speed at 100 bonding
            if (avgBonding > 70f)
            {
                float bondingBonus = ((avgBonding - 70f) / 30f) * 0.02f;
                result.AddFactor(bondingBonus, _highBondingText);
            }
        }

        private void ApplyRingSystemSpeedEffects(ref ExplainedNumber result)
        {
            var ringBehavior = RingSystem.RingSystemCampaignBehavior.Instance;
            if (ringBehavior == null || !RingSystem.RingSystemCampaignBehavior.IsEnabled) return;
            if (ringBehavior.EquippedRingIds == null || ringBehavior.EquippedRingIds.Count == 0) return;

            var effects = ringBehavior.PlayerEffects;
            if (effects == null) return;

            float corruption = effects.GetTotalCorruption();
            float power = effects.GetTotalRingPower();

            // High corruption slows the party (burden of the ring)
            // -6% max at 100 corruption
            if (corruption > 30f)
            {
                float corruptionPenalty = -((corruption - 30f) / 70f) * 0.06f;
                result.AddFactor(corruptionPenalty, _ringCorruptionSlowText);
            }

            // Check for specific ring bonuses
            foreach (var ringId in ringBehavior.EquippedRingIds)
            {
                // Elven rings grant speed bonuses
                if (ringId == "hon_ring_vilya") // Ring of Air
                {
                    // Vilya grants +4% speed (power of wind)
                    float powerMultiplier = 1f + (power / 100f) * 0.5f; // Scales with power
                    result.AddFactor(0.04f * powerMultiplier, _ringSpeedBonusText);
                }
                else if (ringId == "hon_ring_narya" || ringId == "hon_ring_nenya")
                {
                    // Other elven rings grant smaller bonus
                    float powerMultiplier = 1f + (power / 100f) * 0.3f;
                    result.AddFactor(0.02f * powerMultiplier, _ringSpeedBonusText);
                }
                
                // One Ring can grant invisibility-like speed (wraith speed) at high power
                if (RingSystem.RingItemManager.IsOneRing(ringId) && power > 50f)
                {
                    float wrathSpeed = ((power - 50f) / 50f) * 0.05f;
                    result.AddFactor(wrathSpeed, new TextObject("{=hon_wraith_speed}Wraith Swiftness"));
                }
            }
        }
    }
}
