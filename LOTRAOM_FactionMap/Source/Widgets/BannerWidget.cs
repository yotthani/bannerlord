extern alias SNV;
using System;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

using EngineTexture = TaleWorlds.Engine.Texture;
using TwoDimTexture = TaleWorlds.TwoDimension.Texture;

namespace LOTRAOM.FactionMap.Widgets
{
    /// <summary>
    /// Widget that displays a banner/flag at a specific position on the map.
    /// The banner is placed at a normalized (0-1) position relative to the parent (MapContainerWidget).
    /// It fades in/out smoothly when the target position changes.
    ///
    /// The anchor point is the bottom-center of the banner image (the "pole" sticks into the ground).
    ///
    /// GOLDEN RULES:
    ///   - Never touch the Brush
    ///   - Never call base.OnHoverBegin()
    ///   - Let base class handle OnRender (don't override) — set Sprite = _loadedSprite
    /// </summary>
    public class BannerWidget : ImageWidget
    {
        // Target position (normalized 0-1, set by VM)
        private float _targetX = -1f;
        private float _targetY = -1f;

        // Current displayed position (for smooth movement)
        private float _currentX = -1f;
        private float _currentY = -1f;

        // Visibility animation
        private float _alpha;            // 0 = invisible, 1 = fully visible
        private bool _wantVisible;       // true when we have a valid target
        private const float FadeSpeed = 5f;  // fade in/out speed (per second)

        // Banner size in pixels
        private float _bannerWidth = 48f;
        private float _bannerHeight = 64f;

        // Faction color tint
        private Color _factionColor = new Color(1f, 1f, 1f, 1f);
        private string _bannerColorHexStr = "#FFFFFFFF";

        // Texture loading
        private bool _textureLoaded;
        private bool _updateLogged;
        private bool _loadFailed;
        private Sprite _loadedSprite;
        private string _bannerImage = "banner_flag";

        // Stamp-down animation: banner starts above and slams down into position
        private float _stampProgress; // 0 = high above, 1 = landed
        private const float StampSpeed = 6f; // speed of slam-down

        // Side-based glow
        private string _bannerSide = "neutral";
        private Color _glowColor = new Color(1f, 1f, 1f, 0f); // transparent = no glow

        // Display scale factor (< 1.0 makes the banner smaller on screen)
        private const float DisplayScale = 0.76f;

        public BannerWidget(UIContext context) : base(context)
        {
            OverrideDefaultStateSwitchingEnabled = true;
            // Base class renders Sprite automatically via OnRender
        }

        #region Properties

        /// <summary>
        /// Normalized X position (0-1) on the map where the banner pole touches the ground.
        /// Set to negative to hide the banner.
        /// </summary>
        [Editor(false)]
        public float BannerPosX
        {
            get => _targetX;
            set
            {
                if (Math.Abs(_targetX - value) > 0.0001f)
                {
                    float oldX = _targetX;
                    _targetX = value;
                    _wantVisible = _targetX >= 0f && _targetY >= 0f;

                    if (_wantVisible)
                    {
                        // Snap position immediately and start stamp-down animation
                        _currentX = _targetX;
                        _stampProgress = 0f; // reset: will slam down from above
                    }
                    OnPropertyChanged(value, nameof(BannerPosX));
                }
            }
        }

        /// <summary>
        /// Normalized Y position (0-1) on the map where the banner pole touches the ground.
        /// Set to negative to hide the banner.
        /// </summary>
        [Editor(false)]
        public float BannerPosY
        {
            get => _targetY;
            set
            {
                if (Math.Abs(_targetY - value) > 0.0001f)
                {
                    float oldY = _targetY;
                    _targetY = value;
                    _wantVisible = _targetX >= 0f && _targetY >= 0f;

                    if (_wantVisible)
                    {
                        // Snap position immediately
                        _currentY = _targetY;
                        _stampProgress = 0f;
                    }
                    OnPropertyChanged(value, nameof(BannerPosY));
                }
            }
        }

        /// <summary>
        /// Faction color hex string (#RRGGBBAA) to tint the banner glow.
        /// Parsed to Color internally.
        /// </summary>
        [Editor(false)]
        public string BannerColorHex
        {
            get => _bannerColorHexStr;
            set
            {
                if (_bannerColorHexStr != value)
                {
                    _bannerColorHexStr = value;
                    _factionColor = ParseHexColor(value);
                    OnPropertyChanged(value, nameof(BannerColorHex));
                }
            }
        }

        /// <summary>
        /// Name of the banner image file (without extension) in GUI/SpriteData/FactionMap/.
        /// </summary>
        [Editor(false)]
        public string BannerImage
        {
            get => _bannerImage;
            set
            {
                if (_bannerImage != value)
                {
                    _bannerImage = value;
                    _textureLoaded = false;
                    _loadFailed = false;
                    _loadedSprite = null;
                    OnPropertyChanged(value, nameof(BannerImage));
                }
            }
        }

        /// <summary>
        /// Faction side ("free" / "evil" / "neutral") — determines glow color.
        /// "free" = golden glow, "evil" = red glow, "neutral" = no glow.
        /// </summary>
        [Editor(false)]
        public string BannerSide
        {
            get => _bannerSide;
            set
            {
                if (_bannerSide != value)
                {
                    _bannerSide = value ?? "neutral";
                    _glowColor = _bannerSide switch
                    {
                        "free" => new Color(1.0f, 0.85f, 0.2f, 1f),  // warm gold
                        "evil" => new Color(1.0f, 0.15f, 0.1f, 1f),  // deep red
                        _ => new Color(1f, 1f, 1f, 0f),               // transparent = no glow
                    };
                    OnPropertyChanged(value, nameof(BannerSide));
                }
            }
        }

        #endregion

        #region Update

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);

            // Fade animation
            float targetAlpha = _wantVisible ? 1f : 0f;
            if (Math.Abs(_alpha - targetAlpha) > 0.001f)
            {
                _alpha += Math.Sign(targetAlpha - _alpha) * FadeSpeed * dt;
                _alpha = Math.Max(0f, Math.Min(1f, _alpha));
            }
            else
            {
                _alpha = targetAlpha;
            }

            // Apply alpha to widget opacity so fade actually takes effect
            this.AlphaFactor = _alpha;

            // When fully faded out, reset current position so size collapses to 0
            if (!_wantVisible && _alpha <= 0.001f)
            {
                _currentX = -1f;
                _currentY = -1f;
            }

            // Stamp-down animation: 0 → 1 (banner drops from above into ground)
            if (_wantVisible && _stampProgress < 1f)
            {
                _stampProgress += StampSpeed * dt;
                if (_stampProgress > 1f) _stampProgress = 1f;
            }

            // Snap position immediately (no smooth move between locations)
            if (_wantVisible && _targetX >= 0f && _targetY >= 0f)
            {
                _currentX = _targetX;
                _currentY = _targetY;
            }

            // Position the widget within the parent (MapContainerWidget)
            if (ParentWidget != null && _currentX >= 0f && _currentY >= 0f)
            {
                float parentW = ParentWidget.Size.X;
                float parentH = ParentWidget.Size.Y;
                if (parentW > 0 && parentH > 0)
                {
                    float displayW = _bannerWidth * DisplayScale;
                    float displayH = _bannerHeight * DisplayScale;
                    ScaledSuggestedWidth = displayW;
                    ScaledSuggestedHeight = displayH;

                    // Anchor at bottom-center: the pole tip is at the target position
                    float baseX = _currentX * parentW - displayW * 0.5f;
                    float baseY = _currentY * parentH - displayH;

                    // Stamp-down: starts 150px above target, slams down with easing
                    float stampOffset = (1f - _stampProgress) * -150f;
                    // Ease-out: decelerate as it lands (quadratic)
                    float t = _stampProgress;
                    float eased = t * (2f - t); // ease-out quad
                    stampOffset = (1f - eased) * -150f;

                    ScaledPositionXOffset = baseX;
                    ScaledPositionYOffset = baseY + stampOffset;
                }
            }
            else
            {
                ScaledSuggestedWidth = 0f;
                ScaledSuggestedHeight = 0f;
            }

            // Texture loading
            TryLoadTexture();

            if (_textureLoaded && !_updateLogged)
                _updateLogged = true;
        }

        #endregion

        #region Rendering

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            // Draw glow BEFORE the base sprite so it appears behind the banner
            if (_loadedSprite?.Texture != null && _glowColor.Alpha > 0.01f && _alpha > 0.01f)
            {
                float contextAlpha = _alpha * Context.ContextAlpha;
                const float glowAlpha = 0.55f;
                const float glowExpand = 4f; // pixels of glow expansion on each side

                // Draw multiple offset copies for a soft glow effect
                float[] offsets = { -glowExpand, 0f, glowExpand };
                foreach (float ox in offsets)
                {
                    foreach (float oy in offsets)
                    {
                        if (ox == 0f && oy == 0f)
                            continue; // skip center (that's where the real banner goes)

                        SimpleMaterial glowMat = drawContext.CreateSimpleMaterial();
                        glowMat.OverlayEnabled = false;
                        glowMat.CircularMaskingEnabled = false;
                        glowMat.Texture = _loadedSprite.Texture;
                        glowMat.NinePatchParameters = _loadedSprite.NinePatchParameters;
                        glowMat.Color = _glowColor;
                        glowMat.ColorFactor = 1f; // force glow color
                        glowMat.AlphaFactor = contextAlpha * glowAlpha * (1f / 8f); // divide across 8 copies
                        glowMat.HueFactor = 0f;
                        glowMat.SaturationFactor = 0f;
                        glowMat.ValueFactor = 20f; // slightly brightened

                        Rectangle2D glowRect = AreaRect;
                        glowRect.SetVisualOffset(ox, oy);
                        glowRect.ValidateVisuals();
                        glowRect.CalculateVisualMatrixFrame();
                        drawContext.DrawSprite(_loadedSprite, glowMat, in glowRect, _scaleToUse);
                    }
                }
            }

            // Base ImageWidget.OnRender draws the actual banner sprite on top
            base.OnRender(twoDimensionContext, drawContext);
        }

        #endregion

        #region Texture Loading

        private void TryLoadTexture()
        {
            if (_textureLoaded || _loadFailed || string.IsNullOrEmpty(_bannerImage))
                return;

            try
            {
                string modPath = SubModule.ModulePath;
                if (string.IsNullOrEmpty(modPath))
                {
                    _loadFailed = true;
                    return;
                }

                string texturePath = System.IO.Path.Combine(modPath, "GUI", "SpriteData", "FactionMap");
                string file = System.IO.Path.Combine(texturePath, $"{_bannerImage}.png");

                if (!System.IO.File.Exists(file))
                {
                    _loadFailed = true;
                    SubModule.LogError($"[Banner] File not found: {file}");
                    return;
                }

                string fileName = System.IO.Path.GetFileName(file);
                string folder = System.IO.Path.GetDirectoryName(file);

                EngineTexture engineTex = EngineTexture.LoadTextureFromPath(fileName, folder);
                if (engineTex == null)
                {
                    _loadFailed = true;
                    SubModule.LogError($"[Banner] LoadTextureFromPath returned null");
                    return;
                }

                var twoDimTex = new TwoDimTexture(new TaleWorlds.Engine.GauntletUI.EngineTexture(engineTex));
                _loadedSprite = new RuntimeSprite(twoDimTex, twoDimTex.Width, twoDimTex.Height);
                Sprite = _loadedSprite;  // Required — initializes internal ImageWidget state for rendering
                _textureLoaded = true;

                // Use actual texture dimensions as default banner size
                _bannerWidth = twoDimTex.Width;
                _bannerHeight = twoDimTex.Height;

            }
            catch (Exception ex)
            {
                _loadFailed = true;
                SubModule.LogError($"[Banner] Exception: {ex.Message}");
            }
        }

        #endregion

        #region Hover — disabled for banner

        protected override void OnHoverBegin()
        {
            // Banner is non-interactive — ignore hover
        }

        protected override void OnHoverEnd()
        {
            // Banner is non-interactive — ignore hover
        }

        #endregion

        #region Color Helpers

        /// <summary>
        /// Parse a hex color string (#RRGGBBAA, Bannerlord format) to a Color.
        /// Falls back to white if the string is invalid.
        /// </summary>
        private static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 7)
                return new Color(1f, 1f, 1f, 1f);

            hex = hex.TrimStart('#');
            try
            {
                float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float a = hex.Length >= 8
                    ? int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f
                    : 1f;
                return new Color(r, g, b, a);
            }
            catch
            {
                return new Color(1f, 1f, 1f, 1f);
            }
        }

        #endregion
    }
}
