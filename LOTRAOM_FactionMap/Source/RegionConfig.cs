using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace LOTRAOM.FactionMap
{
    /// <summary>
    /// Utility for resolving game CultureObjects from string IDs.
    /// </summary>
    public static class RegionConfig
    {
        /// <summary>
        /// Try to resolve a CultureObject from the game's object manager.
        /// Falls back gracefully if the culture doesn't exist.
        /// </summary>
        public static CultureObject ResolveCulture(string cultureId)
        {
            if (string.IsNullOrEmpty(cultureId)) return null;

            try
            {
                return MBObjectManager.Instance?.GetObject<CultureObject>(cultureId);
            }
            catch
            {
                // Culture not found - might be running without the LOTR content mod
                SubModule.LogError($"Culture '{cultureId}' not found. Is the content mod loaded?");
                return null;
            }
        }
    }
}
