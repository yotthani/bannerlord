using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace EquipPresets
{
    public class EquipPresetsSettings : AttributeGlobalSettings<EquipPresetsSettings>
    {
        public override string Id => "EquipPresets_v1";
        public override string DisplayName => "Equipment Presets";
        public override string FolderName => "EquipPresets";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Equipment Presets",
            HintText = "Master toggle. Save / load equipment presets in the inventory screen.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableEquipmentPresets { get; set; } = true;

        [SettingPropertyInteger("Max Presets Per Character", 1, 20,
            HintText = "Maximum number of equipment presets per character.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public int MaxPresetsPerCharacter { get; set; } = 10;

        [SettingPropertyBool("Debug Mode",
            HintText = "Show diagnostic messages in chat HUD.",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool DebugMode { get; set; } = false;

        public static EquipPresetsSettings Get() => Instance ?? new EquipPresetsSettings();
    }
}
