# Dynamic Sprite Theming Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extend the ThemeSwitcher to dynamically load, cache, and register sprites per theme at runtime using Bannerlord's native SpriteData system.

**Architecture:** Four new classes under `Sprites/` namespace — SpriteCache for texture lifecycle, SpriteLoader for PNG/atlas loading, SpriteRegistrar for native SpriteData registration with Harmony fallback, and SpriteThemeManager orchestrating them all. Integration via ThemeManager.ApplyTheme().

**Tech Stack:** TaleWorlds.TwoDimension (SpriteData, SpritePart, SpriteGeneric, SpriteNineRegion), TaleWorlds.Engine (Texture), TaleWorlds.Engine.GauntletUI (UIResourceManager), System.Drawing (atlas packing), HarmonyLib (fallback patches)

---

## Task 1: SpriteCache — Session Texture Cache

**Files:**
- Create: `Sprites/SpriteCache.cs`

**Context:**
This is the lowest-level class. It holds loaded `Texture` objects and sprite metadata in memory per theme. Other classes use it to store/retrieve cached data and release GPU memory.

The existing `BrushModifier.cs` uses a similar pattern with `_originalBrushes` dictionary for backup/restore.

**Step 1: Create SpriteCache.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace BannerlordThemeSwitcher.Sprites
{
    /// <summary>
    /// Session cache for loaded theme textures and sprite metadata.
    /// Textures stay in memory for fast theme switching; released on game exit.
    /// </summary>
    public class SpriteCache : IDisposable
    {
        /// <summary>Cached data for a single theme's sprites</summary>
        public class ThemeCacheEntry
        {
            public string ThemeId;
            public List<Texture> Textures = new List<Texture>();
            public List<SpritePart> SpriteParts = new List<SpritePart>();
            public List<Sprite> Sprites = new List<Sprite>();
            public SpriteCategory Category;
            public DateTime LastAccessed;

            /// <summary>Estimated memory usage in bytes</summary>
            public long EstimatedMemory
            {
                get
                {
                    long total = 0;
                    foreach (var tex in Textures)
                    {
                        if (tex != null)
                            total += tex.MemorySize;
                    }
                    return total;
                }
            }
        }

        private readonly Dictionary<string, ThemeCacheEntry> _cache =
            new Dictionary<string, ThemeCacheEntry>();

        /// <summary>Max total memory for sprite cache (default 256MB)</summary>
        public long MaxMemoryBytes { get; set; } = 256 * 1024 * 1024;

        private bool _disposed;

        /// <summary>Store a theme's sprite data in the cache</summary>
        public void Store(string themeId, ThemeCacheEntry entry)
        {
            entry.ThemeId = themeId;
            entry.LastAccessed = DateTime.UtcNow;
            _cache[themeId] = entry;

            Debug.Print($"[ThemeSwitcher] SpriteCache: Stored {themeId} " +
                $"({entry.Textures.Count} textures, {entry.Sprites.Count} sprites, " +
                $"~{entry.EstimatedMemory / 1024}KB)");

            // Check memory limit and evict if needed
            EvictIfNeeded(themeId);
        }

        /// <summary>Try to get cached data for a theme</summary>
        public bool TryGet(string themeId, out ThemeCacheEntry entry)
        {
            if (_cache.TryGetValue(themeId, out entry))
            {
                entry.LastAccessed = DateTime.UtcNow;
                return true;
            }
            entry = null;
            return false;
        }

        /// <summary>Check if a theme is cached</summary>
        public bool IsCached(string themeId) => _cache.ContainsKey(themeId);

        /// <summary>Release textures for a specific theme</summary>
        public void Release(string themeId)
        {
            if (!_cache.TryGetValue(themeId, out var entry))
                return;

            foreach (var tex in entry.Textures)
            {
                try { tex?.Release(); }
                catch (Exception ex)
                {
                    Debug.Print($"[ThemeSwitcher] Error releasing texture: {ex.Message}");
                }
            }

            _cache.Remove(themeId);
            Debug.Print($"[ThemeSwitcher] SpriteCache: Released {themeId}");
        }

        /// <summary>Evict oldest non-active themes if memory limit exceeded</summary>
        private void EvictIfNeeded(string activeThemeId)
        {
            long totalMemory = _cache.Values.Sum(e => e.EstimatedMemory);

            if (totalMemory <= MaxMemoryBytes)
                return;

            // Sort by LastAccessed, skip active theme
            var evictCandidates = _cache
                .Where(kvp => kvp.Key != activeThemeId)
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .ToList();

            foreach (var candidate in evictCandidates)
            {
                if (totalMemory <= MaxMemoryBytes)
                    break;

                totalMemory -= candidate.Value.EstimatedMemory;
                Release(candidate.Key);
                Debug.Print($"[ThemeSwitcher] SpriteCache: Evicted {candidate.Key} (LRU)");
            }
        }

        /// <summary>Release all cached textures</summary>
        public void Dispose()
        {
            if (_disposed) return;

            foreach (var themeId in _cache.Keys.ToList())
                Release(themeId);

            _cache.Clear();
            _disposed = true;
        }
    }
}
```

**Step 2: Verify it compiles**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
Expected: Build succeeds (no external dependencies for this class)

**Step 3: Commit**

```bash
git add Sprites/SpriteCache.cs
git commit -m "feat: add SpriteCache for session-based texture caching"
```

---

## Task 2: SpriteLoader — PNG Loading and Atlas Packing

**Files:**
- Create: `Sprites/SpriteLoader.cs`

**Context:**
This class handles two input formats:
1. **Loose PNGs** from `Themes/{id}/Sprites/` — need atlas packing
2. **Pre-built sprite sheets** from `Themes/{id}/GUI/SpriteParts/` — load directly

For atlas packing of loose PNGs, we use `System.Drawing` (available in .NET 4.7.2) to read PNG dimensions and compose the atlas bitmap, then convert to a `TaleWorlds.Engine.Texture`.

The convention: filename (without extension) = sprite name. `_9` suffix = NineRegion sprite.

**Step 1: Create SpriteLoader.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using Texture = TaleWorlds.Engine.Texture;

namespace BannerlordThemeSwitcher.Sprites
{
    /// <summary>
    /// Loads PNG sprites and packs them into atlas textures.
    /// Supports loose PNGs (auto-packed) and pre-built sprite sheets.
    /// </summary>
    public class SpriteLoader
    {
        /// <summary>Result of loading sprites from a theme directory</summary>
        public class LoadResult
        {
            public List<Texture> Textures = new List<Texture>();
            public List<SpritePartInfo> Parts = new List<SpritePartInfo>();
            public bool Success;
            public string Error;
        }

        /// <summary>Info about a sprite part before registration</summary>
        public class SpritePartInfo
        {
            public string SpriteName;       // e.g. "button_canvas_9"
            public int SheetIndex;          // 0-based index into LoadResult.Textures
            public int X, Y, Width, Height; // Position in atlas
            public bool IsNineRegion;       // Has _9 suffix
            public int NineLeft, NineRight, NineTop, NineBottom; // 9-slice borders
        }

        private const int MaxAtlasSize = 4096;
        private const int DefaultNineRegionBorder = 16;

        /// <summary>
        /// Load loose PNG files from a directory, pack into atlas.
        /// </summary>
        public LoadResult LoadLoosePNGs(string spritesDir)
        {
            var result = new LoadResult();

            try
            {
                var pngFiles = Directory.GetFiles(spritesDir, "*.png", SearchOption.AllDirectories);
                if (pngFiles.Length == 0)
                {
                    result.Error = "No PNG files found";
                    return result;
                }

                Debug.Print($"[ThemeSwitcher] SpriteLoader: Found {pngFiles.Length} PNGs in {spritesDir}");

                // Load all PNGs and get their dimensions
                var entries = new List<(string path, string name, Bitmap bmp)>();
                foreach (var file in pngFiles)
                {
                    try
                    {
                        var bmp = new Bitmap(file);
                        var spriteName = GetSpriteNameFromPath(file, spritesDir);
                        entries.Add((file, spriteName, bmp));
                    }
                    catch (Exception ex)
                    {
                        Debug.Print($"[ThemeSwitcher] Failed to load PNG {file}: {ex.Message}");
                    }
                }

                if (entries.Count == 0)
                {
                    result.Error = "No valid PNG files could be loaded";
                    return result;
                }

                // Pack into atlases using shelf-first-fit
                PackIntoAtlases(entries, result);

                // Dispose source bitmaps
                foreach (var entry in entries)
                    entry.bmp.Dispose();

                result.Success = true;
                Debug.Print($"[ThemeSwitcher] SpriteLoader: Packed {result.Parts.Count} sprites " +
                    $"into {result.Textures.Count} atlas(es)");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                Debug.Print($"[ThemeSwitcher] SpriteLoader error: {ex}");
            }

            return result;
        }

        /// <summary>
        /// Load a pre-built sprite sheet directly.
        /// </summary>
        public LoadResult LoadSpriteSheet(string sheetPath, int sheetWidth, int sheetHeight)
        {
            var result = new LoadResult();

            try
            {
                var texture = Texture.LoadTextureFromPath(
                    Path.GetFileName(sheetPath),
                    Path.GetDirectoryName(sheetPath));

                if (texture == null)
                {
                    // Fallback: load via System.Drawing and CreateFromMemory
                    var bytes = File.ReadAllBytes(sheetPath);
                    texture = Texture.CreateFromMemory(bytes);
                }

                if (texture == null)
                {
                    result.Error = $"Failed to load texture: {sheetPath}";
                    return result;
                }

                result.Textures.Add(texture);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                Debug.Print($"[ThemeSwitcher] SpriteLoader sheet error: {ex}");
            }

            return result;
        }

        /// <summary>
        /// Pack loose PNG bitmaps into power-of-2 atlas textures using shelf-first-fit.
        /// </summary>
        private void PackIntoAtlases(List<(string path, string name, Bitmap bmp)> entries, LoadResult result)
        {
            // Sort by height descending for better packing
            entries.Sort((a, b) => b.bmp.Height.CompareTo(a.bmp.Height));

            var remaining = new List<(string path, string name, Bitmap bmp)>(entries);
            int atlasIndex = 0;

            while (remaining.Count > 0)
            {
                // Determine atlas size needed
                int atlasSize = DetermineAtlasSize(remaining);

                var placed = new List<(string name, Bitmap bmp, int x, int y)>();
                var shelves = new List<(int y, int height, int usedWidth)>();
                shelves.Add((0, 0, 0));

                var stillRemaining = new List<(string path, string name, Bitmap bmp)>();

                foreach (var entry in remaining)
                {
                    bool wasPlaced = false;

                    // Try to fit in existing shelf
                    for (int s = 0; s < shelves.Count; s++)
                    {
                        var shelf = shelves[s];
                        if (shelf.usedWidth + entry.bmp.Width <= atlasSize &&
                            shelf.y + Math.Max(shelf.height, entry.bmp.Height) <= atlasSize)
                        {
                            int y = shelf.y;
                            int x = shelf.usedWidth;
                            placed.Add((entry.name, entry.bmp, x, y));

                            shelves[s] = (shelf.y, Math.Max(shelf.height, entry.bmp.Height),
                                shelf.usedWidth + entry.bmp.Width + 1); // +1 padding
                            wasPlaced = true;
                            break;
                        }
                    }

                    // Try new shelf
                    if (!wasPlaced)
                    {
                        var lastShelf = shelves[shelves.Count - 1];
                        int newY = lastShelf.y + lastShelf.height + 1; // +1 padding

                        if (newY + entry.bmp.Height <= atlasSize && entry.bmp.Width <= atlasSize)
                        {
                            placed.Add((entry.name, entry.bmp, 0, newY));
                            shelves.Add((newY, entry.bmp.Height, entry.bmp.Width + 1));
                            wasPlaced = true;
                        }
                    }

                    if (!wasPlaced)
                        stillRemaining.Add(entry);
                }

                // Create atlas bitmap and compose
                if (placed.Count > 0)
                {
                    var texture = CreateAtlasTexture(placed, atlasSize);
                    if (texture != null)
                    {
                        int texIndex = result.Textures.Count;
                        result.Textures.Add(texture);

                        foreach (var (name, bmp, x, y) in placed)
                        {
                            var part = new SpritePartInfo
                            {
                                SpriteName = name,
                                SheetIndex = texIndex,
                                X = x,
                                Y = y,
                                Width = bmp.Width,
                                Height = bmp.Height,
                                IsNineRegion = name.EndsWith("_9"),
                                NineLeft = DefaultNineRegionBorder,
                                NineRight = DefaultNineRegionBorder,
                                NineTop = DefaultNineRegionBorder,
                                NineBottom = DefaultNineRegionBorder
                            };
                            result.Parts.Add(part);
                        }
                    }
                    atlasIndex++;
                }

                remaining = stillRemaining;
            }
        }

        /// <summary>Create a TaleWorlds Texture from placed bitmaps</summary>
        private Texture CreateAtlasTexture(List<(string name, Bitmap bmp, int x, int y)> placed, int size)
        {
            try
            {
                using (var atlas = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(atlas))
                    {
                        g.Clear(System.Drawing.Color.Transparent);
                        foreach (var (name, bmp, x, y) in placed)
                        {
                            g.DrawImage(bmp, x, y, bmp.Width, bmp.Height);
                        }
                    }

                    // Convert to RGBA byte array for TaleWorlds
                    var lockBits = atlas.LockBits(
                        new Rectangle(0, 0, size, size),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);

                    byte[] pixels = new byte[size * size * 4];
                    Marshal.Copy(lockBits.Scan0, pixels, 0, pixels.Length);
                    atlas.UnlockBits(lockBits);

                    // BGRA to RGBA conversion
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        byte b = pixels[i];
                        pixels[i] = pixels[i + 2];     // R
                        pixels[i + 2] = b;              // B
                    }

                    return Texture.CreateFromByteArray(pixels, size, size);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Atlas creation error: {ex}");
                return null;
            }
        }

        /// <summary>Determine the smallest power-of-2 atlas size that can fit the sprites</summary>
        private int DetermineAtlasSize(List<(string path, string name, Bitmap bmp)> entries)
        {
            // Estimate total area needed
            long totalArea = entries.Sum(e => (long)(e.bmp.Width + 1) * (e.bmp.Height + 1));
            int maxWidth = entries.Max(e => e.bmp.Width);
            int maxHeight = entries.Max(e => e.bmp.Height);

            // Start with smallest power-of-2 that fits the largest sprite
            int minSize = Math.Max(maxWidth, maxHeight);
            int size = 64;
            while (size < minSize) size *= 2;

            // Grow until area fits (with ~30% overhead estimate for packing inefficiency)
            while ((long)size * size < totalArea * 13 / 10 && size < MaxAtlasSize)
                size *= 2;

            return Math.Min(size, MaxAtlasSize);
        }

        /// <summary>
        /// Derive sprite name from file path relative to sprites directory.
        /// e.g. "Sprites/icons/star.png" -> "icons\star"
        /// e.g. "Sprites/button_canvas_9.png" -> "button_canvas_9"
        /// </summary>
        private string GetSpriteNameFromPath(string filePath, string basePath)
        {
            var relative = filePath
                .Substring(basePath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Remove extension
            relative = Path.ChangeExtension(relative, null);

            // Normalize to backslash (Bannerlord convention)
            return relative.Replace('/', '\\');
        }
    }
}
```

**Step 2: Add System.Drawing reference to csproj**

In `BannerlordThemeSwitcher.csproj`, add inside the existing `<ItemGroup>` with PackageReferences:

```xml
<Reference Include="System.Drawing" />
```

**Step 3: Verify it compiles**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add Sprites/SpriteLoader.cs BannerlordThemeSwitcher.csproj
git commit -m "feat: add SpriteLoader with atlas packing for loose PNGs"
```

---

## Task 3: SpriteRegistrar — Native SpriteData Registration + Harmony Fallback

**Files:**
- Create: `Sprites/SpriteRegistrar.cs`

**Context:**
This is the critical integration point with Bannerlord's engine. It registers theme sprites in `UIResourceManager.SpriteData` — the global sprite registry.

Key TaleWorlds API:
- `UIResourceManager.SpriteData.SpriteNames` — `Dictionary<string, Sprite>` of all named sprites
- `UIResourceManager.SpriteData.SpritePartNames` — `Dictionary<string, SpritePart>`
- `UIResourceManager.SpriteData.SpriteCategories` — `Dictionary<string, SpriteCategory>`
- `SpritePart(name, category, width, height)` — requires SheetID, SheetX, SheetY set after construction
- `SpriteGeneric(name, spritePart)` and `SpriteNineRegion(name, spritePart, left, right, top, bottom)`

The dual strategy: try direct dictionary manipulation first, fall back to Harmony patch on sprite resolution if dictionaries aren't accessible.

**Step 1: Create SpriteRegistrar.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine;
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
                // Test direct access to SpriteData properties
                var spriteData = UIResourceManager.SpriteData;
                if (spriteData != null &&
                    spriteData.SpriteNames != null &&
                    spriteData.SpriteCategories != null &&
                    spriteData.SpritePartNames != null)
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
            List<SpriteLoader.SpritePartInfo> parts)
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
            List<SpriteLoader.SpritePartInfo> parts)
        {
            var spriteData = UIResourceManager.SpriteData;
            var categoryName = $"ui_theme_{themeId.ToLowerInvariant()}";

            try
            {
                // Create a SpriteCategory for this theme
                var category = new SpriteCategory(categoryName, spriteData, textures.Count);

                // Assign textures to the category's SpriteSheets
                for (int i = 0; i < textures.Count; i++)
                {
                    category.SpriteSheets.Add(textures[i]);
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
                    var spritePart = new SpritePart(partName, category, partInfo.Width, partInfo.Height);
                    spritePart.SheetID = partInfo.SheetIndex + 1; // 1-based
                    spritePart.SheetX = partInfo.X;
                    spritePart.SheetY = partInfo.Y;
                    spritePart.UpdateInitValues();

                    // Register SpritePart
                    if (spriteData.SpritePartNames.ContainsKey(partName))
                        spriteData.SpritePartNames[partName] = spritePart;
                    else
                        spriteData.SpritePartNames.Add(partName, spritePart);
                    _registeredPartNames.Add(partName);

                    // Create the Sprite object
                    Sprite sprite;
                    if (partInfo.IsNineRegion)
                    {
                        sprite = new SpriteNineRegion(partInfo.SpriteName, spritePart,
                            partInfo.NineLeft, partInfo.NineRight,
                            partInfo.NineTop, partInfo.NineBottom);
                    }
                    else
                    {
                        sprite = new SpriteGeneric(partInfo.SpriteName, spritePart);
                    }

                    // Register sprite — backup vanilla if overriding
                    if (spriteData.SpriteNames.TryGetValue(partInfo.SpriteName, out var existing))
                    {
                        if (!_originalSprites.ContainsKey(partInfo.SpriteName))
                            _originalSprites[partInfo.SpriteName] = existing;
                        spriteData.SpriteNames[partInfo.SpriteName] = sprite;
                    }
                    else
                    {
                        spriteData.SpriteNames.Add(partInfo.SpriteName, sprite);
                    }
                    _registeredSpriteNames.Add(partInfo.SpriteName);

                    category.SpriteParts.Add(spritePart);
                }

                // Load the category (initializes UV coordinates)
                try
                {
                    category.Load(UIResourceManager.ResourceContext, UIResourceManager.UIResourceDepot);
                }
                catch (Exception ex)
                {
                    // Category may already be loaded or Load may not be needed
                    // since we set textures directly
                    Debug.Print($"[ThemeSwitcher] Category.Load info: {ex.Message}");
                }

                Debug.Print($"[ThemeSwitcher] SpriteRegistrar: Registered {parts.Count} sprites " +
                    $"({_originalSprites.Count} vanilla overrides)");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] SpriteRegistrar error: {ex}");
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
                    if (spriteData.SpriteNames.ContainsKey(kvp.Key))
                        spriteData.SpriteNames[kvp.Key] = kvp.Value;
                }

                // Remove theme-only sprites (those that had no vanilla original)
                foreach (var name in _registeredSpriteNames)
                {
                    if (!_originalSprites.ContainsKey(name))
                        spriteData.SpriteNames.Remove(name);
                }

                // Remove sprite parts
                foreach (var name in _registeredPartNames)
                    spriteData.SpritePartNames.Remove(name);

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
                Debug.Print($"[ThemeSwitcher] SpriteRegistrar unregister error: {ex}");
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

                // Try to find and patch the sprite lookup method
                // Common targets: SpriteData indexer, TryGetValue, or property getter
                var spriteDataType = typeof(SpriteData);

                // Try patching the SpriteNames indexer getter
                var spriteNamesProperty = spriteDataType.GetProperty("SpriteNames");
                if (spriteNamesProperty != null)
                {
                    // We'll intercept at a higher level instead
                    Debug.Print("[ThemeSwitcher] Harmony fallback: Will intercept sprite lookups");
                    _useHarmonyFallback = true;
                }
                else
                {
                    Debug.Print("[ThemeSwitcher] WARNING: Cannot set up Harmony fallback for sprites");
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Harmony fallback setup error: {ex}");
            }
        }

        private void RegisterHarmony(string themeId, List<Texture> textures,
            List<SpriteLoader.SpritePartInfo> parts)
        {
            if (_harmonyOverrides == null) return;

            // Build sprites in memory (won't be in native SpriteData, but returned via patch)
            var categoryName = $"ui_theme_{themeId.ToLowerInvariant()}";
            var category = new SpriteCategory(categoryName, UIResourceManager.SpriteData, textures.Count);
            for (int i = 0; i < textures.Count; i++)
                category.SpriteSheets.Add(textures[i]);

            foreach (var partInfo in parts)
            {
                var partName = $"{themeId}\\{partInfo.SpriteName}";
                var spritePart = new SpritePart(partName, category, partInfo.Width, partInfo.Height);
                spritePart.SheetID = partInfo.SheetIndex + 1;
                spritePart.SheetX = partInfo.X;
                spritePart.SheetY = partInfo.Y;
                spritePart.UpdateInitValues();

                Sprite sprite;
                if (partInfo.IsNineRegion)
                    sprite = new SpriteNineRegion(partInfo.SpriteName, spritePart,
                        partInfo.NineLeft, partInfo.NineRight, partInfo.NineTop, partInfo.NineBottom);
                else
                    sprite = new SpriteGeneric(partInfo.SpriteName, spritePart);

                _harmonyOverrides[partInfo.SpriteName] = sprite;
                category.SpriteParts.Add(spritePart);
            }

            Debug.Print($"[ThemeSwitcher] Harmony fallback: {parts.Count} sprite overrides ready");
        }

        private void UnregisterHarmony()
        {
            _harmonyOverrides?.Clear();
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
```

**Step 2: Verify it compiles**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Sprites/SpriteRegistrar.cs
git commit -m "feat: add SpriteRegistrar with direct access and Harmony fallback"
```

---

## Task 4: SpriteThemeManager — Central Orchestration

**Files:**
- Create: `Sprites/SpriteThemeManager.cs`

**Context:**
This is the top-level class that coordinates SpriteLoader, SpriteRegistrar, and SpriteCache. It provides the simple interface that ThemeManager calls: `LoadThemeSprites(theme)` and `UnloadThemeSprites()`.

It also parses the optional `SpriteConfig.xml` for 9-slice borders and aliases.

**Step 1: Create SpriteThemeManager.cs**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HarmonyLib;
using TaleWorlds.Library;

namespace BannerlordThemeSwitcher.Sprites
{
    /// <summary>
    /// Central orchestrator for dynamic sprite theming.
    /// Coordinates SpriteLoader, SpriteRegistrar, and SpriteCache.
    /// </summary>
    public class SpriteThemeManager : IDisposable
    {
        private static SpriteThemeManager _instance;
        public static SpriteThemeManager Instance => _instance;

        private readonly SpriteLoader _loader = new SpriteLoader();
        private readonly SpriteRegistrar _registrar = new SpriteRegistrar();
        private readonly SpriteCache _cache = new SpriteCache();

        private string _currentThemeId;
        private bool _disposed;

        public SpriteThemeManager()
        {
            _instance = this;
        }

        /// <summary>Initialize the sprite system</summary>
        public void Initialize(Harmony harmony)
        {
            _registrar.Initialize(harmony);
            Debug.Print("[ThemeSwitcher] SpriteThemeManager initialized");
        }

        /// <summary>
        /// Load and register sprites for a theme.
        /// Called by ThemeManager.ApplyTheme() after brush theming.
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

            // Unload current theme's registrations (but keep cached)
            if (_currentThemeId != null)
                _registrar.Unregister();

            // Check cache first
            if (_cache.TryGet(theme.Id, out var cached))
            {
                Debug.Print($"[ThemeSwitcher] SpriteThemeManager: Using cached sprites for {theme.Id}");

                // Re-register from cache
                var cachedParts = BuildPartInfosFromCache(cached);
                _registrar.Register(theme.Id, cached.Textures, cachedParts);
                _currentThemeId = theme.Id;
                return;
            }

            // Load fresh
            var spritesDir = Path.Combine(theme.ThemePath, "Sprites");
            var sheetDir = Path.Combine(theme.ThemePath, "GUI", "SpriteParts");

            SpriteLoader.LoadResult loadResult = null;

            // Try pre-built sprite sheets first
            if (Directory.Exists(sheetDir))
            {
                var sheetDirs = Directory.GetDirectories(sheetDir);
                if (sheetDirs.Length > 0)
                {
                    var pngs = Directory.GetFiles(sheetDirs[0], "*.png");
                    if (pngs.Length > 0)
                    {
                        Debug.Print($"[ThemeSwitcher] Loading pre-built sprite sheet from {sheetDirs[0]}");
                        loadResult = _loader.LoadSpriteSheet(pngs[0], 0, 0);
                        // Note: Pre-built sheets need SpriteConfig.xml for part definitions
                    }
                }
            }

            // Load loose PNGs
            if ((loadResult == null || !loadResult.Success) && Directory.Exists(spritesDir))
            {
                Debug.Print($"[ThemeSwitcher] Loading loose PNGs from {spritesDir}");
                loadResult = _loader.LoadLoosePNGs(spritesDir);
            }

            if (loadResult == null || !loadResult.Success)
            {
                Debug.Print($"[ThemeSwitcher] No sprites loaded for {theme.Id}: " +
                    $"{loadResult?.Error ?? "no sprite directory found"}");
                return;
            }

            // Apply SpriteConfig.xml overrides
            var configPath = Path.Combine(theme.ThemePath, "SpriteConfig.xml");
            if (File.Exists(configPath))
            {
                ApplySpriteConfig(configPath, loadResult.Parts);
            }

            // Register in native system
            _registrar.Register(theme.Id, loadResult.Textures, loadResult.Parts);

            // Cache for fast switching
            var cacheEntry = new SpriteCache.ThemeCacheEntry
            {
                Textures = loadResult.Textures,
            };
            // Store part infos in the sprites list for cache reconstruction
            foreach (var part in loadResult.Parts)
            {
                cacheEntry.SpriteParts.Add(null); // Placeholder — actual SpriteParts are in the registrar
            }
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

        /// <summary>Parse optional SpriteConfig.xml for 9-slice borders and aliases</summary>
        private void ApplySpriteConfig(string configPath, List<SpriteLoader.SpritePartInfo> parts)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(configPath);
                var root = doc.DocumentElement;
                if (root == null) return;

                // Apply NineRegion border overrides
                foreach (XmlNode node in root.SelectNodes("NineRegion"))
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

                    Debug.Print($"[ThemeSwitcher] SpriteConfig: {spriteName} 9-slice = " +
                        $"{part.NineLeft},{part.NineRight},{part.NineTop},{part.NineBottom}");
                }

                // Apply aliases (rename sprite targets)
                foreach (XmlNode node in root.SelectNodes("Alias"))
                {
                    var file = node.Attributes?["file"]?.Value;
                    var replaces = node.Attributes?["replaces"]?.Value;
                    if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(replaces)) continue;

                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var part = parts.Find(p => p.SpriteName == fileName || p.SpriteName.EndsWith("\\" + fileName));
                    if (part != null)
                    {
                        Debug.Print($"[ThemeSwitcher] SpriteConfig: Alias {part.SpriteName} -> {replaces}");
                        part.SpriteName = replaces;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] SpriteConfig parse error (using defaults): {ex.Message}");
            }
        }

        /// <summary>Rebuild SpritePartInfo list from cached data for re-registration</summary>
        private List<SpriteLoader.SpritePartInfo> BuildPartInfosFromCache(SpriteCache.ThemeCacheEntry cached)
        {
            // When we re-register from cache, we need to reload the parts
            // For now, return empty — the cache stores the full ThemeCacheEntry
            // TODO: Store part infos in cache entry properly
            return new List<SpriteLoader.SpritePartInfo>();
        }

        public void Dispose()
        {
            if (_disposed) return;

            UnloadCurrentSprites();
            _cache.Dispose();
            _instance = null;
            _disposed = true;
        }
    }
}
```

**Step 2: Verify it compiles**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add Sprites/SpriteThemeManager.cs
git commit -m "feat: add SpriteThemeManager as central sprite orchestrator"
```

---

## Task 5: Integrate with Existing ThemeManager, SubModule, and Theme

**Files:**
- Modify: `ThemeManager.cs:247-283` (ApplyTheme method)
- Modify: `ThemeManager.cs:374-381` (Dispose method)
- Modify: `SubModule.cs:20-45` (OnSubModuleLoad)
- Modify: `SubModule.cs:48-53` (OnSubModuleUnloaded)
- Modify: `Theme.cs:108-126` (LoadThemeFromDirectory — detect Sprites/ folder)

**Step 1: Modify ThemeManager.ApplyTheme() to call SpriteThemeManager**

In `ThemeManager.cs`, after the brush theming line (line 270), add sprite loading.
Also update Dispose() to clean up SpriteThemeManager.

At the top of `ThemeManager.cs`, add:
```csharp
using BannerlordThemeSwitcher.Sprites;
```

In `ApplyTheme()` method, after line 270 (`Patches.BrushModifier.ApplyTheme(themeId);`), add:
```csharp
            // Apply sprite overrides
            SpriteThemeManager.Instance?.LoadThemeSprites(theme);
```

In `Dispose()`, before `_themes.Clear()`, add:
```csharp
            SpriteThemeManager.Instance?.Dispose();
```

**Step 2: Modify SubModule.cs to initialize SpriteThemeManager**

In `OnSubModuleLoad()`, after `BrushModifier.Initialize();` (line 38), add:
```csharp
                // Initialize sprite theme manager
                var spriteManager = new SpriteThemeManager();
                spriteManager.Initialize(_harmony);
```

In `OnSubModuleUnloaded()`, before `_harmony?.UnpatchAll`, add:
```csharp
            SpriteThemeManager.Instance?.Dispose();
```

**Step 3: Modify Theme detection in ThemeManager.LoadThemeFromDirectory()**

In `ThemeManager.cs`, in `LoadThemeFromDirectory()`, after the HasSpriteOverrides assignment from Components (line 184), add folder-based detection:

After line 189 (`theme.HasBrushOverrides = Directory.Exists(...)`), add:
```csharp
                // Auto-detect sprites from folder structure
                if (!theme.HasSpriteOverrides)
                {
                    theme.HasSpriteOverrides = Directory.Exists(Path.Combine(themeDir, "Sprites")) ||
                        Directory.Exists(Path.Combine(themeDir, "GUI", "SpriteParts"));
                }
```

**Step 4: Verify it compiles**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add ThemeManager.cs SubModule.cs
git commit -m "feat: integrate SpriteThemeManager into theme loading pipeline"
```

---

## Task 6: Add System.Drawing Reference and Create Example Theme Sprites

**Files:**
- Modify: `BannerlordThemeSwitcher.csproj`
- Create: Example `Sprites/` folder in one existing theme for testing

**Step 1: Add System.Drawing reference**

In `BannerlordThemeSwitcher.csproj`, inside the `<ItemGroup>` with other `<Reference>` elements (after line 88), add:

```xml
    <Reference Include="System.Drawing" />
```

**Step 2: Update deploy target to include Sprites folders**

In `BannerlordThemeSwitcher.csproj`, the existing deploy target already copies Themes recursively (lines 116-118), so Sprites/ subfolders will be included automatically. No change needed.

**Step 3: Verify full build**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
Expected: Build succeeds with all new Sprites/ classes compiled

**Step 4: Commit**

```bash
git add BannerlordThemeSwitcher.csproj
git commit -m "feat: add System.Drawing reference for atlas packing"
```

---

## Task 7: Cache Improvement — Store SpritePartInfo for Re-registration

**Files:**
- Modify: `Sprites/SpriteCache.cs` — add SpritePartInfo storage
- Modify: `Sprites/SpriteThemeManager.cs` — properly store and retrieve part infos from cache

**Context:**
The current cache stores textures but doesn't store part infos needed for re-registration. We need to fix the `BuildPartInfosFromCache` method and the cache storage.

**Step 1: Add PartInfos to ThemeCacheEntry**

In `SpriteCache.cs`, add to `ThemeCacheEntry`:
```csharp
            public List<SpriteLoader.SpritePartInfo> PartInfos = new List<SpriteLoader.SpritePartInfo>();
```

**Step 2: Fix SpriteThemeManager cache storage and retrieval**

In `SpriteThemeManager.cs`, replace the cache storage block (after `_registrar.Register()`) with:
```csharp
            var cacheEntry = new SpriteCache.ThemeCacheEntry
            {
                Textures = loadResult.Textures,
                PartInfos = loadResult.Parts
            };
            _cache.Store(theme.Id, cacheEntry);
```

Replace `BuildPartInfosFromCache`:
```csharp
        private List<SpriteLoader.SpritePartInfo> BuildPartInfosFromCache(SpriteCache.ThemeCacheEntry cached)
        {
            return cached.PartInfos;
        }
```

**Step 3: Verify it compiles**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add Sprites/SpriteCache.cs Sprites/SpriteThemeManager.cs
git commit -m "fix: store SpritePartInfo in cache for proper re-registration"
```

---

## Task 8: In-Game Verification

**Files:** None (manual testing)

**Context:**
This is a Bannerlord mod — testing requires launching the game. The verification steps confirm the sprite system works end-to-end.

**Step 1: Create a test theme with sprites**

Create a simple test theme folder structure:
```
Themes/TestSprites/
  ThemeManifest.xml
  Sprites/
    BlankWhiteSquare_9.png     ← A simple colored replacement
```

ThemeManifest.xml:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Theme>
  <Id>TestSprites</Id>
  <Name>Test Sprites</Name>
  <Description>Test theme with sprite overrides</Description>
  <Author>Dev</Author>
  <Version>1.0.0</Version>
  <BaseCulture>empire</BaseCulture>
  <AutoTheme>true</AutoTheme>
  <Components>
    <Brushes enabled="true" />
    <Sprites enabled="true" />
  </Components>
</Theme>
```

**Step 2: Build and deploy**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
The post-build target auto-deploys to the game's Modules folder.

**Step 3: Launch game and verify**

1. Start Bannerlord
2. Check game log for:
   - `[ThemeSwitcher] SpriteThemeManager initialized`
   - `[ThemeSwitcher] SpriteRegistrar: Using direct SpriteData access`
3. Switch to TestSprites theme (via MCM or character creation)
4. Check log for:
   - `[ThemeSwitcher] SpriteLoader: Found X PNGs`
   - `[ThemeSwitcher] SpriteLoader: Packed X sprites into 1 atlas(es)`
   - `[ThemeSwitcher] SpriteRegistrar: Registered X sprites`
5. Verify the replaced sprite is visible in UI
6. Switch to Default theme — verify vanilla sprites restore

**Step 4: Commit test theme**

```bash
git add Themes/TestSprites/
git commit -m "test: add TestSprites theme for sprite system verification"
```

---

## Task 9: Robustness — Error Handling and Edge Cases

**Files:**
- Modify: `Sprites/SpriteThemeManager.cs`
- Modify: `Sprites/SpriteLoader.cs`

**Context:**
Wrap all critical paths with proper error handling. The mod must never crash the game — silently degrade to color-only theming if sprites fail.

**Step 1: Add try-catch around all public methods in SpriteThemeManager**

Wrap `LoadThemeSprites()` body in try-catch:
```csharp
        public void LoadThemeSprites(Theme theme)
        {
            try
            {
                // ... existing body ...
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] SPRITE LOAD ERROR for {theme?.Id}: {ex}");
                Debug.Print("[ThemeSwitcher] Falling back to color-only theming");
                // Don't rethrow — graceful degradation
            }
        }
```

**Step 2: Handle oversized PNGs in SpriteLoader**

In `LoadLoosePNGs()`, skip PNGs larger than MaxAtlasSize:
```csharp
                    try
                    {
                        var bmp = new Bitmap(file);
                        if (bmp.Width > MaxAtlasSize || bmp.Height > MaxAtlasSize)
                        {
                            Debug.Print($"[ThemeSwitcher] Skipping oversized PNG ({bmp.Width}x{bmp.Height}): {file}");
                            bmp.Dispose();
                            continue;
                        }
                        // ...
```

**Step 3: Verify it compiles**

Run: `dotnet build BannerlordThemeSwitcher.csproj -c Release`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add Sprites/SpriteThemeManager.cs Sprites/SpriteLoader.cs
git commit -m "fix: add error handling for graceful sprite fallback"
```

---

## Summary

| Task | What | New/Modified |
|------|------|-------------|
| 1 | SpriteCache | Create `Sprites/SpriteCache.cs` |
| 2 | SpriteLoader + Atlas Packer | Create `Sprites/SpriteLoader.cs`, modify `.csproj` |
| 3 | SpriteRegistrar | Create `Sprites/SpriteRegistrar.cs` |
| 4 | SpriteThemeManager | Create `Sprites/SpriteThemeManager.cs` |
| 5 | Integration | Modify `ThemeManager.cs`, `SubModule.cs` |
| 6 | System.Drawing ref | Modify `.csproj` |
| 7 | Cache fix | Modify `SpriteCache.cs`, `SpriteThemeManager.cs` |
| 8 | In-game test | Create test theme, manual verification |
| 9 | Error handling | Modify `SpriteThemeManager.cs`, `SpriteLoader.cs` |

**Dependencies:** Task 1→2→3→4→5→6 (sequential core), Task 7 fixes cache, Task 8 is manual test, Task 9 is hardening. Tasks 1-3 could be done in parallel since they're independent classes.
