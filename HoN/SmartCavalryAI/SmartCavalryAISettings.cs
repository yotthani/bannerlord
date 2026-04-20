using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace SmartCavalryAI
{
    public class SmartCavalryAISettings : AttributeGlobalSettings<SmartCavalryAISettings>
    {
        public override string Id => "SmartCavalryAI_v1";
        public override string DisplayName => "Smart Cavalry AI";
        public override string FolderName => "SmartCavalryAI";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Smart Cavalry AI",
            HintText = "Master toggle. Coordinated line charges + collision avoidance.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableSmartCavalryAI { get; set; } = true;

        [SettingPropertyBool("Friendly Collision Avoidance",
            HintText = "Cavalry avoids running over friendly infantry.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableFriendlyCollisionAvoidance { get; set; } = true;

        [SettingPropertyFloatingInteger("Charge Formation Strictness", 0f, 1f, "#0%",
            HintText = "How tightly cavalry must align before charging. Higher = wait longer.",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("Charge Tuning")]
        public float ChargeFormationStrictness { get; set; } = 0.7f;

        [SettingPropertyFloatingInteger("Reform Distance After Charge", 10f, 80f, "0",
            HintText = "Meters past the charge target before reforming.",
            Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("Charge Tuning")]
        public float ReformDistanceAfterCharge { get; set; } = 25f;

        [SettingPropertyFloatingInteger("Charge Line Spacing", 0.8f, 3f, "0.0",
            HintText = "Multiplier for spacing between cavalry units in charge line.",
            Order = 4, RequireRestart = false)]
        [SettingPropertyGroup("Charge Tuning")]
        public float ChargeLineSpacing { get; set; } = 1.2f;

        public static SmartCavalryAISettings Get() => Instance ?? new SmartCavalryAISettings();
    }
}
