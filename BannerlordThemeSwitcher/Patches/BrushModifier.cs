using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;

namespace BannerlordThemeSwitcher.Patches
{
    /// <summary>
    /// Modifies brush objects at runtime using themed brush XMLs.
    /// 
    /// Supports two modes:
    /// 1. XML-based: Load brush definitions from XML files using ColorRef attributes
    ///    Example: ColorRef="Primary", FontColorRef="TextHighlight"
    /// 2. AutoTheme: Pattern-based automatic theming when no XML definition exists
    /// 
    /// ColorRef attributes reference colors from the theme's ColorScheme.
    /// </summary>
    public static class BrushModifier
    {
        private static Dictionary<string, Brush> _originalBrushes = new Dictionary<string, Brush>();
        private static FieldInfo _brushesField;
        private static FieldInfo _stylesField;
        private static FieldInfo _layersField;
        private static bool _initialized = false;
        private static string _currentThemeId = null;
        
        // Parsed brush data: ThemeId -> BaseBrushName -> BrushData
        private static Dictionary<string, Dictionary<string, ThemeBrushData>> _themeBrushData = 
            new Dictionary<string, Dictionary<string, ThemeBrushData>>();
        
        // Theme settings
        private static Dictionary<string, bool> _themeAutoMode = new Dictionary<string, bool>();

        public static void Initialize()
        {
            try
            {
                Debug.Print("[ThemeSwitcher] BrushModifier initializing...");
                
                _brushesField = typeof(BrushFactory).GetField("_brushes", 
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _stylesField = typeof(Brush).GetField("_styles",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _layersField = typeof(Style).GetField("_layers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                    
                if (_brushesField == null)
                {
                    Debug.Print("[ThemeSwitcher] ERROR: Cannot find _brushes field!");
                    return;
                }
                
                _initialized = true;
                Debug.Print("[ThemeSwitcher] BrushModifier initialized");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] BrushModifier init error: {ex}");
            }
        }

        /// <summary>
        /// Set whether a theme uses AutoTheme mode (pattern-based theming)
        /// </summary>
        public static void SetAutoThemeMode(string themeId, bool enabled)
        {
            _themeAutoMode[themeId] = enabled;
        }

        /// <summary>
        /// Load themed brushes from XML file using ColorRef attributes
        /// </summary>
        public static void LoadThemedBrushesXml(string xmlPath, string themeId, ColorScheme scheme)
        {
            if (!File.Exists(xmlPath))
            {
                Debug.Print($"[ThemeSwitcher] Brush file not found: {xmlPath}");
                return;
            }

            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                
                if (!_themeBrushData.ContainsKey(themeId))
                    _themeBrushData[themeId] = new Dictionary<string, ThemeBrushData>();
                
                var brushNodes = doc.SelectNodes("//Brush[@Name]");
                int loadedCount = 0;
                
                foreach (XmlNode brushNode in brushNodes)
                {
                    var fullName = brushNode.Attributes["Name"]?.Value;
                    if (string.IsNullOrEmpty(fullName)) continue;
                    
                    // Extract base name (remove .ThemeId suffix if present)
                    string baseName = fullName;
                    var lastDot = fullName.LastIndexOf('.');
                    if (lastDot > 0)
                    {
                        var suffix = fullName.Substring(lastDot + 1);
                        if (suffix.Equals(themeId, StringComparison.OrdinalIgnoreCase))
                            baseName = fullName.Substring(0, lastDot);
                    }
                    
                    var brushData = ParseBrushNode(brushNode, scheme);
                    if (brushData != null)
                    {
                        brushData.BaseName = baseName;
                        _themeBrushData[themeId][baseName] = brushData;
                        loadedCount++;
                    }
                }
                
                Debug.Print($"[ThemeSwitcher] Loaded {loadedCount} brushes from {Path.GetFileName(xmlPath)}");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Error loading brush XML: {ex.Message}");
            }
        }

        /// <summary>
        /// Parse a brush node, resolving ColorRef attributes against the scheme
        /// </summary>
        private static ThemeBrushData ParseBrushNode(XmlNode brushNode, ColorScheme scheme)
        {
            var data = new ThemeBrushData();
            
            // Parse layers
            foreach (XmlNode layerNode in brushNode.SelectNodes("Layers/BrushLayer"))
            {
                var layerName = layerNode.Attributes["Name"]?.Value ?? "Default";
                
                // Check for ColorRef first, then fall back to Color
                Color? color = ResolveColor(layerNode, "ColorRef", "Color", scheme);
                if (color.HasValue)
                {
                    data.LayerColors[layerName] = color.Value;
                    if (layerName == "Default")
                    {
                        data.LayerColor = color.Value;
                        data.HasLayerColor = true;
                    }
                }
            }
            
            // Parse styles
            foreach (XmlNode styleNode in brushNode.SelectNodes("Styles/Style"))
            {
                var styleName = styleNode.Attributes["Name"]?.Value;
                if (string.IsNullOrEmpty(styleName)) continue;
                
                var styleData = new StyleColorData();
                
                // Font colors
                Color? fontColor = ResolveColor(styleNode, "FontColorRef", "FontColor", scheme);
                if (fontColor.HasValue)
                {
                    styleData.FontColor = fontColor.Value;
                    styleData.HasFontColor = true;
                }
                
                Color? glowColor = ResolveColor(styleNode, "TextGlowColorRef", "TextGlowColor", scheme);
                if (glowColor.HasValue)
                {
                    styleData.TextGlowColor = glowColor.Value;
                    styleData.HasGlowColor = true;
                }
                
                Color? outlineColor = ResolveColor(styleNode, "TextOutlineColorRef", "TextOutlineColor", scheme);
                if (outlineColor.HasValue)
                {
                    styleData.TextOutlineColor = outlineColor.Value;
                    styleData.HasOutlineColor = true;
                }
                
                // Style layer colors
                foreach (XmlNode layerNode in styleNode.SelectNodes("BrushLayer"))
                {
                    var layerName = layerNode.Attributes["Name"]?.Value ?? "Default";
                    Color? layerColor = ResolveColor(layerNode, "ColorRef", "Color", scheme);
                    if (layerColor.HasValue)
                    {
                        styleData.LayerColors[layerName] = layerColor.Value;
                        if (layerName == "Default")
                        {
                            styleData.LayerColor = layerColor.Value;
                            styleData.HasLayerColor = true;
                        }
                    }
                }
                
                data.StyleColors[styleName.ToLowerInvariant()] = styleData;
                
                // Copy default style colors to top level
                if (styleName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    if (styleData.HasFontColor)
                    {
                        data.FontColor = styleData.FontColor;
                        data.HasFontColor = true;
                    }
                    if (styleData.HasGlowColor)
                    {
                        data.TextGlowColor = styleData.TextGlowColor;
                        data.HasGlowColor = true;
                    }
                    if (styleData.HasOutlineColor)
                    {
                        data.TextOutlineColor = styleData.TextOutlineColor;
                        data.HasOutlineColor = true;
                    }
                }
            }
            
            return data;
        }

        /// <summary>
        /// Resolve color from either a Ref attribute or direct value
        /// </summary>
        private static Color? ResolveColor(XmlNode node, string refAttr, string valueAttr, ColorScheme scheme)
        {
            // First check for ColorRef attribute
            var refValue = node.Attributes?[refAttr]?.Value;
            if (!string.IsNullOrEmpty(refValue))
            {
                return scheme.GetColor(refValue);
            }
            
            // Fall back to direct color value
            var colorValue = node.Attributes?[valueAttr]?.Value;
            if (!string.IsNullOrEmpty(colorValue))
            {
                return ParseColor(colorValue);
            }
            
            return null;
        }

        /// <summary>
        /// Apply a theme
        /// </summary>
        public static void ApplyTheme(string themeId)
        {
            if (!_initialized)
            {
                Initialize();
                if (!_initialized) return;
            }
            
            if (themeId == _currentThemeId)
                return;
                
            Debug.Print($"[ThemeSwitcher] ═══════════════════════════════════════════════════════");
            Debug.Print($"[ThemeSwitcher] APPLYING THEME: {themeId}");
            
            try
            {
                var brushFactory = UIResourceManager.BrushFactory;
                if (brushFactory == null)
                {
                    Debug.Print("[ThemeSwitcher] BrushFactory is null!");
                    return;
                }
                
                var brushes = _brushesField.GetValue(brushFactory) as Dictionary<string, Brush>;
                if (brushes == null)
                {
                    Debug.Print("[ThemeSwitcher] Cannot get brushes dictionary!");
                    return;
                }
                
                // Get theme
                var theme = ThemeManager.Instance?.GetTheme(themeId);
                ColorScheme colors = theme?.Colors ?? DefaultColorSchemes.Default;
                bool autoTheme = _themeAutoMode.TryGetValue(themeId, out var auto) && auto;
                
                Debug.Print($"[ThemeSwitcher] Primary: {ColorScheme.ColorToHex(colors.Primary)}, AutoTheme: {autoTheme}");
                
                // Restore originals
                if (_currentThemeId != null && _originalBrushes.Count > 0)
                {
                    foreach (var kvp in _originalBrushes)
                    {
                        if (brushes.ContainsKey(kvp.Key))
                            brushes[kvp.Key] = kvp.Value;
                    }
                    _originalBrushes.Clear();
                }
                
                // Reset to default
                if (themeId == "Default" || string.IsNullOrEmpty(themeId))
                {
                    _currentThemeId = "Default";
                    BrushRendererPatch.SetThemeColors(null);
                    Debug.Print("[ThemeSwitcher] Reset to default theme");
                    return;
                }
                
                // Get brush data for this theme (load from XML if not already loaded)
                if (!_themeBrushData.ContainsKey(themeId))
                {
                    LoadThemeBrushFiles(themeId, colors);
                }
                
                var themeBrushes = _themeBrushData.TryGetValue(themeId, out var data) ? data : null;
                var renderColors = new Dictionary<string, Color>();
                int xmlModified = 0;
                int autoModified = 0;
                
                foreach (var kvp in brushes.ToList())
                {
                    var brushName = kvp.Key;
                    var brush = kvp.Value;
                    ThemeBrushData brushData = null;
                    
                    // Check for XML-defined brush
                    if (themeBrushes != null && themeBrushes.TryGetValue(brushName, out brushData))
                    {
                        if (!_originalBrushes.ContainsKey(brushName))
                            _originalBrushes[brushName] = brush;
                        
                        var themed = ApplyBrushData(brush, brushData);
                        if (themed != null)
                        {
                            brushes[brushName] = themed;
                            xmlModified++;
                            if (brushData.HasLayerColor)
                                renderColors[brushName] = brushData.LayerColor;
                        }
                    }
                    // AutoTheme mode - apply pattern-based theming
                    else if (autoTheme)
                    {
                        brushData = GetAutoThemeBrushData(brushName, colors);
                        if (brushData != null)
                        {
                            if (!_originalBrushes.ContainsKey(brushName))
                                _originalBrushes[brushName] = brush;
                            
                            var themed = ApplyBrushData(brush, brushData);
                            if (themed != null)
                            {
                                brushes[brushName] = themed;
                                autoModified++;
                                if (brushData.HasLayerColor)
                                    renderColors[brushName] = brushData.LayerColor;
                            }
                        }
                    }
                }
                
                _currentThemeId = themeId;
                BrushRendererPatch.SetThemeColors(renderColors);
                
                Debug.Print($"[ThemeSwitcher] Modified: {xmlModified} from XML, {autoModified} from AutoTheme");
                Debug.Print($"[ThemeSwitcher] ═══════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Error applying theme: {ex}");
            }
        }

        /// <summary>
        /// Load brush XML files for a theme
        /// </summary>
        private static void LoadThemeBrushFiles(string themeId, ColorScheme scheme)
        {
            var theme = ThemeManager.Instance?.GetTheme(themeId);
            var modPath = Path.Combine(BasePath.Name, "Modules", "BannerlordThemeSwitcher");
            
            Debug.Print($"[ThemeSwitcher] LoadThemeBrushFiles for {themeId}");
            Debug.Print($"[ThemeSwitcher] Module path: {modPath}");
            
            // Load shared brushes from Data folder (not GUI/Brushes to avoid game auto-loading)
            var sharedBrushes = Path.Combine(modPath, "Data", "ThemedBrushes.xml");
            Debug.Print($"[ThemeSwitcher] Looking for shared brushes at: {sharedBrushes}");
            
            if (File.Exists(sharedBrushes))
            {
                Debug.Print($"[ThemeSwitcher] Found! Loading shared brushes...");
                LoadThemedBrushesXml(sharedBrushes, themeId, scheme);
            }
            else
            {
                Debug.Print($"[ThemeSwitcher] WARNING: Shared brushes file NOT FOUND!");
            }
            
            if (theme == null || string.IsNullOrEmpty(theme.ThemePath))
                return;
            
            Debug.Print($"[ThemeSwitcher] Theme path: {theme.ThemePath}");
            
            // Load theme-specific brush files from theme's Data folder
            var themeDataPath = Path.Combine(theme.ThemePath, "Data");
            if (Directory.Exists(themeDataPath))
            {
                foreach (var file in Directory.GetFiles(themeDataPath, "*.xml"))
                {
                    Debug.Print($"[ThemeSwitcher] Loading theme data: {file}");
                    LoadThemedBrushesXml(file, themeId, scheme);
                }
            }
            
            // Also check GUI/Brushes for backwards compatibility (theme authors)
            var brushesPath = Path.Combine(theme.ThemePath, "GUI", "Brushes");
            if (Directory.Exists(brushesPath))
            {
                foreach (var file in Directory.GetFiles(brushesPath, "*.xml"))
                {
                    Debug.Print($"[ThemeSwitcher] Loading theme brushes: {file}");
                    LoadThemedBrushesXml(file, themeId, scheme);
                }
            }
        }

        /// <summary>
        /// Apply brush data to create a themed brush
        /// </summary>
        private static Brush ApplyBrushData(Brush original, ThemeBrushData data)
        {
            try
            {
                var themed = original.Clone();
                
                // Apply default style colors
                if (themed.DefaultStyle != null)
                {
                    if (data.HasFontColor)
                        themed.DefaultStyle.FontColor = data.FontColor;
                    if (data.HasGlowColor)
                        themed.DefaultStyle.TextGlowColor = data.TextGlowColor;
                    if (data.HasOutlineColor)
                        themed.DefaultStyle.TextOutlineColor = data.TextOutlineColor;
                }
                
                // Apply layer colors
                if (data.HasLayerColor)
                {
                    foreach (var layer in themed.Layers)
                    {
                        if (data.LayerColors.TryGetValue(layer.Name, out var color))
                            layer.Color = color;
                        else
                            layer.Color = data.LayerColor;
                    }
                }
                
                // Apply style colors
                var styles = _stylesField?.GetValue(themed) as Dictionary<string, Style>;
                if (styles != null)
                {
                    foreach (var styleKvp in styles)
                    {
                        var styleName = styleKvp.Key.ToLowerInvariant();
                        var style = styleKvp.Value;
                        
                        StyleColorData styleData = null;
                        if (data.StyleColors.TryGetValue(styleName, out styleData))
                        {
                            if (styleData.HasFontColor)
                                style.FontColor = styleData.FontColor;
                            if (styleData.HasGlowColor)
                                style.TextGlowColor = styleData.TextGlowColor;
                            if (styleData.HasOutlineColor)
                                style.TextOutlineColor = styleData.TextOutlineColor;
                            
                            // Apply style layer colors
                            var styleLayers = _layersField?.GetValue(style) as Dictionary<string, StyleLayer>;
                            if (styleLayers != null && (styleData.HasLayerColor || styleData.LayerColors.Count > 0))
                            {
                                foreach (var layerKvp in styleLayers)
                                {
                                    if (styleData.LayerColors.TryGetValue(layerKvp.Key, out var layerColor))
                                        layerKvp.Value.Color = layerColor;
                                    else if (styleData.HasLayerColor)
                                        layerKvp.Value.Color = styleData.LayerColor;
                                }
                            }
                        }
                        else if (data.HasFontColor || data.HasLayerColor)
                        {
                            // Apply default colors to unspecified styles
                            if (data.HasFontColor)
                                style.FontColor = data.FontColor;
                            
                            var styleLayers = _layersField?.GetValue(style) as Dictionary<string, StyleLayer>;
                            if (styleLayers != null && data.HasLayerColor)
                            {
                                foreach (var layerKvp in styleLayers)
                                {
                                    layerKvp.Value.Color = data.LayerColor;
                                }
                            }
                        }
                    }
                }
                
                return themed;
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Error applying brush data to {original.Name}: {ex.Message}");
                return null;
            }
        }

        #region AutoTheme Pattern Matching
        
        /// <summary>
        /// Generate brush data based on brush name patterns (AutoTheme mode)
        /// </summary>
        private static ThemeBrushData GetAutoThemeBrushData(string brushName, ColorScheme c)
        {
            var name = brushName.ToLowerInvariant();
            
            // Skip certain brushes
            if (ShouldSkipBrush(name))
                return null;
            
            var data = new ThemeBrushData { BaseName = brushName };
            bool matched = false;
            
            // Text brushes
            if (IsTextBrush(name))
            {
                matched = ApplyTextPattern(name, data, c);
            }
            // Button brushes
            else if (name.Contains("button") || name.Contains("btn"))
            {
                matched = ApplyButtonPattern(name, data, c);
            }
            // Frame/border brushes
            else if (name.Contains("frame") || name.Contains("border") || name.Contains("outline"))
            {
                matched = ApplyFramePattern(name, data, c);
            }
            // Background brushes
            else if (name.Contains("background") || name.Contains("canvas") || name.Contains("panel") ||
                     name.Contains("fill") || name.Contains("backdrop"))
            {
                matched = ApplyBackgroundPattern(name, data, c);
            }
            // UI component brushes
            else
            {
                matched = ApplyComponentPattern(name, data, c);
            }
            
            return matched ? data : null;
        }
        
        private static bool ShouldSkipBrush(string name)
        {
            return name.Contains("debug") || name.Contains("empty") || name.Contains("invisible") ||
                   name.Contains("transparent") || name.Contains("clear") ||
                   (name.Contains("icon") && !name.Contains("background") && !name.Contains("frame")) ||
                   (name.Contains("sprite") && !name.Contains("background")) ||
                   name.Contains("crest") || name.Contains("sigil") || name.Contains("emblem");
        }
        
        private static bool IsTextBrush(string name)
        {
            return name.Contains("text") || name.Contains("label") || name.Contains("title") ||
                   name.Contains("header") || name.Contains("caption") || name.EndsWith(".t") ||
                   name.Contains("font");
        }
        
        private static bool ApplyTextPattern(string name, ThemeBrushData data, ColorScheme c)
        {
            if (name.Contains("title") || name.Contains("header") || name.Contains("caption"))
            {
                data.FontColor = c.TextTitle;
                data.HasFontColor = true;
                data.TextOutlineColor = c.Shadow;
                data.HasOutlineColor = true;
            }
            else if (name.Contains("gold") || name.Contains("price") || name.Contains("cost") || name.Contains("denar"))
            {
                data.FontColor = c.Gold;
                data.HasFontColor = true;
            }
            else if (name.Contains("positive") || name.Contains("success") || name.Contains("bonus") || name.Contains("green"))
            {
                data.FontColor = c.Success;
                data.HasFontColor = true;
            }
            else if (name.Contains("negative") || name.Contains("error") || name.Contains("penalty") || name.Contains("red"))
            {
                data.FontColor = c.Error;
                data.HasFontColor = true;
            }
            else if (name.Contains("warning") || name.Contains("caution") || name.Contains("yellow"))
            {
                data.FontColor = c.Warning;
                data.HasFontColor = true;
            }
            else if (name.Contains("muted") || name.Contains("hint") || name.Contains("secondary") || name.Contains("description"))
            {
                data.FontColor = c.TextMuted;
                data.HasFontColor = true;
            }
            else if (name.Contains("disabled") || name.Contains("inactive") || name.Contains("locked"))
            {
                data.FontColor = c.TextDisabled;
                data.HasFontColor = true;
            }
            else if (name.Contains("highlight") || name.Contains("link") || name.Contains("selected"))
            {
                data.FontColor = c.TextHighlight;
                data.HasFontColor = true;
            }
            else
            {
                data.FontColor = c.Text;
                data.HasFontColor = true;
            }
            
            // Add hover style
            data.StyleColors["hovered"] = new StyleColorData { FontColor = c.TextHighlight, HasFontColor = true };
            
            return true;
        }
        
        private static bool ApplyButtonPattern(string name, ThemeBrushData data, ColorScheme c)
        {
            if (name.Contains("frame") || name.Contains("border"))
            {
                data.LayerColor = c.ButtonBorder;
                data.HasLayerColor = true;
                data.StyleColors["hovered"] = new StyleColorData { LayerColor = c.BorderHighlight, HasLayerColor = true };
                data.StyleColors["disabled"] = new StyleColorData { LayerColor = c.BorderMuted, HasLayerColor = true };
            }
            else if (name.Contains("text"))
            {
                data.FontColor = c.Text;
                data.HasFontColor = true;
                data.StyleColors["hovered"] = new StyleColorData { FontColor = c.TextHighlight, HasFontColor = true };
                data.StyleColors["pressed"] = new StyleColorData { FontColor = c.Primary, HasFontColor = true };
            }
            else
            {
                data.LayerColor = c.ButtonBackground;
                data.HasLayerColor = true;
                data.StyleColors["hovered"] = new StyleColorData { LayerColor = c.ButtonHover, HasLayerColor = true };
                data.StyleColors["pressed"] = new StyleColorData { LayerColor = c.ButtonPressed, HasLayerColor = true };
                data.StyleColors["selected"] = new StyleColorData { LayerColor = c.ButtonPressed, HasLayerColor = true };
                data.StyleColors["disabled"] = new StyleColorData { LayerColor = c.ButtonDisabled, HasLayerColor = true };
            }
            return true;
        }
        
        private static bool ApplyFramePattern(string name, ThemeBrushData data, ColorScheme c)
        {
            if (name.Contains("highlight") || name.Contains("selected") || name.Contains("gold"))
            {
                data.LayerColor = c.BorderHighlight;
            }
            else if (name.Contains("muted") || name.Contains("subtle") || name.Contains("secondary"))
            {
                data.LayerColor = c.BorderMuted;
            }
            else
            {
                data.LayerColor = c.Border;
            }
            data.HasLayerColor = true;
            data.StyleColors["hovered"] = new StyleColorData { LayerColor = c.BorderHighlight, HasLayerColor = true };
            data.StyleColors["selected"] = new StyleColorData { LayerColor = c.BorderHighlight, HasLayerColor = true };
            return true;
        }
        
        private static bool ApplyBackgroundPattern(string name, ThemeBrushData data, ColorScheme c)
        {
            if (name.Contains("dark") || name.Contains("overlay") || name.Contains("modal") || name.Contains("popup"))
            {
                data.LayerColor = c.BackgroundDark;
            }
            else if (name.Contains("light") || name.Contains("elevated") || name.Contains("card"))
            {
                data.LayerColor = c.BackgroundLight;
            }
            else if (name.Contains("accent") || name.Contains("tint"))
            {
                data.LayerColor = c.BackgroundAccent;
            }
            else
            {
                data.LayerColor = c.Background;
            }
            data.HasLayerColor = true;
            data.StyleColors["hovered"] = new StyleColorData { LayerColor = c.BackgroundHover, HasLayerColor = true };
            data.StyleColors["selected"] = new StyleColorData { LayerColor = c.BackgroundSelected, HasLayerColor = true };
            return true;
        }
        
        private static bool ApplyComponentPattern(string name, ThemeBrushData data, ColorScheme c)
        {
            // Scrollbar
            if (name.Contains("scroll"))
            {
                if (name.Contains("thumb") || name.Contains("handle"))
                {
                    data.LayerColor = c.Border;
                    data.StyleColors["hovered"] = new StyleColorData { LayerColor = c.BorderHighlight, HasLayerColor = true };
                }
                else
                {
                    data.LayerColor = c.BackgroundLight;
                }
                data.HasLayerColor = true;
                return true;
            }
            
            // Slider
            if (name.Contains("slider"))
            {
                if (name.Contains("thumb") || name.Contains("handle") || name.Contains("fill"))
                    data.LayerColor = c.Primary;
                else
                    data.LayerColor = c.BackgroundLight;
                data.HasLayerColor = true;
                return true;
            }
            
            // Checkbox/toggle
            if (name.Contains("checkbox") || name.Contains("toggle"))
            {
                data.LayerColor = c.Border;
                data.HasLayerColor = true;
                data.StyleColors["selected"] = new StyleColorData { LayerColor = c.Primary, HasLayerColor = true };
                return true;
            }
            
            // Progress bars
            if (name.Contains("progress") || name.Contains("bar") || name.Contains("meter"))
            {
                if (name.Contains("health") || name.Contains("hp"))
                    data.LayerColor = c.Health;
                else if (name.Contains("experience") || name.Contains("xp"))
                    data.LayerColor = c.Experience;
                else if (name.Contains("morale"))
                    data.LayerColor = c.Morale;
                else if (name.Contains("fill"))
                    data.LayerColor = c.Primary;
                else
                    data.LayerColor = c.BackgroundDark;
                data.HasLayerColor = true;
                return true;
            }
            
            // Tabs/list items
            if (name.Contains("tab") || name.Contains("list") || name.Contains("item") || 
                name.Contains("row") || name.Contains("entry") || name.Contains("dropdown"))
            {
                data.LayerColor = c.Background;
                data.HasLayerColor = true;
                data.StyleColors["hovered"] = new StyleColorData { LayerColor = c.BackgroundHover, HasLayerColor = true };
                data.StyleColors["selected"] = new StyleColorData { LayerColor = c.BackgroundSelected, HasLayerColor = true };
                return true;
            }
            
            // Dividers
            if (name.Contains("divider") || name.Contains("separator") || name.Contains("line"))
            {
                data.LayerColor = c.BorderMuted;
                data.HasLayerColor = true;
                return true;
            }
            
            // Tooltips
            if (name.Contains("tooltip") || name.Contains("tip"))
            {
                data.LayerColor = c.BackgroundDark;
                data.HasLayerColor = true;
                return true;
            }
            
            // Input fields
            if (name.Contains("input") || name.Contains("field") || name.Contains("textbox") || name.Contains("edit"))
            {
                data.LayerColor = c.BackgroundDark;
                data.HasLayerColor = true;
                data.StyleColors["hovered"] = new StyleColorData { LayerColor = c.Background, HasLayerColor = true };
                return true;
            }
            
            return false;
        }
        
        #endregion

        private static Color ParseColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6) hex += "FF";
                uint val = Convert.ToUInt32(hex, 16);
                float r = ((val >> 24) & 0xFF) / 255f;
                float g = ((val >> 16) & 0xFF) / 255f;
                float b = ((val >> 8) & 0xFF) / 255f;
                float a = (val & 0xFF) / 255f;
                return new Color(r, g, b, a);
            }
            catch { return Color.White; }
        }

        #region Data Classes
        
        private class ThemeBrushData
        {
            public string BaseName;
            public Color FontColor;
            public bool HasFontColor;
            public Color TextGlowColor;
            public bool HasGlowColor;
            public Color TextOutlineColor;
            public bool HasOutlineColor;
            public Color LayerColor;
            public bool HasLayerColor;
            public Dictionary<string, Color> LayerColors = new Dictionary<string, Color>();
            public Dictionary<string, StyleColorData> StyleColors = new Dictionary<string, StyleColorData>();
        }
        
        private class StyleColorData
        {
            public Color FontColor;
            public bool HasFontColor;
            public Color TextGlowColor;
            public bool HasGlowColor;
            public Color TextOutlineColor;
            public bool HasOutlineColor;
            public Color LayerColor;
            public bool HasLayerColor;
            public Dictionary<string, Color> LayerColors = new Dictionary<string, Color>();
        }
        
        #endregion
    }
}
