using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace BattleActionBar
{
    public class BattleActionBarSettings : AttributeGlobalSettings<BattleActionBarSettings>
    {
        public override string Id => "BattleActionBar_v1";
        public override string DisplayName => "Battle Action Bar";
        public override string FolderName => "BattleActionBar";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Battle Action Bar",
            HintText = "Master toggle. Adds a context-sensitive action bar in field battles.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableBattleActionBar { get; set; } = true;

        [SettingPropertyBool("Cancel Stance On Move",
            HintText = "Active stances (PikeWall, Testudo, ...) auto-cancel when the formation moves.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool CancelStanceOnMove { get; set; } = true;

        [SettingPropertyBool("Enable Volley Fire",
            HintText = "Coordinated archer fire on command (sub-feature of action bar).",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableVolleyFire { get; set; } = true;

        [SettingPropertyBool("Debug Mode",
            HintText = "Show diagnostic messages in the chat HUD.",
            Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool DebugMode { get; set; } = false;

        public static BattleActionBarSettings Get() => Instance ?? new BattleActionBarSettings();
    }
}
