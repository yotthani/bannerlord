using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace ArmorCosmetics
{
    public class ArmorCosmeticsSettings : AttributeGlobalSettings<ArmorCosmeticsSettings>
    {
        public override string Id => "ArmorCosmetics_v1";
        public override string DisplayName => "Armor Cosmetics";
        public override string FolderName => "ArmorCosmetics";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Armor Cosmetics",
            HintText = "Master toggle. Disables both transmog overrides and slot hiding.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableArmorCosmetics { get; set; } = true;

        [SettingPropertyBool("Enable Cosmetic Picker",
            HintText = "Allow cycling through inventory items as cosmetic overrides via the UI.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableCosmeticPicker { get; set; } = true;

        [SettingPropertyBool("Debug Mode",
            HintText = "Show diagnostic messages in the chat HUD.",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool DebugMode { get; set; } = false;

        public static ArmorCosmeticsSettings Get() => Instance ?? new ArmorCosmeticsSettings();
    }
}
