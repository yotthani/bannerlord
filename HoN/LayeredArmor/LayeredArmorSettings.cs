using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace LayeredArmor
{
    /// <summary>
    /// MCM settings for the standalone LayeredArmor module.
    /// Three calculation modes mirror the HoN original:
    ///   0 = Additive       (inner + middle + outer — very strong)
    ///   1 = Highest+Bonus  (base + bonus% of layers — default, balanced)
    ///   2 = Weighted       (outer 1.0 + middle 0.5 + inner 0.3)
    /// </summary>
    public class LayeredArmorSettings : AttributeGlobalSettings<LayeredArmorSettings>
    {
        public override string Id => "LayeredArmor_v1";
        public override string DisplayName => "Layered Armor";
        public override string FolderName => "LayeredArmor";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Layered Armor",
            HintText = "Master toggle. When off, vanilla armor is used.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableLayeredArmor { get; set; } = true;

        [SettingPropertyDropdown("Calculation Mode", Order = 1, RequireRestart = false,
            HintText = "How under-layer armor combines with outer armor.")]
        [SettingPropertyGroup("General")]
        public Dropdown<string> ArmorLayerCalculationDropdown { get; set; } = new Dropdown<string>(
            new[] { "Additive", "Highest + Bonus", "Weighted" }, 1);

        [SettingPropertyFloatingInteger("Under-Armor Bonus", 0f, 0.5f, "#0%",
            HintText = "Used in 'Highest + Bonus' mode: fraction of under-layer armor added to the outer.",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public float UnderArmorBonusPercent { get; set; } = 0.15f;

        public static LayeredArmorSettings Get() => Instance ?? new LayeredArmorSettings();
    }
}
