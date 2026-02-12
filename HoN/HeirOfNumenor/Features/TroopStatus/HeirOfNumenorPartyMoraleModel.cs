using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.TroopStatus
{
    /// <summary>
    /// Custom party morale model that integrates TroopStatus effects with native morale calculation.
    /// This makes our custom status effects visible in the native morale tooltip!
    /// </summary>
    public class HeirOfNumenorPartyMoraleModel : PartyMoraleModel
    {
        // Text objects for tooltip display
        private static readonly TextObject _fearPenaltyText = new TextObject("{=hon_fear_penalty}Troop Fear");
        private static readonly TextObject _bondingBonusText = new TextObject("{=hon_bonding_bonus}Unit Cohesion");
        private static readonly TextObject _frustrationPenaltyText = new TextObject("{=hon_frustration_penalty}Troop Frustration");
        private static readonly TextObject _experienceBonusText = new TextObject("{=hon_experience_bonus}Battle Veterans");
        private static readonly TextObject _loyaltyBonusText = new TextObject("{=hon_loyalty_bonus}Troop Loyalty");
        private static readonly TextObject _ringCorruptionText = new TextObject("{=hon_ring_corruption}Ring's Dark Influence");
        private static readonly TextObject _virtualCaptainText = new TextObject("{=hon_captain_bonus}Experienced Captains");

        /// <summary>
        /// High morale threshold from base game.
        /// </summary>
        public override float HighMoraleValue => BaseModel.HighMoraleValue;

        /// <summary>
        /// Daily starvation morale penalty.
        /// </summary>
        public override int GetDailyStarvationMoralePenalty(PartyBase party)
        {
            return BaseModel.GetDailyStarvationMoralePenalty(party);
        }

        /// <summary>
        /// Daily no-wage morale penalty.
        /// </summary>
        public override int GetDailyNoWageMoralePenalty(MobileParty party)
        {
            return BaseModel.GetDailyNoWageMoralePenalty(party);
        }

        /// <summary>
        /// Base morale value for a party.
        /// </summary>
        public override float GetStandardBaseMorale(PartyBase party)
        {
            return BaseModel.GetStandardBaseMorale(party);
        }

        /// <summary>
        /// Morale change on victory.
        /// </summary>
        public override float GetVictoryMoraleChange(PartyBase party)
        {
            return BaseModel.GetVictoryMoraleChange(party);
        }

        /// <summary>
        /// Morale change on defeat.
        /// </summary>
        public override float GetDefeatMoraleChange(PartyBase party)
        {
            return BaseModel.GetDefeatMoraleChange(party);
        }

        /// <summary>
        /// Main morale calculation - adds our custom TroopStatus effects.
        /// </summary>
        public override ExplainedNumber GetEffectivePartyMorale(MobileParty party, bool includeDescription = false)
        {
            // Start with base game calculation
            ExplainedNumber result = BaseModel.GetEffectivePartyMorale(party, includeDescription);

            // Only apply our effects if TroopStatus is enabled
            if (!TroopStatusCampaignBehavior.IsEnabled) return result;
            if (party != MobileParty.MainParty) return result; // Only player party for now

            try
            {
                ApplyTroopStatusEffects(party, ref result);
                ApplyRingSystemEffects(ref result);
                ApplyMemorySystemEffects(party, ref result);
            }
            catch
            {
                // Silently fail if our systems aren't loaded
            }

            return result;
        }

        private void ApplyTroopStatusEffects(MobileParty party, ref ExplainedNumber result)
        {
            var statusManager = TroopStatusManager.Instance;
            if (statusManager == null) return;

            // Get average status values across all troops
            float totalFear = 0f;
            float totalBonding = 0f;
            float totalFrustration = 0f;
            float totalExperience = 0f;
            float totalLoyalty = 0f;
            int troopTypeCount = 0;

            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                if (element.Character == null || element.Character.IsHero) continue;

                var status = statusManager.GetTroopStatus(element.Character.StringId);
                if (status == null) continue;

                int count = element.Number;
                totalFear += status.Fear * count;
                totalBonding += status.Bonding * count;
                totalFrustration += status.Frustration * count;
                totalExperience += status.BattleExperience * count;
                totalLoyalty += status.Loyalty * count;
                troopTypeCount += count;
            }

            if (troopTypeCount == 0) return;

            // Calculate weighted averages
            float avgFear = totalFear / troopTypeCount;
            float avgBonding = totalBonding / troopTypeCount;
            float avgFrustration = totalFrustration / troopTypeCount;
            float avgExperience = totalExperience / troopTypeCount;
            float avgLoyalty = totalLoyalty / troopTypeCount;

            // Apply effects (tuned to be meaningful but not overwhelming)
            
            // Fear: High fear significantly reduces morale (-12 max at 100 fear)
            if (avgFear > 15f)
            {
                float fearPenalty = -(avgFear - 15f) * 0.15f;
                result.Add(fearPenalty, _fearPenaltyText);
            }

            // Bonding: High bonding improves morale (+8 max at 100 bonding)
            if (avgBonding > 40f)
            {
                float bondingBonus = (avgBonding - 40f) * 0.13f;
                result.Add(bondingBonus, _bondingBonusText);
            }

            // Frustration: Frustrated troops have lower morale (-8 max at 100 frustration)
            if (avgFrustration > 25f)
            {
                float frustrationPenalty = -(avgFrustration - 25f) * 0.11f;
                result.Add(frustrationPenalty, _frustrationPenaltyText);
            }

            // Battle Experience: Veterans are more confident (+5 max at 100 experience)
            if (avgExperience > 50f)
            {
                float expBonus = (avgExperience - 50f) * 0.10f;
                result.Add(expBonus, _experienceBonusText);
            }

            // Loyalty: Loyal troops fight harder (+4 max, -4 min)
            float loyaltyEffect = (avgLoyalty - 50f) * 0.08f;
            if (System.Math.Abs(loyaltyEffect) > 0.5f)
            {
                result.Add(loyaltyEffect, _loyaltyBonusText);
            }
        }

        private void ApplyRingSystemEffects(ref ExplainedNumber result)
        {
            var ringBehavior = RingSystem.RingSystemCampaignBehavior.Instance;
            if (ringBehavior == null || !RingSystem.RingSystemCampaignBehavior.IsEnabled) return;

            var effects = ringBehavior.PlayerEffects;
            if (effects == null) return;

            // High corruption affects party morale
            float corruption = effects.GetTotalCorruption();
            if (corruption > 20f)
            {
                // -1 to -8 morale based on corruption (20-100)
                float corruptionPenalty = -((corruption - 20f) / 80f) * 8f;
                result.Add(corruptionPenalty, _ringCorruptionText);
            }

            // Ring power can boost morale slightly (leadership effect)
            float power = effects.GetTotalRingPower();
            if (power > 30f)
            {
                // Small bonus from ring power (+0 to +3)
                float powerBonus = ((power - 30f) / 70f) * 3f;
                result.Add(powerBonus, new TextObject("{=hon_ring_power}Ring's Aura"));
            }
        }

        private void ApplyMemorySystemEffects(MobileParty party, ref ExplainedNumber result)
        {
            var memoryBehavior = MemorySystem.MemorySystemCampaignBehavior.Instance;
            if (memoryBehavior == null || !MemorySystem.MemorySystemCampaignBehavior.IsEnabled) return;

            // Count active captains
            int captainCount = memoryBehavior.GetActiveCaptainCount();
            if (captainCount > 0)
            {
                // Each virtual captain provides a small morale bonus (+1 per captain, max +5)
                float captainBonus = System.Math.Min(captainCount * 1f, 5f);
                result.Add(captainBonus, _virtualCaptainText);
            }
        }
    }
}
