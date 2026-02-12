using System.Collections.Generic;

namespace BannerlordThemeSwitcher
{
    /// <summary>
    /// Represents a UI theme configuration loaded from ThemeManifest.xml.
    /// </summary>
    public class Theme
    {
        /// <summary>Unique identifier (folder name)</summary>
        public string Id { get; set; }
        
        /// <summary>Display name</summary>
        public string Name { get; set; }
        
        /// <summary>Theme description</summary>
        public string Description { get; set; }
        
        /// <summary>Theme author</summary>
        public string Author { get; set; }
        
        /// <summary>Version string</summary>
        public string Version { get; set; }
        
        /// <summary>Full path to theme folder</summary>
        public string ThemePath { get; set; }
        
        /// <summary>Whether this is the built-in Default theme</summary>
        public bool IsBuiltIn { get; set; }
        
        /// <summary>Whether theme has brush overrides</summary>
        public bool HasBrushOverrides { get; set; }
        
        /// <summary>Whether theme has sprite overrides</summary>
        public bool HasSpriteOverrides { get; set; }
        
        /// <summary>Kingdom IDs this theme is bound to</summary>
        public List<string> BoundKingdoms { get; set; }
        
        /// <summary>Culture ID this theme is based on (for default color scheme)</summary>
        public string BaseCultureId { get; set; }
        
        /// <summary>Complete color scheme for this theme</summary>
        public ColorScheme Colors { get; set; }
        
        /// <summary>Enable pattern-based automatic theming for brushes not defined in XML</summary>
        public bool AutoTheme { get; set; } = true;
        
        /// <summary>List of brush XML files</summary>
        public List<string> BrushFiles { get; set; }

        public Theme()
        {
            BoundKingdoms = new List<string>();
            BrushFiles = new List<string>();
            Colors = new ColorScheme();
        }

        /// <summary>
        /// Initialize color scheme from base culture or custom colors
        /// </summary>
        public void InitializeColorScheme()
        {
            // Start with base culture colors if specified
            if (!string.IsNullOrEmpty(BaseCultureId))
            {
                Colors = DefaultColorSchemes.GetByCultureId(BaseCultureId).Clone();
            }
            else
            {
                Colors = DefaultColorSchemes.Default.Clone();
            }
        }

        public override string ToString() => $"{Name} ({Id})";
    }
}
