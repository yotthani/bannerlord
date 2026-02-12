using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using System.Collections.Generic;

namespace HeirOfNumenor
{
    /// <summary>
    /// Item quality levels based on Bannerlord's modifier system
    /// </summary>
    public enum ItemQualityLevel
    {
        Destroyed = -50,    // -50% value
        Damaged = -30,      // -30% value  
        Rusty = -20,        // Rusty/Cracked/Tattered -20% value
        Worn = -10,         // Worn/Battered -10% value
        Normal = 0          // No modifier
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "HeirOfNumenor_InventoryActions";
        public override string DisplayName => "Heir of Numenor - Inventory Actions";
        public override string FolderName => "HeirOfNumenor";
        public override string FormatType => "json2";

        // Damaged Items Settings
        [SettingPropertyGroup("Sell Damaged Items", GroupOrder = 0)]
        [SettingPropertyDropdown(
            "Minimum Quality Level",
            Order = 0,
            RequireRestart = false,
            HintText = "Items at or below this quality will be sold. Rusty/Cracked/Tattered = -20%, Worn/Battered = -10%")]
        public Dropdown<string> DamagedQualityDropdown { get; set; } = new Dropdown<string>(
            new string[]
            {
                "Destroyed (-50%)",
                "Damaged (-30%)",
                "Rusty/Cracked (-20%)",
                "Worn/Battered (-10%)"
            }, 
            2); // Default: Rusty/Cracked (-20%)

        [SettingPropertyGroup("Sell Damaged Items", GroupOrder = 0)]
        [SettingPropertyFloatingInteger(
            "Custom Modifier Threshold", 
            -1f, 
            0f, 
            "#0%",
            Order = 1,
            RequireRestart = false,
            HintText = "Fine-tune: Items with a modifier below this value will be sold. Only used if you want a custom threshold.")]
        public float DamagedThreshold { get; set; } = -0.20f;

        [SettingPropertyGroup("Sell Damaged Items", GroupOrder = 0)]
        [SettingPropertyBool(
            "Use Custom Threshold",
            Order = 2,
            RequireRestart = false,
            HintText = "If enabled, uses the custom slider value instead of the dropdown.")]
        public bool UseCustomThreshold { get; set; } = false;

        [SettingPropertyGroup("Sell Damaged Items", GroupOrder = 0)]
        [SettingPropertyBool(
            "Include Equipped Items",
            Order = 3,
            RequireRestart = false,
            HintText = "If enabled, will also sell damaged items that are currently equipped.")]
        public bool SellDamagedEquipped { get; set; } = false;

        [SettingPropertyGroup("Sell Damaged Items", GroupOrder = 0)]
        [SettingPropertyBool(
            "Exclude Horses",
            Order = 4,
            RequireRestart = false,
            HintText = "If enabled, will not sell damaged horses.")]
        public bool ExcludeDamagedHorses { get; set; } = true;

        // Low Value Settings
        [SettingPropertyGroup("Sell Low Value Items", GroupOrder = 1)]
        [SettingPropertyInteger(
            "Maximum Value Threshold",
            1,
            10000,
            "0 denars",
            Order = 0,
            RequireRestart = false,
            HintText = "Items with a value at or below this amount will be sold. Default: 100 denars")]
        public int LowValueThreshold { get; set; } = 100;

        [SettingPropertyGroup("Sell Low Value Items", GroupOrder = 1)]
        [SettingPropertyBool(
            "Include Equipped Items",
            Order = 1,
            RequireRestart = false,
            HintText = "If enabled, will also sell low value items that are currently equipped.")]
        public bool SellLowValueEquipped { get; set; } = false;

        [SettingPropertyGroup("Sell Low Value Items", GroupOrder = 1)]
        [SettingPropertyBool(
            "Exclude Food",
            Order = 2,
            RequireRestart = false,
            HintText = "If enabled, will not sell food items regardless of value.")]
        public bool ExcludeLowValueFood { get; set; } = true;

        [SettingPropertyGroup("Sell Low Value Items", GroupOrder = 1)]
        [SettingPropertyBool(
            "Exclude Horses",
            Order = 3,
            RequireRestart = false,
            HintText = "If enabled, will not sell horses regardless of value.")]
        public bool ExcludeLowValueHorses { get; set; } = true;

        [SettingPropertyGroup("Sell Low Value Items", GroupOrder = 1)]
        [SettingPropertyBool(
            "Exclude Trade Goods",
            Order = 4,
            RequireRestart = false,
            HintText = "If enabled, will not sell trade goods regardless of value.")]
        public bool ExcludeLowValueTradeGoods { get; set; } = false;

        // General Settings
        [SettingPropertyGroup("General", GroupOrder = 2)]
        [SettingPropertyBool(
            "Show Confirmation Dialog",
            Order = 0,
            RequireRestart = false,
            HintText = "If enabled, will show a confirmation dialog before selling items.")]
        public bool ShowConfirmation { get; set; } = true;

        [SettingPropertyGroup("General", GroupOrder = 2)]
        [SettingPropertyBool(
            "Play Sound Effects",
            Order = 1,
            RequireRestart = false,
            HintText = "If enabled, will play sound effects when actions are performed.")]
        public bool PlaySounds { get; set; } = true;

        [SettingPropertyGroup("General", GroupOrder = 2)]
        [SettingPropertyBool(
            "Debug Mode",
            Order = 2,
            RequireRestart = false,
            HintText = "If enabled, shows debug messages in the game log.")]
        public bool DebugMode { get; set; } = false;

        [SettingPropertyGroup("General", GroupOrder = 2)]
        [SettingPropertyBool(
            "Ring Screen Debug Boundary",
            Order = 3,
            RequireRestart = false,
            HintText = "If enabled, shows the red debug boundary overlay on the Ring Screen.")]
        public bool RingScreenDebugBoundary { get; set; } = false;

        /// <summary>
        /// Gets the effective damage threshold based on dropdown or custom value
        /// </summary>
        public float GetEffectiveDamageThreshold()
        {
            if (UseCustomThreshold)
            {
                return DamagedThreshold;
            }

            // Map dropdown selection to threshold value
            return DamagedQualityDropdown.SelectedIndex switch
            {
                0 => -0.50f, // Destroyed
                1 => -0.30f, // Damaged
                2 => -0.20f, // Rusty/Cracked
                3 => -0.10f, // Worn/Battered
                _ => -0.20f  // Default
            };
        }

        public void Initialize()
        {
            // Initialization logic if needed
        }

        // ═══════════════════════════════════════════════════════════════════
        // SIEGE DISMOUNT SETTINGS
        // ═══════════════════════════════════════════════════════════════════
        
        [SettingPropertyGroup("Siege Mount Behavior", GroupOrder = 10)]
        [SettingPropertyDropdown(
            "Siege Mount Behavior",
            Order = 0,
            RequireRestart = false,
            HintText = "How to handle your mount when entering a siege battle.")]
        public Dropdown<string> SiegeMountBehaviorDropdown { get; set; } = new Dropdown<string>(
            new string[]
            {
                "Vanilla (no change)",
                "Auto Dismount - Horse Stays on Map",
                "Auto Dismount - Horse to Inventory",
                "Auto Remount After Siege"
            }, 
            2); // Default: Horse to Inventory
        
        public SiegeMountBehavior GetSiegeMountBehavior() => SiegeMountBehaviorDropdown.SelectedIndex switch
        {
            0 => SiegeMountBehavior.Vanilla,
            1 => SiegeMountBehavior.DismountKeepOnMap,
            2 => SiegeMountBehavior.DismountToInventory,
            3 => SiegeMountBehavior.AutoRemountAfter,
            _ => SiegeMountBehavior.DismountToInventory
        };

        // ═══════════════════════════════════════════════════════════════════
        // LAYERED ARMOR SETTINGS
        // ═══════════════════════════════════════════════════════════════════
        
        [SettingPropertyGroup("Layered Armor System", GroupOrder = 11)]
        [SettingPropertyBool(
            "Enable Layered Armor",
            Order = 0,
            RequireRestart = true,
            HintText = "Allows wearing multiple armor layers (gambeson + chainmail + plate) for combined protection.")]
        public bool EnableLayeredArmor { get; set; } = false;
        
        [SettingPropertyGroup("Layered Armor System", GroupOrder = 11)]
        [SettingPropertyDropdown(
            "Layer Calculation Mode",
            Order = 1,
            RequireRestart = false,
            HintText = "How to calculate combined armor from layers.")]
        public Dropdown<string> ArmorLayerCalculationDropdown { get; set; } = new Dropdown<string>(
            new string[]
            {
                "Additive (sum all layers)",
                "Highest + Bonus (main armor + % from under-layers)",
                "Weighted (outer=100%, mid=50%, inner=30%)"
            }, 
            1); // Default: Highest + Bonus
        
        [SettingPropertyGroup("Layered Armor System", GroupOrder = 11)]
        [SettingPropertyFloatingInteger(
            "Under-Armor Bonus Percent",
            0.05f, 0.50f, "#0%",
            Order = 2,
            RequireRestart = false,
            HintText = "Bonus armor percent from each under-layer (for 'Highest + Bonus' mode).")]
        public float UnderArmorBonusPercent { get; set; } = 0.15f;

        // ═══════════════════════════════════════════════════════════════════
        // MIXED FORMATION SETTINGS
        // ═══════════════════════════════════════════════════════════════════
        
        [SettingPropertyGroup("Mixed Formation Layouts", GroupOrder = 12)]
        [SettingPropertyBool(
            "Enable Formation Layouts",
            Order = 0,
            RequireRestart = false,
            HintText = "Allows custom internal layouts for mixed infantry/ranged formations.")]
        public bool EnableMixedFormationLayouts { get; set; } = true;
        
        [SettingPropertyGroup("Mixed Formation Layouts", GroupOrder = 12)]
        [SettingPropertyDropdown(
            "Default Mixed Layout",
            Order = 1,
            RequireRestart = false,
            HintText = "Default arrangement for mixed formations.")]
        public Dropdown<string> DefaultMixedLayoutDropdown { get; set; } = new Dropdown<string>(
            new string[]
            {
                "Vanilla (default behavior)",
                "Infantry Front, Ranged Back",
                "Ranged Front, Infantry Back",
                "Ranged on Wings, Infantry Center",
                "Checkerboard (alternating)"
            }, 
            1); // Default: Infantry Front
        
        [SettingPropertyGroup("Mixed Formation Layouts", GroupOrder = 12)]
        [SettingPropertyInteger(
            "Ranged Row Depth",
            1, 5,
            Order = 2,
            RequireRestart = false,
            HintText = "Number of rows for ranged units in the formation.")]
        public int RangedRowDepth { get; set; } = 2;
        
        [SettingPropertyGroup("Mixed Formation Layouts", GroupOrder = 12)]
        [SettingPropertyInteger(
            "Infantry Row Depth",
            1, 8,
            Order = 3,
            RequireRestart = false,
            HintText = "Number of rows for infantry units in the formation.")]
        public int InfantryRowDepth { get; set; } = 3;

        // ═══════════════════════════════════════════════════════════════════
        // BATTLE ACTION BAR SETTINGS
        // ═══════════════════════════════════════════════════════════════════
        
        [SettingPropertyGroup("Battle Action Bar", GroupOrder = 13)]
        [SettingPropertyBool(
            "Enable Battle Action Bar",
            Order = 0,
            RequireRestart = false,
            HintText = "Shows a quick action bar during battle for special unit orders (pike stance, volley fire, etc.).")]
        public bool EnableBattleActionBar { get; set; } = true;
        
        [SettingPropertyGroup("Battle Action Bar", GroupOrder = 13)]
        [SettingPropertyDropdown(
            "Action Bar Position",
            Order = 1,
            RequireRestart = false,
            HintText = "Where to display the action bar on screen.")]
        public Dropdown<string> ActionBarPositionDropdown { get; set; } = new Dropdown<string>(
            new string[]
            {
                "Bottom Center",
                "Top Center",
                "Left Side",
                "Right Side"
            }, 
            0); // Default: Bottom Center
        
        [SettingPropertyGroup("Battle Action Bar", GroupOrder = 13)]
        [SettingPropertyBool(
            "Show Hotkey Labels",
            Order = 2,
            RequireRestart = false,
            HintText = "Display keyboard shortcuts on action buttons.")]
        public bool ShowActionBarHotkeys { get; set; } = true;
        
        [SettingPropertyGroup("Battle Action Bar", GroupOrder = 13)]
        [SettingPropertyBool(
            "Auto-Cancel Stance on Move",
            Order = 3,
            RequireRestart = false,
            HintText = "If enabled, special stances (pike wall, etc.) cancel when formation is ordered to move.")]
        public bool AutoCancelStanceOnMove { get; set; } = false;

        // ═══════════════════════════════════════════════════════════════════
        // SMART CAVALRY AI SETTINGS
        // ═══════════════════════════════════════════════════════════════════
        
        [SettingPropertyGroup("Smart Cavalry AI", GroupOrder = 14)]
        [SettingPropertyBool(
            "Enable Smart Cavalry AI",
            Order = 0,
            RequireRestart = false,
            HintText = "Improves cavalry charge behavior: formation cohesion, collision avoidance, and coordinated charges.")]
        public bool EnableSmartCavalryAI { get; set; } = true;
        
        [SettingPropertyGroup("Smart Cavalry AI", GroupOrder = 14)]
        [SettingPropertyFloatingInteger(
            "Charge Formation Strictness",
            0f, 1f, "#0%",
            Order = 1,
            RequireRestart = false,
            HintText = "How strictly cavalry maintains formation during charge. 0% = loose, 100% = rigid line.")]
        public float ChargeFormationStrictness { get; set; } = 0.7f;
        
        [SettingPropertyGroup("Smart Cavalry AI", GroupOrder = 14)]
        [SettingPropertyBool(
            "Enable Friendly Collision Avoidance",
            Order = 2,
            RequireRestart = false,
            HintText = "Cavalry will path around friendly units (shield walls, etc.) instead of getting stuck.")]
        public bool EnableFriendlyCollisionAvoidance { get; set; } = true;
        
        [SettingPropertyGroup("Smart Cavalry AI", GroupOrder = 14)]
        [SettingPropertyFloatingInteger(
            "Charge Line Spacing",
            0.5f, 3f, "0.0",
            Order = 3,
            RequireRestart = false,
            HintText = "Spacing between cavalry units during charge (in unit widths).")]
        public float ChargeLineSpacing { get; set; } = 1.2f;
        
        [SettingPropertyGroup("Smart Cavalry AI", GroupOrder = 14)]
        [SettingPropertyFloatingInteger(
            "Reform Distance After Charge",
            10f, 50f, "0",
            Order = 4,
            RequireRestart = false,
            HintText = "Distance (meters) cavalry travels past enemy before reforming for next charge.")]
        public float ReformDistanceAfterCharge { get; set; } = 25f;
    }

    // ═══════════════════════════════════════════════════════════════════
    // FEATURE ENUMS
    // ═══════════════════════════════════════════════════════════════════
    
    public enum SiegeMountBehavior
    {
        Vanilla,
        DismountKeepOnMap,
        DismountToInventory,
        AutoRemountAfter
    }
    
    public enum ArmorLayerCalculationMode
    {
        Additive,
        HighestPlusBonus,
        Weighted
    }
    
    public enum MixedFormationLayout
    {
        Vanilla,
        InfantryFrontRangedBack,
        RangedFrontInfantryBack,
        RangedWingsInfantryCenter,
        Checkerboard
    }
    
    public enum ActionBarPosition
    {
        BottomCenter,
        TopCenter,
        LeftSide,
        RightSide
    }
}

        // ==================== Armor Cosmetics ====================
        [SettingPropertyGroup("Armor Cosmetics", GroupOrder = 15)]
        [SettingPropertyBool("Enable Armor Cosmetics", Order = 0, RequireRestart = false,
            HintText = "Enable hiding or replacing armor visuals while keeping stats.")]
        public bool EnableArmorCosmetics { get; set; } = true;
        
        [SettingPropertyGroup("Armor Cosmetics", GroupOrder = 15)]
        [SettingPropertyBool("Apply to Companions", Order = 1, RequireRestart = false,
            HintText = "Allow cosmetic customization for companions.")]
        public bool CosmeticsForCompanions { get; set; } = true;
        
        [SettingPropertyGroup("Armor Cosmetics", GroupOrder = 15)]
        [SettingPropertyBool("Remember Cosmetics", Order = 2, RequireRestart = false,
            HintText = "Save cosmetic settings to savegame.")]
        public bool SaveCosmetics { get; set; } = true;

        // === EXPERIMENTAL FEATURES (Default OFF) ===
        
        [SettingPropertyGroup("Experimental Features", GroupOrder = 99)]
        [SettingPropertyBool("Enable Volley Fire", Order = 1, RequireRestart = false,
            HintText = "[EXPERIMENTAL] Enable coordinated volley fire for archers. May cause issues.")]
        public bool EnableVolleyFire { get; set; } = false;
        
        [SettingPropertyGroup("Experimental Features", GroupOrder = 99)]
        [SettingPropertyBool("Enable Cosmetic Picker", Order = 2, RequireRestart = false,
            HintText = "[EXPERIMENTAL] Enable item selection for armor cosmetics. Cycles through inventory items.")]
        public bool EnableCosmeticPicker { get; set; } = false;
