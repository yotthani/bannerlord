using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Defines an elliptical orbit for rings.
    /// Updated to match the React mock design.
    /// </summary>
    public class RingOrbit
    {
        public string OrbitName { get; }
        public float RadiusX { get; }           // Horizontal radius
        public float RadiusY { get; }           // Vertical radius (flat for table perspective)
        public float CenterOffsetY { get; }     // Vertical offset from center
        public float BaseScale { get; }         // Base ring size multiplier
        public int ZIndexBase { get; }          // Base z-index for front rings
        public float RotationOffset { get; set; }  // Current rotation in radians
        public int RingCount { get; }
        
        public RingOrbit(string name, float radiusX, float radiusY, float centerOffsetY, 
                        float baseScale, int zIndexBase, int ringCount)
        {
            OrbitName = name;
            RadiusX = radiusX;
            RadiusY = radiusY;
            CenterOffsetY = centerOffsetY;
            BaseScale = baseScale;
            ZIndexBase = zIndexBase;
            RingCount = ringCount;
            RotationOffset = 0f;
        }
        
        /// <summary>
        /// Calculate position for a ring at given index on this orbit.
        /// Uses standard ellipse parametric equations.
        /// Angle 270° (or -90°) is top, going clockwise.
        /// 
        /// Reference values for Elven orbit (RadiusX=200, RadiusY=80):
        /// - Top (270°): offset (0, -80)
        /// - Bottom-left (150°): offset (-173, 40)
        /// - Bottom-right (30°): offset (173, 40)
        /// </summary>
        public OrbitalPosition CalculatePosition(int ringIndex, float centerX, float centerY)
        {
            // Distribute rings evenly, starting at top (270° = 3π/2 radians)
            float angleStep = (2f * (float)Math.PI) / RingCount;
            // Start at 270° (top) and go clockwise (decreasing angle)
            float baseAngle = (3f * (float)Math.PI / 2f);  // 270° in radians
            float angle = baseAngle - RotationOffset - (ringIndex * angleStep);
            
            // Normalize angle to [0, 2π]
            while (angle < 0) angle += 2f * (float)Math.PI;
            while (angle >= 2f * (float)Math.PI) angle -= 2f * (float)Math.PI;
            
            // Parametric ellipse equations
            // x = RadiusX * cos(angle), y = RadiusY * sin(angle)
            // Positive Y is DOWN in screen coordinates
            float x = RadiusX * (float)Math.Cos(angle);
            float y = RadiusY * (float)Math.Sin(angle) + CenterOffsetY;
            
            // Depth calculation: at 270° (top), sin = -1 (back); at 90° (bottom), sin = 1 (front)
            float sinAngle = (float)Math.Sin(angle);
            bool isBack = sinAngle < -0.3f;  // Back when in upper portion
            bool isFront = sinAngle > 0.3f;  // Front when in lower portion
            float frontness = (sinAngle + 1f) / 2f;  // 0 (back/top) to 1 (front/bottom)
            
            // Scale: back = 0.55, front = 1.0 (more dramatic difference)
            float scale = 0.55f + (frontness * 0.45f);
            
            // Z-index calculation
            int zIndex;
            if (isBack)
            {
                // Back rings: low z-index (behind One Ring)
                zIndex = 10 + (int)(frontness * 40f);
            }
            else
            {
                // Front rings: high z-index (in front of One Ring)
                zIndex = ZIndexBase + (int)(frontness * 50f);
            }
            
            // Opacity: back = 0.45, front = 1.0 (much more dramatic for visual depth)
            float opacity = 0.45f + (frontness * 0.55f);
            
            return new OrbitalPosition
            {
                X = x,
                Y = y,
                Scale = scale * BaseScale,
                Opacity = opacity,
                ZOrder = zIndex,
                IsInBack = isBack,
                Angle = angle
            };
        }
        
        /// <summary>Get the angle step between rings</summary>
        public float GetAngleStep() => (2f * (float)Math.PI) / RingCount;
        
        /// <summary>
        /// Calculate target angle to bring a specific ring to front (bottom of ellipse).
        /// </summary>
        public float CalculateRotationToFront(int ringIndex)
        {
            float angleStep = (2f * (float)Math.PI) / RingCount;
            // Ring's position relative to base (270° top)
            float ringAngleFromTop = ringIndex * angleStep;
            
            // Target: bring ring to 90° (bottom/front)
            // From 270° going clockwise by ringAngleFromTop, we need rotation to reach 90°
            // 90° is 180° (π radians) clockwise from 270°
            float targetRotation = ((float)Math.PI) - ringAngleFromTop;
            
            // Calculate shortest rotation from current
            float deltaAngle = targetRotation - RotationOffset;
            while (deltaAngle > Math.PI) deltaAngle -= 2f * (float)Math.PI;
            while (deltaAngle < -Math.PI) deltaAngle += 2f * (float)Math.PI;
            
            return RotationOffset + deltaAngle;
        }
        
        /// <summary>
        /// Rotate by one ring position.
        /// </summary>
        public void RotateByOneRing(bool clockwise = true)
        {
            float step = (2f * (float)Math.PI) / RingCount;
            RotationOffset += clockwise ? step : -step;
            NormalizeRotation();
        }
        
        /// <summary>
        /// Set rotation directly (for animated transitions).
        /// </summary>
        public void SetRotation(float radians)
        {
            RotationOffset = radians;
            // Don't normalize during animation to allow smooth rotation past 0/2π
        }
        
        private void NormalizeRotation()
        {
            while (RotationOffset > 2f * (float)Math.PI)
                RotationOffset -= 2f * (float)Math.PI;
            while (RotationOffset < 0)
                RotationOffset += 2f * (float)Math.PI;
        }
    }
    
    /// <summary>
    /// Calculated position data for a ring in orbit.
    /// </summary>
    public struct OrbitalPosition
    {
        public float X;
        public float Y;
        public float Scale;
        public float Opacity;
        public int ZOrder;
        public float Angle;
        public bool IsInBack;
        public float Frontness;
    }
    
    /// <summary>
    /// Extended RingItemVM with orbital position properties.
    /// </summary>
    public class OrbitalRingVM : ViewModel
    {
        private readonly Action<OrbitalRingVM> _onSelect;
        private readonly Action<OrbitalRingVM> _onRightClick;
        
        // Ring identity
        private string _ringId;
        private string _ringName;
        private string _shortName;
        private string _category;  // "Elven", "Dwarven", "Mortal", "One"
        
        // Ring state
        private bool _isOwned;
        private bool _isEquipped;
        private bool _isSelected;
        private string _effectText;
        private string _loreText;
        
        // Orbital position (updated by OrbitManager)
        private float _posX;
        private float _posY;
        private float _scale;
        private float _opacity;
        private int _zOrder;
        private bool _isInBack;
        
        // For rotation targeting
        public int OrbitIndex { get; set; }
        public string OrbitName { get; set; }
        
        // Computed visual properties
        private float _ringSize;
        private float _glowSize;
        private string _ringTint;
        private string _glowColor;
        private string _frameColor;
        
        // Selection glow color (transparent version of category color)
        private string _selectionGlowColor;
        
        // Bobbing animation
        private float _bobOffset = 0f;
        private float _bobPhase = 0f;
        
        // Shadow position (floating shadow below ring)
        private float _shadowMarginTop;
        private float _shadowOpacityValue;
        
        // Shadow position (carved indent below ring - legacy)
        private float _shadowPosY;
        private float _shadowWidth;
        private float _shadowHeight;
        private float _shadowOpacity;
        
        // Sprite name for the ring image
        private string _spriteName;
        
        // Fallback sprite if specific ring sprite not found
        private const string FallbackSprite = "MapBar\\mapbar_center_circle_frame";
        
        public OrbitalRingVM(string id, string name, string shortName, string category,
                            string effectText, string loreText,
                            Action<OrbitalRingVM> onSelect, Action<OrbitalRingVM> onRightClick)
        {
            _ringId = id;
            _ringName = name;
            _shortName = shortName;
            _category = category;
            _effectText = effectText;
            _loreText = loreText;
            _onSelect = onSelect;
            _onRightClick = onRightClick;
            
            // Set sprite name based on ring ID
            _spriteName = GetSpriteNameForRing(id);
            
            // Initialize glow values (subtle warm orange corona for all rings)
            _shadowMarginTop = 18f;
            _shadowOpacityValue = 0.25f;  // Reduced from 0.5f
            _glowScale = 1f;
            _ringAuraOpacity = 0.08f;  // Very subtle at rest
            _ringAuraScale = 1.05f;
            _bobPhase = (float)(new Random(id.GetHashCode()).NextDouble() * Math.PI * 2); // Random starting phase
            
            UpdateCategoryColors();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SPRITE CONFIGURATION
        // Custom sprites enabled - requires compiled sprite sheets in GUI/SpriteParts/
        // ═══════════════════════════════════════════════════════════════════════════
        private const bool USE_CUSTOM_SPRITES = true;
        
        /// <summary>
        /// Maps ring ID to sprite path.
        /// Sprite paths match HeirOfNumenorSpriteData.xml definitions.
        /// </summary>
        private static string GetSpriteNameForRing(string ringId)
        {
            if (!USE_CUSTOM_SPRITES)
            {
                return FallbackSprite;
            }
            
            // Custom sprite mapping - paths from generated HeirOfNumenorSpriteData.xml
            // Handle both "hon_ring_xxx" and "ring_xxx" formats
            return ringId switch
            {
                // The One Ring
                "ring_one" or "hon_ring_one_ring" => "rings\\ring_one",
                
                // Elven Rings (Three)
                "ring_elven_narya" or "hon_ring_narya" => "rings\\ring_elven_narya",
                "ring_elven_nenya" or "hon_ring_nenya" => "rings\\ring_elven_nenya",
                "ring_elven_vilya" or "hon_ring_vilya" => "rings\\ring_elven_vilya",
                
                // Dwarven Rings (Seven)
                "ring_dwarf_1" or "hon_ring_dwarf_1" => "rings\\ring_dwarf_1",
                "ring_dwarf_2" or "hon_ring_dwarf_2" => "rings\\ring_dwarf_2",
                "ring_dwarf_3" or "hon_ring_dwarf_3" => "rings\\ring_dwarf_3",
                "ring_dwarf_4" or "hon_ring_dwarf_4" => "rings\\ring_dwarf_4",
                "ring_dwarf_5" or "hon_ring_dwarf_5" => "rings\\ring_dwarf_5",
                "ring_dwarf_6" or "hon_ring_dwarf_6" => "rings\\ring_dwarf_6",
                "ring_dwarf_7" or "hon_ring_dwarf_7" => "rings\\ring_dwarf_7",
                
                // Mortal Rings (Nine) - Note: game uses "nazgul" IDs internally
                "ring_mortal_1" or "hon_ring_mortal_1" or "hon_ring_nazgul_1" => "rings\\ring_mortal_1",
                "ring_mortal_2" or "hon_ring_mortal_2" or "hon_ring_nazgul_2" => "rings\\ring_mortal_2",
                "ring_mortal_3" or "hon_ring_mortal_3" or "hon_ring_nazgul_3" => "rings\\ring_mortal_3",
                "ring_mortal_4" or "hon_ring_mortal_4" or "hon_ring_nazgul_4" => "rings\\ring_mortal_4",
                "ring_mortal_5" or "hon_ring_mortal_5" or "hon_ring_nazgul_5" => "rings\\ring_mortal_5",
                "ring_mortal_6" or "hon_ring_mortal_6" or "hon_ring_nazgul_6" => "rings\\ring_mortal_6",
                "ring_mortal_7" or "hon_ring_mortal_7" or "hon_ring_nazgul_7" => "rings\\ring_mortal_7",
                "ring_mortal_8" or "hon_ring_mortal_8" or "hon_ring_nazgul_8" => "rings\\ring_mortal_8",
                "ring_mortal_9" or "hon_ring_mortal_9" or "hon_ring_nazgul_9" => "rings\\ring_mortal_9",
                
                _ => FallbackSprite
            };
        }
        
        private void UpdateCategoryColors()
        {
            switch (_category)
            {
                case "Elven":
                    _glowColor = "#90EE9050";  // Light leaf green glow
                    _frameColor = "#90EE90AA";
                    _ringTint = "#98FB98FF";   // Pale green
                    _selectionGlowColor = "#90EE9060";
                    _tableGlowColor = "#FFAA6640";  // Subtle warm orange corona (same for all)
                    break;
                case "Dwarven":
                    _glowColor = "#C0D4E850";  // Mithril silver glow
                    _frameColor = "#C0D4E8AA";
                    _ringTint = "#D0E8FFFF";   // Light mithril blue-silver
                    _selectionGlowColor = "#C0D4E860";
                    _tableGlowColor = "#FFAA6640";  // Subtle warm orange corona (same for all)
                    break;
                case "Mortal":
                    _glowColor = "#FFA50050";  // Light orange glow
                    _frameColor = "#FFA500AA";
                    _ringTint = "#FFB84DFF";   // Light orange
                    _selectionGlowColor = "#FFA50060";
                    _tableGlowColor = "#FFAA6640";  // Subtle warm orange corona (same for all)
                    break;
                case "One":
                    _glowColor = "#FFD70066";  // Gold glow
                    _frameColor = "#FFD700CC";
                    _ringTint = "#FFD700FF";
                    _selectionGlowColor = "#FFD70080";  // Brighter transparent gold glow
                    _tableGlowColor = "#FFAA6650";  // Slightly brighter for One Ring
                    break;
                default:
                    _glowColor = "#FFFFFF33";
                    _frameColor = "#FFFFFFAA";
                    _ringTint = "#FFFFFFFF";
                    _selectionGlowColor = "#FFFFFF60";
                    _tableGlowColor = "#FFAA6640";
                    break;
            }
            
            OnPropertyChanged(nameof(SelectionGlowColor));
            OnPropertyChanged(nameof(TableGlowColor));
        }
        
        /// <summary>
        /// Update position from orbital calculation.
        /// </summary>
        public void UpdateOrbitalPosition(OrbitalPosition pos, float baseRingSize = 46f)
        {
            PosX = pos.X;
            PosY = pos.Y;
            Scale = pos.Scale;
            Opacity = pos.Opacity;
            ZOrder = pos.ZOrder;
            IsInBack = pos.IsInBack;
            
            // Update computed sizes
            RingSize = baseRingSize * pos.Scale;
            GlowSize = RingSize + 18f;
            
            // Shadow (carved position) - below the ring (relative offset)
            ShadowPosY = pos.Y + 4f;  // Slightly below ring position
            ShadowWidth = 28f * pos.Scale;
            ShadowHeight = ShadowWidth * 0.4f;
            ShadowOpacity = pos.IsInBack ? 0.15f : 0.35f;
            
            // Adjust visual properties based on state
            if (!_isOwned)
            {
                _ringTint = "#303030FF";  // Darkened when not owned
                _glowColor = "#00000000";  // No glow
            }
        }
        
        #region Ring Identity Properties
        
        [DataSourceProperty]
        public string RingId 
        { 
            get => _ringId; 
            set { if (_ringId != value) { _ringId = value; OnPropertyChangedWithValue(value, nameof(RingId)); } } 
        }
        
        [DataSourceProperty]
        public string RingName 
        { 
            get => _ringName; 
            set { if (_ringName != value) { _ringName = value; OnPropertyChangedWithValue(value, nameof(RingName)); } } 
        }
        
        [DataSourceProperty]
        public string ShortName 
        { 
            get => _shortName; 
            set { if (_shortName != value) { _shortName = value; OnPropertyChangedWithValue(value, nameof(ShortName)); } } 
        }
        
        [DataSourceProperty]
        public string Category 
        { 
            get => _category; 
            set { if (_category != value) { _category = value; OnPropertyChangedWithValue(value, nameof(Category)); UpdateCategoryColors(); } } 
        }
        
        #endregion
        
        #region Ring State Properties
        
        [DataSourceProperty]
        public bool IsOwned 
        { 
            get => _isOwned; 
            set { if (_isOwned != value) { _isOwned = value; OnPropertyChangedWithValue(value, nameof(IsOwned)); } } 
        }
        
        [DataSourceProperty]
        public bool IsEquipped 
        { 
            get => _isEquipped; 
            set { if (_isEquipped != value) { _isEquipped = value; OnPropertyChangedWithValue(value, nameof(IsEquipped)); } } 
        }
        
        [DataSourceProperty]
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChangedWithValue(value, nameof(IsSelected)); } } 
        }
        
        [DataSourceProperty]
        public string EffectText 
        { 
            get => _effectText; 
            set { if (_effectText != value) { _effectText = value; OnPropertyChangedWithValue(value, nameof(EffectText)); } } 
        }
        
        [DataSourceProperty]
        public string LoreText 
        { 
            get => _loreText; 
            set { if (_loreText != value) { _loreText = value; OnPropertyChangedWithValue(value, nameof(LoreText)); } } 
        }
        
        #endregion
        
        #region Orbital Position Properties
        
        [DataSourceProperty]
        public float PosX 
        { 
            get => _posX; 
            set 
            { 
                if (Math.Abs(_posX - value) > 0.01f) 
                { 
                    _posX = value; 
                    OnPropertyChangedWithValue(value, nameof(PosX));
                    OnPropertyChanged(nameof(ScreenX));
                    OnPropertyChanged(nameof(MarginLeft));
                    OnPropertyChanged(nameof(MarginRight));
                } 
            } 
        }
        
        [DataSourceProperty]
        public float PosY 
        { 
            get => _posY; 
            set 
            { 
                if (Math.Abs(_posY - value) > 0.01f) 
                { 
                    _posY = value; 
                    OnPropertyChangedWithValue(value, nameof(PosY));
                    OnPropertyChanged(nameof(ScreenY));
                    OnPropertyChanged(nameof(MarginTop));
                    OnPropertyChanged(nameof(MarginBottom));
                } 
            } 
        }
        
        [DataSourceProperty]
        public float Scale 
        { 
            get => _scale; 
            set { if (Math.Abs(_scale - value) > 0.01f) { _scale = value; OnPropertyChangedWithValue(value, nameof(Scale)); } } 
        }
        
        [DataSourceProperty]
        public float Opacity 
        { 
            get => _opacity; 
            set { if (Math.Abs(_opacity - value) > 0.01f) { _opacity = value; OnPropertyChangedWithValue(value, nameof(Opacity)); } } 
        }
        
        [DataSourceProperty]
        public int ZOrder 
        { 
            get => _zOrder; 
            set { if (_zOrder != value) { _zOrder = value; OnPropertyChangedWithValue(value, nameof(ZOrder)); } } 
        }
        
        [DataSourceProperty]
        public bool IsInBack 
        { 
            get => _isInBack; 
            set { if (_isInBack != value) { _isInBack = value; OnPropertyChangedWithValue(value, nameof(IsInBack)); } } 
        }
        
        // ═══ MARGIN-BASED POSITIONING FOR CENTER ALIGNMENT ═══
        // These are used when widget has HorizontalAlignment="Center" VerticalAlignment="Center"
        // Margin values are 2x the actual pixel offset from center
        // PosX/PosY represent offset from center in pixels
        
        /// <summary>Margin to push widget RIGHT from center (positive PosX)</summary>
        [DataSourceProperty]
        public float MarginLeft => _posX > 0 ? _posX * 2f : 0f;
        
        /// <summary>Margin to push widget LEFT from center (negative PosX)</summary>
        [DataSourceProperty]
        public float MarginRight => _posX < 0 ? -_posX * 2f : 0f;
        
        /// <summary>Margin to push widget DOWN from center (positive PosY)</summary>
        [DataSourceProperty]
        public float MarginTop => _posY > 0 ? _posY * 2f : 0f;
        
        /// <summary>Margin to push widget UP from center (negative PosY)</summary>
        [DataSourceProperty]
        public float MarginBottom => _posY < 0 ? -_posY * 2f : 0f;
        
        // Screen coordinates for Left/Top alignment (container is 600x400)
        // The visual center of the table surface in the 3D image is higher up than geometric center
        // Table surface center appears around Y=150 in the container (not 200)
        private const float ContainerCenterX = 300f;
        private const float ContainerCenterY = 150f;  // Adjusted up to match table surface
        
        [DataSourceProperty]
        public float ScreenX => ContainerCenterX + _posX - (_ringSize / 2f);
        
        [DataSourceProperty]
        public float ScreenY => ContainerCenterY + _posY - (_ringSize / 2f);
        
        #endregion
        
        #region Visual Properties
        
        [DataSourceProperty]
        public float RingSize 
        { 
            get => _ringSize; 
            set 
            { 
                if (Math.Abs(_ringSize - value) > 0.1f) 
                { 
                    _ringSize = value; 
                    OnPropertyChangedWithValue(value, nameof(RingSize));
                    OnPropertyChanged(nameof(ScreenX));
                    OnPropertyChanged(nameof(ScreenY));
                } 
            } 
        }
        
        [DataSourceProperty]
        public float GlowSize 
        { 
            get => _glowSize; 
            set { if (Math.Abs(_glowSize - value) > 0.1f) { _glowSize = value; OnPropertyChangedWithValue(value, nameof(GlowSize)); } } 
        }
        
        [DataSourceProperty]
        public string RingTint 
        { 
            get => _ringTint; 
            set { if (_ringTint != value) { _ringTint = value; OnPropertyChangedWithValue(value, nameof(RingTint)); } } 
        }
        
        [DataSourceProperty]
        public string GlowColor 
        { 
            get => _glowColor; 
            set { if (_glowColor != value) { _glowColor = value; OnPropertyChangedWithValue(value, nameof(GlowColor)); } } 
        }
        
        [DataSourceProperty]
        public string FrameColor 
        { 
            get => _frameColor; 
            set { if (_frameColor != value) { _frameColor = value; OnPropertyChangedWithValue(value, nameof(FrameColor)); } } 
        }
        
        /// <summary>Selection glow color - transparent version of category color</summary>
        [DataSourceProperty]
        public string SelectionGlowColor 
        { 
            get => _selectionGlowColor ?? "#FFFFFF60"; 
            set { if (_selectionGlowColor != value) { _selectionGlowColor = value; OnPropertyChangedWithValue(value, nameof(SelectionGlowColor)); } } 
        }
        
        /// <summary>Table glow color - brighter version of category color for table reflection</summary>
        [DataSourceProperty]
        public string TableGlowColor 
        { 
            get => _tableGlowColor ?? "#FFFFFF80"; 
        }
        private string _tableGlowColor;
        
        #region Bobbing Animation Properties
        
        /// <summary>Current bob offset from animation</summary>
        [DataSourceProperty]
        public float BobOffset 
        { 
            get => _bobOffset; 
            set 
            { 
                if (Math.Abs(_bobOffset - value) > 0.1f) 
                { 
                    _bobOffset = value; 
                    OnPropertyChangedWithValue(value, nameof(BobOffset));
                    OnPropertyChanged(nameof(BobMarginBottom));
                    OnPropertyChanged(nameof(BobMarginTop));
                    // Update shadow when bob changes
                    UpdateShadowFromBob();
                } 
            } 
        }
        
        /// <summary>Animation phase (for continuous sine wave)</summary>
        public float BobPhase 
        { 
            get => _bobPhase; 
            set => _bobPhase = value; 
        }
        
        /// <summary>Margin bottom for upward bob (positive offset = push up = margin bottom)</summary>
        [DataSourceProperty]
        public float BobMarginBottom => Math.Max(0, _bobOffset * 2f);
        
        /// <summary>Margin top for downward bob (negative offset = push down = margin top)</summary>
        [DataSourceProperty]
        public float BobMarginTop => Math.Max(0, -_bobOffset * 2f);
        
        /// <summary>Glow margin top - glow stays on table surface</summary>
        [DataSourceProperty]
        public float ShadowMarginTop 
        { 
            get => _shadowMarginTop; 
            set { if (Math.Abs(_shadowMarginTop - value) > 0.1f) { _shadowMarginTop = value; OnPropertyChangedWithValue(value, nameof(ShadowMarginTop)); } } 
        }
        
        /// <summary>Glow opacity - brightens as ring gets closer to table</summary>
        [DataSourceProperty]
        public float ShadowOpacityValue 
        { 
            get => _shadowOpacityValue; 
            set { if (Math.Abs(_shadowOpacityValue - value) > 0.01f) { _shadowOpacityValue = value; OnPropertyChangedWithValue(value, nameof(ShadowOpacityValue)); } } 
        }
        
        /// <summary>Glow scale - grows as ring gets closer to table</summary>
        [DataSourceProperty]
        public float GlowScale 
        { 
            get => _glowScale; 
            set { if (Math.Abs(_glowScale - value) > 0.01f) { _glowScale = value; OnPropertyChangedWithValue(value, nameof(GlowScale)); } } 
        }
        private float _glowScale = 1f;
        
        /// <summary>Ring aura opacity - glows around ring when floating up (sucking light from surface)</summary>
        [DataSourceProperty]
        public float RingAuraOpacity 
        { 
            get => _ringAuraOpacity; 
            set { if (Math.Abs(_ringAuraOpacity - value) > 0.01f) { _ringAuraOpacity = value; OnPropertyChangedWithValue(value, nameof(RingAuraOpacity)); } } 
        }
        private float _ringAuraOpacity = 0f;
        
        /// <summary>Ring aura scale - grows when ring is higher</summary>
        [DataSourceProperty]
        public float RingAuraScale 
        { 
            get => _ringAuraScale; 
            set { if (Math.Abs(_ringAuraScale - value) > 0.01f) { _ringAuraScale = value; OnPropertyChangedWithValue(value, nameof(RingAuraScale)); } } 
        }
        private float _ringAuraScale = 1f;
        
        /// <summary>Update glow position, opacity, and scale based on current bob offset</summary>
        /// <remarks>
        /// OLD SHADOW APPROACH (for reference - to restore, change TableGlowColor to black in XML):
        /// - Shadow opacity: higher ring = more faded (0.45 base, ±0.15)
        /// - Shadow margin: moves down when ring goes up
        /// - Color: #000000DD (black)
        /// 
        /// CURRENT GLOW APPROACH:
        /// - TABLE GLOW: brightens when ring is closer to table (corona on surface)
        /// - RING AURA: brightens when ring is higher (sucking light from surface)
        /// - Color: warm golden/orange (#FFAA6650)
        /// </remarks>
        private void UpdateShadowFromBob()
        {
            // _bobOffset: positive = up (away from table), negative = down (closer to table)
            
            // === TABLE GLOW (corona on surface) ===
            // More prominent glow when ring is closer to table
            
            float baseGlowOffset = 20f;
            float heightContribution = _bobOffset * 0.4f;
            ShadowMarginTop = baseGlowOffset + heightContribution;
            
            // Table glow opacity: ring closer (negative bob) = brighter, ring higher = dimmer
            // More visible: At rest: 0.4, At max down: 0.7, At max up: 0.15
            float tableBaseOpacity = 0.4f;
            float tableOpacityVariation = -_bobOffset / 15f;  // Stronger variation
            ShadowOpacityValue = Math.Max(0.1f, Math.Min(0.7f, tableBaseOpacity + tableOpacityVariation));
            
            // Table glow scale: ring closer = larger, ring higher = smaller
            float tableBaseScale = 1.1f;
            float tableScaleVariation = -_bobOffset / 18f;
            GlowScale = Math.Max(0.6f, Math.Min(1.6f, tableBaseScale + tableScaleVariation));
            
            // === RING AURA (mystical glow around ring when floating up) ===
            // More visible when ring is higher - magical energy effect
            
            // Aura opacity: ring higher = brighter glow. At rest: 0.15, At max up: 0.5
            float auraBaseOpacity = 0.15f;
            float auraOpacityVariation = _bobOffset / 16f;  // Stronger variation
            RingAuraOpacity = Math.Max(0f, Math.Min(0.55f, auraBaseOpacity + auraOpacityVariation));
            
            // Aura scale: ring higher = slightly larger aura
            // At rest: 1.05, At max up: 1.25, At max down: 1.0
            float auraBaseScale = 1.05f;
            float auraScaleVariation = _bobOffset / 35f;
            RingAuraScale = Math.Max(1.0f, Math.Min(1.3f, auraBaseScale + auraScaleVariation));
        }
        
        #endregion
        
        [DataSourceProperty]
        public float ShadowPosY
        {
            get => _shadowPosY;
            set { if (Math.Abs(_shadowPosY - value) > 0.1f) { _shadowPosY = value; OnPropertyChangedWithValue(value, nameof(ShadowPosY)); } }
        }
        
        [DataSourceProperty]
        public float ShadowWidth
        {
            get => _shadowWidth;
            set { if (Math.Abs(_shadowWidth - value) > 0.1f) { _shadowWidth = value; OnPropertyChangedWithValue(value, nameof(ShadowWidth)); } }
        }
        
        [DataSourceProperty]
        public float ShadowHeight
        {
            get => _shadowHeight;
            set { if (Math.Abs(_shadowHeight - value) > 0.1f) { _shadowHeight = value; OnPropertyChangedWithValue(value, nameof(ShadowHeight)); } }
        }
        
        [DataSourceProperty]
        public float ShadowOpacity
        {
            get => _shadowOpacity;
            set { if (Math.Abs(_shadowOpacity - value) > 0.01f) { _shadowOpacity = value; OnPropertyChangedWithValue(value, nameof(ShadowOpacity)); } }
        }
        
        [DataSourceProperty]
        public string SpriteName
        {
            get => _spriteName;
            set { if (_spriteName != value) { _spriteName = value; OnPropertyChangedWithValue(value, nameof(SpriteName)); } }
        }
        
        #endregion
        
        #region Commands
        
        public void ExecuteSelect() => _onSelect?.Invoke(this);
        
        public void ExecuteRightClick() => _onRightClick?.Invoke(this);
        
        #endregion
    }
    
    /// <summary>
    /// Manages all three orbits and their rings.
    /// Updated to match React mock design with proper z-ordering.
    /// </summary>
    public class OrbitManager
    {
        // Orbits (flat ellipses for table perspective)
        public RingOrbit ElvenOrbit { get; }
        public RingOrbit DwarvenOrbit { get; }
        public RingOrbit MortalOrbit { get; }
        
        // Center position (not used with margin-based positioning, but kept for compatibility)
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        
        // Ring lists per orbit
        public List<OrbitalRingVM> ElvenRings { get; } = new List<OrbitalRingVM>();
        public List<OrbitalRingVM> DwarvenRings { get; } = new List<OrbitalRingVM>();
        public List<OrbitalRingVM> MortalRings { get; } = new List<OrbitalRingVM>();
        public OrbitalRingVM OneRing { get; set; }
        
        // Ring sizes matching mock
        private readonly Dictionary<string, float> _baseSizes = new Dictionary<string, float>
        {
            { "Elven", 55f },
            { "Dwarven", 45f },
            { "Mortal", 40f },
            { "One", 80f }
        };
        
        // One Ring floating offset
        private const float OneRingFloatOffset = -45f;
        
        public OrbitManager(float centerX = 0, float centerY = 0)
        {
            CenterX = centerX;
            CenterY = centerY;
            
            // ═══ ELLIPSE PARAMETERS MATCHING WORKING XML VALUES ═══
            // Container: 800x500, rings use center alignment with margin positioning
            // Reference positions (margin values / 2 = actual offset):
            //   Top: MarginBottom=160 → Y offset = -80
            //   Bottom-left: MarginRight=346, MarginTop=80 → offset (-173, 40)
            //   Bottom-right: MarginLeft=346, MarginTop=80 → offset (173, 40)
            //
            // These correspond to ellipse with RadiusX=200, RadiusY=80:
            //   270° (top): cos=0, sin=-1 → (0, -80) ✓
            //   150° (bottom-left): cos=-0.866, sin=0.5 → (-173, 40) ✓
            //   30° (bottom-right): cos=0.866, sin=0.5 → (173, 40) ✓
            
            // Inner: Elven (3 rings) - RadiusX=200, RadiusY=80
            ElvenOrbit = new RingOrbit("Elven", 200f, 80f, 0f, 1.0f, 200, 3);
            
            // Middle: Dwarven (7 rings) - larger ellipse
            DwarvenOrbit = new RingOrbit("Dwarven", 300f, 120f, 0f, 0.95f, 300, 7);
            
            // Outer: Mortal (9 rings) - largest ellipse
            MortalOrbit = new RingOrbit("Mortal", 380f, 150f, 0f, 0.9f, 400, 9);
        }
        
        /// <summary>
        /// Update all ring positions based on current orbit rotations.
        /// </summary>
        public void UpdateAllPositions()
        {
            // First pass: calculate base positions for all rings
            var elvenPositions = new List<OrbitalPosition>();
            var dwarvenPositions = new List<OrbitalPosition>();
            var mortalPositions = new List<OrbitalPosition>();
            
            for (int i = 0; i < ElvenRings.Count && i < ElvenOrbit.RingCount; i++)
                elvenPositions.Add(ElvenOrbit.CalculatePosition(i, CenterX, CenterY));
            
            for (int i = 0; i < DwarvenRings.Count && i < DwarvenOrbit.RingCount; i++)
                dwarvenPositions.Add(DwarvenOrbit.CalculatePosition(i, CenterX, CenterY));
            
            for (int i = 0; i < MortalRings.Count && i < MortalOrbit.RingCount; i++)
                mortalPositions.Add(MortalOrbit.CalculatePosition(i, CenterX, CenterY));
            
            // Second pass: apply anti-alignment offsets for FRONT rings only
            // When rings from different orbits align vertically at the front, offset them horizontally
            const float alignmentThreshold = 25f;  // How close X positions trigger offset
            const float offsetAmount = 18f;        // How much to nudge apart
            
            // Check Elven vs Dwarven front rings
            for (int e = 0; e < elvenPositions.Count; e++)
            {
                if (elvenPositions[e].IsInBack) continue;  // Only care about front rings
                
                for (int d = 0; d < dwarvenPositions.Count; d++)
                {
                    if (dwarvenPositions[d].IsInBack) continue;
                    
                    float xDiff = Math.Abs(elvenPositions[e].X - dwarvenPositions[d].X);
                    if (xDiff < alignmentThreshold)
                    {
                        // Nudge apart: Elven goes towards center, Dwarven goes outward
                        var ePos = elvenPositions[e];
                        var dPos = dwarvenPositions[d];
                        
                        float direction = ePos.X >= 0 ? 1f : -1f;
                        ePos.X -= direction * offsetAmount * 0.5f;  // Inner ring towards center
                        dPos.X += direction * offsetAmount * 0.5f;  // Outer ring outward
                        
                        elvenPositions[e] = ePos;
                        dwarvenPositions[d] = dPos;
                    }
                }
            }
            
            // Check Elven vs Mortal front rings
            for (int e = 0; e < elvenPositions.Count; e++)
            {
                if (elvenPositions[e].IsInBack) continue;
                
                for (int m = 0; m < mortalPositions.Count; m++)
                {
                    if (mortalPositions[m].IsInBack) continue;
                    
                    float xDiff = Math.Abs(elvenPositions[e].X - mortalPositions[m].X);
                    if (xDiff < alignmentThreshold)
                    {
                        var ePos = elvenPositions[e];
                        var mPos = mortalPositions[m];
                        
                        float direction = ePos.X >= 0 ? 1f : -1f;
                        ePos.X -= direction * offsetAmount * 0.6f;
                        mPos.X += direction * offsetAmount * 0.6f;
                        
                        elvenPositions[e] = ePos;
                        mortalPositions[m] = mPos;
                    }
                }
            }
            
            // Check Dwarven vs Mortal front rings
            for (int d = 0; d < dwarvenPositions.Count; d++)
            {
                if (dwarvenPositions[d].IsInBack) continue;
                
                for (int m = 0; m < mortalPositions.Count; m++)
                {
                    if (mortalPositions[m].IsInBack) continue;
                    
                    float xDiff = Math.Abs(dwarvenPositions[d].X - mortalPositions[m].X);
                    if (xDiff < alignmentThreshold)
                    {
                        var dPos = dwarvenPositions[d];
                        var mPos = mortalPositions[m];
                        
                        float direction = dPos.X >= 0 ? 1f : -1f;
                        dPos.X -= direction * offsetAmount * 0.4f;
                        mPos.X += direction * offsetAmount * 0.4f;
                        
                        dwarvenPositions[d] = dPos;
                        mortalPositions[m] = mPos;
                    }
                }
            }
            
            // Third pass: apply final positions to ring VMs
            for (int i = 0; i < ElvenRings.Count && i < elvenPositions.Count; i++)
            {
                ElvenRings[i].UpdateOrbitalPosition(elvenPositions[i], _baseSizes["Elven"]);
                ElvenRings[i].OrbitIndex = i;
                ElvenRings[i].OrbitName = "elven";
            }
            
            for (int i = 0; i < DwarvenRings.Count && i < dwarvenPositions.Count; i++)
            {
                DwarvenRings[i].UpdateOrbitalPosition(dwarvenPositions[i], _baseSizes["Dwarven"]);
                DwarvenRings[i].OrbitIndex = i;
                DwarvenRings[i].OrbitName = "dwarven";
            }
            
            for (int i = 0; i < MortalRings.Count && i < mortalPositions.Count; i++)
            {
                MortalRings[i].UpdateOrbitalPosition(mortalPositions[i], _baseSizes["Mortal"]);
                MortalRings[i].OrbitIndex = i;
                MortalRings[i].OrbitName = "mortal";
            }
            
            // The One Ring floats above center - uses relative offsets (0, negative for up)
            if (OneRing != null)
            {
                OneRing.PosX = 0f;  // Center relative
                OneRing.PosY = OneRingFloatOffset;  // Offset up from center
                OneRing.Scale = 1.0f;
                OneRing.Opacity = 1.0f;
                OneRing.ZOrder = 150;  // Between back rings (0-50) and front Elven (200+)
                OneRing.RingSize = _baseSizes["One"];
                OneRing.GlowSize = _baseSizes["One"] + 28f;
                OneRing.IsInBack = false;
                
                // Shadow for floating effect (on the table below)
                OneRing.ShadowPosY = 10f;  // Relative offset below center
                OneRing.ShadowWidth = _baseSizes["One"] * 0.5f;
                OneRing.ShadowHeight = _baseSizes["One"] * 0.5f * 0.15f;
                OneRing.ShadowOpacity = 0.35f;
            }
        }
        
        /// <summary>
        /// Rotate a specific orbit by one ring position.
        /// </summary>
        public void RotateOrbit(string orbitName, bool clockwise = true)
        {
            switch (orbitName.ToLower())
            {
                case "elven":
                    ElvenOrbit.RotateByOneRing(clockwise);
                    break;
                case "dwarven":
                    DwarvenOrbit.RotateByOneRing(clockwise);
                    break;
                case "mortal":
                    MortalOrbit.RotateByOneRing(clockwise);
                    break;
                case "all":
                    ElvenOrbit.RotateByOneRing(clockwise);
                    DwarvenOrbit.RotateByOneRing(clockwise);
                    MortalOrbit.RotateByOneRing(clockwise);
                    break;
            }
            UpdateAllPositions();
        }
        
        /// <summary>
        /// Rotate all orbits together.
        /// </summary>
        public void RotateAll(bool clockwise = true)
        {
            RotateOrbit("all", clockwise);
        }
        
        /// <summary>
        /// Rotate a specific ring to the front of its orbit.
        /// </summary>
        public void RotateRingToFront(OrbitalRingVM ring)
        {
            if (ring == null || string.IsNullOrEmpty(ring.OrbitName)) return;
            
            RingOrbit orbit = ring.OrbitName.ToLower() switch
            {
                "elven" => ElvenOrbit,
                "dwarven" => DwarvenOrbit,
                "mortal" => MortalOrbit,
                _ => null
            };
            
            if (orbit != null)
            {
                float targetRotation = orbit.CalculateRotationToFront(ring.OrbitIndex);
                orbit.SetRotation(targetRotation);
                UpdateAllPositions();
            }
        }
        
        /// <summary>
        /// Get all rings sorted by Z-order for rendering (lower first).
        /// </summary>
        public List<OrbitalRingVM> GetAllRingsSortedByZOrder()
        {
            var all = new List<OrbitalRingVM>();
            all.AddRange(ElvenRings);
            all.AddRange(DwarvenRings);
            all.AddRange(MortalRings);
            if (OneRing != null) all.Add(OneRing);
            
            // Sort by Z-order (lower = behind, render first)
            all.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));
            return all;
        }
        
        /// <summary>
        /// Get orbit info for legend display.
        /// </summary>
        public (string Name, string Color, int Count)[] GetOrbitInfo()
        {
            return new[]
            {
                ("Elven", "#C0C0FF", ElvenOrbit.RingCount),
                ("Dwarven", "#CD7F32", DwarvenOrbit.RingCount),
                ("Mortal", "#708090", MortalOrbit.RingCount)
            };
        }
    }
}
