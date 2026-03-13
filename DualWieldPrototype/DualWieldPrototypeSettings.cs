using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
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
            HintText = "Master toggle for the current clean prototype.")]
        public bool EnablePrototype { get; set; } = true;

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Live Messages",
            Order = 1,
            RequireRestart = false,
            HintText = "Show short on-screen debug help during missions. File logging stays the primary output.")]
        public bool LiveMessages { get; set; } = false;

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Debug File Logging",
            Order = 2,
            RequireRestart = false,
            HintText = "Write runtime diagnostics to dualwieldprototype.log in the module folder.")]
        public bool DebugFileLogging { get; set; } = true;

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Deep Action Logging",
            Order = 3,
            RequireRestart = false,
            HintText = "Adds pre/post action state, stages, weights and movement flags for forced left-hand proxy attacks.")]
        public bool DeepActionLogging { get; set; } = true;

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Trace Native Channel Calls",
            Order = 4,
            RequireRestart = false,
            HintText = "Logs every Agent.SetActionChannel call for the main agent in supported missions.")]
        public bool TraceNativeChannelCalls { get; set; } = false;

        [SettingPropertyGroup("General", GroupOrder = 0)]
        [SettingPropertyBool(
            "Unarmed Trace Mode",
            Order = 5,
            RequireRestart = false,
            HintText = "Disables the prototype combat override and only traces native unarmed combat.")]
        public bool UnarmedTraceMode { get; set; } = false;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyBool(
            "Fist Compare Mode",
            Order = 0,
            RequireRestart = false,
            HintText = "Overrides both mouse buttons for diagnostics: LMB forces right-fist proxy, RMB forces left-fist proxy.")]
        public bool FistCompareMode { get; set; } = false;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Proxy Attack Action",
            Order = 1,
            RequireRestart = false,
            HintText = "Selects which action the clean RMB proxy path uses. This keeps the same input/context path and swaps only the played action.")]
        public MCM.Common.Dropdown<string> ProxyAttackAction { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "LeftFistSwing",
                "SlashLeft1hLeftStance"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyFloatingInteger(
            "Proxy Cooldown",
            0.1f,
            1.0f,
            "#0.00",
            Order = 2,
            RequireRestart = false,
            HintText = "Minimum time between two forced left-fist proxy attacks on RMB.")]
        public float OffHandCooldownSeconds { get; set; } = 0.32f;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyBool(
            "Gate Slash Proxy To Left Stance",
            Order = 3,
            RequireRestart = false,
            HintText = "If SlashLeft1hLeftStance is selected, wait for a real left_stance window before firing the proxy action.")]
        public bool GateSlashProxyToLeftStance { get; set; } = true;

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
