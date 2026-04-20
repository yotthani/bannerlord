using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace CompanionRoles
{
    public class CompanionRolesSettings : AttributeGlobalSettings<CompanionRolesSettings>
    {
        public override string Id => "CompanionRoles_v1";
        public override string DisplayName => "Companion Roles";
        public override string FolderName => "CompanionRoles";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Companion Roles",
            HintText = "Master toggle. Detect and surface combat roles for companions.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableCompanionRoles { get; set; } = true;

        [SettingPropertyBool("Show Role Icons",
            HintText = "Display role icons next to companion portraits in party screen.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool ShowRoleIcons { get; set; } = true;

        [SettingPropertyBool("Debug Mode",
            HintText = "Show diagnostic messages in the chat HUD.",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool DebugMode { get; set; } = false;

        public static CompanionRolesSettings Get() => Instance ?? new CompanionRolesSettings();
    }
}
