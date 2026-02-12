using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.CustomResourceSystem
{
    /// <summary>
    /// Types of resources that can be tracked.
    /// </summary>
    public enum ResourceType
    {
        /// <summary>
        /// Consumable items (beer, food, medicine).
        /// Satisfied by having items in inventory/stash.
        /// </summary>
        Consumable,

        /// <summary>
        /// Location-based needs (forest, mountains, home).
        /// Satisfied by being in or near specific terrain/settlements.
        /// </summary>
        Location,

        /// <summary>
        /// Action-based needs (combat, hunting, trading).
        /// Satisfied by performing specific actions.
        /// </summary>
        Action,

        /// <summary>
        /// Social needs (ally contact, enemy presence).
        /// Satisfied by interactions with specific factions/cultures.
        /// </summary>
        Social,

        /// <summary>
        /// Time-based needs (rest, celebration, ritual).
        /// Satisfied by periodic events or time passage.
        /// </summary>
        Temporal
    }

    /// <summary>
    /// How the resource satisfaction is calculated.
    /// </summary>
    public enum SatisfactionMode
    {
        /// <summary>Satisfaction level (0-100) decays daily, needs replenishment.</summary>
        Decay,

        /// <summary>Counts days since last satisfaction, triggers at threshold.</summary>
        DaysSince,

        /// <summary>Accumulates over time, triggers at threshold.</summary>
        Accumulate,

        /// <summary>Binary - either satisfied or not.</summary>
        Binary
    }

    /// <summary>
    /// Defines a trackable resource and its behavior.
    /// </summary>
    public class ResourceDefinition
    {
        /// <summary>Unique identifier for this resource.</summary>
        public string Id { get; set; }

        /// <summary>Display name for UI.</summary>
        public string DisplayName { get; set; }

        /// <summary>Description of the resource.</summary>
        public string Description { get; set; }

        /// <summary>Type of resource.</summary>
        public ResourceType Type { get; set; }

        /// <summary>How satisfaction is tracked.</summary>
        public SatisfactionMode Mode { get; set; }

        /// <summary>Daily decay/accumulation rate.</summary>
        public float DailyRate { get; set; }

        /// <summary>Starting satisfaction level (0-100).</summary>
        public float InitialValue { get; set; }

        /// <summary>Maximum satisfaction level.</summary>
        public float MaxValue { get; set; }

        /// <summary>Minimum satisfaction level.</summary>
        public float MinValue { get; set; }

        #region Satisfaction Sources

        /// <summary>Item IDs that satisfy this resource (for Consumable type).</summary>
        public List<string> SatisfyingItems { get; set; }

        /// <summary>Terrain types that satisfy this resource (for Location type).</summary>
        public List<string> SatisfyingTerrains { get; set; }

        /// <summary>Settlement types that satisfy this resource.</summary>
        public List<string> SatisfyingSettlements { get; set; }

        /// <summary>Events that satisfy this resource (for Action type).</summary>
        public List<string> SatisfyingEvents { get; set; }

        /// <summary>Culture IDs that satisfy this resource (for Social type).</summary>
        public List<string> SatisfyingCultures { get; set; }

        /// <summary>Amount of satisfaction gained per source trigger.</summary>
        public float SatisfactionGain { get; set; }

        #endregion

        public ResourceDefinition()
        {
            SatisfyingItems = new List<string>();
            SatisfyingTerrains = new List<string>();
            SatisfyingSettlements = new List<string>();
            SatisfyingEvents = new List<string>();
            SatisfyingCultures = new List<string>();
            
            Mode = SatisfactionMode.Decay;
            DailyRate = 1f;
            InitialValue = 50f;
            MaxValue = 100f;
            MinValue = 0f;
            SatisfactionGain = 10f;
        }

        /// <summary>
        /// Parses a ResourceDefinition from XML.
        /// </summary>
        public static ResourceDefinition FromXml(XmlNode node)
        {
            var def = new ResourceDefinition();

            def.Id = node.Attributes?["id"]?.Value ?? "unknown";
            def.DisplayName = GetChildText(node, "DisplayName", def.Id);
            def.Description = GetChildText(node, "Description", "");

            // Type
            string typeStr = GetChildText(node, "Type", "Consumable");
            if (Enum.TryParse<ResourceType>(typeStr, true, out var resType))
                def.Type = resType;

            // Mode
            string modeStr = GetChildText(node, "Mode", "Decay");
            if (Enum.TryParse<SatisfactionMode>(modeStr, true, out var mode))
                def.Mode = mode;

            // Rates and values
            def.DailyRate = GetChildFloat(node, "DailyRate", 1f);
            def.InitialValue = GetChildFloat(node, "InitialValue", 50f);
            def.MaxValue = GetChildFloat(node, "MaxValue", 100f);
            def.MinValue = GetChildFloat(node, "MinValue", 0f);
            def.SatisfactionGain = GetChildFloat(node, "SatisfactionGain", 10f);

            // Satisfaction sources
            def.SatisfyingItems = GetChildList(node, "SatisfyingItems", "Item");
            def.SatisfyingTerrains = GetChildList(node, "SatisfyingTerrains", "Terrain");
            def.SatisfyingSettlements = GetChildList(node, "SatisfyingSettlements", "Settlement");
            def.SatisfyingEvents = GetChildList(node, "SatisfyingEvents", "Event");
            def.SatisfyingCultures = GetChildList(node, "SatisfyingCultures", "Culture");

            return def;
        }

        #region XML Helpers

        private static string GetChildText(XmlNode parent, string childName, string defaultValue)
        {
            var child = parent.SelectSingleNode(childName);
            return child?.InnerText?.Trim() ?? defaultValue;
        }

        private static float GetChildFloat(XmlNode parent, string childName, float defaultValue)
        {
            var text = GetChildText(parent, childName, null);
            if (text != null && float.TryParse(text, out float val))
                return val;
            return defaultValue;
        }

        private static List<string> GetChildList(XmlNode parent, string containerName, string itemName)
        {
            var list = new List<string>();
            var container = parent.SelectSingleNode(containerName);
            if (container != null)
            {
                foreach (XmlNode item in container.SelectNodes(itemName))
                {
                    var value = item.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(value))
                        list.Add(value);
                }
            }
            return list;
        }

        #endregion
    }

    /// <summary>
    /// Tracks the current satisfaction state of a resource for a troop type.
    /// </summary>
    public class HoNResourceState
    {
        [SaveableField(1)]
        public string ResourceId;

        [SaveableField(2)]
        public float CurrentValue;

        [SaveableField(3)]
        public float DaysSinceLastSatisfied;

        [SaveableField(4)]
        public float AccumulatedValue;

        [SaveableField(5)]
        public bool IsSatisfied;

        public HoNResourceState()
        {
            CurrentValue = 50f;
            DaysSinceLastSatisfied = 0f;
            AccumulatedValue = 0f;
            IsSatisfied = true;
        }

        public HoNResourceState(string resourceId, float initialValue) : this()
        {
            ResourceId = resourceId;
            CurrentValue = initialValue;
        }
    }
}
