using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;

namespace BannerlordThemeSwitcher.Patches
{
    /// <summary>
    /// Harmony patch for BrushRenderer.Render to apply theme colors at render time.
    /// Modifies both BrushLayerState (backgrounds) and BrushState (text) colors.
    /// </summary>
    public static class BrushRendererPatch
    {
        // Theme colors: BrushBaseName -> ThemeColor
        private static Dictionary<string, Color> _themeColors = new Dictionary<string, Color>();
        private static bool _enabled = false;
        
        // Reflection fields for accessing BrushRenderer internals
        private static FieldInfo _currentBrushLayerStateField;
        private static FieldInfo _currentBrushStateField;
        private static FieldInfo _brushField;

        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                // Get reflection fields
                _currentBrushLayerStateField = typeof(BrushRenderer).GetField("_currentBrushLayerState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _currentBrushStateField = typeof(BrushRenderer).GetField("_currentBrushState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _brushField = typeof(BrushRenderer).GetField("_brush",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                    
                if (_currentBrushLayerStateField == null)
                {
                    Debug.Print("[ThemeSwitcher] ERROR: Cannot find _currentBrushLayerState field!");
                    return;
                }
                
                if (_currentBrushStateField == null)
                {
                    Debug.Print("[ThemeSwitcher] ERROR: Cannot find _currentBrushState field!");
                    return;
                }
                
                var renderMethod = typeof(BrushRenderer).GetMethod("Render",
                    BindingFlags.Instance | BindingFlags.Public);
                    
                if (renderMethod == null)
                {
                    Debug.Print("[ThemeSwitcher] ERROR: Cannot find BrushRenderer.Render method!");
                    return;
                }
                
                var prefix = typeof(BrushRendererPatch).GetMethod("Render_Prefix",
                    BindingFlags.Static | BindingFlags.Public);
                    
                harmony.Patch(renderMethod, prefix: new HarmonyMethod(prefix));
                
                // Also patch CreateTextMaterial for text color
                var textMethod = typeof(BrushRenderer).GetMethod("CreateTextMaterial",
                    BindingFlags.Instance | BindingFlags.Public);
                if (textMethod != null)
                {
                    var textPrefix = typeof(BrushRendererPatch).GetMethod("CreateTextMaterial_Prefix",
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(textMethod, prefix: new HarmonyMethod(textPrefix));
                    Debug.Print("[ThemeSwitcher] CreateTextMaterial patch applied!");
                }
                    
                Debug.Print("[ThemeSwitcher] BrushRenderer.Render patch applied!");
                Debug.Print($"[ThemeSwitcher] _currentBrushStateField found: {_currentBrushStateField != null}");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] ERROR patching BrushRenderer: {ex}");
            }
        }

        public static void SetThemeColors(Dictionary<string, Color> colors)
        {
            _themeColors = colors ?? new Dictionary<string, Color>();
            _enabled = _themeColors.Count > 0;
            Debug.Print($"[ThemeSwitcher] BrushRendererPatch: Set {_themeColors.Count} theme colors, enabled={_enabled}");
        }

        public static void ClearThemeColors()
        {
            _themeColors.Clear();
            _enabled = false;
            Debug.Print("[ThemeSwitcher] BrushRendererPatch: Cleared theme colors");
        }

        // Debug counter to avoid spamming logs
        private static int _debugCounter = 0;

        /// <summary>
        /// Get base brush name by stripping (Clone) suffixes
        /// </summary>
        private static string GetBaseName(string brushName)
        {
            while (brushName.EndsWith("(Clone)"))
            {
                brushName = brushName.Substring(0, brushName.Length - 7);
            }
            return brushName;
        }

        /// <summary>
        /// Prefix for CreateTextMaterial - modify text colors
        /// </summary>
        public static void CreateTextMaterial_Prefix(BrushRenderer __instance)
        {
            if (!_enabled)
                return;
                
            try
            {
                var brush = _brushField?.GetValue(__instance) as Brush;
                if (brush == null || string.IsNullOrEmpty(brush.Name))
                    return;
                
                var baseName = GetBaseName(brush.Name);
                
                if (!_themeColors.TryGetValue(baseName, out var themeColor))
                    return;
                
                // Modify the cached BrushState (text colors)
                var brushState = (BrushState)_currentBrushStateField.GetValue(__instance);
                brushState.FontColor = themeColor;
                _currentBrushStateField.SetValue(__instance, brushState);
            }
            catch { }
        }

        /// <summary>
        /// Prefix - modify the cached layer colors before render
        /// </summary>
        public static void Render_Prefix(BrushRenderer __instance)
        {
            if (!_enabled)
                return;
                
            try
            {
                var brush = _brushField?.GetValue(__instance) as Brush;
                if (brush == null || string.IsNullOrEmpty(brush.Name))
                    return;
                
                var baseName = GetBaseName(brush.Name);
                
                if (!_themeColors.TryGetValue(baseName, out var themeColor))
                    return;
                
                // Modify the cached BrushState (text colors)
                var brushState = (BrushState)_currentBrushStateField.GetValue(__instance);
                brushState.FontColor = themeColor;
                _currentBrushStateField.SetValue(__instance, brushState);
                
                // Get the cached layer state dictionary (background colors)
                var layerState = _currentBrushLayerStateField.GetValue(__instance) 
                    as Dictionary<string, BrushLayerState>;
                    
                if (layerState == null || layerState.Count == 0)
                    return;
                
                // Debug output (only first few times per brush)
                if (_debugCounter < 20)
                {
                    Debug.Print($"[ThemeSwitcher] RENDER INTERCEPT: {brush.Name} (base: {baseName}) -> color + text, {layerState.Count} layers");
                    _debugCounter++;
                }
                
                // Modify each layer's color
                var keys = new List<string>(layerState.Keys);
                foreach (var key in keys)
                {
                    var state = layerState[key];
                    state.Color = themeColor;
                    layerState[key] = state;
                }
            }
            catch (Exception ex)
            {
                if (_debugCounter < 10)
                {
                    Debug.Print($"[ThemeSwitcher] Render_Prefix error: {ex.Message}");
                    _debugCounter++;
                }
            }
        }
    }
}
