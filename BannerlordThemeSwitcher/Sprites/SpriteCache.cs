using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using Texture = TaleWorlds.Engine.Texture;

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
            /// <summary>Theme identifier this entry belongs to</summary>
            public string ThemeId;

            /// <summary>GPU textures loaded for this theme (Engine.Texture for memory management)</summary>
            public List<Texture> Textures = new List<Texture>();

            /// <summary>Sprite part metadata for cache reconstruction on re-registration</summary>
            public List<SpritePartInfo> PartInfos = new List<SpritePartInfo>();

            /// <summary>Timestamp of last access for LRU eviction</summary>
            public DateTime LastAccessed;

            /// <summary>Estimated GPU memory usage in bytes</summary>
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
        public long MaxMemoryBytes { get; set; } = 256L * 1024 * 1024;

        private bool _disposed;

        /// <summary>Store a theme's sprite data in the cache</summary>
        public void Store(string themeId, ThemeCacheEntry entry)
        {
            entry.ThemeId = themeId;
            entry.LastAccessed = DateTime.UtcNow;
            _cache[themeId] = entry;

            Debug.Print($"[ThemeSwitcher] SpriteCache: Stored {themeId} " +
                $"({entry.Textures.Count} textures, {entry.PartInfos.Count} parts, " +
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
                    Debug.Print($"[ThemeSwitcher] SpriteCache: Error releasing texture: {ex.Message}");
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

            Debug.Print($"[ThemeSwitcher] SpriteCache: Memory {totalMemory / 1024}KB exceeds " +
                $"limit {MaxMemoryBytes / 1024}KB, evicting...");

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

            Debug.Print("[ThemeSwitcher] SpriteCache: Disposing all cached textures...");

            foreach (var themeId in _cache.Keys.ToList())
                Release(themeId);

            _cache.Clear();
            _disposed = true;

            Debug.Print("[ThemeSwitcher] SpriteCache: Disposed");
        }
    }
}
