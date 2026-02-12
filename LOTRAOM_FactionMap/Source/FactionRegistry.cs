using System.Collections.Generic;

namespace LOTRAOM.FactionMap
{
    /// <summary>
    /// Static registry for region and faction data loaded from regions.json + factions.json.
    /// Initialized by FactionSelectionVM before PolygonWidgets are created,
    /// so widgets can look up their BBox and faction data at construction time.
    /// </summary>
    public static class FactionRegistry
    {
        private static Dictionary<string, RegionData> _regions = new Dictionary<string, RegionData>();
        private static Dictionary<string, FactionData> _factions = new Dictionary<string, FactionData>();

        /// <summary>
        /// Called from FactionSelectionVM.LoadRegionsAndFactions() after parsing both JSON files.
        /// Must happen before LoadMovie() creates PolygonWidgets.
        /// </summary>
        public static void Initialize(Dictionary<string, RegionData> regions, Dictionary<string, FactionData> factions)
        {
            _regions = regions ?? new Dictionary<string, RegionData>();
            _factions = factions ?? new Dictionary<string, FactionData>();
        }

        /// <summary>
        /// Get region data by region key (e.g. "kingdom_of_rohan", "deco_1").
        /// Returns null if not found or not yet initialized.
        /// </summary>
        public static RegionData GetRegion(string regionKey)
        {
            if (string.IsNullOrEmpty(regionKey) || _regions == null)
                return null;
            return _regions.TryGetValue(regionKey, out var data) ? data : null;
        }

        /// <summary>
        /// Get faction data for a region by following the region → faction link.
        /// Returns null for Tier 1 regions (no faction) or unknown regions.
        /// </summary>
        public static FactionData GetFactionForRegion(string regionKey)
        {
            var region = GetRegion(regionKey);
            if (region == null || string.IsNullOrEmpty(region.FactionId))
                return null;
            return _factions.TryGetValue(region.FactionId, out var faction) ? faction : null;
        }

        /// <summary>
        /// Get all region keys. Used by FactionLabelWidget to iterate all regions.
        /// </summary>
        public static IEnumerable<string> GetAllRegionKeys()
        {
            return _regions?.Keys;
        }

        /// <summary>
        /// Get faction data directly by faction ID (e.g. "kingdom_of_rohan").
        /// Returns null if not found.
        /// </summary>
        public static FactionData GetFaction(string factionId)
        {
            if (string.IsNullOrEmpty(factionId) || _factions == null)
                return null;
            return _factions.TryGetValue(factionId, out var data) ? data : null;
        }
    }
}
