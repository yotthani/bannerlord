namespace HeirOfNumenor.Features.TroopStatus
{
    /// <summary>
    /// Types of statuses that can be tracked per troop type.
    /// Extensible - add new types as needed.
    /// </summary>
    public enum HoNTroopStatusType
    {
        /// <summary>
        /// Fear level - increases from losses, terrifying enemies, ring corruption.
        /// High fear causes desertion.
        /// </summary>
        Fear,

        /// <summary>
        /// Frustration - increases from unpaid wages, poor conditions, long campaigns.
        /// High frustration reduces performance.
        /// </summary>
        Frustration,

        /// <summary>
        /// Bonding - increases over time with player, shared battles.
        /// High bonding provides resistance to negative statuses and bonuses.
        /// </summary>
        Bonding,

        /// <summary>
        /// Battle Experience - tracks combat exposure beyond normal XP.
        /// Used for veteran status calculations.
        /// </summary>
        BattleExperience,

        /// <summary>
        /// Loyalty - general loyalty to the player/faction.
        /// Affected by multiple factors.
        /// </summary>
        Loyalty,

        /// <summary>
        /// Ring Exposure - how much the troops have been exposed to ring corruption.
        /// Separate from Fear but contributes to it.
        /// </summary>
        RingExposure,
    }

    /// <summary>
    /// Status effect direction for calculations.
    /// </summary>
    public enum StatusDirection
    {
        /// <summary>Higher is better (Bonding, Loyalty)</summary>
        Positive,
        /// <summary>Higher is worse (Fear, Frustration)</summary>
        Negative,
        /// <summary>Neutral tracking (BattleExperience)</summary>
        Neutral
    }

    /// <summary>
    /// Helper methods for HoNTroopStatusType.
    /// </summary>
    public static class HoNTroopStatusTypeExtensions
    {
        /// <summary>
        /// Gets whether this status is positive, negative, or neutral.
        /// </summary>
        public static StatusDirection GetDirection(this HoNTroopStatusType statusType)
        {
            return statusType switch
            {
                HoNTroopStatusType.Fear => StatusDirection.Negative,
                HoNTroopStatusType.Frustration => StatusDirection.Negative,
                HoNTroopStatusType.Bonding => StatusDirection.Positive,
                HoNTroopStatusType.BattleExperience => StatusDirection.Neutral,
                HoNTroopStatusType.Loyalty => StatusDirection.Positive,
                HoNTroopStatusType.RingExposure => StatusDirection.Negative,
                _ => StatusDirection.Neutral
            };
        }

        /// <summary>
        /// Gets the default starting value for a status type.
        /// </summary>
        public static float GetDefaultValue(this HoNTroopStatusType statusType)
        {
            return statusType switch
            {
                HoNTroopStatusType.Fear => 0f,
                HoNTroopStatusType.Frustration => 0f,
                HoNTroopStatusType.Bonding => 0f,
                HoNTroopStatusType.BattleExperience => 0f,
                HoNTroopStatusType.Loyalty => 50f,  // Start neutral
                HoNTroopStatusType.RingExposure => 0f,
                _ => 0f
            };
        }

        /// <summary>
        /// Gets the daily natural change rate (decay/growth toward baseline).
        /// Positive = grows, Negative = decays.
        /// </summary>
        public static float GetDailyNaturalChange(this HoNTroopStatusType statusType)
        {
            return statusType switch
            {
                HoNTroopStatusType.Fear => -0.5f,       // Fear slowly fades
                HoNTroopStatusType.Frustration => -0.3f, // Frustration slowly fades
                HoNTroopStatusType.Bonding => 0.5f,     // Bonding slowly grows
                HoNTroopStatusType.BattleExperience => 0f, // Doesn't change naturally
                HoNTroopStatusType.Loyalty => 0f,       // Doesn't change naturally
                HoNTroopStatusType.RingExposure => -0.2f, // Very slowly fades
                _ => 0f
            };
        }

        /// <summary>
        /// Gets the display name for a status type.
        /// </summary>
        public static string GetDisplayName(this HoNTroopStatusType statusType)
        {
            return statusType switch
            {
                HoNTroopStatusType.Fear => "Fear",
                HoNTroopStatusType.Frustration => "Frustration",
                HoNTroopStatusType.Bonding => "Bonding",
                HoNTroopStatusType.BattleExperience => "Battle Experience",
                HoNTroopStatusType.Loyalty => "Loyalty",
                HoNTroopStatusType.RingExposure => "Ring Exposure",
                _ => statusType.ToString()
            };
        }
    }
}
