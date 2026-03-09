using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TaleWorlds.Library;
using Texture = TaleWorlds.Engine.Texture;

namespace BannerlordThemeSwitcher.Sprites
{
    /// <summary>
    /// Result of a sprite loading operation containing textures and sprite metadata.
    /// </summary>
    public class LoadResult
    {
        /// <summary>List of atlas or sheet textures created during loading.</summary>
        public List<Texture> Textures { get; set; } = new List<Texture>();

        /// <summary>Sprite part metadata describing each sprite's position within an atlas.</summary>
        public List<SpritePartInfo> Parts { get; set; } = new List<SpritePartInfo>();

        /// <summary>Whether the loading operation completed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Error message if loading failed, null otherwise.</summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// Metadata for a single sprite packed into an atlas or sprite sheet.
    /// </summary>
    public class SpritePartInfo
    {
        /// <summary>Derived sprite name (relative path without extension).</summary>
        public string SpriteName { get; set; }

        /// <summary>Zero-based index into the textures list identifying which atlas this sprite belongs to.</summary>
        public int SheetIndex { get; set; }

        /// <summary>X position in the atlas (pixels from left).</summary>
        public int X { get; set; }

        /// <summary>Y position in the atlas (pixels from top).</summary>
        public int Y { get; set; }

        /// <summary>Width of the sprite in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Height of the sprite in pixels.</summary>
        public int Height { get; set; }

        /// <summary>True if the sprite name ends with _9, indicating nine-region slicing.</summary>
        public bool IsNineRegion { get; set; }

        /// <summary>Nine-region left border in pixels (default 16).</summary>
        public int NineLeft { get; set; } = SpriteLoader.DefaultNineRegionBorder;

        /// <summary>Nine-region right border in pixels (default 16).</summary>
        public int NineRight { get; set; } = SpriteLoader.DefaultNineRegionBorder;

        /// <summary>Nine-region top border in pixels (default 16).</summary>
        public int NineTop { get; set; } = SpriteLoader.DefaultNineRegionBorder;

        /// <summary>Nine-region bottom border in pixels (default 16).</summary>
        public int NineBottom { get; set; } = SpriteLoader.DefaultNineRegionBorder;
    }

    /// <summary>
    /// Loads sprite images from loose PNGs or pre-built sprite sheets.
    /// Loose PNGs are packed into atlas textures via shelf-first-fit bin-packing.
    /// Pre-built sheets are loaded directly as engine textures.
    /// </summary>
    public static class SpriteLoader
    {
        /// <summary>Maximum atlas texture dimension (width and height).</summary>
        public const int MaxAtlasSize = 4096;

        /// <summary>Default nine-region border size in pixels.</summary>
        public const int DefaultNineRegionBorder = 16;

        /// <summary>Padding between sprites in the atlas (pixels).</summary>
        private const int Padding = 1;

        // =====================================================================
        // Public Methods
        // =====================================================================

        /// <summary>
        /// Loads all loose PNG files from the given directory, packs them into
        /// atlas textures using shelf-first-fit bin-packing, and returns the
        /// resulting textures with sprite metadata.
        /// </summary>
        /// <param name="spritesDir">Directory to recursively scan for .png files.</param>
        /// <returns>A <see cref="LoadResult"/> containing packed atlas textures and sprite info.</returns>
        public static LoadResult LoadLoosePNGs(string spritesDir)
        {
            var result = new LoadResult();

            try
            {
                if (!Directory.Exists(spritesDir))
                {
                    result.Error = $"Sprites directory not found: {spritesDir}";
                    LogMessage(result.Error);
                    return result;
                }

                // Recursively find all .png files
                var pngFiles = Directory.GetFiles(spritesDir, "*.png", SearchOption.AllDirectories);
                if (pngFiles.Length == 0)
                {
                    result.Error = "No PNG files found in sprites directory";
                    LogMessage(result.Error);
                    return result;
                }

                LogMessage($"Found {pngFiles.Length} PNG files in {spritesDir}");

                // Load bitmaps and collect sprite entries
                var entries = new List<SpriteEntry>();
                foreach (var file in pngFiles)
                {
                    Bitmap bmp = null;
                    try
                    {
                        bmp = new Bitmap(file);

                        // Skip oversized PNGs
                        if (bmp.Width > MaxAtlasSize || bmp.Height > MaxAtlasSize)
                        {
                            LogMessage($"WARNING: Skipping oversized PNG ({bmp.Width}x{bmp.Height}): {file}");
                            bmp.Dispose();
                            continue;
                        }

                        var spriteName = GetSpriteNameFromPath(file, spritesDir);
                        entries.Add(new SpriteEntry
                        {
                            SpriteName = spriteName,
                            Bitmap = bmp,
                            Width = bmp.Width,
                            Height = bmp.Height,
                            FilePath = file
                        });
                    }
                    catch (Exception ex)
                    {
                        bmp?.Dispose();
                        LogMessage($"WARNING: Failed to load PNG {file}: {ex.Message}");
                    }
                }

                if (entries.Count == 0)
                {
                    result.Error = "No valid PNG files could be loaded";
                    LogMessage(result.Error);
                    return result;
                }

                // Sort by height descending for shelf-first-fit packing
                entries.Sort((a, b) => b.Height.CompareTo(a.Height));

                // Pack into atlas(es)
                PackIntoAtlases(entries, result);

                // Dispose source bitmaps
                foreach (var entry in entries)
                {
                    entry.Bitmap.Dispose();
                }

                result.Success = true;
                LogMessage($"Packed {entries.Count} sprites into {result.Textures.Count} atlas(es)");
            }
            catch (Exception ex)
            {
                result.Error = $"Error loading loose PNGs: {ex.Message}";
                LogMessage(result.Error);
            }

            return result;
        }

        /// <summary>
        /// Loads a pre-built sprite sheet from disk as an engine texture.
        /// Tries <see cref="Texture.LoadTextureFromPath"/> first, then falls
        /// back to reading raw bytes and <see cref="Texture.CreateFromMemory"/>.
        /// </summary>
        /// <param name="sheetPath">Full path to the sprite sheet image file.</param>
        /// <param name="sheetWidth">Width of the sprite sheet in pixels.</param>
        /// <param name="sheetHeight">Height of the sprite sheet in pixels.</param>
        /// <returns>A <see cref="LoadResult"/> containing the loaded texture.</returns>
        public static LoadResult LoadSpriteSheet(string sheetPath, int sheetWidth, int sheetHeight)
        {
            var result = new LoadResult();

            try
            {
                if (!File.Exists(sheetPath))
                {
                    result.Error = $"Sprite sheet not found: {sheetPath}";
                    LogMessage(result.Error);
                    return result;
                }

                var fileName = Path.GetFileName(sheetPath);
                var dirName = Path.GetDirectoryName(sheetPath);

                // Try engine's built-in loader first
                Texture texture = null;
                try
                {
                    texture = Texture.LoadTextureFromPath(fileName, dirName);
                }
                catch (Exception ex)
                {
                    LogMessage($"LoadTextureFromPath failed for {fileName}: {ex.Message}, trying fallback");
                }

                // Fallback: read raw bytes
                if (texture == null)
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(sheetPath);
                        texture = Texture.CreateFromMemory(bytes);
                    }
                    catch (Exception ex)
                    {
                        result.Error = $"Failed to load sprite sheet: {ex.Message}";
                        LogMessage(result.Error);
                        return result;
                    }
                }

                if (texture == null)
                {
                    result.Error = $"Both loading methods returned null for: {sheetPath}";
                    LogMessage(result.Error);
                    return result;
                }

                result.Textures.Add(texture);
                result.Success = true;
                LogMessage($"Loaded sprite sheet: {fileName} ({sheetWidth}x{sheetHeight})");
            }
            catch (Exception ex)
            {
                result.Error = $"Error loading sprite sheet: {ex.Message}";
                LogMessage(result.Error);
            }

            return result;
        }

        /// <summary>
        /// Derives a sprite name from a file path relative to a base directory.
        /// Removes the file extension and normalizes path separators to backslashes.
        /// </summary>
        /// <param name="filePath">Full path to the sprite file.</param>
        /// <param name="basePath">Base directory path to make the name relative to.</param>
        /// <returns>
        /// The sprite name derived from the relative path.
        /// For example, "Sprites\button_canvas_9.png" relative to "Sprites" yields "button_canvas_9".
        /// "Sprites\icons\star.png" relative to "Sprites" yields "icons\star".
        /// </returns>
        public static string GetSpriteNameFromPath(string filePath, string basePath)
        {
            // Normalize both paths to use the same separator
            var normalizedFile = Path.GetFullPath(filePath);
            var normalizedBase = Path.GetFullPath(basePath);

            // Ensure base path ends with separator
            if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar.ToString()))
                normalizedBase += Path.DirectorySeparatorChar;

            // Get relative path
            string relativePath;
            if (normalizedFile.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = normalizedFile.Substring(normalizedBase.Length);
            }
            else
            {
                // Fallback: just use filename
                relativePath = Path.GetFileName(filePath);
            }

            // Remove extension
            relativePath = Path.ChangeExtension(relativePath, null);

            // Normalize to backslash
            relativePath = relativePath.Replace('/', '\\');

            // Remove trailing dot if ChangeExtension left one
            if (relativePath.EndsWith("."))
                relativePath = relativePath.Substring(0, relativePath.Length - 1);

            return relativePath;
        }

        // =====================================================================
        // Atlas Packing (Shelf-First-Fit)
        // =====================================================================

        /// <summary>
        /// Packs sprite entries into one or more atlas textures using shelf-first-fit.
        /// </summary>
        private static void PackIntoAtlases(List<SpriteEntry> entries, LoadResult result)
        {
            var remaining = new List<SpriteEntry>(entries);

            while (remaining.Count > 0)
            {
                int atlasIndex = result.Textures.Count;
                var placed = new List<PlacedSprite>();

                // Run shelf-first-fit packing
                var notPlaced = ShelfPack(remaining, placed, MaxAtlasSize);

                if (placed.Count == 0)
                {
                    // Nothing could fit — skip remaining
                    LogMessage($"WARNING: {remaining.Count} sprite(s) could not fit in any atlas");
                    break;
                }

                // Determine atlas dimensions (next power-of-2 fitting all placed sprites)
                int maxX = 0;
                int maxY = 0;
                foreach (var p in placed)
                {
                    int right = p.X + p.Entry.Width;
                    int bottom = p.Y + p.Entry.Height;
                    if (right > maxX) maxX = right;
                    if (bottom > maxY) maxY = bottom;
                }

                int atlasWidth = NextPowerOfTwo(maxX);
                int atlasHeight = NextPowerOfTwo(maxY);

                // Clamp to max
                if (atlasWidth > MaxAtlasSize) atlasWidth = MaxAtlasSize;
                if (atlasHeight > MaxAtlasSize) atlasHeight = MaxAtlasSize;

                LogMessage($"Creating atlas #{atlasIndex}: {atlasWidth}x{atlasHeight} with {placed.Count} sprites");

                // Compose the atlas bitmap
                Texture atlasTexture = ComposeAtlas(placed, atlasWidth, atlasHeight);
                if (atlasTexture == null)
                {
                    LogMessage("ERROR: Failed to compose atlas texture");
                    break;
                }

                result.Textures.Add(atlasTexture);

                // Generate SpritePartInfo for each placed sprite
                foreach (var p in placed)
                {
                    bool isNine = p.Entry.SpriteName.EndsWith("_9");
                    var info = new SpritePartInfo
                    {
                        SpriteName = p.Entry.SpriteName,
                        SheetIndex = atlasIndex,
                        X = p.X,
                        Y = p.Y,
                        Width = p.Entry.Width,
                        Height = p.Entry.Height,
                        IsNineRegion = isNine
                    };

                    // Nine-region defaults are already set by the property initializers
                    result.Parts.Add(info);
                }

                remaining = notPlaced;
            }
        }

        /// <summary>
        /// Shelf-first-fit bin-packing algorithm.
        /// Packs sprites into shelves left-to-right with 1px padding.
        /// When the current shelf is full, a new shelf is started below.
        /// </summary>
        /// <returns>List of entries that did not fit in the atlas.</returns>
        private static List<SpriteEntry> ShelfPack(
            List<SpriteEntry> entries,
            List<PlacedSprite> placed,
            int maxSize)
        {
            var notPlaced = new List<SpriteEntry>();

            // Shelf state
            int shelfX = 0;       // Current X position in the current shelf
            int shelfY = 0;       // Y position of the current shelf's top edge
            int shelfHeight = 0;  // Height of the tallest sprite on the current shelf

            foreach (var entry in entries)
            {
                int requiredWidth = entry.Width + Padding;
                int requiredHeight = entry.Height + Padding;

                // Check if the sprite fits on the current shelf
                if (shelfX + requiredWidth <= maxSize)
                {
                    // Check if the sprite fits vertically
                    int newShelfHeight = Math.Max(shelfHeight, requiredHeight);
                    if (shelfY + newShelfHeight <= maxSize)
                    {
                        placed.Add(new PlacedSprite { Entry = entry, X = shelfX, Y = shelfY });
                        shelfX += requiredWidth;
                        shelfHeight = newShelfHeight;
                        continue;
                    }
                }

                // Try starting a new shelf
                int newShelfY = shelfY + shelfHeight;
                if (newShelfY + requiredHeight <= maxSize && requiredWidth <= maxSize)
                {
                    shelfY = newShelfY;
                    shelfX = 0;
                    shelfHeight = requiredHeight;

                    placed.Add(new PlacedSprite { Entry = entry, X = shelfX, Y = shelfY });
                    shelfX += requiredWidth;
                }
                else
                {
                    // Does not fit in this atlas at all
                    notPlaced.Add(entry);
                }
            }

            return notPlaced;
        }

        /// <summary>
        /// Composes placed sprites into a single atlas bitmap, converts to RGBA byte array,
        /// and creates an engine <see cref="Texture"/>.
        /// </summary>
        private static Texture ComposeAtlas(List<PlacedSprite> placed, int atlasWidth, int atlasHeight)
        {
            Bitmap atlasBmp = null;
            Graphics gfx = null;

            try
            {
                atlasBmp = new Bitmap(atlasWidth, atlasHeight, PixelFormat.Format32bppArgb);
                gfx = Graphics.FromImage(atlasBmp);
                gfx.Clear(System.Drawing.Color.Transparent);

                // Draw each sprite into the atlas
                foreach (var p in placed)
                {
                    gfx.DrawImage(p.Entry.Bitmap, p.X, p.Y, p.Entry.Width, p.Entry.Height);
                }

                gfx.Flush();
                gfx.Dispose();
                gfx = null;

                // Convert BGRA bitmap to RGBA byte array
                var rgba = BitmapToRgba(atlasBmp, atlasWidth, atlasHeight);

                // Create engine texture from raw bytes
                var texture = Texture.CreateFromByteArray(rgba, atlasWidth, atlasHeight);
                return texture;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to compose atlas: {ex.Message}");
                return null;
            }
            finally
            {
                gfx?.Dispose();
                atlasBmp?.Dispose();
            }
        }

        /// <summary>
        /// Converts a 32bpp ARGB bitmap to an RGBA byte array suitable for texture creation.
        /// </summary>
        private static byte[] BitmapToRgba(Bitmap bitmap, int width, int height)
        {
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int byteCount = width * height * 4;
                var bgra = new byte[byteCount];
                Marshal.Copy(bmpData.Scan0, bgra, 0, byteCount);

                // Convert BGRA to RGBA by swapping B and R channels
                var rgba = new byte[byteCount];
                for (int i = 0; i < byteCount; i += 4)
                {
                    rgba[i]     = bgra[i + 2]; // R <- B
                    rgba[i + 1] = bgra[i + 1]; // G <- G
                    rgba[i + 2] = bgra[i];     // B <- R
                    rgba[i + 3] = bgra[i + 3]; // A <- A
                }

                return rgba;
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Returns the smallest power of two that is greater than or equal to the given value.
        /// </summary>
        private static int NextPowerOfTwo(int value)
        {
            if (value <= 0) return 1;

            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value++;

            return value;
        }

        /// <summary>
        /// Logs a message through the engine debug system.
        /// </summary>
        private static void LogMessage(string message)
        {
            Debug.Print($"[ThemeSwitcher] SpriteLoader: {message}");
        }

        // =====================================================================
        // Internal Data Structures
        // =====================================================================

        /// <summary>
        /// Intermediate representation of a sprite before atlas packing.
        /// </summary>
        private class SpriteEntry
        {
            public string SpriteName;
            public Bitmap Bitmap;
            public int Width;
            public int Height;
            public string FilePath;
        }

        /// <summary>
        /// A sprite that has been assigned a position in an atlas.
        /// </summary>
        private class PlacedSprite
        {
            public SpriteEntry Entry;
            public int X;
            public int Y;
        }
    }
}
