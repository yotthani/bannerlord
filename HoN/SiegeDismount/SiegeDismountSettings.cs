using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace SiegeDismount
{
    public enum SiegeMountBehaviorType
    {
        Vanilla,
        DismountKeepOnMap,
        DismountToInventory,
        AutoRemountAfter
    }

    public class SiegeDismountSettings : AttributeGlobalSettings<SiegeDismountSettings>
    {
        public override string Id => "SiegeDismount_v1";
        public override string DisplayName => "Siege Dismount";
        public override string FolderName => "SiegeDismount";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Siege Dismount",
            HintText = "Master toggle. When off, no siege dismount handling is applied.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableSiegeDismount { get; set; } = true;

        [SettingPropertyDropdown("Siege Mount Behavior", Order = 1, RequireRestart = false,
            HintText = "Vanilla = no change. KeepOnMap = horse spawns nearby. " +
                       "ToInventory = horse goes to inventory. AutoRemount = same + restore after siege.")]
        [SettingPropertyGroup("General")]
        public Dropdown<string> SiegeMountBehaviorDropdown { get; set; } = new Dropdown<string>(
            new[] { "Vanilla", "Dismount Keep On Map", "Dismount To Inventory", "Auto Remount After" }, 3);

        [SettingPropertyBool("Debug Mode",
            HintText = "Show diagnostic messages in the chat HUD.",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool DebugMode { get; set; } = false;

        public SiegeMountBehaviorType GetSiegeMountBehavior()
        {
            if (!EnableSiegeDismount) return SiegeMountBehaviorType.Vanilla;
            return SiegeMountBehaviorDropdown.SelectedIndex switch
            {
                0 => SiegeMountBehaviorType.Vanilla,
                1 => SiegeMountBehaviorType.DismountKeepOnMap,
                2 => SiegeMountBehaviorType.DismountToInventory,
                3 => SiegeMountBehaviorType.AutoRemountAfter,
                _ => SiegeMountBehaviorType.Vanilla
            };
        }

        public static SiegeDismountSettings Get() => Instance ?? new SiegeDismountSettings();
    }
}
