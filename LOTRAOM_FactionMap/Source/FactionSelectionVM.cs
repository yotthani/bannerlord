using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using LOTRAOM.FactionMap.Widgets;

namespace LOTRAOM.FactionMap
{
    /// <summary>
    /// Faction definition loaded from factions.json.
    /// Keyed by stable snake_case ID (e.g. "kingdom_of_rohan").
    /// </summary>
    public class FactionData
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public bool Playable { get; set; }
        public string GameFaction { get; set; } = "";
        public string Description { get; set; } = "";
        public string Image { get; set; } = "";
        public string[] Traits { get; set; } = Array.Empty<string>();
        public FactionBonus[] Bonuses { get; set; } = Array.Empty<FactionBonus>();
        public FactionSpecialUnit SpecialUnit { get; set; }
        public FactionPerk[] Perks { get; set; } = Array.Empty<FactionPerk>();
        public string Side { get; set; } = "neutral";  // "free" | "evil" | "neutral"
        public string[] Strengths { get; set; } = Array.Empty<string>();
        public string[] Weaknesses { get; set; } = Array.Empty<string>();
        public int Difficulty { get; set; } = 0;  // 0=not set, 1-5
    }

    public class FactionBonus
    {
        public string Text { get; set; } = "";
        public bool Positive { get; set; } = true;
    }

    public class FactionSpecialUnit
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class FactionPerk
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// ViewModel for the faction map overlay that sits on top of
    /// the character creation culture stage.
    ///
    /// Bound properties drive the Gauntlet XML:
    ///   - Map hover state (which region is highlighted)
    ///   - Selection state (which region is chosen)
    ///   - Info panel content (name, description, traits)
    ///   - Tooltip position and visibility
    ///   - Landmark markers
    /// </summary>
    public class FactionSelectionVM : ViewModel
    {
        private readonly Action<CultureObject> _onCultureSelected;
        private readonly Action _onPreviousStage;

        private string _selectedRegionName = "";
        private string _selectedFactionName = "";
        private string _selectedFactionDesc = "";
        private bool _hasSelection;
        private bool _selectedFactionPlayable;
        private bool _selectedHasCulture;
        private bool _showLandmarks = true;
        private string _title = "Choose your Realm";

        // Banner placement for selected region
        private float _bannerPosX = -1f;
        private float _bannerPosY = -1f;
        private string _bannerColorHex = "#FFFFFFFF";
        private string _bannerImage = "banner_flag";
        private string _bannerSide = "neutral";

        // Detail panel data
        private string _specialUnitName = "";
        private string _specialUnitDesc = "";
        private bool _hasSpecialUnit;
        private string _factionImageId = "";
        private string _selectedFactionSide = "neutral";
        private int _difficulty;
        private string _difficultyText = "";
        private string _factionColorHex = "#1a1a2eFF";  // dark default for panel bg
        private string _factionAccentColorHex = "#835513FF"; // section header border accent

        /// <summary>
        /// Region data loaded from regions.json, keyed by region ID (e.g. "kingdom_of_rohan", "deco_1").
        /// </summary>
        private Dictionary<string, RegionData> _regions = new Dictionary<string, RegionData>();

        /// <summary>
        /// Faction data loaded from factions.json, keyed by faction ID (e.g. "kingdom_of_rohan").
        /// </summary>
        private Dictionary<string, FactionData> _factions = new Dictionary<string, FactionData>();

        public FactionSelectionVM(Action<CultureObject> onCultureSelected, Action onPreviousStage = null)
        {
            _onCultureSelected = onCultureSelected;
            _onPreviousStage = onPreviousStage;
            FactionTraits = new MBBindingList<FactionTraitItemVM>();
            FactionBonuses = new MBBindingList<FactionBonusItemVM>();
            FactionPerks = new MBBindingList<FactionPerkItemVM>();
            FactionStrengths = new MBBindingList<FactionBonusItemVM>();
            FactionWeaknesses = new MBBindingList<FactionBonusItemVM>();
            FactionLandmarks = new MBBindingList<LandmarkItemVM>();
            AllLandmarks = new MBBindingList<LandmarkItemVM>();

            LoadRegionsAndFactions();
            LoadAllLandmarks();
        }

        #region Title and Navigation

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set { if (_title != value) { _title = value; OnPropertyChangedWithValue(value); } }
        }

        #endregion

        #region Selection

        [DataSourceProperty]
        public bool HasSelection
        {
            get => _hasSelection;
            set { if (value != _hasSelection) { _hasSelection = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(HasNoSelection)); OnPropertyChanged(nameof(CanConfirm)); } }
        }

        [DataSourceProperty] public bool HasNoSelection => !_hasSelection;

        [DataSourceProperty]
        public bool SelectedFactionPlayable
        {
            get => _selectedFactionPlayable;
            set
            {
                if (value != _selectedFactionPlayable)
                {
                    _selectedFactionPlayable = value;
                    OnPropertyChangedWithValue(value);
                    OnPropertyChanged(nameof(SelectedFactionNotPlayable));
                    OnPropertyChanged(nameof(CanConfirm));
                }
            }
        }

        [DataSourceProperty] public bool SelectedFactionNotPlayable => !_selectedFactionPlayable;

        [DataSourceProperty] public bool CanConfirm => _hasSelection && _selectedFactionPlayable && _selectedHasCulture;

        [DataSourceProperty]
        public string SelectedFactionName
        {
            get => _selectedFactionName;
            set { if (value != _selectedFactionName) { _selectedFactionName = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public string SelectedFactionDesc
        {
            get => _selectedFactionDesc;
            set { if (value != _selectedFactionDesc) { _selectedFactionDesc = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public MBBindingList<FactionTraitItemVM> FactionTraits { get; }

        [DataSourceProperty]
        public MBBindingList<FactionBonusItemVM> FactionBonuses { get; }

        [DataSourceProperty]
        public MBBindingList<FactionPerkItemVM> FactionPerks { get; }

        [DataSourceProperty]
        public string SpecialUnitName
        {
            get => _specialUnitName;
            set { if (value != _specialUnitName) { _specialUnitName = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public string SpecialUnitDesc
        {
            get => _specialUnitDesc;
            set { if (value != _specialUnitDesc) { _specialUnitDesc = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public bool HasSpecialUnit
        {
            get => _hasSpecialUnit;
            set { if (value != _hasSpecialUnit) { _hasSpecialUnit = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public string FactionImageId
        {
            get => _factionImageId;
            set { if (value != _factionImageId) { _factionImageId = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public MBBindingList<FactionBonusItemVM> FactionStrengths { get; }

        [DataSourceProperty]
        public MBBindingList<FactionBonusItemVM> FactionWeaknesses { get; }

        [DataSourceProperty]
        public bool HasStrengths => FactionStrengths.Count > 0;

        [DataSourceProperty]
        public bool HasWeaknesses => FactionWeaknesses.Count > 0;

        [DataSourceProperty]
        public string SelectedFactionSide
        {
            get => _selectedFactionSide;
            set
            {
                if (value != _selectedFactionSide)
                {
                    _selectedFactionSide = value;
                    OnPropertyChangedWithValue(value);
                    OnPropertyChanged(nameof(IsFreePeoples));
                    OnPropertyChanged(nameof(IsEvil));
                    OnPropertyChanged(nameof(SideDisplayText));
                }
            }
        }

        [DataSourceProperty] public bool IsFreePeoples => _selectedFactionSide == "free";
        [DataSourceProperty] public bool IsEvil => _selectedFactionSide == "evil";

        [DataSourceProperty]
        public string SideDisplayText
        {
            get
            {
                switch (_selectedFactionSide)
                {
                    case "free": return "Free Peoples";
                    case "evil": return "Forces of Evil";
                    default: return "Neutral";
                }
            }
        }

        [DataSourceProperty]
        public int Difficulty
        {
            get => _difficulty;
            set { if (value != _difficulty) { _difficulty = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(HasDifficulty)); } }
        }

        [DataSourceProperty] public bool HasDifficulty => _difficulty > 0;

        [DataSourceProperty]
        public string DifficultyText
        {
            get => _difficultyText;
            set { if (value != _difficultyText) { _difficultyText = value; OnPropertyChangedWithValue(value); } }
        }

        /// <summary>
        /// Hex color for the panel background tint, derived from faction color.
        /// Very dark version: 15% of faction color mixed with near-black base.
        /// Format: #RRGGBBAA (Bannerlord convention).
        /// </summary>
        [DataSourceProperty]
        public string FactionColorHex
        {
            get => _factionColorHex;
            set { if (value != _factionColorHex) { _factionColorHex = value; OnPropertyChangedWithValue(value); } }
        }

        /// <summary>
        /// Accent color for section header borders, derived from faction color.
        /// Slightly darkened/saturated version suitable for UI frame tinting.
        /// Format: #RRGGBBAA (Bannerlord convention).
        /// </summary>
        [DataSourceProperty]
        public string FactionAccentColorHex
        {
            get => _factionAccentColorHex;
            set { if (value != _factionAccentColorHex) { _factionAccentColorHex = value; OnPropertyChangedWithValue(value); } }
        }

        /// <summary>
        /// Landmarks for the currently selected faction (shown in info panel).
        /// </summary>
        [DataSourceProperty]
        public MBBindingList<LandmarkItemVM> FactionLandmarks { get; }

        /// <summary>
        /// All landmarks for display on the map (capitals shown as markers).
        /// </summary>
        [DataSourceProperty]
        public MBBindingList<LandmarkItemVM> AllLandmarks { get; }

        /// <summary>
        /// Whether to show landmark markers on the map.
        /// </summary>
        [DataSourceProperty]
        public bool ShowLandmarks
        {
            get => _showLandmarks;
            set { if (value != _showLandmarks) { _showLandmarks = value; OnPropertyChangedWithValue(value); } }
        }

        #endregion

        #region Banner

        /// <summary>
        /// Normalized X position for the selection banner (0-1). Negative = hidden.
        /// </summary>
        [DataSourceProperty]
        public float BannerPosX
        {
            get => _bannerPosX;
            set { if (Math.Abs(_bannerPosX - value) > 0.0001f) { _bannerPosX = value; OnPropertyChangedWithValue(value); } }
        }

        /// <summary>
        /// Normalized Y position for the selection banner (0-1). Negative = hidden.
        /// </summary>
        [DataSourceProperty]
        public float BannerPosY
        {
            get => _bannerPosY;
            set { if (Math.Abs(_bannerPosY - value) > 0.0001f) { _bannerPosY = value; OnPropertyChangedWithValue(value); } }
        }

        /// <summary>
        /// Hex color string for the banner faction tint.
        /// </summary>
        [DataSourceProperty]
        public string BannerColorHex
        {
            get => _bannerColorHex;
            set { if (_bannerColorHex != value) { _bannerColorHex = value; OnPropertyChangedWithValue(value); } }
        }

        /// <summary>
        /// Banner image name (without .png) — per-faction banner loaded from GUI/SpriteData/FactionMap/.
        /// </summary>
        [DataSourceProperty]
        public string BannerImage
        {
            get => _bannerImage;
            set { if (_bannerImage != value) { _bannerImage = value; OnPropertyChangedWithValue(value); } }
        }

        /// <summary>
        /// Faction side ("free" / "evil" / "neutral") — used for banner glow color.
        /// </summary>
        [DataSourceProperty]
        public string BannerSide
        {
            get => _bannerSide;
            set { if (_bannerSide != value) { _bannerSide = value; OnPropertyChangedWithValue(value); } }
        }

        #endregion

        #region Hover Tooltip (PropertyBasedTooltip with faction color)

        private string _lastHoveredFaction = "";

        /// <summary>
        /// Called each frame to poll hover state from the widget layer.
        /// Shows a colored PropertyBasedTooltip at the cursor via InformationManager.
        /// </summary>
        public void Tick()
        {
            string current = PolygonWidget.HoveredFactionName ?? "";
            if (current != _lastHoveredFaction)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    // Find faction color
                    Color color = new Color(1f, 1f, 1f, 1f);
                    foreach (var kvp in _factions)
                    {
                        if (kvp.Value.Name == current && !string.IsNullOrEmpty(kvp.Value.Color))
                        {
                            color = ParseHexColor(kvp.Value.Color);
                            break;
                        }
                    }

                    // Show native PropertyBasedTooltip with faction name in faction color
                    var props = new List<TooltipProperty>
                    {
                        new TooltipProperty(current, "", 0, color, false,
                            TooltipProperty.TooltipPropertyFlags.Title)
                    };
                    InformationManager.ShowTooltip(typeof(List<TooltipProperty>), props);
                }
                else
                {
                    InformationManager.HideTooltip();
                }
                _lastHoveredFaction = current;
            }
        }

        /// <summary>
        /// Create a very dark panel tint from a faction color.
        /// Blends 18% of the faction hue into a dark base (#12121AFF).
        /// </summary>
        private static string MakeDarkPanelHex(string factionHex)
        {
            if (string.IsNullOrEmpty(factionHex) || factionHex.Length < 7)
                return "#1a1a2eFF";
            var c = ParseHexColor(factionHex);
            // Mix: 82% dark base + 18% faction color
            float baseR = 0.07f, baseG = 0.07f, baseB = 0.10f;
            float mix = 0.18f;
            int r = (int)(((1f - mix) * baseR + mix * c.Red) * 255f);
            int g = (int)(((1f - mix) * baseG + mix * c.Green) * 255f);
            int b = (int)(((1f - mix) * baseB + mix * c.Blue) * 255f);
            r = Math.Max(0, Math.Min(255, r));
            g = Math.Max(0, Math.Min(255, g));
            b = Math.Max(0, Math.Min(255, b));
            return $"#{r:X2}{g:X2}{b:X2}D0";  // slight transparency so map peeks through edges
        }

        /// <summary>
        /// Create an accent color for section header borders from faction color.
        /// Darkens/saturates the color to work as a UI frame tint.
        /// </summary>
        private static string MakeAccentColorHex(string factionHex)
        {
            if (string.IsNullOrEmpty(factionHex) || factionHex.Length < 7)
                return "#835513FF";
            var c = ParseHexColor(factionHex);
            // Darken to ~50% brightness for frame tinting
            int r = (int)(c.Red * 0.55f * 255f);
            int g = (int)(c.Green * 0.55f * 255f);
            int b = (int)(c.Blue * 0.55f * 255f);
            r = Math.Max(15, Math.Min(255, r));
            g = Math.Max(15, Math.Min(255, g));
            b = Math.Max(15, Math.Min(255, b));
            return $"#{r:X2}{g:X2}{b:X2}FF";
        }

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

        #region Commands

        /// <summary>
        /// Called from the Gauntlet XML when user clicks a region on the map.
        /// PolygonWidget sets LastClickedRegionName before this fires.
        /// </summary>
        public void ExecuteSelectRegion()
        {
            string regionName = PolygonWidget.LastClickedRegionName;
            if (string.IsNullOrEmpty(regionName))
                return;

            _selectedRegionName = regionName;

            FactionTraits.Clear();
            FactionBonuses.Clear();
            FactionPerks.Clear();
            FactionStrengths.Clear();
            FactionWeaknesses.Clear();
            FactionLandmarks.Clear();

            // Two-level lookup: region → faction ID → faction data
            var region = FactionRegistry.GetRegion(regionName);
            var faction = FactionRegistry.GetFactionForRegion(regionName);
            if (faction != null)
            {
                SelectedFactionName = faction.Name;
                SelectedFactionDesc = faction.Description;
                SelectedFactionPlayable = faction.Playable;
                _selectedHasCulture = !string.IsNullOrEmpty(faction.GameFaction);
                HasSelection = true;

                // Populate traits list
                if (faction.Traits != null)
                {
                    foreach (var trait in faction.Traits)
                        FactionTraits.Add(new FactionTraitItemVM(trait));
                }

                // Populate bonuses
                if (faction.Bonuses != null)
                {
                    foreach (var bonus in faction.Bonuses)
                        FactionBonuses.Add(new FactionBonusItemVM(bonus.Text, bonus.Positive));
                }

                // Populate perks
                if (faction.Perks != null)
                {
                    foreach (var perk in faction.Perks)
                        FactionPerks.Add(new FactionPerkItemVM(perk.Name, perk.Description));
                }

                // Special unit
                if (faction.SpecialUnit != null && !string.IsNullOrEmpty(faction.SpecialUnit.Name))
                {
                    SpecialUnitName = faction.SpecialUnit.Name;
                    SpecialUnitDesc = faction.SpecialUnit.Description;
                    HasSpecialUnit = true;
                }
                else
                {
                    SpecialUnitName = "";
                    SpecialUnitDesc = "";
                    HasSpecialUnit = false;
                }

                // Strengths & Weaknesses (with prefix symbols for visual clarity)
                if (faction.Strengths != null)
                {
                    foreach (var s in faction.Strengths)
                        FactionStrengths.Add(new FactionBonusItemVM("+ " + s, true));
                }
                if (faction.Weaknesses != null)
                {
                    foreach (var w in faction.Weaknesses)
                        FactionWeaknesses.Add(new FactionBonusItemVM("- " + w, false));
                }
                OnPropertyChanged(nameof(HasStrengths));
                OnPropertyChanged(nameof(HasWeaknesses));

                // Side (Free Peoples / Forces of Evil / Neutral)
                SelectedFactionSide = faction.Side ?? "neutral";

                // Difficulty — visual stars + label
                Difficulty = faction.Difficulty;
                string stars = faction.Difficulty > 0
                    ? new string('*', faction.Difficulty) + new string(' ', 5 - faction.Difficulty)
                    : "";
                DifficultyText = faction.Difficulty switch
                {
                    1 => "Difficulty: Easy  " + stars,
                    2 => "Difficulty: Normal  " + stars,
                    3 => "Difficulty: Hard  " + stars,
                    4 => "Difficulty: Very Hard  " + stars,
                    5 => "Difficulty: Extreme  " + stars,
                    _ => ""
                };

                // Faction image ID
                FactionImageId = faction.Image ?? "";

                // Panel background tint from faction color
                FactionColorHex = MakeDarkPanelHex(faction.Color);
                FactionAccentColorHex = MakeAccentColorHex(faction.Color);

                // Update banner position at capital — ONLY for playable factions
                if (faction.Playable && region != null && region.HasCapitalPos)
                {
                    BannerColorHex = faction.Color ?? "#FFFFFFFF";
                    BannerSide = faction.Side ?? "neutral";
                    BannerImage = "banner_" + region.FactionId;
                    BannerPosX = region.CapitalX;
                    BannerPosY = region.CapitalY;
                }
                else
                {
                    // Not playable or no capital position — hide banner
                    BannerSide = "neutral";
                    BannerPosX = -1f;
                    BannerPosY = -1f;
                }
            }
            else
            {
                SelectedFactionName = regionName;
                SelectedFactionDesc = "";
                SelectedFactionPlayable = false;
                _selectedHasCulture = false;
                HasSelection = true;
                HasSpecialUnit = false;
                SpecialUnitName = "";
                SpecialUnitDesc = "";
                FactionImageId = "";
                SelectedFactionSide = "neutral";
                Difficulty = 0;
                DifficultyText = "";
                FactionColorHex = "#1a1a2eD0";
                FactionAccentColorHex = "#835513FF";
                OnPropertyChanged(nameof(HasStrengths));
                OnPropertyChanged(nameof(HasWeaknesses));

                // No faction — hide banner
                BannerPosX = -1f;
                BannerPosY = -1f;
            }
        }

        /// <summary>
        /// Called when the user clicks the back button.
        /// </summary>
        public void OnPreviousStage()
        {
            _onPreviousStage?.Invoke();
        }

        /// <summary>
        /// Called when user clicks "Confirm Selection".
        /// Resolves the game culture from the faction's game_faction field.
        /// </summary>
        public void ExecuteConfirm()
        {
            if (string.IsNullOrEmpty(_selectedRegionName))
                return;

            // Two-level lookup: region → faction
            var faction = FactionRegistry.GetFactionForRegion(_selectedRegionName);
            if (faction != null)
            {
                string cultureId = faction.GameFaction;
                if (string.IsNullOrEmpty(cultureId))
                {
                    SubModule.LogError($"No game_faction mapped for {_selectedRegionName} ({faction.Name})");
                    return;
                }

                var culture = RegionConfig.ResolveCulture(cultureId);
                if (culture == null)
                {
                    SubModule.LogError($"Culture '{cultureId}' not available!");
                    return;
                }

                SubModule.Log($"Faction confirmed: {faction.Name} → {culture.Name}");
                _onCultureSelected?.Invoke(culture);
            }
            else
            {
                SubModule.LogError($"Unknown region or no faction: {_selectedRegionName}");
            }
        }

        #endregion

        private void UpdateSelectionInfo()
        {
            // Kept for compatibility — selection now handled in ExecuteSelectRegion
        }

        private void LoadAllLandmarks()
        {
            AllLandmarks.Clear();
            foreach (var landmark in LandmarkConfig.Landmarks)
            {
                // Only show capitals on the main map overlay
                if (landmark.Type == LandmarkConfig.LandmarkType.Capital)
                {
                    AllLandmarks.Add(new LandmarkItemVM(landmark));
                }
            }
        }

        /// <summary>
        /// Load region geometry from regions.json and faction definitions from factions.json.
        /// Both files live in ModuleData/.
        /// </summary>
        private void LoadRegionsAndFactions()
        {
            try
            {
                string modPath = SubModule.ModulePath;
                if (string.IsNullOrEmpty(modPath)) return;

                // Load regions.json
                string regionsPath = System.IO.Path.Combine(modPath, "ModuleData", "regions.json");
                if (!System.IO.File.Exists(regionsPath))
                {
                    SubModule.LogError($"regions.json not found: {regionsPath}");
                    return;
                }
                string regionsJson = System.IO.File.ReadAllText(regionsPath);
                _regions = SimpleJsonParser.ParseRegions(regionsJson);
                SubModule.Log($"Loaded {_regions.Count} regions from regions.json");

                // Load factions.json
                string factionsPath = System.IO.Path.Combine(modPath, "ModuleData", "factions.json");
                if (!System.IO.File.Exists(factionsPath))
                {
                    SubModule.LogError($"factions.json not found: {factionsPath}");
                    return;
                }
                string factionsJson = System.IO.File.ReadAllText(factionsPath);
                _factions = SimpleJsonParser.ParseFactions(factionsJson);
                SubModule.Log($"Loaded {_factions.Count} factions from factions.json");

                // Initialize registry for PolygonWidget lookups
                FactionRegistry.Initialize(_regions, _factions);
            }
            catch (Exception ex)
            {
                SubModule.LogError($"Error loading regions/factions: {ex.Message}");
            }
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            if (!string.IsNullOrEmpty(_lastHoveredFaction))
                InformationManager.HideTooltip();
            FactionTraits.Clear();
            FactionBonuses.Clear();
            FactionPerks.Clear();
            FactionStrengths.Clear();
            FactionWeaknesses.Clear();
            FactionLandmarks.Clear();
            AllLandmarks.Clear();
        }
    }

    /// <summary>
    /// ViewModel for faction trait items (simple string tags).
    /// </summary>
    public class FactionTraitItemVM : ViewModel
    {
        private string _text;

        public FactionTraitItemVM(string text) { _text = text; }

        [DataSourceProperty]
        public string Text
        {
            get => _text;
            set { if (value != _text) { _text = value; OnPropertyChangedWithValue(value); } }
        }
    }

    /// <summary>
    /// ViewModel for faction bonus items (positive/negative effects).
    /// </summary>
    public class FactionBonusItemVM : ViewModel
    {
        private string _text;
        private bool _isPositive;

        public FactionBonusItemVM(string text, bool isPositive)
        {
            _text = text;
            _isPositive = isPositive;
        }

        [DataSourceProperty]
        public string Text
        {
            get => _text;
            set { if (value != _text) { _text = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public bool IsPositive
        {
            get => _isPositive;
            set { if (value != _isPositive) { _isPositive = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(IsNegative)); } }
        }

        [DataSourceProperty]
        public bool IsNegative => !_isPositive;
    }

    /// <summary>
    /// ViewModel for faction perk items (name + description).
    /// </summary>
    public class FactionPerkItemVM : ViewModel
    {
        private string _perkName;
        private string _perkDescription;

        public FactionPerkItemVM(string name, string description)
        {
            _perkName = name;
            _perkDescription = description;
        }

        [DataSourceProperty]
        public string PerkName
        {
            get => _perkName;
            set { if (value != _perkName) { _perkName = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public string PerkDescription
        {
            get => _perkDescription;
            set { if (value != _perkDescription) { _perkDescription = value; OnPropertyChangedWithValue(value); } }
        }
    }

    /// <summary>
    /// ViewModel for landmark items (cities, fortresses, etc.)
    /// </summary>
    public class LandmarkItemVM : ViewModel
    {
        private readonly LandmarkConfig.LandmarkDef _def;

        public LandmarkItemVM(LandmarkConfig.LandmarkDef def)
        {
            _def = def;
        }

        [DataSourceProperty]
        public string Id => _def.Id;

        [DataSourceProperty]
        public string Name => _def.Name;

        [DataSourceProperty]
        public string Description => _def.Description;

        [DataSourceProperty]
        public string TypeName => _def.Type.ToString();

        [DataSourceProperty]
        public bool IsCapital => _def.Type == LandmarkConfig.LandmarkType.Capital;

        [DataSourceProperty]
        public bool IsCity => _def.Type == LandmarkConfig.LandmarkType.City;

        [DataSourceProperty]
        public bool IsFortress => _def.Type == LandmarkConfig.LandmarkType.Fortress;

        [DataSourceProperty]
        public bool IsRuin => _def.Type == LandmarkConfig.LandmarkType.Ruin;

        [DataSourceProperty]
        public bool IsPort => _def.Type == LandmarkConfig.LandmarkType.Port;

        /// <summary>
        /// X position in texture coordinates (2048x1423).
        /// </summary>
        [DataSourceProperty]
        public int PosX => _def.X;

        /// <summary>
        /// Y position in texture coordinates (2048x1423).
        /// </summary>
        [DataSourceProperty]
        public int PosY => _def.Y;

        /// <summary>
        /// Normalized X position (0-1) for UI positioning.
        /// </summary>
        [DataSourceProperty]
        public float NormalizedX => _def.X / 2048f;

        /// <summary>
        /// Normalized Y position (0-1) for UI positioning.
        /// </summary>
        [DataSourceProperty]
        public float NormalizedY => _def.Y / 1423f;

        /// <summary>
        /// Faction ID this landmark belongs to.
        /// </summary>
        [DataSourceProperty]
        public int FactionId => _def.FactionId;

        /// <summary>
        /// Type icon based on landmark type.
        /// </summary>
        [DataSourceProperty]
        public string TypeIcon
        {
            get
            {
                return _def.Type switch
                {
                    LandmarkConfig.LandmarkType.Capital => "marker_capital",
                    LandmarkConfig.LandmarkType.City => "marker_city",
                    LandmarkConfig.LandmarkType.Fortress => "marker_fortress",
                    LandmarkConfig.LandmarkType.Ruin => "marker_ruin",
                    LandmarkConfig.LandmarkType.Port => "marker_port",
                    LandmarkConfig.LandmarkType.Landmark => "marker_landmark",
                    _ => "marker_default"
                };
            }
        }
    }
}
