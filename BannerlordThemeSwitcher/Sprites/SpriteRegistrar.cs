using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using Texture = TaleWorlds.Engine.Texture;

namespace BannerlordThemeSwitcher.Sprites
{
    /// <summary>
    /// Registers theme sprites in the native UIResourceManager.SpriteData system.
    /// Primary: direct dictionary access via public properties.
    /// Fallback: Harmony patch on sprite lookup if properties unavailable.
    ///
    /// Real API (Bannerlord 1.3.x decompiled):
    ///   SpriteData.Sprites          → Dictionary&lt;string, Sprite&gt;
    ///   SpriteData.SpriteParts      → Dictionary&lt;string, SpritePart&gt;
    ///   SpriteData.SpriteCategories → Dictionary&lt;string, SpriteCategory&gt;
    ///   SpriteCategory(name, sheetCount, alwaysLoad)
    ///   SpriteGeneric(name, spritePart, in SpriteNinePatchParameters)
    ///   SpriteNinePatchParameters(left, right, top, bottom) — struct
    /// </summary>
    public class SpriteRegistrar
    {
        private bool _useDirectAccess;
        private bool _useHarmonyFallback;
        private bool _initialized;

        // Backup of overridden vanilla sprites for restore
        private readonly Dictionary<string, Sprite> _originalSprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, SpritePart> _originalParts = new Dictionary<string, SpritePart>();

        // Currently registered theme sprites (for cleanup)
        private readonly HashSet<string> _registeredSpriteNames = new HashSet<string>();
        private readonly HashSet<string> _registeredPartNames = new HashSet<string>();
        private string _registeredCategoryName;

        // Harmony fallback data (static for patch access)
        private static Dictionary<string, Sprite> _harmonyOverrides;
        private Harmony _harmony;

        public bool IsInitialized => _initialized;

        /// <summary>Initialize and detect available registration method</summary>
        public void Initialize(Harmony harmony)
        {
            _harmony = harmony;

            try
            {
                // Test direct access to SpriteData public properties
                var spriteData = UIResourceManager.SpriteData;
                if (spriteData != null &&
                    spriteData.Sprites != null &&
                    spriteData.SpriteCategories != null &&
                    spriteData.SpriteParts != null)
                {
                    _useDirectAccess = true;
                    Debug.Print("[ThemeSwitcher] SpriteRegistrar: Using direct SpriteData access");
                }
                else
                {
                    throw new Exception("SpriteData properties returned null");
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] SpriteRegistrar: Direct access failed ({ex.Message}), " +
                    "trying Harmony fallback");
                SetupHarmonyFallback();
            }

            _initialized = true;
        }

        /// <summary>
        /// Register theme sprites in the native system.
        /// </summary>
        public void Register(string themeId, List<Texture> textures,
            List<SpritePartInfo> parts)
        {
            if (!_initialized) return;

            if (_useDirectAccess)
                RegisterDirect(themeId, textures, parts);
            else if (_useHarmonyFallback)
                RegisterHarmony(themeId, textures, parts);
        }

        /// <summary>Unregister all theme sprites and restore vanilla originals</summary>
        public void Unregister()
        {
            if (_useDirectAccess)
                UnregisterDirect();
            else if (_useHarmonyFallback)
                UnregisterHarmony();
        }

        #region Direct Access

        private void RegisterDirect(string themeId, List<Texture> textures,
            List<SpritePartInfo> parts)
        {
            var spriteData = UIResourceManager.SpriteData;
            var categoryName = $"ui_theme_{themeId.ToLowerInvariant()}";

            try
            {
                // Create a SpriteCategory for this theme
                // Real signature: SpriteCategory(string name, int spriteSheetCount, bool alwaysLoad = false)
                var category = new SpriteCategory(categoryName, textures.Count, false);

                // Assign textures to the category's SpriteSheets
                // SpriteCategory.SpriteSheets expects TaleWorlds.TwoDimension.Texture,
                // so we wrap each Engine.Texture via the EngineTexture adapter
                for (int i = 0; i < textures.Count; i++)
                {
                    var twoDimTex = new TaleWorlds.TwoDimension.Texture(new EngineTexture(textures[i]));
                    category.SpriteSheets.Add(twoDimTex);
                }

                // Register the category
                if (spriteData.SpriteCategories.ContainsKey(categoryName))
                    spriteData.SpriteCategories[categoryName] = category;
                else
                    spriteData.SpriteCategories.Add(categoryName, category);
                _registeredCategoryName = categoryName;

                // Register each sprite part and sprite
                foreach (var partInfo in parts)
                {
                    var partName = $"{themeId}\\{partInfo.SpriteName}";

                    // Create SpritePart
                    // Real signature: SpritePart(string name, SpriteCategory category, int width, int height)
                    var spritePart = new SpritePart(partName, category, partInfo.Width, partInfo.Height);
                    spritePart.SheetID = partInfo.SheetIndex + 1; // 1-based
                    spritePart.SheetX = partInfo.X;
                    spritePart.SheetY = partInfo.Y;
                    spritePart.UpdateInitValues();

                    // Register SpritePart — backup vanilla if overriding
                    if (spriteData.SpriteParts.ContainsKey(partName))
                    {
                        if (!_originalParts.ContainsKey(partName))
                            _originalParts[partName] = spriteData.SpriteParts[partName];
                        spriteData.SpriteParts[partName] = spritePart;
                    }
                    else
                    {
                        spriteData.SpriteParts.Add(partName, spritePart);
                    }
                    _registeredPartNames.Add(partName);

                    // Build nine-patch parameters
                    // Real: SpriteNinePatchParameters is a struct
                    // Use SpriteNinePatchParameters.Empty for non-nine-region sprites
                    SpriteNinePatchParameters ninePatch;
                    if (partInfo.IsNineRegion)
                    {
                        ninePatch = new SpriteNinePatchParameters(
                            partInfo.NineLeft, partInfo.NineRight,
                            partInfo.NineTop, partInfo.NineBottom);
                    }
                    else
                    {
                        ninePatch = SpriteNinePatchParameters.Empty;
                    }

                    // Create the Sprite object
                    // Real signature: SpriteGeneric(string name, SpritePart spritePart, in SpriteNinePatchParameters)
                    // There is NO SpriteNineRegion class — SpriteGeneric handles both via ninePatch params
                    var sprite = new SpriteGeneric(partInfo.SpriteName, spritePart, in ninePatch);

                    // Register sprite — backup vanilla if overriding
                    if (spriteData.Sprites.TryGetValue(partInfo.SpriteName, out var existing))
                    {
                        if (!_originalSprites.ContainsKey(partInfo.SpriteName))
                            _originalSprites[partInfo.SpriteName] = existing;
                        spriteData.Sprites[partInfo.SpriteName] = sprite;
                    }
                    else
                    {
                        spriteData.Sprites.Add(partInfo.SpriteName, sprite);
                    }
                    _registeredSpriteNames.Add(partInfo.SpriteName);
                }

                // Load the category (initializes UV coordinates from sheet sizes)
                try
                {
                    category.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
                }
                catch (Exception ex)
                {
                    // Category.Load may not be needed when textures are set directly
                    // since we already called UpdateInitValues on each SpritePart
                    Debug.Print($"[ThemeSwitcher] SpriteRegistrar: Category.Load info: {ex.Message}");
                }

                Debug.Print($"[ThemeSwitcher] SpriteRegistrar: Registered {parts.Count} sprites " +
                    $"({_originalSprites.Count} vanilla overrides)");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] SpriteRegistrar: RegisterDirect error: {ex}");
                // Roll back partial registration to prevent orphaned entries in SpriteData
                try { UnregisterDirect(); }
                catch (Exception cleanupEx)
                {
                    Debug.Print($"[ThemeSwitcher] SpriteRegistrar: Cleanup after error also failed: {cleanupEx.Message}");
                }
            }
        }

        private void UnregisterDirect()
        {
            var spriteData = UIResourceManager.SpriteData;
            if (spriteData == null) return;

            try
            {
                // Restore vanilla sprites
                foreach (var kvp in _originalSprites)
                {
                    if (spriteData.Sprites.ContainsKey(kvp.Key))
                        spriteData.Sprites[kvp.Key] = kvp.Value;
                }

                // Remove theme-only sprites (those that had no vanilla original)
                foreach (var name in _registeredSpriteNames)
                {
                    if (!_originalSprites.ContainsKey(name))
                        spriteData.Sprites.Remove(name);
                }

                // Remove sprite parts
                foreach (var name in _registeredPartNames)
                {
                    if (_originalParts.TryGetValue(name, out var originalPart))
                        spriteData.SpriteParts[name] = originalPart;
                    else
                        spriteData.SpriteParts.Remove(name);
                }

                // Remove category
                if (_registeredCategoryName != null)
                {
                    if (spriteData.SpriteCategories.TryGetValue(_registeredCategoryName, out var cat))
                    {
                        try { cat.Unload(); } catch { }
                    }
                    spriteData.SpriteCategories.Remove(_registeredCategoryName);
                }

                Debug.Print($"[ThemeSwitcher] SpriteRegistrar: Unregistered, " +
                    $"restored {_originalSprites.Count} vanilla sprites");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] SpriteRegistrar: UnregisterDirect error: {ex}");
            }
            finally
            {
                _originalSprites.Clear();
                _originalParts.Clear();
                _registeredSpriteNames.Clear();
                _registeredPartNames.Clear();
                _registeredCategoryName = null;
            }
        }

        #endregion

        #region Harmony Fallback

        private void SetupHarmonyFallback()
        {
            try
            {
                _harmonyOverrides = new Dictionary<string, Sprite>();
                _useHarmonyFallback = true;
                Debug.Print("[ThemeSwitcher] SpriteRegistrar: Harmony fallback will intercept sprite lookups");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] SpriteRegistrar: Harmony fallback setup error: {ex}");
            }
        }

        private void RegisterHarmony(string themeId, List<Texture> textures,
            List<SpritePartInfo> parts)
        {
            if (_harmonyOverrides == null) return;

            // Build sprites in memory (won't be in native SpriteData, but returned via patch)
            var categoryName = $"ui_theme_{themeId.ToLowerInvariant()}";
            var category = new SpriteCategory(categoryName, textures.Count, false);
            for (int i = 0; i < textures.Count; i++)
                category.SpriteSheets.Add(new TaleWorlds.TwoDimension.Texture(new EngineTexture(textures[i])));

            foreach (var partInfo in parts)
            {
                var partName = $"{themeId}\\{partInfo.SpriteName}";
                var spritePart = new SpritePart(partName, category, partInfo.Width, partInfo.Height);
                spritePart.SheetID = partInfo.SheetIndex + 1;
                spritePart.SheetX = partInfo.X;
                spritePart.SheetY = partInfo.Y;
                spritePart.UpdateInitValues();

                SpriteNinePatchParameters ninePatch = partInfo.IsNineRegion
                    ? new SpriteNinePatchParameters(partInfo.NineLeft, partInfo.NineRight,
                        partInfo.NineTop, partInfo.NineBottom)
                    : SpriteNinePatchParameters.Empty;

                var sprite = new SpriteGeneric(partInfo.SpriteName, spritePart, in ninePatch);
                _harmonyOverrides[partInfo.SpriteName] = sprite;
            }

            Debug.Print($"[ThemeSwitcher] SpriteRegistrar: Harmony fallback — {parts.Count} sprite overrides ready");
        }

        private void UnregisterHarmony()
        {
            _harmonyOverrides?.Clear();
            Debug.Print("[ThemeSwitcher] SpriteRegistrar: Harmony fallback — cleared overrides");
        }

        /// <summary>
        /// Static method for Harmony patches to check for sprite overrides.
        /// Called from within patched sprite lookup methods.
        /// </summary>
        public static bool TryGetOverride(string spriteName, out Sprite sprite)
        {
            if (_harmonyOverrides != null && _harmonyOverrides.TryGetValue(spriteName, out sprite))
                return true;
            sprite = null;
            return false;
        }

        #endregion
    }
}
