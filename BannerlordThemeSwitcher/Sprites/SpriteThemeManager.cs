using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HarmonyLib;
using TaleWorlds.Library;
using Texture = TaleWorlds.Engine.Texture;

namespace BannerlordThemeSwitcher.Sprites
{
    /// <summary>
    /// Central orchestrator for dynamic sprite theming.
    /// Coordinates SpriteLoader (static), SpriteRegistrar, and SpriteCache
    /// to provide a simple interface for ThemeManager integration.
    ///
    /// Pipeline:
    ///   Theme switch → check cache → miss? → SpriteLoader loads PNGs/sheets
    ///   → SpriteRegistrar registers in native SpriteData → SpriteCache stores
    ///   → hit? → SpriteRegistrar re-registers from cached PartInfos
    /// </summary>
    public class SpriteThemeManager : IDisposable
    {
        private static SpriteThemeManager _instance;
        public static SpriteThemeManager Instance => _instance;

        private readonly SpriteRegistrar _registrar = new SpriteRegistrar();
        private readonly SpriteCache _cache = new SpriteCache();

        private string _currentThemeId;
        private bool _disposed;

        public SpriteThemeManager()
        {
            _instance = this;
        }

        /// <summary>
        /// Initialize the sprite system. Must be called during SubModule load
        /// after Harmony is set up.
        /// </summary>
        public void Initialize(Harmony harmony)
        {
            _registrar.Initialize(harmony);
            Debug.Print("[ThemeSwitcher] SpriteThemeManager initialized");
        }

        /// <summary>
        /// Load and register sprites for a theme.
        /// Called by ThemeManager.ApplyTheme() after brush theming.
        ///
        /// Flow:
        ///   1. If theme has no sprites → unload current and return
        ///   2. If same theme → skip (already active)
        ///   3. Unregister current theme's sprites (keep in cache)
        ///   4. Try cache hit → re-register from cached data
        ///   5. Cache miss → load from disk, register, then cache
        /// </summary>
        public void LoadThemeSprites(Theme theme)
        {
            if (theme == null || theme.Id == "Default" || !theme.HasSpriteOverrides)
            {
                // No sprites for this theme — unload any active ones
                if (_currentThemeId != null)
                    UnloadCurrentSprites();
                return;
            }

            if (theme.Id == _currentThemeId)
                return; // Already active

            Debug.Print($"[ThemeSwitcher] SpriteThemeManager: Loading sprites for {theme.Id}");

            // Unload current theme's registrations (keep cached for fast switch-back)
            if (_currentThemeId != null)
                _registrar.Unregister();

            // Check cache first
            if (_cache.TryGet(theme.Id, out var cached))
            {
                Debug.Print($"[ThemeSwitcher] SpriteThemeManager: Cache hit for {theme.Id}");
                _registrar.Register(theme.Id, cached.Textures, cached.PartInfos);
                _currentThemeId = theme.Id;
                return;
            }

            // Cache miss — load fresh from disk
            LoadResult loadResult = LoadFromDisk(theme);

            if (loadResult == null || !loadResult.Success)
            {
                Debug.Print($"[ThemeSwitcher] SpriteThemeManager: No sprites loaded for {theme.Id}: " +
                    $"{loadResult?.Error ?? "no sprite directory found"}");
                return;
            }

            // Apply optional SpriteConfig.xml overrides (nine-slice borders, aliases)
            var configPath = Path.Combine(theme.ThemePath, "SpriteConfig.xml");
            if (File.Exists(configPath))
            {
                ApplySpriteConfig(configPath, loadResult.Parts);
            }

            // Register in native SpriteData system
            _registrar.Register(theme.Id, loadResult.Textures, loadResult.Parts);

            // Cache for fast switching later
            var cacheEntry = new SpriteCache.ThemeCacheEntry
            {
                Textures = loadResult.Textures,
                PartInfos = loadResult.Parts
            };
            _cache.Store(theme.Id, cacheEntry);

            _currentThemeId = theme.Id;

            Debug.Print($"[ThemeSwitcher] SpriteThemeManager: {theme.Id} sprites active " +
                $"({loadResult.Parts.Count} sprites, {loadResult.Textures.Count} textures)");
        }

        /// <summary>Unload current theme sprites and restore vanilla</summary>
        public void UnloadCurrentSprites()
        {
            if (_currentThemeId == null) return;

            _registrar.Unregister();
            _currentThemeId = null;

            Debug.Print("[ThemeSwitcher] SpriteThemeManager: Sprites unloaded, vanilla restored");
        }

        /// <summary>
        /// Checks if a theme directory contains sprite assets.
        /// Used by ThemeManager during theme discovery to set HasSpriteOverrides.
        /// </summary>
        public static bool DetectSpriteAssets(string themePath)
        {
            if (string.IsNullOrEmpty(themePath))
                return false;

            // Check for loose PNGs in Sprites/ folder
            var spritesDir = Path.Combine(themePath, "Sprites");
            if (Directory.Exists(spritesDir))
            {
                var pngs = Directory.GetFiles(spritesDir, "*.png", SearchOption.AllDirectories);
                if (pngs.Length > 0)
                    return true;
            }

            // Check for pre-built sprite sheets in GUI/SpriteParts/
            var sheetDir = Path.Combine(themePath, "GUI", "SpriteParts");
            if (Directory.Exists(sheetDir))
            {
                var subdirs = Directory.GetDirectories(sheetDir);
                foreach (var subdir in subdirs)
                {
                    var sheets = Directory.GetFiles(subdir, "*.png");
                    if (sheets.Length > 0)
                        return true;
                }
            }

            return false;
        }

        // =====================================================================
        // Private Implementation
        // =====================================================================

        /// <summary>
        /// Load sprite assets from disk. Tries pre-built sprite sheets first
        /// (only if SpriteConfig.xml defines parts), then falls back to loose PNGs.
        /// </summary>
        private LoadResult LoadFromDisk(Theme theme)
        {
            var spritesDir = Path.Combine(theme.ThemePath, "Sprites");
            var sheetDir = Path.Combine(theme.ThemePath, "GUI", "SpriteParts");
            var configPath = Path.Combine(theme.ThemePath, "SpriteConfig.xml");

            // Pre-built sprite sheets require SpriteConfig.xml to define part metadata
            // (the sheet is just a raw texture — without part definitions there's nothing to register).
            // Only use this path if both the sheets AND config exist.
            if (Directory.Exists(sheetDir) && File.Exists(configPath))
            {
                var allSheetTextures = new List<TaleWorlds.Engine.Texture>();

                var sheetDirs = Directory.GetDirectories(sheetDir);
                foreach (var dir in sheetDirs)
                {
                    var pngs = Directory.GetFiles(dir, "*.png");
                    foreach (var png in pngs)
                    {
                        Debug.Print($"[ThemeSwitcher] Loading pre-built sprite sheet: {png}");
                        var sheetResult = SpriteLoader.LoadSpriteSheet(png, 0, 0);
                        if (sheetResult.Success)
                        {
                            allSheetTextures.AddRange(sheetResult.Textures);
                        }
                    }
                }

                if (allSheetTextures.Count > 0)
                {
                    // Parts will be populated from SpriteConfig.xml by the caller
                    return new LoadResult
                    {
                        Textures = allSheetTextures,
                        Parts = new List<SpritePartInfo>(), // populated via ApplySpriteConfig
                        Success = true
                    };
                }
            }

            // Load loose PNGs (with automatic atlas packing — primary path)
            if (Directory.Exists(spritesDir))
            {
                Debug.Print($"[ThemeSwitcher] Loading loose PNGs from {spritesDir}");
                return SpriteLoader.LoadLoosePNGs(spritesDir);
            }

            return null;
        }

        /// <summary>
        /// Parse optional SpriteConfig.xml for nine-slice border overrides and sprite aliases.
        ///
        /// Format:
        /// <![CDATA[
        /// <SpriteConfig>
        ///   <NineRegion sprite="button_canvas_9" left="12" right="12" top="12" bottom="12" />
        ///   <Alias file="my_button.png" replaces="ButtonBrush1.Sprite" />
        /// </SpriteConfig>
        /// ]]>
        /// </summary>
        private void ApplySpriteConfig(string configPath, List<SpritePartInfo> parts)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(configPath);
                var root = doc.DocumentElement;
                if (root == null) return;

                int nineRegionOverrides = 0;
                int aliasCount = 0;

                // Apply NineRegion border overrides
                var nineNodes = root.SelectNodes("NineRegion");
                if (nineNodes != null)
                {
                    foreach (XmlNode node in nineNodes)
                    {
                        var spriteName = node.Attributes?["sprite"]?.Value;
                        if (string.IsNullOrEmpty(spriteName)) continue;

                        var part = parts.Find(p => p.SpriteName == spriteName);
                        if (part == null) continue;

                        part.IsNineRegion = true;
                        if (int.TryParse(node.Attributes?["left"]?.Value, out int left))
                            part.NineLeft = left;
                        if (int.TryParse(node.Attributes?["right"]?.Value, out int right))
                            part.NineRight = right;
                        if (int.TryParse(node.Attributes?["top"]?.Value, out int top))
                            part.NineTop = top;
                        if (int.TryParse(node.Attributes?["bottom"]?.Value, out int bottom))
                            part.NineBottom = bottom;

                        nineRegionOverrides++;
                    }
                }

                // Apply aliases (rename sprite targets to match vanilla sprite names)
                var aliasNodes = root.SelectNodes("Alias");
                if (aliasNodes != null)
                {
                    foreach (XmlNode node in aliasNodes)
                    {
                        var file = node.Attributes?["file"]?.Value;
                        var replaces = node.Attributes?["replaces"]?.Value;
                        if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(replaces)) continue;

                        var fileName = Path.GetFileNameWithoutExtension(file);
                        var part = parts.Find(p =>
                            p.SpriteName == fileName ||
                            p.SpriteName.EndsWith("\\" + fileName));

                        if (part != null)
                        {
                            Debug.Print($"[ThemeSwitcher] SpriteConfig: Alias {part.SpriteName} -> {replaces}");
                            part.SpriteName = replaces;
                            aliasCount++;
                        }
                    }
                }

                Debug.Print($"[ThemeSwitcher] SpriteConfig: Applied {nineRegionOverrides} nine-region overrides, " +
                    $"{aliasCount} aliases from {configPath}");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] SpriteConfig parse error (using defaults): {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            UnloadCurrentSprites();
            _cache.Dispose();
            _instance = null;
            _disposed = true;

            Debug.Print("[ThemeSwitcher] SpriteThemeManager disposed");
        }
    }
}
