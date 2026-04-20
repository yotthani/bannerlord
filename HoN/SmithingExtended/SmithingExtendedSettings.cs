using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace SmithingExtended
{
    public class SmithingExtendedSettings : AttributeGlobalSettings<SmithingExtendedSettings>
    {
        public override string Id => "SmithingExtended_v1";
        public override string DisplayName => "Smithing Extended";
        public override string FolderName => "SmithingExtended";
        public override string FormatType => "json2";

        // ── Master & stamina ──────────────────────────────────────────────────
        [SettingPropertyBool("Enable Smithing Extended",
            HintText = "Master toggle for the entire feature.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool EnableSmithingExtended { get; set; } = true;

        [SettingPropertyBool("Disable Stamina Cost", Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool DisableSmithingStamina { get; set; } = false;

        [SettingPropertyFloatingInteger("Stamina Cost Multiplier", 0f, 2f, "0.0", Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public float SmithingStaminaMultiplier { get; set; } = 1.0f;

        // ── Item repair ──────────────────────────────────────────────────────
        [SettingPropertyBool("Enable Item Repair", Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("Item Repair", GroupOrder = 1)]
        public bool EnableItemRepair { get; set; } = true;

        [SettingPropertyFloatingInteger("Repair Cost Multiplier", 0.5f, 5f, "0.0", Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("Item Repair", GroupOrder = 1)]
        public float RepairCostMultiplier { get; set; } = 1.0f;

        [SettingPropertyInteger("Base Repair Cost", 100, 5000, Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("Item Repair", GroupOrder = 1)]
        public int BaseRepairCost { get; set; } = 500;

        // ── Armor crafting ───────────────────────────────────────────────────
        [SettingPropertyBool("Enable Armor Smithing", Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("Armor Crafting", GroupOrder = 2)]
        public bool EnableArmorSmithing { get; set; } = true;

        [SettingPropertyInteger("Min Smithing Skill for Armor", 50, 200, Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("Armor Crafting", GroupOrder = 2)]
        public int MinSmithingSkillForArmor { get; set; } = 100;

        // ── Unique items ─────────────────────────────────────────────────────
        [SettingPropertyBool("Enable Unique Items", Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("Unique Items", GroupOrder = 3)]
        public bool EnableUniqueItems { get; set; } = true;

        [SettingPropertyFloatingInteger("Unique Item Chance", 0f, 0.5f, "0.00", Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("Unique Items", GroupOrder = 3)]
        public float UniqueItemChance { get; set; } = 0.02f;

        [SettingPropertyInteger("Min Skill for Unique", 100, 300, Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("Unique Items", GroupOrder = 3)]
        public int MinSkillForUnique { get; set; } = 200;

        [SettingPropertyInteger("Max Unique Bonuses", 1, 5, Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("Unique Items", GroupOrder = 3)]
        public int MaxUniqueBonuses { get; set; } = 3;

        [SettingPropertyFloatingInteger("Unique Skill Bonus Rate", 0f, 0.05f, "0.00", Order = 4, RequireRestart = false)]
        [SettingPropertyGroup("Unique Items", GroupOrder = 3)]
        public float UniqueSkillBonusRate { get; set; } = 0.01f;

        [SettingPropertyBool("Debug Mode", Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("Misc", GroupOrder = 4)]
        public bool DebugMode { get; set; } = false;

        public static SmithingExtendedSettings Get() => Instance ?? new SmithingExtendedSettings();
    }
}
