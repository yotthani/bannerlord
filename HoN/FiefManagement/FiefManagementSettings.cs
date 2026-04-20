using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace FiefManagement
{
    public class FiefManagementSettings : AttributeGlobalSettings<FiefManagementSettings>
    {
        public override string Id => "FiefManagement_v1";
        public override string DisplayName => "Fief Management";
        public override string FolderName => "FiefManagement";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Remote Fief Management",
            HintText = "Manage your fiefs from anywhere on the map (F6 hotkey).",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableFiefManagement { get; set; } = true;

        [SettingPropertyBool("Allow Remote Building Queue",
            HintText = "Add buildings to construction queue remotely.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool AllowRemoteBuildingQueue { get; set; } = true;

        [SettingPropertyBool("Debug Mode", Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool DebugMode { get; set; } = false;

        public static FiefManagementSettings Get() => Instance ?? new FiefManagementSettings();
    }
}
