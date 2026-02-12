using System.Collections.Generic;
using TaleWorlds.Localization;

namespace HeirOfNumenor
{
    /// <summary>
    /// Centralized localization for all mod features.
    /// Uses Bannerlord's TextObject system for multi-language support.
    /// 
    /// To add translations:
    /// 1. Create XML files in ModuleData/Languages/{language_code}/
    /// 2. Match the IDs used here (e.g., "str_mod_loaded")
    /// </summary>
    public static class ModTexts
    {
        // ═══════════════════════════════════════════════════════════════════
        // GENERAL / SYSTEM
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject ModLoaded(string feature, string details) =>
            new TextObject("{=str_mod_loaded}[{FEATURE}] Loaded. {DETAILS}")
                .SetTextVariable("FEATURE", feature)
                .SetTextVariable("DETAILS", details);

        public static TextObject FeatureDisabled(string feature) =>
            new TextObject("{=str_feature_disabled}[{FEATURE}] is disabled in settings.")
                .SetTextVariable("FEATURE", feature);

        public static TextObject Error(string feature, string message) =>
            new TextObject("{=str_error}[{FEATURE}] Error: {MESSAGE}")
                .SetTextVariable("FEATURE", feature)
                .SetTextVariable("MESSAGE", message);

        public static TextObject NoFiefs =>
            new TextObject("{=str_no_fiefs}You don't own any fiefs yet!");

        // ═══════════════════════════════════════════════════════════════════
        // RING SYSTEM
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject RingsOfPower =>
            new TextObject("{=str_rings_title}Rings of Power");

        public static TextObject RingEquipped(string ringName) =>
            new TextObject("{=str_ring_equipped}Equipped {RING_NAME}")
                .SetTextVariable("RING_NAME", ringName);

        public static TextObject RingUnequipped(string ringName) =>
            new TextObject("{=str_ring_unequipped}Unequipped {RING_NAME}")
                .SetTextVariable("RING_NAME", ringName);

        public static TextObject CannotEquipMoreRings =>
            new TextObject("{=str_max_rings}Cannot equip more than 1 ring!");

        public static TextObject AllRingsGranted =>
            new TextObject("{=str_all_rings}All Rings of Power have been granted!");

        public static TextObject DarkForcesStir =>
            new TextObject("{=str_dark_forces}Dark forces stir... You feel eyes watching you from the shadows.");

        public static TextObject CorruptionWarning(float level) =>
            new TextObject("{=str_corruption_warning}The ring's corruption grows stronger... ({LEVEL}%)")
                .SetTextVariable("LEVEL", level.ToString("F0"));

        public static TextObject CorruptionFading =>
            new TextObject("{=str_corruption_fading}The ring's influence fades as you part with it...");

        public static TextObject RingPowerGrowing(string ringName) =>
            new TextObject("{=str_ring_power}The {RING_NAME}'s power grows within you...")
                .SetTextVariable("RING_NAME", ringName);

        public static TextObject CannotSwapYet =>
            new TextObject("{=str_cannot_swap}You must wait for the previous ring's effects to fade before equipping another.");

        // Ring Effect Messages
        public static TextObject ElvenGrace =>
            new TextObject("{=str_elven_grace}Elven grace flows through you...");
        public static TextObject DwarvenFortitude =>
            new TextObject("{=str_dwarven_fortitude}Dwarven fortitude hardens your resolve...");
        public static TextObject DarkDominion =>
            new TextObject("{=str_dark_dominion}A dark dominion clouds your thoughts...");

        // ═══════════════════════════════════════════════════════════════════
        // TROOP STATUS
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject TroopsDeserted(int count, string troopName) =>
            new TextObject("{=str_troops_deserted}{COUNT} {TROOP_NAME} have deserted in fear!")
                .SetTextVariable("COUNT", count)
                .SetTextVariable("TROOP_NAME", troopName);

        public static TextObject BondingDiluted(string troopName, float oldBonding, float newBonding) =>
            new TextObject("{=str_bonding_diluted}{TROOP_NAME} bonding diluted by new recruits ({OLD}% → {NEW}%)")
                .SetTextVariable("TROOP_NAME", troopName)
                .SetTextVariable("OLD", oldBonding.ToString("F0"))
                .SetTextVariable("NEW", newBonding.ToString("F0"));

        // ═══════════════════════════════════════════════════════════════════
        // MEMORY SYSTEM (Virtual Captains)
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject CaptainPromoted(string captainName, string troopName) =>
            new TextObject("{=str_captain_promoted}★ {CAPTAIN_NAME} has emerged as captain of your {TROOP_NAME}!")
                .SetTextVariable("CAPTAIN_NAME", captainName)
                .SetTextVariable("TROOP_NAME", troopName);

        public static TextObject CaptainFallen(string captainName, string troopName) =>
            new TextObject("{=str_captain_fallen}† Captain {CAPTAIN_NAME} has fallen with the last of the {TROOP_NAME}!")
                .SetTextVariable("CAPTAIN_NAME", captainName)
                .SetTextVariable("TROOP_NAME", troopName);

        public static TextObject CaptainFellInBattle(string captainName, string troopName) =>
            new TextObject("{=str_captain_battle}† Captain {CAPTAIN_NAME} fell leading the {TROOP_NAME} in desperate battle!")
                .SetTextVariable("CAPTAIN_NAME", captainName)
                .SetTextVariable("TROOP_NAME", troopName);

        public static TextObject CaptainDisbanded(string captainName) =>
            new TextObject("{=str_captain_disbanded}Captain {CAPTAIN_NAME} disbands as the last troops are released.")
                .SetTextVariable("CAPTAIN_NAME", captainName);

        public static TextObject TroopsAbandoned(string troopName, float bondingLoss) =>
            new TextObject("{=str_troops_abandoned}Your {TROOP_NAME} feel abandoned... (Bonding -{LOSS}%)")
                .SetTextVariable("TROOP_NAME", troopName)
                .SetTextVariable("LOSS", bondingLoss.ToString("F0"));

        // ═══════════════════════════════════════════════════════════════════
        // FIEF MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject FiefManagement =>
            new TextObject("{=str_fief_management}Fief Management");

        public static TextObject OpeningFiefManager =>
            new TextObject("{=str_opening_fief}Opening remote fief management...");

        public static TextObject BuildingQueued(string buildingName) =>
            new TextObject("{=str_building_queued}Queued {BUILDING} for construction.")
                .SetTextVariable("BUILDING", buildingName);

        public static TextObject QueueFull =>
            new TextObject("{=str_queue_full}Construction queue is full!");

        public static TextObject NoBuildingsAvailable =>
            new TextObject("{=str_no_buildings}No buildings available to construct.");

        public static TextObject BuildingRemoved(string buildingName) =>
            new TextObject("{=str_building_removed}Removed {BUILDING} from queue.")
                .SetTextVariable("BUILDING", buildingName);

        public static TextObject NotInQueue =>
            new TextObject("{=str_not_in_queue}Building is not in queue.");

        public static TextObject RemoteBuildingDisabled =>
            new TextObject("{=str_remote_disabled}Remote building management is disabled in settings.");

        // ═══════════════════════════════════════════════════════════════════
        // SMITHING EXTENDED
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject RepairShop =>
            new TextObject("{=str_repair_shop}Visit the Repair Shop");

        public static TextObject RepairShopDesc =>
            new TextObject("{=str_repair_shop_desc}The smithy's repair shop. Here you can repair damaged equipment or upgrade items with better modifiers.");

        public static TextObject ViewItemsForRepair =>
            new TextObject("{=str_view_repair}View items for repair");

        public static TextObject Leave =>
            new TextObject("{=str_leave}Leave");

        public static TextObject SelectItemToRepair =>
            new TextObject("{=str_select_repair}Select Item to Repair");

        public static TextObject SelectItemDesc =>
            new TextObject("{=str_select_repair_desc}Choose an item from your inventory to repair or upgrade.");

        public static TextObject NoRepairableItems =>
            new TextObject("{=str_no_repairable}You have no items that can be repaired or upgraded.");

        public static TextObject NoRepairOptions =>
            new TextObject("{=str_no_repair_options}No repair options available for your current items.");

        public static TextObject NotEnoughGold(int required) =>
            new TextObject("{=str_not_enough_gold}Not enough gold! Need {AMOUNT} gold.")
                .SetTextVariable("AMOUNT", required);

        public static TextObject RepairFailed(int cost) =>
            new TextObject("{=str_repair_failed}The repair attempt failed! Lost {COST} gold.")
                .SetTextVariable("COST", cost);

        public static TextObject RepairSuccess(string quality, int cost) =>
            new TextObject("{=str_repair_success}Successfully {QUALITY} the item for {COST} gold!")
                .SetTextVariable("QUALITY", quality)
                .SetTextVariable("COST", cost);

        public static TextObject UniqueItemCrafted(string itemName) =>
            new TextObject("{=str_unique_crafted}★ You crafted a unique item: {ITEM_NAME}!")
                .SetTextVariable("ITEM_NAME", itemName);

        public static TextObject LegendaryUnique(string itemName) =>
            new TextObject("{=str_legendary_unique}Legendary success! Created unique item: {ITEM_NAME}")
                .SetTextVariable("ITEM_NAME", itemName);

        // Repair Options
        public static TextObject BasicRepair =>
            new TextObject("{=str_basic_repair}Basic Repair");
        public static TextObject BasicRepairDesc =>
            new TextObject("{=str_basic_repair_desc}Remove any damage or negative modifiers");
        public static TextObject FineQuality =>
            new TextObject("{=str_fine_quality}Fine Quality");
        public static TextObject FineQualityDesc =>
            new TextObject("{=str_fine_quality_desc}Upgrade to Fine quality (+10% effectiveness)");
        public static TextObject MasterworkQuality =>
            new TextObject("{=str_masterwork}Masterwork Quality");
        public static TextObject MasterworkDesc =>
            new TextObject("{=str_masterwork_desc}Upgrade to Masterwork quality (+20% effectiveness)");
        public static TextObject LegendaryReforge =>
            new TextObject("{=str_legendary_reforge}Legendary Reforge");
        public static TextObject LegendaryReforgeDesc =>
            new TextObject("{=str_legendary_desc}Attempt to create a legendary quality item (+40% effectiveness)");

        // ═══════════════════════════════════════════════════════════════════
        // FORMATION PRESETS
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject FormationPresets =>
            new TextObject("{=str_formation_presets}Formation Presets");

        public static TextObject SavePreset =>
            new TextObject("{=str_save_preset}Save Preset");

        public static TextObject LoadPreset =>
            new TextObject("{=str_load_preset}Load Preset");

        public static TextObject DeletePreset =>
            new TextObject("{=str_delete_preset}Delete Preset");

        public static TextObject PresetSaved(string name) =>
            new TextObject("{=str_preset_saved}Preset '{NAME}' saved successfully!")
                .SetTextVariable("NAME", name);

        public static TextObject PresetLoaded(string name) =>
            new TextObject("{=str_preset_loaded}Preset '{NAME}' loaded!")
                .SetTextVariable("NAME", name);

        public static TextObject MaxPresetsReached(int max) =>
            new TextObject("{=str_max_presets}Maximum presets ({MAX}) reached for this character!")
                .SetTextVariable("MAX", max);

        // ═══════════════════════════════════════════════════════════════════
        // EQUIPMENT PRESETS
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject EquipmentPresets =>
            new TextObject("{=str_equipment_presets}Equipment Presets");

        public static TextObject SaveEquipment =>
            new TextObject("{=str_save_equipment}Save Current Equipment");

        public static TextObject EquipPreset =>
            new TextObject("{=str_equip_preset}Equip Preset");

        // ═══════════════════════════════════════════════════════════════════
        // CULTURAL NEEDS
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject CultureNeedTriggered(string culture, string message) =>
            new TextObject("{=str_need_triggered}[{CULTURE}] {MESSAGE}")
                .SetTextVariable("CULTURE", culture)
                .SetTextVariable("MESSAGE", message);

        public static TextObject CultureNeedResolved(string culture, string message) =>
            new TextObject("{=str_need_resolved}[{CULTURE}] {MESSAGE}")
                .SetTextVariable("CULTURE", culture)
                .SetTextVariable("MESSAGE", message);

        // Default need messages (can be overridden in XML)
        public static TextObject DwarvesNeedBeer =>
            new TextObject("{=str_dwarves_beer}Your dwarven troops grow irritable without ale...");
        public static TextObject DwarvesBeerSatisfied =>
            new TextObject("{=str_dwarves_beer_ok}Your dwarven troops are satisfied with their ale rations.");
        public static TextObject ElvesNeedForest =>
            new TextObject("{=str_elves_forest}Your elven troops long for the woods...");
        public static TextObject OrcsCraveCombat =>
            new TextObject("{=str_orcs_combat}Your orc troops grow restless without battle...");

        // ═══════════════════════════════════════════════════════════════════
        // COMPANION ROLES
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject Surgeon => new TextObject("{=str_role_surgeon}Surgeon");
        public static TextObject Engineer => new TextObject("{=str_role_engineer}Engineer");
        public static TextObject Scout => new TextObject("{=str_role_scout}Scout");
        public static TextObject Quartermaster => new TextObject("{=str_role_quartermaster}Quartermaster");
        public static TextObject Archer => new TextObject("{=str_role_archer}Archer");
        public static TextObject Cavalry => new TextObject("{=str_role_cavalry}Cavalry");
        public static TextObject Infantry => new TextObject("{=str_role_infantry}Infantry");
        public static TextObject HorseArcher => new TextObject("{=str_role_horse_archer}Horse Archer");

        // ═══════════════════════════════════════════════════════════════════
        // UI LABELS
        // ═══════════════════════════════════════════════════════════════════

        public static TextObject Select => new TextObject("{=str_select}Select");
        public static TextObject Cancel => new TextObject("{=str_cancel}Cancel");
        public static TextObject Apply => new TextObject("{=str_apply}Apply");
        public static TextObject Close => new TextObject("{=str_close}Close");
        public static TextObject Confirm => new TextObject("{=str_confirm}Confirm");
        public static TextObject Yes => new TextObject("{=str_yes}Yes");
        public static TextObject No => new TextObject("{=str_no}No");

        public static TextObject Gold => new TextObject("{=str_gold}Gold");
        public static TextObject Cost => new TextObject("{=str_cost}Cost");
        public static TextObject Success => new TextObject("{=str_success}Success");
        public static TextObject Chance => new TextObject("{=str_chance}Chance");

        // Stats
        public static TextObject Prosperity => new TextObject("{=str_prosperity}Prosperity");
        public static TextObject Loyalty => new TextObject("{=str_loyalty}Loyalty");
        public static TextObject Security => new TextObject("{=str_security}Security");
        public static TextObject FoodStocks => new TextObject("{=str_food}Food Stocks");
        public static TextObject Garrison => new TextObject("{=str_garrison}Garrison");
        public static TextObject TaxIncome => new TextObject("{=str_tax}Tax Income");

        // ═══════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets localized text with a placeholder fallback if localization fails.
        /// </summary>
        public static string GetSafe(TextObject text)
        {
            try
            {
                return text?.ToString() ?? "[Missing Text]";
            }
            catch
            {
                return "[Localization Error]";
            }
        }
    }
}
