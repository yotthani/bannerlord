using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;

namespace BannerlordThemeSwitcher
{
    /// <summary>
    /// Central manager for theme discovery, loading, and switching.
    /// </summary>
    public class ThemeManager : IDisposable
    {
        private static ThemeManager _instance;
        public static ThemeManager Instance => _instance;

        private readonly Dictionary<string, Theme> _themes = new Dictionary<string, Theme>();
        private readonly Dictionary<string, string> _kingdomToTheme = new Dictionary<string, string>();
        
        private string _currentThemeId = "Default";
        private bool _disposed;

        /// <summary>Event fired when theme changes (oldTheme, newTheme)</summary>
        public event Action<string, string> OnThemeChanged;

        /// <summary>Current active theme ID</summary>
        public string CurrentThemeId => _currentThemeId;

        /// <summary>Current active theme object</summary>
        public Theme CurrentTheme => GetTheme(_currentThemeId);

        /// <summary>All discovered themes</summary>
        public IReadOnlyList<Theme> AvailableThemes => _themes.Values.ToList();

        /// <summary>Theme IDs for dropdown</summary>
        public IEnumerable<string> ThemeIds => _themes.Keys;

        public ThemeManager()
        {
            _instance = this;
            
            // Register default theme
            _themes["Default"] = new Theme
            {
                Id = "Default",
                Name = "Vanilla",
                Description = "Original Bannerlord UI",
                Author = "TaleWorlds",
                Version = "1.0.0",
                IsBuiltIn = true,
                HasBrushOverrides = false
            };
        }

        /// <summary>
        /// Discovers all themes from the Themes folder.
        /// </summary>
        public void DiscoverThemes()
        {
            var themesPath = GetThemesPath();
            
            if (!Directory.Exists(themesPath))
            {
                LogMessage($"Themes folder not found, creating: {themesPath}");
                Directory.CreateDirectory(themesPath);
                return;
            }

            foreach (var themeDir in Directory.GetDirectories(themesPath))
            {
                try
                {
                    var theme = LoadThemeFromDirectory(themeDir);
                    if (theme != null && theme.Id != "Default")
                    {
                        _themes[theme.Id] = theme;
                        
                        // Register kingdom bindings
                        foreach (var kingdomId in theme.BoundKingdoms)
                        {
                            _kingdomToTheme[kingdomId.ToLowerInvariant()] = theme.Id;
                            LogMessage($"  Kingdom binding: {kingdomId} -> {theme.Id}");
                        }
                        
                        LogMessage($"Loaded theme: {theme.Name} ({theme.Id})");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error loading theme from {themeDir}: {ex.Message}");
                }
            }
            
            LogMessage($"Discovered {_themes.Count} themes total");
            
            // Debug: List all kingdom bindings
            LogMessage("Kingdom bindings:");
            foreach (var kvp in _kingdomToTheme)
            {
                LogMessage($"  {kvp.Key} -> {kvp.Value}");
            }
        }

        private Theme LoadThemeFromDirectory(string themeDir)
        {
            var folderId = Path.GetFileName(themeDir);
            var manifestPath = Path.Combine(themeDir, "ThemeManifest.xml");
            
            if (!File.Exists(manifestPath))
            {
                // Create theme from folder structure alone
                return new Theme
                {
                    Id = folderId,
                    Name = folderId,
                    Description = $"Theme: {folderId}",
                    Author = "Unknown",
                    Version = "1.0.0",
                    ThemePath = themeDir,
                    HasBrushOverrides = Directory.Exists(Path.Combine(themeDir, "GUI", "Brushes"))
                };
            }

            // Load from manifest
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            
            // Try both root element names for compatibility
            var root = doc.SelectSingleNode("ThemeManifest") ?? doc.SelectSingleNode("Theme");
            if (root == null)
            {
                LogMessage($"No ThemeManifest or Theme root in {manifestPath}");
                return null;
            }

            var theme = new Theme
            {
                Id = GetNodeText(root, "Id", folderId),
                Name = GetNodeText(root, "Name", folderId),
                Description = GetNodeText(root, "Description", ""),
                Author = GetNodeText(root, "Author", "Unknown"),
                Version = GetNodeText(root, "Version", "1.0.0"),
                ThemePath = themeDir,
                IsBuiltIn = false
            };

            // Load kingdom bindings - check both tag names for compatibility
            var bindingsNode = root.SelectSingleNode("KingdomBindings") ?? root.SelectSingleNode("BoundKingdoms");
            if (bindingsNode != null)
            {
                foreach (XmlNode node in bindingsNode.SelectNodes("Kingdom"))
                {
                    // Support both <Kingdom id="vlandia"/> and <Kingdom>vlandia</Kingdom>
                    var id = node.Attributes?["id"]?.Value ?? node.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(id))
                    {
                        theme.BoundKingdoms.Add(id);
                        LogMessage($"    Bound to kingdom: {id}");
                    }
                }
            }
            
            // Load culture bindings (same IDs as kingdoms, but explicit for character creation)
            var cultureBindingsNode = root.SelectSingleNode("CultureBindings");
            if (cultureBindingsNode != null)
            {
                foreach (XmlNode node in cultureBindingsNode.SelectNodes("Culture"))
                {
                    var id = node.Attributes?["id"]?.Value ?? node.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(id))
                        theme.BoundKingdoms.Add(id); // Reuse same list since IDs are identical
                }
            }

            // Load components
            var componentsNode = root.SelectSingleNode("Components");
            if (componentsNode != null)
            {
                theme.HasBrushOverrides = GetBoolAttribute(componentsNode, "Brushes", true);
                theme.HasSpriteOverrides = GetBoolAttribute(componentsNode, "Sprites", false);
            }
            else
            {
                theme.HasBrushOverrides = Directory.Exists(Path.Combine(themeDir, "GUI", "Brushes"));
            }

            // Load base culture for default color scheme
            var baseCultureNode = root.SelectSingleNode("BaseCulture");
            if (baseCultureNode != null)
            {
                theme.BaseCultureId = baseCultureNode.InnerText?.Trim();
            }
            
            // Initialize color scheme from base culture
            theme.InitializeColorScheme();
            
            // Load and apply color scheme overrides
            var colorSchemeNode = root.SelectSingleNode("ColorScheme");
            if (colorSchemeNode != null)
            {
                foreach (XmlNode colorNode in colorSchemeNode.ChildNodes)
                {
                    if (colorNode.NodeType == XmlNodeType.Element)
                    {
                        var colorValue = colorNode.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(colorValue))
                        {
                            var color = ColorScheme.HexToColor(colorValue);
                            theme.Colors.SetColor(colorNode.Name, color);
                        }
                    }
                }
                LogMessage($"  Loaded color scheme with {colorSchemeNode.ChildNodes.Count} color overrides");
            }
            
            // Load AutoTheme setting (defaults to true)
            var autoThemeNode = root.SelectSingleNode("AutoTheme");
            if (autoThemeNode != null)
            {
                var autoValue = autoThemeNode.InnerText?.Trim().ToLowerInvariant();
                theme.AutoTheme = autoValue != "false" && autoValue != "0" && autoValue != "no";
            }
            else
            {
                theme.AutoTheme = true; // Default enabled
            }

            // Discover brush files
            var brushesPath = Path.Combine(themeDir, "GUI", "Brushes");
            if (Directory.Exists(brushesPath))
            {
                theme.BrushFiles = Directory.GetFiles(brushesPath, "*.xml")
                    .Select(Path.GetFileName)
                    .ToList();
            }

            return theme;
        }

        /// <summary>
        /// Applies a theme by ID.
        /// </summary>
        public void ApplyTheme(string themeId)
        {
            if (string.IsNullOrEmpty(themeId))
                themeId = "Default";
                
            if (!_themes.ContainsKey(themeId) && themeId != "Default")
            {
                LogMessage($"Theme not found: {themeId}, using Default");
                themeId = "Default";
            }

            var oldTheme = _currentThemeId;
            if (oldTheme == themeId)
                return;

            _currentThemeId = themeId;
            
            // Set AutoTheme mode for this theme
            var theme = GetTheme(themeId);
            bool autoTheme = theme?.AutoTheme ?? true;
            Patches.BrushModifier.SetAutoThemeMode(themeId, autoTheme);
            
            // Apply theme using BrushModifier (directly modifies brush objects)
            Patches.BrushModifier.ApplyTheme(themeId);
            
            // Trigger UI refresh
            RefreshUI();
            
            LogMessage($"Theme switched: {oldTheme} -> {themeId}");
            
            // Always show message in-game so user knows theme changed
            InformationManager.DisplayMessage(new InformationMessage(
                $"[ThemeSwitcher] Theme: {themeId}",
                Colors.Cyan));
            
            OnThemeChanged?.Invoke(oldTheme, themeId);
        }

        /// <summary>
        /// Gets theme ID bound to a kingdom.
        /// </summary>
        public string GetThemeForKingdom(string kingdomId)
        {
            if (string.IsNullOrEmpty(kingdomId))
                return "Default";
                
            var key = kingdomId.ToLowerInvariant();
            return _kingdomToTheme.TryGetValue(key, out var themeId) ? themeId : "Default";
        }

        /// <summary>
        /// Handles kingdom change event.
        /// </summary>
        public void OnKingdomChanged(string kingdomId)
        {
            if (!(Settings.Instance?.AutoSwitchByKingdom ?? true))
                return;

            var themeId = GetThemeForKingdom(kingdomId);
            LogMessage($"Kingdom changed: {kingdomId} -> theme: {themeId}");
            ApplyTheme(themeId);
        }

        /// <summary>
        /// Gets a theme by ID.
        /// </summary>
        public Theme GetTheme(string themeId)
        {
            return _themes.TryGetValue(themeId, out var theme) ? theme : null;
        }

        /// <summary>
        /// Checks if theme exists.
        /// </summary>
        public bool HasTheme(string themeId) => _themes.ContainsKey(themeId);

        /// <summary>
        /// Forces UI refresh.
        /// </summary>
        public void RefreshUI()
        {
            try
            {
                var brushFactory = UIResourceManager.BrushFactory;
                if (brushFactory == null) return;

                // Trigger BrushChange event via reflection
                var field = typeof(BrushFactory).GetField("BrushChange",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                
                var brushChange = field?.GetValue(brushFactory) as Action;
                brushChange?.Invoke();
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing UI: {ex.Message}");
            }
        }

        private string GetThemesPath()
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var binDir = Path.GetDirectoryName(assemblyLocation);
            var modulePath = Path.GetFullPath(Path.Combine(binDir, "..", ".."));
            return Path.Combine(modulePath, "Themes");
        }

        private string GetNodeText(XmlNode parent, string childName, string defaultValue)
        {
            return parent.SelectSingleNode(childName)?.InnerText ?? defaultValue;
        }

        private bool GetBoolAttribute(XmlNode parent, string childName, bool defaultValue)
        {
            var node = parent.SelectSingleNode(childName);
            var attr = node?.Attributes?["enabled"];
            if (attr == null) return defaultValue;
            return attr.Value.ToLowerInvariant() == "true";
        }

        private void LogMessage(string message)
        {
            Debug.Print($"[ThemeSwitcher] {message}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _themes.Clear();
            _kingdomToTheme.Clear();
            _instance = null;
            _disposed = true;
        }
    }
}
