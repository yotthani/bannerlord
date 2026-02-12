using TaleWorlds.Library;

namespace BannerlordThemeSwitcher
{
    /// <summary>
    /// Predefined color schemes for each Bannerlord culture.
    /// These serve as defaults and can be overridden in theme manifests.
    /// </summary>
    public static class DefaultColorSchemes
    {
        /// <summary>
        /// Default/neutral color scheme (game default style)
        /// </summary>
        public static ColorScheme Default => new ColorScheme();

        /// <summary>
        /// Vlandia - Western European knights theme
        /// </summary>
        public static ColorScheme Vlandia => new ColorScheme
        {
            Primary = ColorScheme.HexToColor("#FFD700FF"),
            Secondary = ColorScheme.HexToColor("#8B0000FF"),
            Tertiary = ColorScheme.HexToColor("#4682B4FF"),
            Text = ColorScheme.HexToColor("#FFFFFFEE"),
            TextMuted = ColorScheme.HexToColor("#B8B8B8FF"),
            TextHighlight = ColorScheme.HexToColor("#FFD700FF"),
            TextTitle = ColorScheme.HexToColor("#FFD700FF"),
            TextOnPrimary = ColorScheme.HexToColor("#1A0A00FF"),
            TextDisabled = ColorScheme.HexToColor("#666666FF"),
            Background = ColorScheme.HexToColor("#1A150A99"),
            BackgroundDark = ColorScheme.HexToColor("#0D0A05BB"),
            BackgroundLight = ColorScheme.HexToColor("#2A2215AA"),
            BackgroundAccent = ColorScheme.HexToColor("#FFD70020"),
            BackgroundHover = ColorScheme.HexToColor("#FFD70040"),
            BackgroundSelected = ColorScheme.HexToColor("#FFD70060"),
            Border = ColorScheme.HexToColor("#FFD700AA"),
            BorderMuted = ColorScheme.HexToColor("#FFD70055"),
            BorderHighlight = ColorScheme.HexToColor("#FFD700FF"),
            BorderSecondary = ColorScheme.HexToColor("#8B0000AA"),
            ButtonBackground = ColorScheme.HexToColor("#FFD70025"),
            ButtonHover = ColorScheme.HexToColor("#FFD70050"),
            ButtonPressed = ColorScheme.HexToColor("#FFD70080"),
            ButtonDisabled = ColorScheme.HexToColor("#44444444"),
            ButtonBorder = ColorScheme.HexToColor("#FFD700CC"),
            Success = ColorScheme.HexToColor("#32CD32FF"),
            Warning = ColorScheme.HexToColor("#FFA500FF"),
            Error = ColorScheme.HexToColor("#DC143CFF"),
            Info = ColorScheme.HexToColor("#4682B4FF"),
            Gold = ColorScheme.HexToColor("#FFD700FF"),
            Experience = ColorScheme.HexToColor("#9370DBFF"),
            Health = ColorScheme.HexToColor("#DC143CFF"),
            Morale = ColorScheme.HexToColor("#32CD32FF"),
            Shadow = ColorScheme.HexToColor("#1A0A0088"),
            Glow = ColorScheme.HexToColor("#FFD70066")
        };

        /// <summary>
        /// Sturgia - Norse/Viking theme
        /// </summary>
        public static ColorScheme Sturgia => new ColorScheme
        {
            Primary = ColorScheme.HexToColor("#00BFFFFF"),
            Secondary = ColorScheme.HexToColor("#C0C0C0FF"),
            Tertiary = ColorScheme.HexToColor("#708090FF"),
            Text = ColorScheme.HexToColor("#FFFFFFEE"),
            TextMuted = ColorScheme.HexToColor("#A8B8C8FF"),
            TextHighlight = ColorScheme.HexToColor("#00BFFFFF"),
            TextTitle = ColorScheme.HexToColor("#00BFFFFF"),
            TextOnPrimary = ColorScheme.HexToColor("#0A1520FF"),
            TextDisabled = ColorScheme.HexToColor("#607080FF"),
            Background = ColorScheme.HexToColor("#0A151F99"),
            BackgroundDark = ColorScheme.HexToColor("#050A0FBB"),
            BackgroundLight = ColorScheme.HexToColor("#152535AA"),
            BackgroundAccent = ColorScheme.HexToColor("#00BFFF20"),
            BackgroundHover = ColorScheme.HexToColor("#00BFFF40"),
            BackgroundSelected = ColorScheme.HexToColor("#00BFFF60"),
            Border = ColorScheme.HexToColor("#C0C0C0AA"),
            BorderMuted = ColorScheme.HexToColor("#C0C0C055"),
            BorderHighlight = ColorScheme.HexToColor("#00BFFFFF"),
            BorderSecondary = ColorScheme.HexToColor("#708090AA"),
            ButtonBackground = ColorScheme.HexToColor("#00BFFF25"),
            ButtonHover = ColorScheme.HexToColor("#00BFFF50"),
            ButtonPressed = ColorScheme.HexToColor("#00BFFF80"),
            ButtonDisabled = ColorScheme.HexToColor("#40506044"),
            ButtonBorder = ColorScheme.HexToColor("#C0C0C0CC"),
            Success = ColorScheme.HexToColor("#90EE90FF"),
            Warning = ColorScheme.HexToColor("#FFD700FF"),
            Error = ColorScheme.HexToColor("#FF6B6BFF"),
            Info = ColorScheme.HexToColor("#87CEEBFF"),
            Gold = ColorScheme.HexToColor("#FFD700FF"),
            Experience = ColorScheme.HexToColor("#87CEEBFF"),
            Health = ColorScheme.HexToColor("#FF6B6BFF"),
            Morale = ColorScheme.HexToColor("#90EE90FF"),
            Shadow = ColorScheme.HexToColor("#0A152088"),
            Glow = ColorScheme.HexToColor("#00BFFF66")
        };

        /// <summary>
        /// Battania - Celtic/Forest theme
        /// </summary>
        public static ColorScheme Battania => new ColorScheme
        {
            Primary = ColorScheme.HexToColor("#228B22FF"),
            Secondary = ColorScheme.HexToColor("#8B4513FF"),
            Tertiary = ColorScheme.HexToColor("#DAA520FF"),
            Text = ColorScheme.HexToColor("#FFFFFFEE"),
            TextMuted = ColorScheme.HexToColor("#A8C8A8FF"),
            TextHighlight = ColorScheme.HexToColor("#228B22FF"),
            TextTitle = ColorScheme.HexToColor("#228B22FF"),
            TextOnPrimary = ColorScheme.HexToColor("#0A200AFF"),
            TextDisabled = ColorScheme.HexToColor("#607060FF"),
            Background = ColorScheme.HexToColor("#0A1F0A99"),
            BackgroundDark = ColorScheme.HexToColor("#050F05BB"),
            BackgroundLight = ColorScheme.HexToColor("#153515AA"),
            BackgroundAccent = ColorScheme.HexToColor("#228B2220"),
            BackgroundHover = ColorScheme.HexToColor("#228B2240"),
            BackgroundSelected = ColorScheme.HexToColor("#228B2260"),
            Border = ColorScheme.HexToColor("#8B4513AA"),
            BorderMuted = ColorScheme.HexToColor("#8B451355"),
            BorderHighlight = ColorScheme.HexToColor("#228B22FF"),
            BorderSecondary = ColorScheme.HexToColor("#DAA520AA"),
            ButtonBackground = ColorScheme.HexToColor("#228B2225"),
            ButtonHover = ColorScheme.HexToColor("#228B2250"),
            ButtonPressed = ColorScheme.HexToColor("#228B2280"),
            ButtonDisabled = ColorScheme.HexToColor("#40504044"),
            ButtonBorder = ColorScheme.HexToColor("#8B4513CC"),
            Success = ColorScheme.HexToColor("#32CD32FF"),
            Warning = ColorScheme.HexToColor("#DAA520FF"),
            Error = ColorScheme.HexToColor("#CD5C5CFF"),
            Info = ColorScheme.HexToColor("#6B8E23FF"),
            Gold = ColorScheme.HexToColor("#DAA520FF"),
            Experience = ColorScheme.HexToColor("#6B8E23FF"),
            Health = ColorScheme.HexToColor("#CD5C5CFF"),
            Morale = ColorScheme.HexToColor("#32CD32FF"),
            Shadow = ColorScheme.HexToColor("#0A200A88"),
            Glow = ColorScheme.HexToColor("#228B2266")
        };

        /// <summary>
        /// Empire - Roman/Byzantine theme
        /// </summary>
        public static ColorScheme Empire => new ColorScheme
        {
            Primary = ColorScheme.HexToColor("#800080FF"),
            Secondary = ColorScheme.HexToColor("#FFD700FF"),
            Tertiary = ColorScheme.HexToColor("#DC143CFF"),
            Text = ColorScheme.HexToColor("#FFFFFFEE"),
            TextMuted = ColorScheme.HexToColor("#C8A8C8FF"),
            TextHighlight = ColorScheme.HexToColor("#FFD700FF"),
            TextTitle = ColorScheme.HexToColor("#FFD700FF"),
            TextOnPrimary = ColorScheme.HexToColor("#200020FF"),
            TextDisabled = ColorScheme.HexToColor("#706070FF"),
            Background = ColorScheme.HexToColor("#1A0A1A99"),
            BackgroundDark = ColorScheme.HexToColor("#0D050DBB"),
            BackgroundLight = ColorScheme.HexToColor("#2A152AAA"),
            BackgroundAccent = ColorScheme.HexToColor("#80008020"),
            BackgroundHover = ColorScheme.HexToColor("#80008040"),
            BackgroundSelected = ColorScheme.HexToColor("#80008060"),
            Border = ColorScheme.HexToColor("#FFD700AA"),
            BorderMuted = ColorScheme.HexToColor("#FFD70055"),
            BorderHighlight = ColorScheme.HexToColor("#FFD700FF"),
            BorderSecondary = ColorScheme.HexToColor("#800080AA"),
            ButtonBackground = ColorScheme.HexToColor("#80008025"),
            ButtonHover = ColorScheme.HexToColor("#80008050"),
            ButtonPressed = ColorScheme.HexToColor("#80008080"),
            ButtonDisabled = ColorScheme.HexToColor("#50405044"),
            ButtonBorder = ColorScheme.HexToColor("#FFD700CC"),
            Success = ColorScheme.HexToColor("#32CD32FF"),
            Warning = ColorScheme.HexToColor("#FFA500FF"),
            Error = ColorScheme.HexToColor("#DC143CFF"),
            Info = ColorScheme.HexToColor("#4169E1FF"),
            Gold = ColorScheme.HexToColor("#FFD700FF"),
            Experience = ColorScheme.HexToColor("#9370DBFF"),
            Health = ColorScheme.HexToColor("#DC143CFF"),
            Morale = ColorScheme.HexToColor("#32CD32FF"),
            Shadow = ColorScheme.HexToColor("#20002088"),
            Glow = ColorScheme.HexToColor("#80008066")
        };

        /// <summary>
        /// Aserai - Arabian/Desert theme
        /// </summary>
        public static ColorScheme Aserai => new ColorScheme
        {
            Primary = ColorScheme.HexToColor("#FF8C00FF"),
            Secondary = ColorScheme.HexToColor("#191970FF"),
            Tertiary = ColorScheme.HexToColor("#40E0D0FF"),
            Text = ColorScheme.HexToColor("#FFFFFFEE"),
            TextMuted = ColorScheme.HexToColor("#D8C8A8FF"),
            TextHighlight = ColorScheme.HexToColor("#FF8C00FF"),
            TextTitle = ColorScheme.HexToColor("#FF8C00FF"),
            TextOnPrimary = ColorScheme.HexToColor("#1A0A00FF"),
            TextDisabled = ColorScheme.HexToColor("#807060FF"),
            Background = ColorScheme.HexToColor("#1A150599"),
            BackgroundDark = ColorScheme.HexToColor("#0D0A02BB"),
            BackgroundLight = ColorScheme.HexToColor("#2A2510AA"),
            BackgroundAccent = ColorScheme.HexToColor("#FF8C0020"),
            BackgroundHover = ColorScheme.HexToColor("#FF8C0040"),
            BackgroundSelected = ColorScheme.HexToColor("#FF8C0060"),
            Border = ColorScheme.HexToColor("#FF8C00AA"),
            BorderMuted = ColorScheme.HexToColor("#FF8C0055"),
            BorderHighlight = ColorScheme.HexToColor("#FF8C00FF"),
            BorderSecondary = ColorScheme.HexToColor("#191970AA"),
            ButtonBackground = ColorScheme.HexToColor("#FF8C0025"),
            ButtonHover = ColorScheme.HexToColor("#FF8C0050"),
            ButtonPressed = ColorScheme.HexToColor("#FF8C0080"),
            ButtonDisabled = ColorScheme.HexToColor("#50403044"),
            ButtonBorder = ColorScheme.HexToColor("#FF8C00CC"),
            Success = ColorScheme.HexToColor("#40E0D0FF"),
            Warning = ColorScheme.HexToColor("#FFD700FF"),
            Error = ColorScheme.HexToColor("#FF6347FF"),
            Info = ColorScheme.HexToColor("#40E0D0FF"),
            Gold = ColorScheme.HexToColor("#FFD700FF"),
            Experience = ColorScheme.HexToColor("#40E0D0FF"),
            Health = ColorScheme.HexToColor("#FF6347FF"),
            Morale = ColorScheme.HexToColor("#40E0D0FF"),
            Shadow = ColorScheme.HexToColor("#1A150588"),
            Glow = ColorScheme.HexToColor("#FF8C0066")
        };

        /// <summary>
        /// Khuzait - Mongol/Steppe theme
        /// </summary>
        public static ColorScheme Khuzait => new ColorScheme
        {
            Primary = ColorScheme.HexToColor("#9ACD32FF"),
            Secondary = ColorScheme.HexToColor("#87CEEBFF"),
            Tertiary = ColorScheme.HexToColor("#CD5C5CFF"),
            Text = ColorScheme.HexToColor("#FFFFFFEE"),
            TextMuted = ColorScheme.HexToColor("#C8D8B8FF"),
            TextHighlight = ColorScheme.HexToColor("#9ACD32FF"),
            TextTitle = ColorScheme.HexToColor("#9ACD32FF"),
            TextOnPrimary = ColorScheme.HexToColor("#0A1A0AFF"),
            TextDisabled = ColorScheme.HexToColor("#607050FF"),
            Background = ColorScheme.HexToColor("#0F1A0A99"),
            BackgroundDark = ColorScheme.HexToColor("#070D05BB"),
            BackgroundLight = ColorScheme.HexToColor("#1A2A15AA"),
            BackgroundAccent = ColorScheme.HexToColor("#9ACD3220"),
            BackgroundHover = ColorScheme.HexToColor("#9ACD3240"),
            BackgroundSelected = ColorScheme.HexToColor("#9ACD3260"),
            Border = ColorScheme.HexToColor("#87CEEBAA"),
            BorderMuted = ColorScheme.HexToColor("#87CEEB55"),
            BorderHighlight = ColorScheme.HexToColor("#9ACD32FF"),
            BorderSecondary = ColorScheme.HexToColor("#CD5C5CAA"),
            ButtonBackground = ColorScheme.HexToColor("#9ACD3225"),
            ButtonHover = ColorScheme.HexToColor("#9ACD3250"),
            ButtonPressed = ColorScheme.HexToColor("#9ACD3280"),
            ButtonDisabled = ColorScheme.HexToColor("#40504044"),
            ButtonBorder = ColorScheme.HexToColor("#87CEEBCC"),
            Success = ColorScheme.HexToColor("#9ACD32FF"),
            Warning = ColorScheme.HexToColor("#FFD700FF"),
            Error = ColorScheme.HexToColor("#CD5C5CFF"),
            Info = ColorScheme.HexToColor("#87CEEBFF"),
            Gold = ColorScheme.HexToColor("#FFD700FF"),
            Experience = ColorScheme.HexToColor("#87CEEBFF"),
            Health = ColorScheme.HexToColor("#CD5C5CFF"),
            Morale = ColorScheme.HexToColor("#9ACD32FF"),
            Shadow = ColorScheme.HexToColor("#0F1A0A88"),
            Glow = ColorScheme.HexToColor("#9ACD3266")
        };

        /// <summary>
        /// Naval - Seafaring theme
        /// </summary>
        public static ColorScheme Naval => new ColorScheme
        {
            Primary = ColorScheme.HexToColor("#4169E1FF"),
            Secondary = ColorScheme.HexToColor("#C0C0C0FF"),
            Tertiary = ColorScheme.HexToColor("#20B2AAFF"),
            Text = ColorScheme.HexToColor("#FFFFFFEE"),
            TextMuted = ColorScheme.HexToColor("#A8B8C8FF"),
            TextHighlight = ColorScheme.HexToColor("#4169E1FF"),
            TextTitle = ColorScheme.HexToColor("#4169E1FF"),
            TextOnPrimary = ColorScheme.HexToColor("#0A0A20FF"),
            TextDisabled = ColorScheme.HexToColor("#506070FF"),
            Background = ColorScheme.HexToColor("#0A0F1A99"),
            BackgroundDark = ColorScheme.HexToColor("#05070DBB"),
            BackgroundLight = ColorScheme.HexToColor("#151A2AAA"),
            BackgroundAccent = ColorScheme.HexToColor("#4169E120"),
            BackgroundHover = ColorScheme.HexToColor("#4169E140"),
            BackgroundSelected = ColorScheme.HexToColor("#4169E160"),
            Border = ColorScheme.HexToColor("#C0C0C0AA"),
            BorderMuted = ColorScheme.HexToColor("#C0C0C055"),
            BorderHighlight = ColorScheme.HexToColor("#4169E1FF"),
            BorderSecondary = ColorScheme.HexToColor("#20B2AAAA"),
            ButtonBackground = ColorScheme.HexToColor("#4169E125"),
            ButtonHover = ColorScheme.HexToColor("#4169E150"),
            ButtonPressed = ColorScheme.HexToColor("#4169E180"),
            ButtonDisabled = ColorScheme.HexToColor("#40506044"),
            ButtonBorder = ColorScheme.HexToColor("#C0C0C0CC"),
            Success = ColorScheme.HexToColor("#20B2AAFF"),
            Warning = ColorScheme.HexToColor("#FFD700FF"),
            Error = ColorScheme.HexToColor("#FF6347FF"),
            Info = ColorScheme.HexToColor("#4169E1FF"),
            Gold = ColorScheme.HexToColor("#FFD700FF"),
            Experience = ColorScheme.HexToColor("#20B2AAFF"),
            Health = ColorScheme.HexToColor("#FF6347FF"),
            Morale = ColorScheme.HexToColor("#20B2AAFF"),
            Shadow = ColorScheme.HexToColor("#0A0F1A88"),
            Glow = ColorScheme.HexToColor("#4169E166")
        };

        /// <summary>
        /// Get color scheme by culture ID
        /// </summary>
        public static ColorScheme GetByCultureId(string cultureId)
        {
            if (string.IsNullOrEmpty(cultureId))
                return Default;

            return cultureId.ToLowerInvariant() switch
            {
                "vlandia" => Vlandia,
                "sturgia" => Sturgia,
                "battania" => Battania,
                "empire" => Empire,
                "aserai" => Aserai,
                "khuzait" => Khuzait,
                "nord" or "naval" => Naval,
                _ => Default
            };
        }
    }
}
