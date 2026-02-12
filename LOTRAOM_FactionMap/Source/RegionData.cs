namespace LOTRAOM.FactionMap
{
    /// <summary>
    /// Region geometry and faction assignment, loaded from regions.json.
    /// Each region is a puzzle piece on the map with a bounding box and
    /// an optional reference to a faction definition in factions.json.
    /// </summary>
    public class RegionData
    {
        /// <summary>
        /// Faction ID referencing a key in factions.json, or null/empty for decoration regions (Tier 1).
        /// </summary>
        public string FactionId { get; set; }

        /// <summary>Normalized bounding box X position (0-1).</summary>
        public float BBoxX { get; set; }
        /// <summary>Normalized bounding box Y position (0-1).</summary>
        public float BBoxY { get; set; }
        /// <summary>Normalized bounding box width (0-1).</summary>
        public float BBoxW { get; set; }
        /// <summary>Normalized bounding box height (0-1).</summary>
        public float BBoxH { get; set; }

        /// <summary>Normalized capital/banner position X (0-1) on the full map. -1 = not set.</summary>
        public float CapitalX { get; set; } = -1f;
        /// <summary>Normalized capital/banner position Y (0-1) on the full map. -1 = not set.</summary>
        public float CapitalY { get; set; } = -1f;

        /// <summary>Whether this region has a valid capital position for banner placement.</summary>
        public bool HasCapitalPos => CapitalX >= 0f && CapitalY >= 0f;
    }
}
