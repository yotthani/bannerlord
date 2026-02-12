using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace LOTRAOM.FactionMap.Widgets
{
    /// <summary>
    /// Container widget that maintains the map's aspect ratio (6166:4096).
    /// Letterboxes within its parent to preserve correct proportions.
    /// Background color (ocean blue) set via XML Sprite + Color.
    /// </summary>
    public class MapContainerWidget : Widget
    {
        private const float MAP_ASPECT = 6166f / 4096f; // ~1.505 (Keyforce map)

        // Background color handled via XML Sprite="BlankWhiteSquare_9" + Color

        public MapContainerWidget(UIContext context) : base(context)
        {
            // Background color/sprite set via XML attributes (ocean blue)
        }

        /// <summary>
        /// Maintain aspect ratio by adjusting size within parent bounds.
        /// Uses letterboxing: fits the map as large as possible while keeping proportions.
        /// </summary>
        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);

            if (ParentWidget == null) return;

            float parentW = ParentWidget.Size.X;
            float parentH = ParentWidget.Size.Y;

            if (parentW <= 0 || parentH <= 0) return;

            float parentAspect = parentW / parentH;

            float targetW, targetH;
            if (parentAspect > MAP_ASPECT)
            {
                // Parent is wider than map -> fit by height, center horizontally
                targetH = parentH;
                targetW = targetH * MAP_ASPECT;
            }
            else
            {
                // Parent is taller than map -> fit by width, center vertically
                targetW = parentW;
                targetH = targetW / MAP_ASPECT;
            }

            // Center within parent using margins.
            // ParentWidget.Size returns scaled coords (physical pixels).
            // MarginLeft/Right/Top/Bottom are UNSCALED — Gauntlet multiplies them by _scaleToUse.
            // We must divide by _scaleToUse to avoid double-scaling on 4K displays.
            float marginX = (parentW - targetW) / 2f;
            float marginY = (parentH - targetH) / 2f;

            float invScale = _scaleToUse > 0f ? 1f / _scaleToUse : 1f;
            MarginLeft = Math.Max(0, marginX * invScale);
            MarginRight = Math.Max(0, marginX * invScale);
            MarginTop = Math.Max(0, marginY * invScale);
            MarginBottom = Math.Max(0, marginY * invScale);
        }

        // Background rendering handled by base Widget via XML Sprite + Color
    }
}
