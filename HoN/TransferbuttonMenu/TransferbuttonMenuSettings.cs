using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace TransferbuttonMenu
{
    /// <summary>
    /// Combined settings for both inventory features in this module:
    /// - Quick actions menu (Sell Damaged / Sell Low Value / Unequip)
    /// - Inventory search box availability
    /// </summary>
    public class TransferbuttonMenuSettings : AttributeGlobalSettings<TransferbuttonMenuSettings>
    {
        public override string Id => "TransferbuttonMenu_v1";
        public override string DisplayName => "Transfer Button Menu";
        public override string FolderName => "TransferbuttonMenu";
        public override string FormatType => "json2";

        // ── Master toggles ────────────────────────────────────────────────────
        [SettingPropertyBool("Enable Quick Actions Menu",
            HintText = "Replace 'Sell All' with a multi-action quick menu (Sell Damaged, Sell Low Value, Unequip).",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool EnableQuickActions { get; set; } = true;

        [SettingPropertyBool("Enable Inventory Search",
            HintText = "Enable search boxes inside the inventory screen for filtering items by name.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool EnableInventorySearch { get; set; } = true;

        // ── Sell Damaged ──────────────────────────────────────────────────────
        [SettingPropertyDropdown("Damaged Quality", Order = 0, RequireRestart = false,
            HintText = "Items at or below this quality will be sold by 'Sell Damaged'.")]
        [SettingPropertyGroup("Sell Damaged", GroupOrder = 1)]
        public Dropdown<string> DamagedQualityDropdown { get; set; } = new Dropdown<string>(
            new[] { "Destroyed (-50%)", "Damaged (-30%)", "Rusty/Cracked (-20%)", "Worn/Battered (-10%)" }, 2);

        [SettingPropertyFloatingInteger("Custom Modifier Threshold", -1f, 0f, "#0%", Order = 1, RequireRestart = false,
            HintText = "Used only when 'Use Custom Threshold' is on.")]
        [SettingPropertyGroup("Sell Damaged", GroupOrder = 1)]
        public float DamagedThreshold { get; set; } = -0.20f;

        [SettingPropertyBool("Use Custom Threshold", Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("Sell Damaged", GroupOrder = 1)]
        public bool UseCustomThreshold { get; set; } = false;

        [SettingPropertyBool("Include Equipped Items", Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("Sell Damaged", GroupOrder = 1)]
        public bool SellDamagedEquipped { get; set; } = false;

        [SettingPropertyBool("Exclude Horses", Order = 4, RequireRestart = false)]
        [SettingPropertyGroup("Sell Damaged", GroupOrder = 1)]
        public bool ExcludeDamagedHorses { get; set; } = true;

        // ── Sell Low Value ────────────────────────────────────────────────────
        [SettingPropertyInteger("Max Value Threshold", 1, 10000, "0 denars", Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("Sell Low Value", GroupOrder = 2)]
        public int LowValueThreshold { get; set; } = 100;

        [SettingPropertyBool("Include Equipped Items", Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("Sell Low Value", GroupOrder = 2)]
        public bool SellLowValueEquipped { get; set; } = false;

        [SettingPropertyBool("Exclude Food", Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("Sell Low Value", GroupOrder = 2)]
        public bool ExcludeLowValueFood { get; set; } = true;

        [SettingPropertyBool("Exclude Horses", Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("Sell Low Value", GroupOrder = 2)]
        public bool ExcludeLowValueHorses { get; set; } = true;

        [SettingPropertyBool("Exclude Trade Goods", Order = 4, RequireRestart = false)]
        [SettingPropertyGroup("Sell Low Value", GroupOrder = 2)]
        public bool ExcludeLowValueTradeGoods { get; set; } = false;

        // ── Misc ──────────────────────────────────────────────────────────────
        [SettingPropertyBool("Show Confirmation Dialog", Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("Misc", GroupOrder = 3)]
        public bool ShowConfirmation { get; set; } = true;

        [SettingPropertyBool("Play Sound Effects", Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("Misc", GroupOrder = 3)]
        public bool PlaySounds { get; set; } = true;

        [SettingPropertyBool("Debug Mode", Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("Misc", GroupOrder = 3)]
        public bool DebugMode { get; set; } = false;

        public float GetEffectiveDamageThreshold()
        {
            if (UseCustomThreshold) return DamagedThreshold;
            return DamagedQualityDropdown.SelectedIndex switch
            {
                0 => -0.50f, 1 => -0.30f, 2 => -0.20f, 3 => -0.10f, _ => -0.20f
            };
        }

        public static TransferbuttonMenuSettings Get() => Instance ?? new TransferbuttonMenuSettings();
    }
}
