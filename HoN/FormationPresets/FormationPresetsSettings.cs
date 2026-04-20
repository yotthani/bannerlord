using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace FormationPresets
{
    public class FormationPresetsSettings : AttributeGlobalSettings<FormationPresetsSettings>
    {
        public override string Id => "FormationPresets_v1";
        public override string DisplayName => "Formation Presets";
        public override string FolderName => "FormationPresets";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Formation Presets",
            HintText = "Save / load formation presets in Order of Battle.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableFormationPresets { get; set; } = true;

        [SettingPropertyBool("Auto-Assign Heroes",
            HintText = "Automatically assign heroes to formations based on their skills when loading a preset.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool AutoAssignHeroes { get; set; } = true;

        [SettingPropertyInteger("Max Formation Presets", 1, 20,
            HintText = "Maximum number of formation presets.",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public int MaxFormationPresets { get; set; } = 10;

        [SettingPropertyBool("Debug Mode", Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool DebugMode { get; set; } = false;

        public static FormationPresetsSettings Get() => Instance ?? new FormationPresetsSettings();
    }
}
