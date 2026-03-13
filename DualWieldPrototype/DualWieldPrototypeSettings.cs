using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DualWieldPrototype
{
    public sealed class DualWieldPrototypeSettings : AttributeGlobalSettings<DualWieldPrototypeSettings>
    {
        public override string Id => "DualWieldPrototype";
        public override string DisplayName => "Dual Wield Prototype";
        public override string FolderName => "DualWieldPrototype";
        public override string FormatType => "json2";

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Enable Prototype",
            Order = 0,
            RequireRestart = false,
            HintText = "Master toggle for the current dual-wield prototype.")]
        public bool EnablePrototype { get; set; } = true;

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Live Messages",
            Order = 1,
            RequireRestart = false,
            HintText = "Show short on-screen debug help during missions. File logging is the primary debug output.")]
        public bool LiveMessages { get; set; } = false;

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Debug File Logging",
            Order = 2,
            RequireRestart = false,
            HintText = "Write detailed runtime diagnostics to dualwieldprototype.log in the module folder.")]
        public bool DebugFileLogging { get; set; } = true;

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Deep Action Logging",
            Order = 3,
            RequireRestart = false,
            HintText = "Adds pre/post action state, stages, weights and movement flags for each forced off-hand attack.")]
        public bool DeepActionLogging { get; set; } = true;

        [SettingPropertyGroup("Control", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Control Mode",
            Order = 0,
            RequireRestart = false,
            HintText = "SplitMouse keeps LMB on the main hand and uses RMB for the off hand. AutoAlternate uses every second LMB for the off hand.")]
        public Dropdown<string> ControlMode { get; set; } = new Dropdown<string>(
            new[]
            {
                "SplitMouse",
                "AutoAlternate"
            },
            0);

        [SettingPropertyGroup("Control", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Playback Mode",
            Order = 1,
            RequireRestart = false,
            HintText = "Current left-hand prototype actions only work reliably on Channel0Combat. Channel1Overlay is kept only for diagnostics and is forced back to Channel0 at runtime.")]
        public Dropdown<string> PlaybackMode { get; set; } = new Dropdown<string>(
            new[]
            {
                "Channel0Combat",
                "Channel1Overlay"
            },
            0);

        [SettingPropertyGroup("Control", GroupOrder = 1)]
        [SettingPropertyFloatingInteger(
            "Off-Hand Cooldown",
            0.1f,
            1.0f,
            "#0.00",
            Order = 2,
            RequireRestart = false,
            HintText = "Minimum time between two forced off-hand attacks.")]
        public float OffHandCooldownSeconds { get; set; } = 0.32f;

        [SettingPropertyGroup("Control", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "RMB Trigger Mode",
            Order = 3,
            RequireRestart = false,
            HintText = "DirectSlash fires the off-hand action as soon as the left-stance window opens. ReleaseFollowUp reproduces the older timing: detect a right-hand release, wait briefly, then fire left-hand on channel 0. LegacyCycle restores the older multi-action RMB iteration for state comparison.")]
        public Dropdown<string> RmbTriggerMode { get; set; } = new Dropdown<string>(
            new[]
            {
                "DirectSlash",
                "ReleaseFollowUp",
                "PrimedSlashLeft",
                "LegacyCycle"
            },
            0);

        [SettingPropertyGroup("Control", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Off-Hand Test Action",
            Order = 4,
            RequireRestart = false,
            HintText = "Sequence cycles through the current off-hand profile. The fixed modes force one action for comparison logging.")]
        public Dropdown<string> OffHandTestAction { get; set; } = new Dropdown<string>(
            new[]
            {
                "Sequence",
                "SlashLeftOnly",
                "ThrustOnly",
                "FistLeftOnly"
            },
            0);

        [SettingPropertyGroup("Control", GroupOrder = 1)]
        [SettingPropertyBool(
            "Ignore Action Priority",
            Order = 5,
            RequireRestart = false,
            HintText = "Lets forced off-hand attacks override more native actions. Stronger, but more invasive.")]
        public bool IgnoreActionPriority { get; set; } = true;

        [SettingPropertyGroup("Control", GroupOrder = 1)]
        [SettingPropertyBool(
            "Fallback To Overlay",
            Order = 6,
            RequireRestart = false,
            HintText = "Deprecated for the current prototype. Left-hand stance actions are treated as Channel0-only.")]
        public bool FallbackToOverlay { get; set; } = false;

        [SettingPropertyGroup("Attach", GroupOrder = 2)]
        [SettingPropertyInteger(
            "Rotation Preset",
            0,
            9,
            Order = 0,
            RequireRestart = false,
            HintText = "0=Identity, 1=180X, 2=180Y, 3=180Z, 4=90Y, 5=-90Y, 6=90X, 7=-90X, 8=90Z, 9=-90Z.")]
        public int RotationPreset { get; set; } = 9;

        [SettingPropertyGroup("Attach", GroupOrder = 2)]
        [SettingPropertyFloatingInteger(
            "Offset X",
            -0.5f,
            0.5f,
            "#0.00",
            Order = 1,
            RequireRestart = false,
            HintText = "Local attach offset on X.")]
        public float OffsetX { get; set; } = 0f;

        [SettingPropertyGroup("Attach", GroupOrder = 2)]
        [SettingPropertyFloatingInteger(
            "Offset Y",
            -0.5f,
            0.5f,
            "#0.00",
            Order = 2,
            RequireRestart = false,
            HintText = "Local attach offset on Y.")]
        public float OffsetY { get; set; } = 0f;

        [SettingPropertyGroup("Attach", GroupOrder = 2)]
        [SettingPropertyFloatingInteger(
            "Offset Z",
            -0.5f,
            0.5f,
            "#0.00",
            Order = 3,
            RequireRestart = false,
            HintText = "Local attach offset on Z.")]
        public float OffsetZ { get; set; } = 0f;

        public static DualWieldPrototypeSettings Get()
        {
            return Instance ?? new DualWieldPrototypeSettings();
        }

        public static void DebugLog(string message)
        {
            DualWieldPrototypeLogger.Log(message);

            if (Get().LiveMessages)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[DW] {message}", Colors.Cyan));
            }
        }
    }
}
