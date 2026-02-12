using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using System;
using TaleWorlds.Library;

namespace HeirOfNumenor
{
    /// <summary>
    /// Comprehensive MCM settings for all mod features.
    /// All features can be enabled/disabled and configured here.
    /// </summary>
    public class ModSettings : AttributeGlobalSettings<ModSettings>
    {
        public override string Id => "HeirOfNumenor";
        public override string DisplayName => "Heir of Numenor (Extension)";
        public override string FolderName => "HeirOfNumenor";
        public override string FormatType => "json";

        #region ═══════════════════════════════════════════════════════════════
        // GENERAL SETTINGS
        #endregion

        [SettingPropertyBool("Enable Debug Messages", Order = 0, RequireRestart = false,
            HintText = "Show debug messages in the game log. Useful for troubleshooting.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool EnableDebugMessages { get; set; } = false;

        [SettingPropertyBool("Safe Mode", Order = 1, RequireRestart = false,
            HintText = "Extra error handling. May slightly reduce performance but increases stability.")]
        [SettingPropertyGroup("General")]
        public bool SafeMode { get; set; } = true;

        #region ═══════════════════════════════════════════════════════════════
        // EQUIPMENT PRESETS
        #endregion

        [SettingPropertyBool("Enable Equipment Presets", Order = 0, RequireRestart = false,
            HintText = "Enable saving and loading equipment presets in the inventory screen.")]
        [SettingPropertyGroup("Equipment Presets", GroupOrder = 1)]
        public bool EnableEquipmentPresets { get; set; } = true;

        [SettingPropertyInteger("Max Presets Per Character", 1, 20, Order = 1, RequireRestart = false,
            HintText = "Maximum number of equipment presets per character.")]
        [SettingPropertyGroup("Equipment Presets")]
        public int MaxPresetsPerCharacter { get; set; } = 10;

        #region ═══════════════════════════════════════════════════════════════
        // INVENTORY FEATURES
        #endregion

        [SettingPropertyBool("Enable Inventory Search", Order = 0, RequireRestart = false,
            HintText = "Enable the search boxes in the inventory screen to filter items by name.")]
        [SettingPropertyGroup("Inventory Features", GroupOrder = 1)]
        public bool EnableInventorySearch { get; set; } = true;

        #region ═══════════════════════════════════════════════════════════════
        // FORMATION PRESETS
        #endregion

        [SettingPropertyBool("Enable Formation Presets", Order = 0, RequireRestart = false,
            HintText = "Enable saving and loading formation presets in Order of Battle.")]
        [SettingPropertyGroup("Formation Presets", GroupOrder = 2)]
        public bool EnableFormationPresets { get; set; } = true;

        [SettingPropertyBool("Auto-Assign Heroes", Order = 1, RequireRestart = false,
            HintText = "Automatically assign heroes to formations based on their skills.")]
        [SettingPropertyGroup("Formation Presets")]
        public bool AutoAssignHeroes { get; set; } = true;

        [SettingPropertyInteger("Max Formation Presets", 1, 20, Order = 2, RequireRestart = false,
            HintText = "Maximum number of formation presets.")]
        [SettingPropertyGroup("Formation Presets")]
        public int MaxFormationPresets { get; set; } = 10;

        #region ═══════════════════════════════════════════════════════════════
        // COMPANION ROLES
        #endregion

        [SettingPropertyBool("Enable Companion Roles Display", Order = 0, RequireRestart = false,
            HintText = "Show companion roles (Surgeon, Engineer, etc.) in the party screen.")]
        [SettingPropertyGroup("Companion Roles", GroupOrder = 3)]
        public bool EnableCompanionRoles { get; set; } = true;

        [SettingPropertyBool("Show Role Icons", Order = 1, RequireRestart = false,
            HintText = "Display role icons next to companion portraits.")]
        [SettingPropertyGroup("Companion Roles")]
        public bool ShowRoleIcons { get; set; } = true;

        #region ═══════════════════════════════════════════════════════════════
        // RING SYSTEM
        #endregion

        [SettingPropertyBool("Enable Ring System", Order = 0, RequireRestart = false,
            HintText = "Enable the Rings of Power system (LOTR-themed).")]
        [SettingPropertyGroup("Ring System", GroupOrder = 4)]
        public bool EnableRingSystem { get; set; } = true;

        [SettingPropertyBool("Enable Ring Corruption", Order = 1, RequireRestart = false,
            HintText = "Rings cause corruption over time.")]
        [SettingPropertyGroup("Ring System")]
        public bool EnableRingCorruption { get; set; } = true;

        [SettingPropertyFloatingInteger("Corruption Rate Multiplier", 0.1f, 5f, "0.0", Order = 2, RequireRestart = false,
            HintText = "Multiplier for corruption gain rate. Higher = faster corruption.")]
        [SettingPropertyGroup("Ring System")]
        public float CorruptionRateMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("Enable Ring Threats", Order = 3, RequireRestart = false,
            HintText = "Ring-bearers attract enemy attention (Nazgûl, hunters).")]
        [SettingPropertyGroup("Ring System")]
        public bool EnableRingThreats { get; set; } = true;

        [SettingPropertyFloatingInteger("Threat Rate Multiplier", 0.1f, 5f, "0.0", Order = 4, RequireRestart = false,
            HintText = "Multiplier for threat accumulation. Higher = more frequent attacks.")]
        [SettingPropertyGroup("Ring System")]
        public float ThreatRateMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("Enable Ring Screen (MapBar)", Order = 5, RequireRestart = false,
            HintText = "Show Ring System button in the map bar.")]
        [SettingPropertyGroup("Ring System")]
        public bool EnableRingScreen { get; set; } = true;

        #region ═══════════════════════════════════════════════════════════════
        // TROOP STATUS SYSTEM
        #endregion

        [SettingPropertyBool("Enable Troop Status System", Order = 0, RequireRestart = false,
            HintText = "Track Fear, Bonding, Frustration, Loyalty for troops.")]
        [SettingPropertyGroup("Troop Status", GroupOrder = 5)]
        public bool EnableTroopStatus { get; set; } = true;

        [SettingPropertyBool("Enable Status UI Icons", Order = 1, RequireRestart = false,
            HintText = "Show status icons in the party screen for each troop type.")]
        [SettingPropertyGroup("Troop Status")]
        public bool EnableTroopStatusUI { get; set; } = true;

        [SettingPropertyBool("Enable Desertion", Order = 2, RequireRestart = false,
            HintText = "Troops with high fear may desert.")]
        [SettingPropertyGroup("Troop Status")]
        public bool EnableDesertion { get; set; } = true;

        [SettingPropertyFloatingInteger("Fear Gain Multiplier", 0f, 3f, "0.0", Order = 3, RequireRestart = false,
            HintText = "Multiplier for fear gain. 0 = no fear gain.")]
        [SettingPropertyGroup("Troop Status")]
        public float FearGainMultiplier { get; set; } = 1.0f;

        [SettingPropertyFloatingInteger("Bonding Gain Multiplier", 0f, 3f, "0.0", Order = 4, RequireRestart = false,
            HintText = "Multiplier for bonding gain. Higher = faster bonding.")]
        [SettingPropertyGroup("Troop Status")]
        public float BondingGainMultiplier { get; set; } = 1.0f;

        [SettingPropertyInteger("Fear Desertion Threshold", 50, 100, Order = 5, RequireRestart = false,
            HintText = "Fear level at which troops may start deserting.")]
        [SettingPropertyGroup("Troop Status")]
        public int FearDesertionThreshold { get; set; } = 85;

        #region ═══════════════════════════════════════════════════════════════
        // MEMORY SYSTEM (Virtual Captains)
        #endregion

        [SettingPropertyBool("Enable Memory System", Order = 0, RequireRestart = false,
            HintText = "Enable Virtual Captains that emerge from highly bonded troops.")]
        [SettingPropertyGroup("Memory System", GroupOrder = 6)]
        public bool EnableMemorySystem { get; set; } = true;

        [SettingPropertyInteger("Captain Bonding Threshold", 50, 95, Order = 1, RequireRestart = false,
            HintText = "Bonding level required for a captain to emerge (%).")]
        [SettingPropertyGroup("Memory System")]
        public int CaptainBondingThreshold { get; set; } = 75;

        [SettingPropertyInteger("Min Troops for Captain", 5, 30, Order = 2, RequireRestart = false,
            HintText = "Minimum troops of a type required for a captain to emerge.")]
        [SettingPropertyGroup("Memory System")]
        public int MinTroopsForCaptain { get; set; } = 10;

        [SettingPropertyBool("Enable Captain Death", Order = 3, RequireRestart = false,
            HintText = "Captains can die in battle with heavy casualties.")]
        [SettingPropertyGroup("Memory System")]
        public bool EnableCaptainDeath { get; set; } = true;

        [SettingPropertyFloatingInteger("Captain Bonus Multiplier", 0.5f, 3f, "0.0", Order = 4, RequireRestart = false,
            HintText = "Multiplier for captain bonuses (morale, damage, etc.).")]
        [SettingPropertyGroup("Memory System")]
        public float CaptainBonusMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("Enable Captain to Companion Promotion", Order = 5, RequireRestart = false,
            HintText = "Legendary captains can become real companions (very rare, requires special event).")]
        [SettingPropertyGroup("Memory System")]
        public bool EnableCaptainPromotion { get; set; } = true;

        [SettingPropertyInteger("Battles Required for Promotion", 20, 100, Order = 6, RequireRestart = false,
            HintText = "Minimum battles a captain must survive before becoming eligible for promotion.")]
        [SettingPropertyGroup("Memory System")]
        public int BattlesForCaptainPromotion { get; set; } = 50;

        [SettingPropertyFloatingInteger("Captain Promotion Chance", 0.01f, 0.5f, "0.00", Order = 7, RequireRestart = false,
            HintText = "Base chance per battle for an eligible captain to be promoted (very rare by default).")]
        [SettingPropertyGroup("Memory System")]
        public float CaptainPromotionChance { get; set; } = 0.02f;

        [SettingPropertyInteger("Additional Companion Limit", 0, 10, Order = 8, RequireRestart = false,
            HintText = "Increase the maximum companion limit by this amount (for promoted captains).")]
        [SettingPropertyGroup("Memory System")]
        public int AdditionalCompanionLimit { get; set; } = 2;

        #region ═══════════════════════════════════════════════════════════════
        // CUSTOM RESOURCE SYSTEM (Cultural Needs)
        #endregion

        [SettingPropertyBool("Enable Cultural Needs", Order = 0, RequireRestart = false,
            HintText = "Cultures have unique needs (Dwarves need beer, Elves need forests, etc.).")]
        [SettingPropertyGroup("Cultural Needs", GroupOrder = 7)]
        public bool EnableCulturalNeeds { get; set; } = true;

        [SettingPropertyFloatingInteger("Need Decay Multiplier", 0f, 3f, "0.0", Order = 1, RequireRestart = false,
            HintText = "How fast needs decay. 0 = no decay (needs never trigger).")]
        [SettingPropertyGroup("Cultural Needs")]
        public float NeedDecayMultiplier { get; set; } = 1.0f;

        [SettingPropertyFloatingInteger("Need Effect Multiplier", 0f, 3f, "0.0", Order = 2, RequireRestart = false,
            HintText = "Multiplier for frustration/bonding effects from unmet needs.")]
        [SettingPropertyGroup("Cultural Needs")]
        public float NeedEffectMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("Show Need Warnings", Order = 3, RequireRestart = false,
            HintText = "Display messages when cultural needs are triggered/resolved.")]
        [SettingPropertyGroup("Cultural Needs")]
        public bool ShowNeedWarnings { get; set; } = true;

        #region ═══════════════════════════════════════════════════════════════
        // FIEF MANAGEMENT
        #endregion

        [SettingPropertyBool("Enable Remote Fief Management", Order = 0, RequireRestart = false,
            HintText = "Manage your fiefs from anywhere on the map (F6 hotkey).")]
        [SettingPropertyGroup("Fief Management", GroupOrder = 8)]
        public bool EnableFiefManagement { get; set; } = true;

        [SettingPropertyBool("Allow Remote Building Queue", Order = 1, RequireRestart = false,
            HintText = "Add buildings to construction queue remotely.")]
        [SettingPropertyGroup("Fief Management")]
        public bool AllowRemoteBuildingQueue { get; set; } = true;

        #region ═══════════════════════════════════════════════════════════════
        // SMITHING EXTENDED
        #endregion

        [SettingPropertyBool("Enable Smithing Extended", Order = 0, RequireRestart = false,
            HintText = "Enable all Smithing Extended features.")]
        [SettingPropertyGroup("Smithing Extended", GroupOrder = 9)]
        public bool EnableSmithingExtended { get; set; } = true;

        [SettingPropertyBool("Disable Stamina Cost", Order = 1, RequireRestart = false,
            HintText = "Remove stamina cost for all smithing actions.")]
        [SettingPropertyGroup("Smithing Extended")]
        public bool DisableSmithingStamina { get; set; } = false;

        [SettingPropertyFloatingInteger("Stamina Cost Multiplier", 0f, 2f, "0.0", Order = 2, RequireRestart = false,
            HintText = "Multiplier for stamina costs. 0.5 = half, 2.0 = double.")]
        [SettingPropertyGroup("Smithing Extended")]
        public float SmithingStaminaMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("Enable Item Repair", Order = 3, RequireRestart = false,
            HintText = "Enable repairing and upgrading items at settlements.")]
        [SettingPropertyGroup("Smithing Extended")]
        public bool EnableItemRepair { get; set; } = true;

        [SettingPropertyFloatingInteger("Repair Cost Multiplier", 0.5f, 5f, "0.0", Order = 4, RequireRestart = false,
            HintText = "Multiplier for repair costs.")]
        [SettingPropertyGroup("Smithing Extended")]
        public float RepairCostMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("Enable Armor Smithing", Order = 5, RequireRestart = false,
            HintText = "Enable crafting armor from parts.")]
        [SettingPropertyGroup("Smithing Extended")]
        public bool EnableArmorSmithing { get; set; } = true;

        [SettingPropertyBool("Enable Unique Items", Order = 6, RequireRestart = false,
            HintText = "High-skill smiths can create unique items with special bonuses.")]
        [SettingPropertyGroup("Smithing Extended")]
        public bool EnableUniqueItems { get; set; } = true;

        [SettingPropertyFloatingInteger("Unique Item Chance", 0f, 0.5f, "0.00", Order = 7, RequireRestart = false,
            HintText = "Base chance to create a unique item (0.02 = 2%).")]
        [SettingPropertyGroup("Smithing Extended")]
        public float UniqueItemChance { get; set; } = 0.02f;

        [SettingPropertyInteger("Min Skill for Unique", 100, 300, Order = 8, RequireRestart = false,
            HintText = "Minimum smithing skill to have any chance at unique items.")]
        [SettingPropertyGroup("Smithing Extended")]
        public int MinSkillForUnique { get; set; } = 200;

        [SettingPropertyInteger("Base Repair Cost", 100, 5000, Order = 9, RequireRestart = false,
            HintText = "Base gold cost for item repairs.")]
        [SettingPropertyGroup("Smithing Extended")]
        public int BaseRepairCost { get; set; } = 500;

        [SettingPropertyInteger("Max Unique Bonuses", 1, 5, Order = 10, RequireRestart = false,
            HintText = "Maximum number of unique bonuses an item can have.")]
        [SettingPropertyGroup("Smithing Extended")]
        public int MaxUniqueBonuses { get; set; } = 3;

        [SettingPropertyFloatingInteger("Unique Skill Bonus Rate", 0f, 0.05f, "0.00", Order = 11, RequireRestart = false,
            HintText = "Additional unique chance per 50 smithing skill above minimum.")]
        [SettingPropertyGroup("Smithing Extended")]
        public float UniqueSkillBonusRate { get; set; } = 0.01f;

        [SettingPropertyInteger("Min Smithing Skill for Armor", 50, 200, Order = 12, RequireRestart = false,
            HintText = "Minimum smithing skill required to craft armor.")]
        [SettingPropertyGroup("Smithing Extended")]
        public int MinSmithingSkillForArmor { get; set; } = 100;

        #region ═══════════════════════════════════════════════════════════════
        // STATIC ACCESS
        #endregion

        private static ModSettings _instance;
        private static ModSettings _defaults;

        /// <summary>
        /// Gets settings instance with fallback to defaults if MCM not available.
        /// Thread-safe and null-safe.
        /// </summary>
        public static ModSettings Get()
        {
            try
            {
                if (Instance != null)
                {
                    _instance = Instance;
                    return _instance;
                }
            }
            catch
            {
                // MCM not available
            }

            // Return cached or create default
            _defaults ??= new ModSettings();
            return _defaults;
        }

        /// <summary>
        /// Logs a debug message if debug mode is enabled.
        /// </summary>
        public static void DebugLog(string message)
        {
            try
            {
                if (Get().EnableDebugMessages)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[DEBUG] {message}", Colors.Gray));
                }
            }
            catch { }
        }

        /// <summary>
        /// Logs a debug message with feature name if debug mode is enabled.
        /// </summary>
        public static void DebugLog(string feature, string message)
        {
            try
            {
                if (Get().EnableDebugMessages)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[{feature}] {message}", Colors.Gray));
                }
            }
            catch { }
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void ErrorLog(string feature, string message, Exception ex = null)
        {
            try
            {
                string fullMessage = $"[{feature}] Error: {message}";
                if (ex != null && Get().EnableDebugMessages)
                {
                    fullMessage += $" - {ex.Message}";
                }
                InformationManager.DisplayMessage(new InformationMessage(fullMessage, Colors.Red));
            }
            catch { }
        }
    }
}
