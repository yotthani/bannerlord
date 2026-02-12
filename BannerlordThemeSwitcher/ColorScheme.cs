using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.Library;

namespace BannerlordThemeSwitcher
{
    /// <summary>
    /// Defines a complete color scheme for a theme with purpose-specific colors.
    /// Colors are defined centrally and referenced by name in brush definitions.
    /// </summary>
    public class ColorScheme
    {
        // Helper to create Color from RGBA hex value
        private static Color RGBA(uint rgba)
        {
            float r = ((rgba >> 24) & 0xFF) / 255f;
            float g = ((rgba >> 16) & 0xFF) / 255f;
            float b = ((rgba >> 8) & 0xFF) / 255f;
            float a = (rgba & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // PRIMARY COLORS - Main theme identity
        // ═══════════════════════════════════════════════════════════════════════════════
        
        /// <summary>Main accent color - buttons, highlights, primary UI elements</summary>
        public Color Primary { get; set; } = RGBA(0xFFD700FF);
        
        /// <summary>Secondary accent - complementary to primary</summary>
        public Color Secondary { get; set; } = RGBA(0x8B0000FF);
        
        /// <summary>Tertiary accent - additional variety</summary>
        public Color Tertiary { get; set; } = RGBA(0x4169E1FF);

        // ═══════════════════════════════════════════════════════════════════════════════
        // TEXT COLORS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        /// <summary>Primary text color - main readable text</summary>
        public Color Text { get; set; } = RGBA(0xFFFFFFFF);
        
        /// <summary>Secondary text - less important, muted</summary>
        public Color TextMuted { get; set; } = RGBA(0xAAAAAAFF);
        
        /// <summary>Highlighted/emphasized text</summary>
        public Color TextHighlight { get; set; } = RGBA(0xFFD700FF);
        
        /// <summary>Title text color</summary>
        public Color TextTitle { get; set; } = RGBA(0xFFD700FF);
        
        /// <summary>Text on primary colored backgrounds</summary>
        public Color TextOnPrimary { get; set; } = RGBA(0x000000FF);
        
        /// <summary>Disabled/inactive text</summary>
        public Color TextDisabled { get; set; } = RGBA(0x666666FF);

        // ═══════════════════════════════════════════════════════════════════════════════
        // BACKGROUND COLORS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        /// <summary>Main panel background</summary>
        public Color Background { get; set; } = RGBA(0x1A1A1A99);
        
        /// <summary>Darker background for contrast</summary>
        public Color BackgroundDark { get; set; } = RGBA(0x0D0D0DBB);
        
        /// <summary>Lighter background for elevation</summary>
        public Color BackgroundLight { get; set; } = RGBA(0x2A2A2A99);
        
        /// <summary>Background with primary color tint</summary>
        public Color BackgroundAccent { get; set; } = RGBA(0xFFD70022);
        
        /// <summary>Hover state background</summary>
        public Color BackgroundHover { get; set; } = RGBA(0xFFD70044);
        
        /// <summary>Selected/active state background</summary>
        public Color BackgroundSelected { get; set; } = RGBA(0xFFD70066);

        // ═══════════════════════════════════════════════════════════════════════════════
        // BORDER/FRAME COLORS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        /// <summary>Standard border color</summary>
        public Color Border { get; set; } = RGBA(0xFFD700AA);
        
        /// <summary>Subtle/muted border</summary>
        public Color BorderMuted { get; set; } = RGBA(0xFFD70055);
        
        /// <summary>Highlighted/focused border</summary>
        public Color BorderHighlight { get; set; } = RGBA(0xFFD700FF);
        
        /// <summary>Secondary border color</summary>
        public Color BorderSecondary { get; set; } = RGBA(0x8B0000AA);

        // ═══════════════════════════════════════════════════════════════════════════════
        // BUTTON COLORS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        /// <summary>Button background - default state</summary>
        public Color ButtonBackground { get; set; } = RGBA(0xFFD70033);
        
        /// <summary>Button background - hovered</summary>
        public Color ButtonHover { get; set; } = RGBA(0xFFD70066);
        
        /// <summary>Button background - pressed</summary>
        public Color ButtonPressed { get; set; } = RGBA(0xFFD70099);
        
        /// <summary>Button background - disabled</summary>
        public Color ButtonDisabled { get; set; } = RGBA(0x44444444);
        
        /// <summary>Button frame/border</summary>
        public Color ButtonBorder { get; set; } = RGBA(0xFFD700CC);

        // ═══════════════════════════════════════════════════════════════════════════════
        // STATE COLORS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        /// <summary>Success/positive state</summary>
        public Color Success { get; set; } = RGBA(0x28A745FF);
        
        /// <summary>Warning state</summary>
        public Color Warning { get; set; } = RGBA(0xFFC107FF);
        
        /// <summary>Error/danger state</summary>
        public Color Error { get; set; } = RGBA(0xDC3545FF);
        
        /// <summary>Info/neutral state</summary>
        public Color Info { get; set; } = RGBA(0x17A2B8FF);

        // ═══════════════════════════════════════════════════════════════════════════════
        // SPECIAL COLORS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        /// <summary>Gold/currency color</summary>
        public Color Gold { get; set; } = RGBA(0xFFD700FF);
        
        /// <summary>Experience/progress color</summary>
        public Color Experience { get; set; } = RGBA(0x9370DBFF);
        
        /// <summary>Health bar color</summary>
        public Color Health { get; set; } = RGBA(0xDC143CFF);
        
        /// <summary>Morale indicator color</summary>
        public Color Morale { get; set; } = RGBA(0x32CD32FF);
        
        /// <summary>Shadow/glow effect color</summary>
        public Color Shadow { get; set; } = RGBA(0x00000088);
        
        /// <summary>Glow effect color (usually primary with alpha)</summary>
        public Color Glow { get; set; } = RGBA(0xFFD70066);

        // ═══════════════════════════════════════════════════════════════════════════════
        // METHODS
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Get color by name/placeholder
        /// </summary>
        public Color GetColor(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "primary" => Primary,
                "secondary" => Secondary,
                "tertiary" => Tertiary,
                "text" => Text,
                "textmuted" => TextMuted,
                "texthighlight" => TextHighlight,
                "texttitle" => TextTitle,
                "textonprimary" => TextOnPrimary,
                "textdisabled" => TextDisabled,
                "background" => Background,
                "backgrounddark" => BackgroundDark,
                "backgroundlight" => BackgroundLight,
                "backgroundaccent" => BackgroundAccent,
                "backgroundhover" => BackgroundHover,
                "backgroundselected" => BackgroundSelected,
                "border" => Border,
                "bordermuted" => BorderMuted,
                "borderhighlight" => BorderHighlight,
                "bordersecondary" => BorderSecondary,
                "buttonbackground" => ButtonBackground,
                "buttonhover" => ButtonHover,
                "buttonpressed" => ButtonPressed,
                "buttondisabled" => ButtonDisabled,
                "buttonborder" => ButtonBorder,
                "success" => Success,
                "warning" => Warning,
                "error" => Error,
                "info" => Info,
                "gold" => Gold,
                "experience" => Experience,
                "health" => Health,
                "morale" => Morale,
                "shadow" => Shadow,
                "glow" => Glow,
                _ => Primary // Default fallback
            };
        }

        /// <summary>
        /// Get all color names available in the scheme
        /// </summary>
        public static IEnumerable<string> GetColorNames()
        {
            return new[]
            {
                "Primary", "Secondary", "Tertiary",
                "Text", "TextMuted", "TextHighlight", "TextTitle", "TextOnPrimary", "TextDisabled",
                "Background", "BackgroundDark", "BackgroundLight", "BackgroundAccent", "BackgroundHover", "BackgroundSelected",
                "Border", "BorderMuted", "BorderHighlight", "BorderSecondary",
                "ButtonBackground", "ButtonHover", "ButtonPressed", "ButtonDisabled", "ButtonBorder",
                "Success", "Warning", "Error", "Info",
                "Gold", "Experience", "Health", "Morale", "Shadow", "Glow"
            };
        }

        /// <summary>
        /// Convert Color to hex string (#RRGGBBAA)
        /// </summary>
        public static string ColorToHex(Color color)
        {
            return $"#{(int)(color.Red * 255):X2}{(int)(color.Green * 255):X2}{(int)(color.Blue * 255):X2}{(int)(color.Alpha * 255):X2}";
        }

        /// <summary>
        /// Parse hex string to Color
        /// </summary>
        public static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.White;
            
            hex = hex.TrimStart('#');
            
            if (hex.Length == 6)
                hex += "FF"; // Add full alpha if not specified
            
            if (hex.Length != 8) return Color.White;
            
            try
            {
                // Parse as RRGGBBAA format
                uint rgba = Convert.ToUInt32(hex, 16);
                
                // Extract RGBA components and convert to float
                float r = ((rgba >> 24) & 0xFF) / 255f;
                float g = ((rgba >> 16) & 0xFF) / 255f;
                float b = ((rgba >> 8) & 0xFF) / 255f;
                float a = (rgba & 0xFF) / 255f;
                
                return new Color(r, g, b, a);
            }
            catch
            {
                return Color.White;
            }
        }

        /// <summary>
        /// Load color scheme from XML node
        /// </summary>
        public static ColorScheme FromXml(XmlNode colorSchemeNode)
        {
            var scheme = new ColorScheme();
            
            if (colorSchemeNode == null) return scheme;

            foreach (XmlNode node in colorSchemeNode.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element) continue;
                
                string colorName = node.Name;
                string colorValue = node.InnerText.Trim();
                
                if (string.IsNullOrEmpty(colorValue)) continue;
                
                Color color = HexToColor(colorValue);
                scheme.SetColor(colorName, color);
            }

            return scheme;
        }

        /// <summary>
        /// Set color by name
        /// </summary>
        public void SetColor(string name, Color color)
        {
            switch (name.ToLowerInvariant())
            {
                case "primary": Primary = color; break;
                case "secondary": Secondary = color; break;
                case "tertiary": Tertiary = color; break;
                case "text": Text = color; break;
                case "textmuted": TextMuted = color; break;
                case "texthighlight": TextHighlight = color; break;
                case "texttitle": TextTitle = color; break;
                case "textonprimary": TextOnPrimary = color; break;
                case "textdisabled": TextDisabled = color; break;
                case "background": Background = color; break;
                case "backgrounddark": BackgroundDark = color; break;
                case "backgroundlight": BackgroundLight = color; break;
                case "backgroundaccent": BackgroundAccent = color; break;
                case "backgroundhover": BackgroundHover = color; break;
                case "backgroundselected": BackgroundSelected = color; break;
                case "border": Border = color; break;
                case "bordermuted": BorderMuted = color; break;
                case "borderhighlight": BorderHighlight = color; break;
                case "bordersecondary": BorderSecondary = color; break;
                case "buttonbackground": ButtonBackground = color; break;
                case "buttonhover": ButtonHover = color; break;
                case "buttonpressed": ButtonPressed = color; break;
                case "buttondisabled": ButtonDisabled = color; break;
                case "buttonborder": ButtonBorder = color; break;
                case "success": Success = color; break;
                case "warning": Warning = color; break;
                case "error": Error = color; break;
                case "info": Info = color; break;
                case "gold": Gold = color; break;
                case "experience": Experience = color; break;
                case "health": Health = color; break;
                case "morale": Morale = color; break;
                case "shadow": Shadow = color; break;
                case "glow": Glow = color; break;
            }
        }

        /// <summary>
        /// Create a copy of this color scheme
        /// </summary>
        public ColorScheme Clone()
        {
            return new ColorScheme
            {
                Primary = Primary,
                Secondary = Secondary,
                Tertiary = Tertiary,
                Text = Text,
                TextMuted = TextMuted,
                TextHighlight = TextHighlight,
                TextTitle = TextTitle,
                TextOnPrimary = TextOnPrimary,
                TextDisabled = TextDisabled,
                Background = Background,
                BackgroundDark = BackgroundDark,
                BackgroundLight = BackgroundLight,
                BackgroundAccent = BackgroundAccent,
                BackgroundHover = BackgroundHover,
                BackgroundSelected = BackgroundSelected,
                Border = Border,
                BorderMuted = BorderMuted,
                BorderHighlight = BorderHighlight,
                BorderSecondary = BorderSecondary,
                ButtonBackground = ButtonBackground,
                ButtonHover = ButtonHover,
                ButtonPressed = ButtonPressed,
                ButtonDisabled = ButtonDisabled,
                ButtonBorder = ButtonBorder,
                Success = Success,
                Warning = Warning,
                Error = Error,
                Info = Info,
                Gold = Gold,
                Experience = Experience,
                Health = Health,
                Morale = Morale,
                Shadow = Shadow,
                Glow = Glow
            };
        }
    }
}
