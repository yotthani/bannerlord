using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using SandBox.ViewModelCollection.Input;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// ViewModel for the Orbital Ring System screen.
    /// Features planetary-style ring orbits around The One Ring.
    /// </summary>
    public class RingScreenVM : ViewModel
    {
        private readonly Action _closeAction;
        private readonly OrbitManager _orbitManager;
        
        // Header
        private string _screenTitle;
        private string _ringCountText;
        private string _loreQuote;
        
        // Selected ring details
        private OrbitalRingVM _selectedRing;
        private string _selectedRingName;
        private string _selectedRingType;
        private string _selectedRingEffect;
        private string _selectedRingLore;
        private bool _hasSelectedRing;
        private bool _isSelectedRingOwned;
        private bool _isSelectedRingEquipped;
        
        // Equipped ring preview (floating above orbits)
        private OrbitalRingVM _equippedRing;
        private bool _hasEquippedRing;
        private string _equippedRingName;
        private string _equippedRingEffect;
        private float _equippedPreviewSize;
        
        // Preview ring animation
        private float _previewAnimPhase = 0f;
        private float _previewPulseScale = 1.0f;
        private float _previewGlowOpacity = 0.3f;
        private float _previewBobOffset = 0f;
        private string _previewRingSprite = "";
        
        // Status panel
        private float _corruptionPercent;
        private float _corruptionBarWidth;
        private string _corruptionText;
        private string _threatLevelDisplay;
        private string _threatDescription;
        private string _activeEffectsText;
        
        // Ring collections for binding
        private MBBindingList<OrbitalRingVM> _allOrbitalRings;
        private MBBindingList<OrbitalRingVM> _equippedRingsList;
        
        // Actions
        private bool _canEquipSelected;
        private bool _canUnequipSelected;
        private string _equipButtonText;
        
        // Input keys for Standard.TripleDialogCloseButtons
        private InputKeyItemVM _cancelInputKey;
        private InputKeyItemVM _doneInputKey;
        private InputKeyItemVM _resetInputKey;
        private HintViewModel _equipHint;
        
        // Context menu
        private bool _isContextMenuVisible;
        private float _contextMenuPosX;
        private float _contextMenuPosY;
        private OrbitalRingVM _contextMenuRing;
        
        // Constants
        private const int MaxEquippedRings = 1;
        private const float BaseCorruptionBarWidth = 200f;
        
        // Animation state
        private const float RotationDuration = 0.4f;      // 400ms like mock
        private const float AutoRotateDuration = 0.5f;    // 500ms for auto-rotate to front
        private const float FloatAnimDuration = 0.55f;    // 550ms for equip/unequip float
        private const float OneRingBobSpeed = 1.5f;       // Bobbing cycle speed
        private const float OneRingBobAmount = 8f;        // Pixels up/down
        
        private bool _isRotating;
        private float _rotationProgress;
        private float _rotationTargetDuration;
        private Dictionary<string, float> _rotationStartAngles;
        private Dictionary<string, float> _rotationEndAngles;
        
        private bool _isFloating;
        private float _floatProgress;
        private OrbitalRingVM _floatingRing;
        private bool _floatDirectionUp;  // true = equip (up), false = unequip (down)
        private float _floatStartX, _floatStartY, _floatEndX, _floatEndY;
        
        // One Ring continuous bobbing animation
        private float _oneRingBobPhase = 0f;
        private float _oneRingBobOffset = 0f;
        private float _elvenOrbitPhase = 0f;
        private float _dwarvenOrbitPhase = (float)Math.PI * 0.33f;  // Offset for visual variety
        private float _mortalOrbitPhase = (float)Math.PI * 0.66f;  // Offset for visual variety
        
        // Float animation visual offset (for equip/unequip)
        private float _floatVisualOffsetY = 0f;
        
        public RingScreenVM(Action closeAction)
        {
            _closeAction = closeAction;
            _screenTitle = new TextObject("{=ring_screen_title}Rings of Power").ToString();
            _loreQuote = new TextObject("{=ring_lore_quote}One Ring to rule them all, One Ring to find them").ToString();
            _equippedPreviewSize = 100f;
            _equipButtonText = new TextObject("{=ring_bear}Bear Ring").ToString();
            
            // Read debug setting from MCM
            try
            {
                _showDebugBoundary = HeirOfNumenor.Settings.Instance?.RingScreenDebugBoundary ?? false;
            }
            catch
            {
                _showDebugBoundary = false;
            }
            
            // Initialize input keys for Standard.TripleDialogCloseButtons
            _cancelInputKey = null;  // Will be bound from GauntletLayer if needed
            _doneInputKey = null;
            _resetInputKey = null;
            _equipHint = new HintViewModel();
            
            // Initialize orbit manager (center will be set by screen size)
            _orbitManager = new OrbitManager(0, 0);
            
            // Initialize collections
            _allOrbitalRings = new MBBindingList<OrbitalRingVM>();
            _equippedRingsList = new MBBindingList<OrbitalRingVM>();
            
            InitializeRings();
            RefreshFromInventory();
            UpdateOrbitalPositions();
            
            // IMPORTANT: Rebuild display list AFTER positions are calculated
            // so that Z-order sorting works correctly
            RebuildRingDisplayList();
            
            RefreshStatusPanel();
        }
        
        /// <summary>
        /// Called by GauntletRingScreen to set up input key bindings for Standard.TripleDialogCloseButtons.
        /// </summary>
        public void SetInputKeys(HotKey cancelKey, HotKey doneKey, HotKey equipKey)
        {
            if (cancelKey != null)
                CancelInputKey = InputKeyItemVM.CreateFromHotKey(cancelKey, true);
            if (doneKey != null)
                DoneInputKey = InputKeyItemVM.CreateFromHotKey(doneKey, true);
            if (equipKey != null)
                ResetInputKey = InputKeyItemVM.CreateFromHotKey(equipKey, true);
        }
        
        #region Initialization
        
        private void InitializeRings()
        {
            // ═══════════════════════════════════════════════════════════
            // THE ONE RING (Center)
            // ═══════════════════════════════════════════════════════════
            _orbitManager.OneRing = CreateRing(
                "hon_ring_one_ring", "The One Ring", "One", "One",
                "Dominion over all ring-bearers. Invisibility. Accelerated corruption.",
                "Forged by Sauron in the fires of Mount Doom. One Ring to rule them all."
            );
            
            // ═══════════════════════════════════════════════════════════
            // ELVEN RINGS (Inner Orbit - 3 rings)
            // ═══════════════════════════════════════════════════════════
            _orbitManager.ElvenRings.Add(CreateRing(
                "hon_ring_narya", "Narya", "Fire", "Elven",
                "Ring of Fire. +15% party morale, resistance to despair.",
                "The Ring of Fire, set with a ruby. Worn by Gandalf."
            ));
            _orbitManager.ElvenRings.Add(CreateRing(
                "hon_ring_nenya", "Nenya", "Water", "Elven",
                "Ring of Water. +20% healing rate, protection from corruption.",
                "The Ring of Adamant, made of mithril with a diamond. Worn by Galadriel."
            ));
            _orbitManager.ElvenRings.Add(CreateRing(
                "hon_ring_vilya", "Vilya", "Air", "Elven",
                "Ring of Air. +25% movement speed, weather influence.",
                "The Ring of Sapphire, mightiest of the Three. Worn by Elrond."
            ));
            
            // ═══════════════════════════════════════════════════════════
            // DWARVEN RINGS (Middle Orbit - 7 rings)
            // ═══════════════════════════════════════════════════════════
            _orbitManager.DwarvenRings.Add(CreateRing(
                "hon_ring_dwarf_1", "Ring of Durin", "Durin", "Dwarven",
                "Amplifies gold-finding. +30% trade profit.",
                "Greatest of the Seven, worn by the Kings of Khazad-dûm."
            ));
            _orbitManager.DwarvenRings.Add(CreateRing(
                "hon_ring_dwarf_2", "Ring of Thráin", "Thráin", "Dwarven",
                "Enhances crafting. +20% smithing quality.",
                "Last of the Seven to remain with the Dwarves."
            ));
            _orbitManager.DwarvenRings.Add(CreateRing(
                "hon_ring_dwarf_3", "Ring of the Firebeards", "Firebeard", "Dwarven",
                "Fire resistance. +15% siege effectiveness.",
                "Given to the Lords of Nogrod."
            ));
            _orbitManager.DwarvenRings.Add(CreateRing(
                "hon_ring_dwarf_4", "Ring of the Broadbeams", "Broadbeam", "Dwarven",
                "Stone mastery. +25% construction speed.",
                "Worn by the Kings of Belegost."
            ));
            _orbitManager.DwarvenRings.Add(CreateRing(
                "hon_ring_dwarf_5", "Ring of the Ironfists", "Ironfist", "Dwarven",
                "Battle fury. +20% melee damage.",
                "Lost in wars with dragons."
            ));
            _orbitManager.DwarvenRings.Add(CreateRing(
                "hon_ring_dwarf_6", "Ring of the Stiffbeards", "Stiffbeard", "Dwarven",
                "Endurance. +30% fatigue resistance.",
                "Hidden in the deeps of the East."
            ));
            _orbitManager.DwarvenRings.Add(CreateRing(
                "hon_ring_dwarf_7", "Ring of the Blacklocks", "Blacklock", "Dwarven",
                "Shadow sight. Reveals hidden enemies.",
                "Consumed by dragon fire."
            ));
            
            // ═══════════════════════════════════════════════════════════
            // MORTAL RINGS (Outer Orbit - 9 rings)
            // ═══════════════════════════════════════════════════════════
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_1", "Ring of the Witch-King", "Witch-King", "Mortal",
                "Terror aura. Enemies flee in fear. High corruption.",
                "Chief of the Nazgûl, Lord of Angmar."
            ));
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_2", "Ring of Khamûl", "Khamûl", "Mortal",
                "Shadow sense. Track enemies across great distances.",
                "The Black Easterling, second of the Nine."
            ));
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_3", "Ring of the Dark Marshal", "Marshal", "Mortal",
                "Command undead. +40% undead troop effectiveness.",
                "General of Sauron's armies."
            ));
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_4", "Ring of the Betrayer", "Betrayer", "Mortal",
                "Sow discord. Enemy parties have -20% cohesion.",
                "A Black Númenórean who served Sauron."
            ));
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_5", "Ring of the Shadow Lord", "Shadow", "Mortal",
                "Move unseen. +50% stealth, invisibility at night.",
                "Master of shadows and deception."
            ));
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_6", "Ring of the Undying", "Undying", "Mortal",
                "Defy death. Survive fatal blows once per battle.",
                "Cheated death itself through dark power."
            ));
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_7", "Ring of the Tainted", "Tainted", "Mortal",
                "Poison mastery. Attacks inflict corruption.",
                "Spreader of plague and corruption."
            ));
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_8", "Ring of the Knight of Umbar", "Knight", "Mortal",
                "Naval dominion. +30% ship speed, coastal raids.",
                "Corsair lord of the southern seas."
            ));
            _orbitManager.MortalRings.Add(CreateRing(
                "hon_ring_nazgul_9", "Ring of the Dwimmerlaik", "Dwimmerlaik", "Mortal",
                "Spectral form. Pass through enemies in battle.",
                "Most wraith-like of the Nine."
            ));
            
            // NOTE: RebuildRingDisplayList is called AFTER UpdateOrbitalPositions
            // in the constructor so that Z-order sorting works correctly
        }
        
        private OrbitalRingVM CreateRing(string id, string nameKey, string shortNameKey, string category,
                                         string effectKey, string loreKey)
        {
            // Use TextObject with string IDs based on ring ID for localization
            string name = new TextObject($"{{={id}_name}}{nameKey}").ToString();
            string shortName = new TextObject($"{{={id}_short}}{shortNameKey}").ToString();
            string effect = new TextObject($"{{={id}_effect}}{effectKey}").ToString();
            string lore = new TextObject($"{{={id}_lore}}{loreKey}").ToString();
            
            return new OrbitalRingVM(
                id, name, shortName, category, effect, lore,
                OnRingSelected, OnRingRightClicked
            );
        }
        
        private void RebuildRingDisplayList()
        {
            _allOrbitalRings.Clear();
            
            // Get all rings sorted by Z-order
            var sorted = _orbitManager.GetAllRingsSortedByZOrder();
            foreach (var ring in sorted)
            {
                _allOrbitalRings.Add(ring);
            }
        }
        
        #endregion
        
        #region Data Properties - Header
        
        [DataSourceProperty]
        public string ScreenTitle 
        { 
            get => _screenTitle; 
            set { if (_screenTitle != value) { _screenTitle = value; OnPropertyChangedWithValue(value, nameof(ScreenTitle)); } } 
        }
        
        [DataSourceProperty]
        public string RingCountText 
        { 
            get => _ringCountText; 
            set { if (_ringCountText != value) { _ringCountText = value; OnPropertyChangedWithValue(value, nameof(RingCountText)); } } 
        }
        
        [DataSourceProperty]
        public string LoreQuote 
        { 
            get => _loreQuote; 
            set { if (_loreQuote != value) { _loreQuote = value; OnPropertyChangedWithValue(value, nameof(LoreQuote)); } } 
        }
        
        // Localized UI Labels
        [DataSourceProperty]
        public string LabelNoRingWorn => new TextObject("{=ring_no_ring_worn}No Ring Worn").ToString();
        
        [DataSourceProperty]
        public string LabelRingLore => new TextObject("{=ring_lore_header}Ring Lore").ToString();
        
        [DataSourceProperty]
        public string LabelPower => new TextObject("{=ring_power}POWER").ToString();
        
        [DataSourceProperty]
        public string LabelHistory => new TextObject("{=ring_history}HISTORY").ToString();
        
        [DataSourceProperty]
        public string LabelInPossession => new TextObject("{=ring_possessed}In Your Possession").ToString();
        
        [DataSourceProperty]
        public string LabelLostToShadow => new TextObject("{=ring_lost}Lost to Shadow").ToString();
        
        [DataSourceProperty]
        public string LabelBearingRing => new TextObject("{=ring_bearing}✧ BEARING THIS RING ✧").ToString();
        
        [DataSourceProperty]
        public string LabelSelectRing => new TextObject("{=ring_select}Select a Ring").ToString();
        
        [DataSourceProperty]
        public string LabelBearerStatus => new TextObject("{=ring_bearer_status}Bearer Status").ToString();
        
        [DataSourceProperty]
        public string LabelCorruption => new TextObject("{=ring_corruption_label}CORRUPTION").ToString();
        
        [DataSourceProperty]
        public string LabelThreatLevel => new TextObject("{=ring_threat_label}THREAT LEVEL").ToString();
        
        [DataSourceProperty]
        public string LabelActivePowers => new TextObject("{=ring_active_powers}ACTIVE POWERS").ToString();
        
        [DataSourceProperty]
        public string LabelTheOne => new TextObject("{=ring_legend_one}The One").ToString();
        
        [DataSourceProperty]
        public string LabelElven => new TextObject("{=ring_legend_elven}Elven (3)").ToString();
        
        [DataSourceProperty]
        public string LabelDwarven => new TextObject("{=ring_legend_dwarven}Dwarven (7)").ToString();
        
        [DataSourceProperty]
        public string LabelMortal => new TextObject("{=ring_legend_mortal}Mortal (9)").ToString();
        
        [DataSourceProperty]
        public string LabelRotateLeft => new TextObject("{=ring_rotate_left}◄ Rotate").ToString();
        
        [DataSourceProperty]
        public string LabelRotateRight => new TextObject("{=ring_rotate_right}Rotate ►").ToString();
        
        [DataSourceProperty]
        public string HintBearRing => new TextObject("{=ring_hint_bear}Bear this Ring").ToString();
        
        [DataSourceProperty]
        public string HintRemoveRing => new TextObject("{=ring_hint_remove}Remove this Ring").ToString();
        
        [DataSourceProperty]
        public string HintClose => new TextObject("{=ring_hint_close}Close").ToString();
        
        [DataSourceProperty]
        public string ButtonLeave => new TextObject("{=ring_btn_leave}Leave").ToString();
        
        [DataSourceProperty]
        public string ButtonDone => new TextObject("{=ring_btn_done}Done").ToString();
        
        #endregion
        
        #region Data Properties - Selected Ring
        
        [DataSourceProperty]
        public string SelectedRingName 
        { 
            get => _selectedRingName; 
            set { if (_selectedRingName != value) { _selectedRingName = value; OnPropertyChangedWithValue(value, nameof(SelectedRingName)); } } 
        }
        
        [DataSourceProperty]
        public string SelectedRingType 
        { 
            get => _selectedRingType; 
            set { if (_selectedRingType != value) { _selectedRingType = value; OnPropertyChangedWithValue(value, nameof(SelectedRingType)); } } 
        }
        
        [DataSourceProperty]
        public string SelectedRingEffect 
        { 
            get => _selectedRingEffect; 
            set { if (_selectedRingEffect != value) { _selectedRingEffect = value; OnPropertyChangedWithValue(value, nameof(SelectedRingEffect)); } } 
        }
        
        [DataSourceProperty]
        public string SelectedRingLore 
        { 
            get => _selectedRingLore; 
            set { if (_selectedRingLore != value) { _selectedRingLore = value; OnPropertyChangedWithValue(value, nameof(SelectedRingLore)); } } 
        }
        
        [DataSourceProperty]
        public bool HasSelectedRing 
        { 
            get => _hasSelectedRing; 
            set { if (_hasSelectedRing != value) { _hasSelectedRing = value; OnPropertyChangedWithValue(value, nameof(HasSelectedRing)); } } 
        }
        
        [DataSourceProperty]
        public bool IsSelectedRingOwned 
        { 
            get => _isSelectedRingOwned; 
            set { if (_isSelectedRingOwned != value) { _isSelectedRingOwned = value; OnPropertyChangedWithValue(value, nameof(IsSelectedRingOwned)); } } 
        }
        
        [DataSourceProperty]
        public bool IsSelectedRingEquipped 
        { 
            get => _isSelectedRingEquipped; 
            set { if (_isSelectedRingEquipped != value) { _isSelectedRingEquipped = value; OnPropertyChangedWithValue(value, nameof(IsSelectedRingEquipped)); } } 
        }
        
        #endregion
        
        #region Data Properties - Equipped Preview
        
        [DataSourceProperty]
        public bool HasEquippedRing 
        { 
            get => _hasEquippedRing; 
            set { if (_hasEquippedRing != value) { _hasEquippedRing = value; OnPropertyChangedWithValue(value, nameof(HasEquippedRing)); } } 
        }
        
        [DataSourceProperty]
        public string EquippedRingName 
        { 
            get => _equippedRingName; 
            set { if (_equippedRingName != value) { _equippedRingName = value; OnPropertyChangedWithValue(value, nameof(EquippedRingName)); } } 
        }
        
        [DataSourceProperty]
        public string EquippedRingEffect 
        { 
            get => _equippedRingEffect; 
            set { if (_equippedRingEffect != value) { _equippedRingEffect = value; OnPropertyChangedWithValue(value, nameof(EquippedRingEffect)); } } 
        }
        
        // Preview ring animation properties
        [DataSourceProperty]
        public float PreviewPulseScale 
        { 
            get => _previewPulseScale; 
            set { if (Math.Abs(_previewPulseScale - value) > 0.001f) { _previewPulseScale = value; OnPropertyChangedWithValue(value, nameof(PreviewPulseScale)); } } 
        }
        
        [DataSourceProperty]
        public float PreviewGlowOpacity 
        { 
            get => _previewGlowOpacity; 
            set { if (Math.Abs(_previewGlowOpacity - value) > 0.001f) { _previewGlowOpacity = value; OnPropertyChangedWithValue(value, nameof(PreviewGlowOpacity)); } } 
        }
        
        [DataSourceProperty]
        public float PreviewBobOffset 
        { 
            get => _previewBobOffset; 
            set { if (Math.Abs(_previewBobOffset - value) > 0.1f) { _previewBobOffset = value; OnPropertyChangedWithValue(value, nameof(PreviewBobOffset)); } } 
        }
        
        [DataSourceProperty]
        public string PreviewRingSprite 
        { 
            get => _previewRingSprite; 
            set { if (_previewRingSprite != value) { _previewRingSprite = value; OnPropertyChangedWithValue(value, nameof(PreviewRingSprite)); } } 
        }
        
        [DataSourceProperty]
        public float EquippedPreviewSize 
        { 
            get => _equippedPreviewSize; 
            set { if (Math.Abs(_equippedPreviewSize - value) > 0.1f) { _equippedPreviewSize = value; OnPropertyChangedWithValue(value, nameof(EquippedPreviewSize)); } } 
        }
        
        [DataSourceProperty]
        public MBBindingList<OrbitalRingVM> EquippedRingsList 
        { 
            get => _equippedRingsList; 
            set { if (_equippedRingsList != value) { _equippedRingsList = value; OnPropertyChangedWithValue(value, nameof(EquippedRingsList)); } } 
        }
        
        [DataSourceProperty]
        public OrbitalRingVM EquippedRingPreview 
        { 
            get => _equippedRing; 
            set { if (_equippedRing != value) { _equippedRing = value; OnPropertyChangedWithValue(value, nameof(EquippedRingPreview)); } } 
        }
        
        #endregion
        
        #region Data Properties - Status Panel
        
        [DataSourceProperty]
        public float CorruptionPercent 
        { 
            get => _corruptionPercent; 
            set { if (Math.Abs(_corruptionPercent - value) > 0.1f) { _corruptionPercent = value; OnPropertyChangedWithValue(value, nameof(CorruptionPercent)); } } 
        }
        
        [DataSourceProperty]
        public float CorruptionBarWidth 
        { 
            get => _corruptionBarWidth; 
            set { if (Math.Abs(_corruptionBarWidth - value) > 0.1f) { _corruptionBarWidth = value; OnPropertyChangedWithValue(value, nameof(CorruptionBarWidth)); } } 
        }
        
        [DataSourceProperty]
        public string CorruptionText 
        { 
            get => _corruptionText; 
            set { if (_corruptionText != value) { _corruptionText = value; OnPropertyChangedWithValue(value, nameof(CorruptionText)); } } 
        }
        
        [DataSourceProperty]
        public string ThreatLevelDisplay 
        { 
            get => _threatLevelDisplay; 
            set { if (_threatLevelDisplay != value) { _threatLevelDisplay = value; OnPropertyChangedWithValue(value, nameof(ThreatLevelDisplay)); } } 
        }
        
        [DataSourceProperty]
        public string ThreatDescription 
        { 
            get => _threatDescription; 
            set { if (_threatDescription != value) { _threatDescription = value; OnPropertyChangedWithValue(value, nameof(ThreatDescription)); } } 
        }
        
        [DataSourceProperty]
        public string ActiveEffectsText 
        { 
            get => _activeEffectsText; 
            set { if (_activeEffectsText != value) { _activeEffectsText = value; OnPropertyChangedWithValue(value, nameof(ActiveEffectsText)); } } 
        }
        
        #endregion
        
        #region Data Properties - Ring Collections
        
        [DataSourceProperty]
        public MBBindingList<OrbitalRingVM> AllOrbitalRings 
        { 
            get => _allOrbitalRings; 
            set { if (_allOrbitalRings != value) { _allOrbitalRings = value; OnPropertyChangedWithValue(value, nameof(AllOrbitalRings)); } } 
        }
        
        // ═══ SIMPLIFIED STATIC LAYOUT PROPERTIES ═══
        
        /// <summary>The One Ring - center of display</summary>
        [DataSourceProperty]
        public OrbitalRingVM OneRing => _orbitManager?.OneRing;
        
        /// <summary>Is The One Ring currently selected?</summary>
        [DataSourceProperty]
        public bool IsOneRingSelected => _selectedRing == _orbitManager?.OneRing;
        
        /// <summary>The 3 Elven Rings for static ellipse display</summary>
        [DataSourceProperty]
        public MBBindingList<OrbitalRingVM> ElvenRings 
        { 
            get 
            {
                var list = new MBBindingList<OrbitalRingVM>();
                if (_orbitManager?.ElvenRings != null)
                {
                    foreach (var ring in _orbitManager.ElvenRings)
                        list.Add(ring);
                }
                return list;
            }
        }
        
        // Individual elven ring properties for hardcoded positioning
        [DataSourceProperty]
        public OrbitalRingVM ElvenRing1 => _orbitManager?.ElvenRings?.Count > 0 ? _orbitManager.ElvenRings[0] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM ElvenRing2 => _orbitManager?.ElvenRings?.Count > 1 ? _orbitManager.ElvenRings[1] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM ElvenRing3 => _orbitManager?.ElvenRings?.Count > 2 ? _orbitManager.ElvenRings[2] : null;
        
        // Individual dwarven ring properties (7 rings)
        [DataSourceProperty]
        public OrbitalRingVM DwarvenRing1 => _orbitManager?.DwarvenRings?.Count > 0 ? _orbitManager.DwarvenRings[0] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM DwarvenRing2 => _orbitManager?.DwarvenRings?.Count > 1 ? _orbitManager.DwarvenRings[1] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM DwarvenRing3 => _orbitManager?.DwarvenRings?.Count > 2 ? _orbitManager.DwarvenRings[2] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM DwarvenRing4 => _orbitManager?.DwarvenRings?.Count > 3 ? _orbitManager.DwarvenRings[3] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM DwarvenRing5 => _orbitManager?.DwarvenRings?.Count > 4 ? _orbitManager.DwarvenRings[4] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM DwarvenRing6 => _orbitManager?.DwarvenRings?.Count > 5 ? _orbitManager.DwarvenRings[5] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM DwarvenRing7 => _orbitManager?.DwarvenRings?.Count > 6 ? _orbitManager.DwarvenRings[6] : null;
        
        // Individual mortal ring properties (9 rings)
        [DataSourceProperty]
        public OrbitalRingVM MortalRing1 => _orbitManager?.MortalRings?.Count > 0 ? _orbitManager.MortalRings[0] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM MortalRing2 => _orbitManager?.MortalRings?.Count > 1 ? _orbitManager.MortalRings[1] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM MortalRing3 => _orbitManager?.MortalRings?.Count > 2 ? _orbitManager.MortalRings[2] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM MortalRing4 => _orbitManager?.MortalRings?.Count > 3 ? _orbitManager.MortalRings[3] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM MortalRing5 => _orbitManager?.MortalRings?.Count > 4 ? _orbitManager.MortalRings[4] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM MortalRing6 => _orbitManager?.MortalRings?.Count > 5 ? _orbitManager.MortalRings[5] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM MortalRing7 => _orbitManager?.MortalRings?.Count > 6 ? _orbitManager.MortalRings[6] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM MortalRing8 => _orbitManager?.MortalRings?.Count > 7 ? _orbitManager.MortalRings[7] : null;
        
        [DataSourceProperty]
        public OrbitalRingVM MortalRing9 => _orbitManager?.MortalRings?.Count > 8 ? _orbitManager.MortalRings[8] : null;
        
        #endregion
        
        #region Data Properties - Actions
        
        [DataSourceProperty]
        public bool CanEquipSelected 
        { 
            get => _canEquipSelected; 
            set { if (_canEquipSelected != value) { _canEquipSelected = value; OnPropertyChangedWithValue(value, nameof(CanEquipSelected)); OnPropertyChanged(nameof(CanEquipOrUnequipSelected)); } } 
        }
        
        [DataSourceProperty]
        public bool CanUnequipSelected 
        { 
            get => _canUnequipSelected; 
            set { if (_canUnequipSelected != value) { _canUnequipSelected = value; OnPropertyChangedWithValue(value, nameof(CanUnequipSelected)); OnPropertyChanged(nameof(CanEquipOrUnequipSelected)); } } 
        }
        
        /// <summary>Combined property for equip/unequip button - enabled when either action is possible</summary>
        [DataSourceProperty]
        public bool CanEquipOrUnequipSelected => _canEquipSelected || _canUnequipSelected;
        
        [DataSourceProperty]
        public string EquipButtonText 
        { 
            get => _equipButtonText; 
            set { if (_equipButtonText != value) { _equipButtonText = value; OnPropertyChangedWithValue(value, nameof(EquipButtonText)); } } 
        }
        
        [DataSourceProperty]
        public InputKeyItemVM CancelInputKey 
        { 
            get => _cancelInputKey; 
            set { if (_cancelInputKey != value) { _cancelInputKey = value; OnPropertyChangedWithValue(value, nameof(CancelInputKey)); } } 
        }
        
        [DataSourceProperty]
        public InputKeyItemVM DoneInputKey 
        { 
            get => _doneInputKey; 
            set { if (_doneInputKey != value) { _doneInputKey = value; OnPropertyChangedWithValue(value, nameof(DoneInputKey)); } } 
        }
        
        [DataSourceProperty]
        public InputKeyItemVM ResetInputKey 
        { 
            get => _resetInputKey; 
            set { if (_resetInputKey != value) { _resetInputKey = value; OnPropertyChangedWithValue(value, nameof(ResetInputKey)); } } 
        }
        
        [DataSourceProperty]
        public HintViewModel EquipHint 
        { 
            get => _equipHint; 
            set { if (_equipHint != value) { _equipHint = value; OnPropertyChangedWithValue(value, nameof(EquipHint)); } } 
        }
        
        #endregion
        
        #region Data Properties - Context Menu
        
        [DataSourceProperty]
        public bool IsContextMenuVisible 
        { 
            get => _isContextMenuVisible; 
            set { if (_isContextMenuVisible != value) { _isContextMenuVisible = value; OnPropertyChangedWithValue(value, nameof(IsContextMenuVisible)); } } 
        }
        
        [DataSourceProperty]
        public float ContextMenuPosX 
        { 
            get => _contextMenuPosX; 
            set { if (Math.Abs(_contextMenuPosX - value) > 0.1f) { _contextMenuPosX = value; OnPropertyChangedWithValue(value, nameof(ContextMenuPosX)); } } 
        }
        
        [DataSourceProperty]
        public float ContextMenuPosY 
        { 
            get => _contextMenuPosY; 
            set { if (Math.Abs(_contextMenuPosY - value) > 0.1f) { _contextMenuPosY = value; OnPropertyChangedWithValue(value, nameof(ContextMenuPosY)); } } 
        }
        
        [DataSourceProperty]
        public bool ContextCanEquip => _contextMenuRing != null && _contextMenuRing.IsOwned && !_contextMenuRing.IsEquipped && _equippedRingsList.Count < MaxEquippedRings;
        
        [DataSourceProperty]
        public bool ContextCanUnequip => _contextMenuRing != null && _contextMenuRing.IsEquipped;
        
        /// <summary>
        /// Show debug boundary overlay (configurable via MCM).
        /// </summary>
        [DataSourceProperty]
        public bool ShowDebugBoundary 
        { 
            get => _showDebugBoundary; 
            set { if (_showDebugBoundary != value) { _showDebugBoundary = value; OnPropertyChangedWithValue(value, nameof(ShowDebugBoundary)); } } 
        }
        private bool _showDebugBoundary = false;  // Default: hidden
        
        #endregion
        
        #region Data Properties - Animation State
        
        /// <summary>
        /// True when any animation is in progress.
        /// </summary>
        [DataSourceProperty]
        public bool IsAnimating => _isRotating || _isFloating;
        
        /// <summary>
        /// True when rotation buttons should be enabled.
        /// </summary>
        [DataSourceProperty]
        public bool CanRotate => !_isRotating && !_isFloating;
        
        /// <summary>
        /// One Ring vertical bobbing offset for floating effect.
        /// Updates continuously to create hovering illusion.
        /// </summary>
        [DataSourceProperty]
        public float OneRingBobOffset
        {
            get => _oneRingBobOffset;
            set 
            { 
                if (Math.Abs(_oneRingBobOffset - value) > 0.1f) 
                { 
                    _oneRingBobOffset = value; 
                    OnPropertyChangedWithValue(value, nameof(OneRingBobOffset));
                    OnPropertyChanged(nameof(OneRingMarginBottom));
                } 
            }
        }
        
        /// <summary>
        /// One Ring margin bottom including bob offset.
        /// Base is 0 (center), bob adds vertical movement.
        /// </summary>
        [DataSourceProperty]
        public float OneRingMarginBottom => Math.Max(0, -_oneRingBobOffset * 2f);
        
        [DataSourceProperty]
        public float OneRingMarginTop => Math.Max(0, _oneRingBobOffset * 2f);
        
        /// <summary>
        /// Float animation Y offset for equip/unequip visual effect.
        /// </summary>
        [DataSourceProperty]
        public float FloatVisualOffsetY
        {
            get => _floatVisualOffsetY;
            set
            {
                if (Math.Abs(_floatVisualOffsetY - value) > 0.1f)
                {
                    _floatVisualOffsetY = value;
                    OnPropertyChangedWithValue(value, nameof(FloatVisualOffsetY));
                }
            }
        }
        
        /// <summary>Is equip/unequip float animation active</summary>
        [DataSourceProperty]
        public bool IsFloatingRing => _isFloating;
        
        /// <summary>Preview ring position during equip animation - starts at ring position, moves to preview</summary>
        [DataSourceProperty]
        public float EquipAnimPosX
        {
            get => _equipAnimPosX;
            set { if (Math.Abs(_equipAnimPosX - value) > 0.1f) { _equipAnimPosX = value; OnPropertyChangedWithValue(value, nameof(EquipAnimPosX)); } }
        }
        private float _equipAnimPosX;
        
        [DataSourceProperty]
        public float EquipAnimPosY
        {
            get => _equipAnimPosY;
            set { if (Math.Abs(_equipAnimPosY - value) > 0.1f) { _equipAnimPosY = value; OnPropertyChangedWithValue(value, nameof(EquipAnimPosY)); } }
        }
        private float _equipAnimPosY;
        
        [DataSourceProperty]
        public float EquipAnimScale
        {
            get => _equipAnimScale;
            set { if (Math.Abs(_equipAnimScale - value) > 0.01f) { _equipAnimScale = value; OnPropertyChangedWithValue(value, nameof(EquipAnimScale)); OnPropertyChanged(nameof(EquipAnimSize)); } }
        }
        private float _equipAnimScale = 1f;
        
        [DataSourceProperty]
        public float EquipAnimSize => 80f * _equipAnimScale;
        
        [DataSourceProperty]
        public float EquipAnimOpacity
        {
            get => _equipAnimOpacity;
            set { if (Math.Abs(_equipAnimOpacity - value) > 0.01f) { _equipAnimOpacity = value; OnPropertyChangedWithValue(value, nameof(EquipAnimOpacity)); } }
        }
        private float _equipAnimOpacity = 0f;
        
        [DataSourceProperty]
        public bool IsEquipAnimating => _isFloating;
        
        #endregion
        
        #region Ring Selection & Actions
        
        private void OnRingSelected(OrbitalRingVM ring)
        {
            // Deselect previous
            if (_selectedRing != null)
                _selectedRing.IsSelected = false;
            
            // Select new
            _selectedRing = ring;
            ring.IsSelected = true;
            
            // Update UI
            HasSelectedRing = true;
            SelectedRingName = ring.RingName;
            SelectedRingType = $"{ring.Category} Ring";
            SelectedRingEffect = ring.EffectText;
            SelectedRingLore = ring.LoreText;
            IsSelectedRingOwned = ring.IsOwned;
            IsSelectedRingEquipped = ring.IsEquipped;
            
            // Update action availability
            CanEquipSelected = ring.IsOwned && !ring.IsEquipped && _equippedRingsList.Count < MaxEquippedRings;
            CanUnequipSelected = ring.IsEquipped;
            
            // Update equip button text based on state
            EquipButtonText = ring.IsEquipped 
                ? new TextObject("{=ring_remove}Remove Ring").ToString() 
                : new TextObject("{=ring_bear}Bear Ring").ToString();
            EquipHint = new HintViewModel(new TextObject(
                ring.IsEquipped 
                    ? "{=ring_remove_hint}Remove this ring from your finger" 
                    : "{=ring_bear_hint}Wear this ring and claim its power"));
            
            // Close context menu if open
            IsContextMenuVisible = false;
            
            // Auto-rotate back rings to front (like the mock)
            if (ring.IsInBack && ring.Category != "One")
            {
                StartAutoRotateToFront(ring);
            }
            
            // Notify for simplified view
            OnPropertyChanged(nameof(IsOneRingSelected));
        }
        
        /// <summary>Called from XML when The One Ring is clicked</summary>
        public void ExecuteSelectOneRing()
        {
            if (_orbitManager?.OneRing != null)
            {
                OnRingSelected(_orbitManager.OneRing);
            }
        }
        
        /// <summary>Called from XML when any ring in ElvenRings is clicked</summary>
        public void ExecuteSelectRing()
        {
            // This will be called on the ring's DataSource (OrbitalRingVM)
            // The VM itself handles its own selection via ExecuteSelect
        }
        
        private void OnRingRightClicked(OrbitalRingVM ring)
        {
            _contextMenuRing = ring;
            
            // Position context menu near the ring
            ContextMenuPosX = ring.PosX;
            ContextMenuPosY = ring.PosY - 50;
            
            // Notify property changes for context menu options
            OnPropertyChanged(nameof(ContextCanEquip));
            OnPropertyChanged(nameof(ContextCanUnequip));
            
            IsContextMenuVisible = true;
        }
        
        public void ExecuteEquipSelected()
        {
            if (_selectedRing == null) return;
            
            // Toggle equip/unequip based on current state
            if (_selectedRing.IsEquipped)
            {
                UnequipRing(_selectedRing);
            }
            else if (CanEquipSelected)
            {
                EquipRing(_selectedRing);
            }
        }
        
        public void ExecuteUnequipSelected()
        {
            if (_selectedRing != null && CanUnequipSelected)
            {
                UnequipRing(_selectedRing);
            }
        }
        
        public void ExecuteContextEquip()
        {
            if (_contextMenuRing != null && ContextCanEquip)
            {
                EquipRing(_contextMenuRing);
            }
            IsContextMenuVisible = false;
        }
        
        public void ExecuteContextUnequip()
        {
            if (_contextMenuRing != null && ContextCanUnequip)
            {
                UnequipRing(_contextMenuRing);
            }
            IsContextMenuVisible = false;
        }
        
        public void ExecuteCloseContextMenu()
        {
            IsContextMenuVisible = false;
        }
        
        /// <summary>
        /// Shows context menu at specified position.
        /// </summary>
        public void ShowContextMenuAt(float x, float y)
        {
            ContextMenuPosX = x;
            ContextMenuPosY = y;
        }
        
        /// <summary>
        /// Hides the context menu.
        /// </summary>
        public void HideContextMenu()
        {
            IsContextMenuVisible = false;
        }
        
        private void EquipRing(OrbitalRingVM ring)
        {
            // Start float animation
            StartFloatAnimation(ring, true);
            
            // Unequip current if at max
            if (_equippedRingsList.Count >= MaxEquippedRings)
            {
                var current = _equippedRingsList.FirstOrDefault();
                if (current != null)
                    UnequipRing(current);
            }
            
            ring.IsEquipped = true;
            _equippedRingsList.Add(ring);
            
            // Update equipped preview
            _equippedRing = ring;
            HasEquippedRing = true;
            EquippedRingName = ring.RingName;
            EquippedRingEffect = ring.EffectText;
            PreviewRingSprite = ring.SpriteName;
            
            // Update selected ring state
            if (_selectedRing == ring)
            {
                IsSelectedRingEquipped = true;
                CanEquipSelected = false;
                CanUnequipSelected = true;
            }
            
            RefreshStatusPanel();
            
            // Apply actual ring effects via RingSystemCampaignBehavior
            var behavior = RingSystemCampaignBehavior.Instance;
            if (behavior != null)
            {
                string ringId = ring.RingId;
                if (!string.IsNullOrEmpty(ringId))
                {
                    behavior.EquipRing(ringId);
                    
                    // Apply ring effects to skills
                    var race = RingAttributes.GetRingRace(ringId);
                    var effectTracker = behavior.PlayerEffects;
                    if (effectTracker != null && Hero.MainHero != null)
                    {
                        // Update skill bonuses based on ring power
                        RingSkillPatches.VirtualFocusTracker.UpdateFromRingPower(
                            Hero.MainHero, effectTracker.GetRingPower(Hero.MainHero), race);
                    }
                }
            }
        }
        
        private void UnequipRing(OrbitalRingVM ring)
        {
            // Start float animation (down direction for unequip)
            StartFloatAnimation(ring, false);
            
            ring.IsEquipped = false;
            _equippedRingsList.Remove(ring);
            
            // Update equipped preview
            if (_equippedRing == ring)
            {
                _equippedRing = _equippedRingsList.FirstOrDefault();
                HasEquippedRing = _equippedRing != null;
                EquippedRingName = _equippedRing?.RingName ?? "";
                EquippedRingEffect = _equippedRing?.EffectText ?? "";
                PreviewRingSprite = _equippedRing?.SpriteName ?? "";
            }
            
            // Update selected ring state
            if (_selectedRing == ring)
            {
                IsSelectedRingEquipped = false;
                CanEquipSelected = ring.IsOwned;
                CanUnequipSelected = false;
            }
            
            RefreshStatusPanel();
            
            // Remove actual ring effects via RingSystemCampaignBehavior
            var behavior = RingSystemCampaignBehavior.Instance;
            if (behavior != null)
            {
                string ringId = ring.RingId;
                if (!string.IsNullOrEmpty(ringId))
                {
                    behavior.UnequipRing(ringId);
                    
                    // Decay ring skill effects
                    if (Hero.MainHero != null)
                    {
                        RingSkillPatches.VirtualFocusTracker.DecayFocus(Hero.MainHero, 1f);
                    }
                }
            }
        }
        
        /// <summary>
        /// Start the float animation for equip/unequip.
        /// </summary>
        private void StartFloatAnimation(OrbitalRingVM ring, bool isEquip)
        {
            _floatingRing = ring;
            _floatDirectionUp = isEquip;
            _floatProgress = 0f;
            _isFloating = true;
            FloatVisualOffsetY = 0f;
            
            // Capture ring position for animation
            if (ring != null)
            {
                _floatStartX = ring.PosX;
                _floatStartY = ring.PosY;
                _floatEndX = ring.PosX;
                _floatEndY = ring.PosY;
                
                // Initialize animation visuals
                EquipAnimPosX = isEquip ? _floatStartX : 0f;
                EquipAnimPosY = isEquip ? _floatStartY : -280f;
                EquipAnimScale = isEquip ? 0.6f : 1.2f;
                EquipAnimOpacity = 1f;
            }
            
            OnPropertyChanged(nameof(IsAnimating));
            OnPropertyChanged(nameof(CanRotate));
            OnPropertyChanged(nameof(IsFloatingRing));
            OnPropertyChanged(nameof(IsEquipAnimating));
        }
        
        #endregion
        
        #region Orbit Rotation Commands
        
        public void ExecuteRotateLeft()
        {
            if (_isRotating || _isFloating) return;
            StartRotationAnimation(clockwise: false);
        }
        
        public void ExecuteRotateRight()
        {
            if (_isRotating || _isFloating) return;
            StartRotationAnimation(clockwise: true);
        }
        
        public void ExecuteRotateElvenLeft() => StartSingleOrbitRotation("elven", false);
        public void ExecuteRotateElvenRight() => StartSingleOrbitRotation("elven", true);
        public void ExecuteRotateDwarvenLeft() => StartSingleOrbitRotation("dwarven", false);
        public void ExecuteRotateDwarvenRight() => StartSingleOrbitRotation("dwarven", true);
        public void ExecuteRotateMortalLeft() => StartSingleOrbitRotation("mortal", false);
        public void ExecuteRotateMortalRight() => StartSingleOrbitRotation("mortal", true);
        
        private void StartSingleOrbitRotation(string orbit, bool clockwise)
        {
            if (_isRotating || _isFloating) return;
            
            // For single orbit rotation, only animate that orbit
            float step = (2f * (float)Math.PI) / GetOrbitRingCount(orbit);
            float direction = clockwise ? 1f : -1f;
            
            _rotationStartAngles = new Dictionary<string, float>
            {
                { "elven", _orbitManager.ElvenOrbit.RotationOffset },
                { "dwarven", _orbitManager.DwarvenOrbit.RotationOffset },
                { "mortal", _orbitManager.MortalOrbit.RotationOffset }
            };
            
            _rotationEndAngles = new Dictionary<string, float>(_rotationStartAngles);
            _rotationEndAngles[orbit] = _rotationStartAngles[orbit] + (direction * step);
            
            _rotationProgress = 0f;
            _rotationTargetDuration = RotationDuration;
            _isRotating = true;
            
            // Notify animation state change
            OnPropertyChanged(nameof(IsAnimating));
            OnPropertyChanged(nameof(CanRotate));
        }
        
        private void StartRotationAnimation(bool clockwise)
        {
            float direction = clockwise ? 1f : -1f;
            
            _rotationStartAngles = new Dictionary<string, float>
            {
                { "elven", _orbitManager.ElvenOrbit.RotationOffset },
                { "dwarven", _orbitManager.DwarvenOrbit.RotationOffset },
                { "mortal", _orbitManager.MortalOrbit.RotationOffset }
            };
            
            _rotationEndAngles = new Dictionary<string, float>
            {
                { "elven", _rotationStartAngles["elven"] + direction * ((2f * (float)Math.PI) / 3) },
                { "dwarven", _rotationStartAngles["dwarven"] + direction * ((2f * (float)Math.PI) / 7) },
                { "mortal", _rotationStartAngles["mortal"] + direction * ((2f * (float)Math.PI) / 9) }
            };
            
            _rotationProgress = 0f;
            _rotationTargetDuration = RotationDuration;
            _isRotating = true;
            
            // Notify animation state change
            OnPropertyChanged(nameof(IsAnimating));
            OnPropertyChanged(nameof(CanRotate));
        }
        
        private void StartAutoRotateToFront(OrbitalRingVM ring)
        {
            if (_isRotating || _isFloating || ring == null || string.IsNullOrEmpty(ring.OrbitName)) return;
            
            var orbit = GetOrbit(ring.OrbitName);
            if (orbit == null) return;
            
            float targetAngle = orbit.CalculateRotationToFront(ring.OrbitIndex);
            
            _rotationStartAngles = new Dictionary<string, float>
            {
                { "elven", _orbitManager.ElvenOrbit.RotationOffset },
                { "dwarven", _orbitManager.DwarvenOrbit.RotationOffset },
                { "mortal", _orbitManager.MortalOrbit.RotationOffset }
            };
            
            _rotationEndAngles = new Dictionary<string, float>(_rotationStartAngles);
            _rotationEndAngles[ring.OrbitName.ToLower()] = targetAngle;
            
            _rotationProgress = 0f;
            _rotationTargetDuration = AutoRotateDuration;
            _isRotating = true;
            
            // Notify animation state change
            OnPropertyChanged(nameof(IsAnimating));
            OnPropertyChanged(nameof(CanRotate));
        }
        
        private int GetOrbitRingCount(string orbit)
        {
            return orbit.ToLower() switch
            {
                "elven" => 3,
                "dwarven" => 7,
                "mortal" => 9,
                _ => 1
            };
        }
        
        private RingOrbit GetOrbit(string orbitName)
        {
            return orbitName?.ToLower() switch
            {
                "elven" => _orbitManager.ElvenOrbit,
                "dwarven" => _orbitManager.DwarvenOrbit,
                "mortal" => _orbitManager.MortalOrbit,
                _ => null
            };
        }
        
        #endregion
        
        #region Animation Update
        
        /// <summary>
        /// Called every frame to update animations.
        /// </summary>
        public void UpdateAnimations(float dt)
        {
            // One Ring continuous bobbing animation (always runs)
            UpdateOneRingBobbing(dt);
            
            // Preview ring animation (always runs when equipped)
            UpdatePreviewAnimation(dt);
            
            if (_isRotating)
            {
                UpdateRotationAnimation(dt);
            }
            
            if (_isFloating)
            {
                UpdateFloatAnimation(dt);
            }
        }
        
        private void UpdatePreviewAnimation(float dt)
        {
            if (!HasEquippedRing || _equippedRing == null) return;
            
            // Update phase
            _previewAnimPhase += dt * 1.2f;  // Slow, mystical speed
            if (_previewAnimPhase > 2f * (float)Math.PI) _previewAnimPhase -= 2f * (float)Math.PI;
            
            // Gentle breathing/pulse effect (0.95 to 1.05)
            PreviewPulseScale = 1.0f + 0.05f * (float)Math.Sin(_previewAnimPhase);
            
            // Pulsing glow (0.2 to 0.5)
            PreviewGlowOpacity = 0.35f + 0.15f * (float)Math.Sin(_previewAnimPhase * 1.3f);
            
            // Gentle bob (levitation effect)
            PreviewBobOffset = 3f * (float)Math.Sin(_previewAnimPhase * 0.8f);
            
            // Update sprite binding
            PreviewRingSprite = _equippedRing.SpriteName;
        }
        
        private void UpdateOneRingBobbing(float dt)
        {
            // Update all rings' bobbing animation, scaled by ring size
            // Reference: One Ring (size 80) has max bob of 8 pixels
            // Rings on same ellipse bob in sync
            
            const float referenceSize = 80f;
            const float referenceBob = 8f;
            const float bobSpeed = 1.5f;
            
            // Shared phases per orbit for synchronized movement
            _elvenOrbitPhase += dt * bobSpeed * 0.9f;
            _dwarvenOrbitPhase += dt * bobSpeed * 0.8f;
            _mortalOrbitPhase += dt * bobSpeed * 0.7f;
            
            // Wrap phases
            if (_elvenOrbitPhase > 2f * (float)Math.PI) _elvenOrbitPhase -= 2f * (float)Math.PI;
            if (_dwarvenOrbitPhase > 2f * (float)Math.PI) _dwarvenOrbitPhase -= 2f * (float)Math.PI;
            if (_mortalOrbitPhase > 2f * (float)Math.PI) _mortalOrbitPhase -= 2f * (float)Math.PI;
            
            // Update One Ring
            if (_orbitManager?.OneRing != null)
            {
                var ring = _orbitManager.OneRing;
                ring.BobPhase += dt * bobSpeed;
                if (ring.BobPhase > 2f * (float)Math.PI) ring.BobPhase -= 2f * (float)Math.PI;
                
                float bobAmount = referenceBob; // Full amount for One Ring
                ring.BobOffset = (float)Math.Sin(ring.BobPhase) * bobAmount;
            }
            
            // Update Elven rings - all in sync
            if (_orbitManager?.ElvenRings != null)
            {
                float sizeRatio = 55f / referenceSize;
                float bobAmount = referenceBob * sizeRatio * 0.8f;
                float bobValue = (float)Math.Sin(_elvenOrbitPhase) * bobAmount;
                
                foreach (var ring in _orbitManager.ElvenRings)
                {
                    ring.BobOffset = bobValue;
                }
            }
            
            // Update Dwarven rings - all in sync
            if (_orbitManager?.DwarvenRings != null)
            {
                float sizeRatio = 45f / referenceSize;
                float bobAmount = referenceBob * sizeRatio * 0.7f;
                float bobValue = (float)Math.Sin(_dwarvenOrbitPhase) * bobAmount;
                
                foreach (var ring in _orbitManager.DwarvenRings)
                {
                    ring.BobOffset = bobValue;
                }
            }
            
            // Update Mortal rings - all in sync
            if (_orbitManager?.MortalRings != null)
            {
                float sizeRatio = 40f / referenceSize;
                float bobAmount = referenceBob * sizeRatio * 0.6f;
                float bobValue = (float)Math.Sin(_mortalOrbitPhase) * bobAmount;
                
                foreach (var ring in _orbitManager.MortalRings)
                {
                    ring.BobOffset = bobValue;
                }
            }
            
            // Legacy: Update the screen-level property for any direct bindings
            _oneRingBobPhase += dt * bobSpeed;
            if (_oneRingBobPhase > 2f * (float)Math.PI)
                _oneRingBobPhase -= 2f * (float)Math.PI;
            OneRingBobOffset = (float)Math.Sin(_oneRingBobPhase) * referenceBob;
        }
        
        private void UpdateRotationAnimation(float dt)
        {
            _rotationProgress += dt / _rotationTargetDuration;
            
            if (_rotationProgress >= 1f)
            {
                // Animation complete - snap to final positions
                _rotationProgress = 1f;
                _isRotating = false;
                
                _orbitManager.ElvenOrbit.SetRotation(_rotationEndAngles["elven"]);
                _orbitManager.DwarvenOrbit.SetRotation(_rotationEndAngles["dwarven"]);
                _orbitManager.MortalOrbit.SetRotation(_rotationEndAngles["mortal"]);
                
                // Notify animation state change
                OnPropertyChanged(nameof(IsAnimating));
                OnPropertyChanged(nameof(CanRotate));
            }
            else
            {
                // Interpolate with easing
                float t = EaseInOutCubic(_rotationProgress);
                
                float elvenAngle = Lerp(_rotationStartAngles["elven"], _rotationEndAngles["elven"], t);
                float dwarvenAngle = Lerp(_rotationStartAngles["dwarven"], _rotationEndAngles["dwarven"], t);
                float mortalAngle = Lerp(_rotationStartAngles["mortal"], _rotationEndAngles["mortal"], t);
                
                _orbitManager.ElvenOrbit.SetRotation(elvenAngle);
                _orbitManager.DwarvenOrbit.SetRotation(dwarvenAngle);
                _orbitManager.MortalOrbit.SetRotation(mortalAngle);
            }
            
            // Update ring positions and rebuild display
            _orbitManager.UpdateAllPositions();
            RebuildRingDisplayList();
        }
        
        private void UpdateFloatAnimation(float dt)
        {
            _floatProgress += dt / FloatAnimDuration;
            
            if (_floatProgress >= 1f)
            {
                // Animation complete
                _floatProgress = 1f;
                _isFloating = false;
                FloatVisualOffsetY = 0f;
                EquipAnimOpacity = 0f;
                
                // Notify animation state change
                OnPropertyChanged(nameof(IsAnimating));
                OnPropertyChanged(nameof(CanRotate));
                OnPropertyChanged(nameof(IsFloatingRing));
                OnPropertyChanged(nameof(IsEquipAnimating));
                
                _floatingRing = null;
                
                // Refresh display after equip/unequip
                RebuildRingDisplayList();
                RefreshStatusPanel();
            }
            else
            {
                // Calculate eased progress
                float t = EaseInOutCubic(_floatProgress);
                
                // Animate position from ring to preview (or vice versa)
                // Preview position: center-top of screen (roughly 0, -300 from table center)
                // Ring position: _floatStartX, _floatStartY (its orbital position)
                
                float previewX = 0f;      // Center horizontally
                float previewY = -280f;   // Above table (preview area)
                
                if (_floatDirectionUp) // Equip: ring -> preview
                {
                    EquipAnimPosX = Lerp(_floatStartX, previewX, t);
                    EquipAnimPosY = Lerp(_floatStartY, previewY, t);
                    EquipAnimScale = Lerp(0.6f, 1.2f, t);  // Grow as it rises
                    EquipAnimOpacity = 1f;  // Fully visible during animation
                    
                    // Arc motion - rise up with curve
                    float arcBoost = (float)Math.Sin(t * Math.PI) * 40f;
                    EquipAnimPosY -= arcBoost;
                }
                else // Unequip: preview -> ring
                {
                    EquipAnimPosX = Lerp(previewX, _floatEndX, t);
                    EquipAnimPosY = Lerp(previewY, _floatEndY, t);
                    EquipAnimScale = Lerp(1.2f, 0.6f, t);  // Shrink as it falls
                    EquipAnimOpacity = 1f - (t * 0.3f);  // Fade slightly
                    
                    // Arc motion - fall with curve
                    float arcBoost = (float)Math.Sin(t * Math.PI) * 30f;
                    EquipAnimPosY -= arcBoost;
                }
                
                // Also update the legacy offset for ring bob effect
                FloatVisualOffsetY = -60f * (float)Math.Sin(t * Math.PI);
            }
        }
        
        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }
        
        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
        
        #endregion
        
        #region Screen Commands
        
        public void ExecuteCancel()
        {
            _closeAction?.Invoke();
        }
        
        public void ExecuteDone()
        {
            // Save changes
            SaveEquippedRings();
            _closeAction?.Invoke();
        }
        
        public void ExecuteGiveAllRings()
        {
            // Debug: Give all rings for testing
            foreach (var ring in _orbitManager.ElvenRings) ring.IsOwned = true;
            foreach (var ring in _orbitManager.DwarvenRings) ring.IsOwned = true;
            foreach (var ring in _orbitManager.MortalRings) ring.IsOwned = true;
            if (_orbitManager.OneRing != null) _orbitManager.OneRing.IsOwned = true;
            
            RefreshRingCount();
        }
        
        #endregion
        
        #region Refresh Methods
        
        private void UpdateOrbitalPositions()
        {
            // Set center based on expected screen layout
            // These will be relative to the orbital container in XML
            _orbitManager.CenterX = 0;
            _orbitManager.CenterY = 0;
            
            _orbitManager.UpdateAllPositions();
        }
        
        private void RefreshFromInventory()
        {
            // Check actual inventory for owned rings from RingSystemCampaignBehavior
            var behavior = RingSystemCampaignBehavior.Instance;
            if (behavior != null)
            {
                var ownedIds = behavior.OwnedRingIds;
                var equippedIds = behavior.EquippedRingIds;
                
                // Update all rings with ownership status
                foreach (var ring in _orbitManager.ElvenRings)
                {
                    ring.IsOwned = ownedIds?.Contains(ring.RingId) ?? false;
                    ring.IsEquipped = equippedIds?.Contains(ring.RingId) ?? false;
                }
                foreach (var ring in _orbitManager.DwarvenRings)
                {
                    ring.IsOwned = ownedIds?.Contains(ring.RingId) ?? false;
                    ring.IsEquipped = equippedIds?.Contains(ring.RingId) ?? false;
                }
                foreach (var ring in _orbitManager.MortalRings)
                {
                    ring.IsOwned = ownedIds?.Contains(ring.RingId) ?? false;
                    ring.IsEquipped = equippedIds?.Contains(ring.RingId) ?? false;
                }
                if (_orbitManager.OneRing != null)
                {
                    _orbitManager.OneRing.IsOwned = ownedIds?.Contains(_orbitManager.OneRing.RingId) ?? false;
                    _orbitManager.OneRing.IsEquipped = equippedIds?.Contains(_orbitManager.OneRing.RingId) ?? false;
                }
                
                // Rebuild equipped list
                _equippedRingsList.Clear();
                var allRings = _orbitManager.GetAllRingsSortedByZOrder();
                foreach (var ring in allRings)
                {
                    if (ring.IsEquipped)
                    {
                        _equippedRingsList.Add(ring);
                    }
                }
                
                // Update equipped preview
                _equippedRing = _equippedRingsList.FirstOrDefault();
                HasEquippedRing = _equippedRing != null;
                EquippedRingName = _equippedRing?.RingName ?? "";
                EquippedRingEffect = _equippedRing?.EffectText ?? "";
                PreviewRingSprite = _equippedRing?.SpriteName ?? "";
            }
            
            RefreshRingCount();
        }
        
        private void RefreshRingCount()
        {
            int owned = 0;
            owned += _orbitManager.ElvenRings.Count(r => r.IsOwned);
            owned += _orbitManager.DwarvenRings.Count(r => r.IsOwned);
            owned += _orbitManager.MortalRings.Count(r => r.IsOwned);
            if (_orbitManager.OneRing?.IsOwned == true) owned++;
            
            var text = new TextObject("{=ring_count}{OWNED} of 20 Rings Found");
            text.SetTextVariable("OWNED", owned);
            RingCountText = text.ToString();
        }
        
        private void RefreshStatusPanel()
        {
            // Calculate corruption based on equipped rings
            float corruption = 0f;
            foreach (var ring in _equippedRingsList)
            {
                corruption += ring.Category switch
                {
                    "One" => 50f,
                    "Mortal" => 15f,
                    "Dwarven" => 5f,
                    "Elven" => 0f,
                    _ => 0f
                };
            }
            
            CorruptionPercent = Math.Min(100f, corruption);
            CorruptionBarWidth = BaseCorruptionBarWidth * (CorruptionPercent / 100f);
            var corruptText = new TextObject("{=ring_corruption}{PERCENT}% Corruption");
            corruptText.SetTextVariable("PERCENT", ((int)CorruptionPercent).ToString());
            CorruptionText = corruptText.ToString();
            
            // Threat level based on rings
            int threatLevel = _equippedRingsList.Count;
            if (_equippedRingsList.Any(r => r.Category == "One")) threatLevel += 4;
            
            ThreatLevelDisplay = new string('◆', Math.Min(5, threatLevel)) + new string('◇', Math.Max(0, 5 - threatLevel));
            ThreatDescription = threatLevel switch
            {
                0 => new TextObject("{=threat_0}Hidden from the Enemy").ToString(),
                1 => new TextObject("{=threat_1}Faint whispers").ToString(),
                2 => new TextObject("{=threat_2}The Eye stirs").ToString(),
                3 => new TextObject("{=threat_3}Servants seek you").ToString(),
                4 => new TextObject("{=threat_4}Nazgûl are hunting").ToString(),
                _ => new TextObject("{=threat_5}SAURON KNOWS").ToString()
            };
            
            // Active effects
            if (_equippedRingsList.Count == 0)
            {
                ActiveEffectsText = new TextObject("{=ring_no_worn}No rings worn").ToString();
            }
            else
            {
                var effects = _equippedRingsList.Select(r => r.ShortName);
                ActiveEffectsText = string.Join(", ", effects);
            }
        }
        
        private void SaveEquippedRings()
        {
            // Sync equipped rings to RingSystemCampaignBehavior
            var behavior = RingSystemCampaignBehavior.Instance;
            if (behavior == null) return;
            
            // The behavior already syncs on equip/unequip calls
            // This method ensures any UI-only changes are persisted
            
            // Log current state for debugging
            ModSettings.DebugLog("RingScreenVM", 
                $"Equipped rings: {_equippedRingsList.Count}, " +
                $"Behavior has: {behavior.EquippedRingIds?.Count ?? 0}");
        }
        
        #endregion
        
        public override void OnFinalize()
        {
            base.OnFinalize();
            // Cleanup
        }
    }
}
