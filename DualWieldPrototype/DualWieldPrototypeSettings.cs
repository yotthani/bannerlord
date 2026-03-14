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
        [SettingPropertyBool(
            "Left Proxy Compare Mode",
            Order = 1,
            RequireRestart = false,
            HintText = "Overrides both mouse buttons for a left-vs-left diagnostic: LMB forces left-fist, RMB forces slashleft_1h_left_stance on the same proxy path.")]
        public bool LeftProxyCompareMode { get; set; } = false;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyBool(
            "Left 1H Slash Compare Mode",
            Order = 2,
            RequireRestart = false,
            HintText = "Overrides mouse buttons for a left-stance 1h compare: LMB forces slashleft ready/release, RMB forces slashright ready/release.")]
        public bool Left1hSlashCompareMode { get; set; } = false;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Left 1H Compare Channel",
            Order = 3,
            RequireRestart = false,
            HintText = "For Left 1H Slash Compare Mode only. Channel0 matches the current proxy path. Channel1 tests the same ready/release pair on the channel used by native left fist attacks.")]
        public MCM.Common.Dropdown<string> Left1hCompareChannel { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "Channel0",
                "Channel1"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Proxy Attack Action",
            Order = 4,
            RequireRestart = false,
            HintText = "Selects which action the clean RMB proxy path uses. This keeps the same input/context path and swaps only the played action.")]
        public MCM.Common.Dropdown<string> ProxyAttackAction { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "LeftFistSwing",
                "SlashLeft1hLeftStance",
                "ResolvedMainUsageAttackLeft",
                "RotDualSlashLeft1hLeftStance",
                "RotDualThrust1hLeftStance"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyBool(
            "Resolver Force Left Stance",
            Order = 5,
            RequireRestart = false,
            HintText = "For the resolved main-usage test, call the native usage resolver with isLeftStance=true instead of the current agent state.")]
        public bool ResolverForceLeftStance { get; set; } = false;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Resolver Main Usage",
            Order = 6,
            RequireRestart = false,
            HintText = "For ResolvedMainUsageAttackLeft, override which main item_usage string is sent into Bannerlord's native usage resolver. This lets us probe native paths on a stable offctx loadout.")]
        public MCM.Common.Dropdown<string> ResolverMainUsage { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "CurrentMainhandUsage",
                "onehanded_block_shield_swing",
                "onehanded_block_shield_swing_thrust"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Resolver Direction",
            Order = 7,
            RequireRestart = false,
            HintText = "For ResolvedMainUsageAttackLeft, choose which native usage direction is sent into the resolver. This probes whether the left-hand attack state is exposed under a different attack direction than AttackLeft.")]
        public MCM.Common.Dropdown<string> ResolverDirection { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "AttackLeft",
                "AttackUp",
                "AttackDown",
                "AttackRight"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyFloatingInteger(
            "Proxy Cooldown",
            0.1f,
            1.0f,
            "#0.00",
            Order = 8,
            RequireRestart = false,
            HintText = "Minimum time between two forced left-fist proxy attacks on RMB.")]
        public float OffHandCooldownSeconds { get; set; } = 0.32f;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Offhand Wield Probe",
            Order = 9,
            RequireRestart = false,
            HintText = "Native offhand wield probe for the current offhand slot. Use this only for diagnostics. Disabled keeps the current attach-only behavior.")]
        public MCM.Common.Dropdown<string> OffhandWieldProbeMode { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "Disabled",
                "Instant",
                "InstantAfterPickUp",
                "WithAnimation"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyBool(
            "Gate Slash Proxy To Left Stance",
            Order = 10,
            RequireRestart = false,
            HintText = "If SlashLeft1hLeftStance is selected, wait for a real left_stance window before firing the proxy action.")]
        public bool GateSlashProxyToLeftStance { get; set; } = true;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyBool(
            "Prime Slash With Left Flags",
            Order = 11,
            RequireRestart = false,
            HintText = "Before slashleft_1h_left_stance, force a left attack movement flag for one controlled test of the deeper combat state.")]
        public bool PrimeSlashWithLeftFlags { get; set; } = true;

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Slash Anim Flag Mode",
            Order = 12,
            RequireRestart = false,
            HintText = "A/B test for slashleft_1h_left_stance: either no extra anim flags or anf_use_left_hand_during_attack.")]
        public MCM.Common.Dropdown<string> SlashAnimFlagMode { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "None",
                "UseLeftHandDuringAttack"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Slash Transition Mode",
            Order = 13,
            RequireRestart = false,
            HintText = "How slash-type proxy calls are pushed onto channel 0. Default keeps Bannerlord's normal blend setup; Instant forces zero blend-in for a hard overwrite test.")]
        public MCM.Common.Dropdown<string> SlashTransitionMode { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "Default",
                "Instant"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyDropdown(
            "Slash Flow Mode",
            Order = 14,
            RequireRestart = false,
            HintText = "DirectQuickRelease forces the current release clip immediately. ReadyThenRelease starts the matching ready action first and triggers release on button-up or timeout.")]
        public MCM.Common.Dropdown<string> SlashFlowMode { get; set; } = new MCM.Common.Dropdown<string>(
            new[]
            {
                "DirectQuickRelease",
                "ReadyThenRelease"
            },
            0);

        [SettingPropertyGroup("Combat", GroupOrder = 1)]
        [SettingPropertyFloatingInteger(
            "Fist Then Slash Delay",
            0.00f,
            0.10f,
            "#0.000",
            Order = 15,
            RequireRestart = false,
            HintText = "Delay for the V compare path: first left fist, then slash on ch0. Use this to probe a very short-lived native left state.")]
        public float FistThenSlashDelaySeconds { get; set; } = 0.045f;

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
