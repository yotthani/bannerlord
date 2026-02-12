using System.Collections.Generic;
using TaleWorlds.Library;

namespace HeirOfNumenor.Features.TroopStatus.UI
{
    /// <summary>
    /// Icon definition for a status type.
    /// </summary>
    public class StatusIconDefinition
    {
        /// <summary>Sprite path for the icon.</summary>
        public string SpriteName { get; set; }

        /// <summary>Color tint for the icon (hex format).</summary>
        public string ColorHex { get; set; }

        /// <summary>Default color for neutral/hidden state.</summary>
        public string DefaultColorHex { get; set; }

        /// <summary>Display order (lower = first).</summary>
        public int Order { get; set; }

        /// <summary>Tooltip/hint text format. Use {0} for value, {1} for state.</summary>
        public string TooltipFormat { get; set; }

        /// <summary>Minimum value to show this indicator (0-100).</summary>
        public float ShowThreshold { get; set; }

        /// <summary>Whether higher values are worse (for color gradient).</summary>
        public bool HigherIsWorse { get; set; }

        /// <summary>Icon size (width/height).</summary>
        public int Size { get; set; }

        public StatusIconDefinition()
        {
            ColorHex = "#FFFFFFFF";
            DefaultColorHex = "#808080FF";
            Order = 0;
            ShowThreshold = 0f;
            Size = 18;
        }

        /// <summary>
        /// Gets the color based on the current value (0-100).
        /// </summary>
        public Color GetColorForValue(float value)
        {
            if (value < ShowThreshold)
                return Color.FromUint(0x40404080); // Dim gray, semi-transparent

            // Color gradient based on severity
            float intensity = value / 100f;

            if (HigherIsWorse)
            {
                // Green (good) -> Yellow -> Red (bad)
                if (intensity < 0.5f)
                {
                    // Green to Yellow
                    float t = intensity * 2f;
                    return new Color(t, 1f, 0f, 1f);
                }
                else
                {
                    // Yellow to Red
                    float t = (intensity - 0.5f) * 2f;
                    return new Color(1f, 1f - t, 0f, 1f);
                }
            }
            else
            {
                // Red (bad) -> Yellow -> Green (good)
                if (intensity < 0.5f)
                {
                    // Red to Yellow
                    float t = intensity * 2f;
                    return new Color(1f, t, 0f, 1f);
                }
                else
                {
                    // Yellow to Green
                    float t = (intensity - 0.5f) * 2f;
                    return new Color(1f - t, 1f, 0f, 1f);
                }
            }
        }

        /// <summary>
        /// Gets the color as hex string for UI binding.
        /// </summary>
        public string GetColorHexForValue(float value)
        {
            var color = GetColorForValue(value);
            return $"#{(int)(color.Red * 255):X2}{(int)(color.Green * 255):X2}{(int)(color.Blue * 255):X2}{(int)(color.Alpha * 255):X2}";
        }
    }

    /// <summary>
    /// Maps HoNTroopStatusType to visual icon definitions.
    /// Configurable for different icon sets.
    /// </summary>
    public static class StatusIconMapper
    {
        private static Dictionary<HoNTroopStatusType, StatusIconDefinition> _iconDefinitions;
        private static bool _isInitialized = false;

        /// <summary>
        /// Gets all icon definitions.
        /// </summary>
        public static IReadOnlyDictionary<HoNTroopStatusType, StatusIconDefinition> IconDefinitions
        {
            get
            {
                if (!_isInitialized) Initialize();
                return _iconDefinitions;
            }
        }

        /// <summary>
        /// Initializes the default icon mappings.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            _iconDefinitions = new Dictionary<HoNTroopStatusType, StatusIconDefinition>
            {
                // Fear - Skull icon
                [HoNTroopStatusType.Fear] = new StatusIconDefinition
                {
                    SpriteName = "General\\Mission\\PersonalKillfeed\\kill_feed_skull",
                    ColorHex = "#FF6060FF",      // Red-ish
                    DefaultColorHex = "#606060FF",
                    Order = 1,
                    TooltipFormat = "Fear: {0:F0}% ({1})",
                    ShowThreshold = 5f,
                    HigherIsWorse = true,
                    Size = 18
                },

                // Bonding - Heart/Shield icon
                [HoNTroopStatusType.Bonding] = new StatusIconDefinition
                {
                    SpriteName = "PartyScreen\\formation_flag",
                    ColorHex = "#60FF60FF",      // Green-ish
                    DefaultColorHex = "#606060FF",
                    Order = 2,
                    TooltipFormat = "Bonding: {0:F0}% ({1})",
                    ShowThreshold = 10f,
                    HigherIsWorse = false,
                    Size = 16
                },

                // Frustration - Angry face / crossed swords
                [HoNTroopStatusType.Frustration] = new StatusIconDefinition
                {
                    SpriteName = "PartyScreen\\upgrade_icon",
                    ColorHex = "#FFAA40FF",      // Orange
                    DefaultColorHex = "#606060FF",
                    Order = 3,
                    TooltipFormat = "Frustration: {0:F0}% ({1})",
                    ShowThreshold = 10f,
                    HigherIsWorse = true,
                    Size = 18
                },

                // Loyalty - Crown/star
                [HoNTroopStatusType.Loyalty] = new StatusIconDefinition
                {
                    SpriteName = "PartyScreen\\recruit_prisoner",
                    ColorHex = "#FFD700FF",      // Gold
                    DefaultColorHex = "#606060FF",
                    Order = 4,
                    TooltipFormat = "Loyalty: {0:F0}% ({1})",
                    ShowThreshold = 0f,  // Always show loyalty
                    HigherIsWorse = false,
                    Size = 18
                },

                // Ring Exposure - Ring/eye icon (if available)
                [HoNTroopStatusType.RingExposure] = new StatusIconDefinition
                {
                    SpriteName = "StdAssets\\character_creation\\eye",
                    ColorHex = "#9040FFFF",      // Purple
                    DefaultColorHex = "#404040FF",
                    Order = 5,
                    TooltipFormat = "Ring Exposure: {0:F0}%",
                    ShowThreshold = 5f,
                    HigherIsWorse = true,
                    Size = 16
                },

                // Battle Experience - Sword
                [HoNTroopStatusType.BattleExperience] = new StatusIconDefinition
                {
                    SpriteName = "General\\Icons\\Troop\\icon_troop_melee",
                    ColorHex = "#4080FFFF",      // Blue
                    DefaultColorHex = "#404040FF",
                    Order = 6,
                    TooltipFormat = "Battle Experience: {0:F0}%",
                    ShowThreshold = 10f,
                    HigherIsWorse = false,
                    Size = 16
                }
            };

            _isInitialized = true;
        }

        /// <summary>
        /// Gets the icon definition for a status type.
        /// </summary>
        public static StatusIconDefinition GetIconDefinition(HoNTroopStatusType statusType)
        {
            if (!_isInitialized) Initialize();
            return _iconDefinitions.TryGetValue(statusType, out var def) ? def : null;
        }

        /// <summary>
        /// Registers a custom icon definition (for mod extensibility).
        /// </summary>
        public static void RegisterIcon(HoNTroopStatusType statusType, StatusIconDefinition definition)
        {
            if (!_isInitialized) Initialize();
            _iconDefinitions[statusType] = definition;
        }

        /// <summary>
        /// Gets which status types should be displayed for a troop.
        /// </summary>
        public static IEnumerable<HoNTroopStatusType> GetDisplayedStatusTypes()
        {
            if (!_isInitialized) Initialize();

            // Return in display order
            var ordered = new List<(HoNTroopStatusType type, int order)>();
            foreach (var kvp in _iconDefinitions)
            {
                ordered.Add((kvp.Key, kvp.Value.Order));
            }
            ordered.Sort((a, b) => a.order.CompareTo(b.order));

            foreach (var item in ordered)
                yield return item.type;
        }

        /// <summary>
        /// Gets the primary statuses to display (Fear, Bonding, Frustration).
        /// </summary>
        public static HoNTroopStatusType[] PrimaryStatuses => new[]
        {
            HoNTroopStatusType.Fear,
            HoNTroopStatusType.Bonding,
            HoNTroopStatusType.Frustration,
            HoNTroopStatusType.Loyalty
        };
    }
}
