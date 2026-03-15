using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DualWield
{
    public class DualWieldSettings : AttributeGlobalSettings<DualWieldSettings>
    {
        public override string Id => "DualWield_v1";
        public override string DisplayName => "Dual Wield";
        public override string FolderName => "DualWield";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Dual Wield", HintText = "Master toggle for the dual wield system.", Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableDualWield { get; set; } = true;

        [SettingPropertyBool("Debug Mode", HintText = "Show debug messages in chat.", Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool DebugMode { get; set; } = false;

        [SettingPropertyInteger("Off-Hand Rotation", 0, 9, HintText = "Rotation preset: 0=Identity, 1=180°X, 2=180°Y, 3=180°Z, 4=90°Y, 5=-90°Y, 6=90°X, 7=-90°X, 8=90°Z, 9=-90°Z. Changes apply live in combat (~1 sec).", Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public int OffHandRotation { get; set; } = 9;

        [SettingPropertyInteger("Attack Mode", 0, 1, HintText = "0 = Separated (LMB=right, MMB=left, RMB=block). 1 = Alternating (LMB alternates R/L).", Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public int AttackMode { get; set; } = 0;

        [SettingPropertyFloatingInteger("Off-Hand Damage Multiplier", 0.5f, 1.0f, "#0%", HintText = "Damage multiplier for off-hand strikes.", Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("Combat")]
        public float OffHandDamageMultiplier { get; set; } = 0.85f;

        [SettingPropertyFloatingInteger("Off-Hand Speed Bonus", 0.0f, 0.5f, "#0%", HintText = "Attack speed bonus for off-hand strikes.", Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("Combat")]
        public float OffHandSpeedBonus { get; set; } = 0.20f;

        [SettingPropertyFloatingInteger("Attack Speed Bonus", 0.0f, 0.5f, "#0%", HintText = "General attack speed bonus while dual wielding.", Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("Combat")]
        public float AttackSpeedBonus { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("Armor Penalty", 0.0f, 0.5f, "#0%", HintText = "Armor effectiveness reduction (no shield trade-off).", Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("Combat")]
        public float ArmorPenalty { get; set; } = 0.20f;

        [SettingPropertyFloatingInteger("Movement Speed Bonus", 0.0f, 0.3f, "#0%", HintText = "Movement speed increase while dual wielding.", Order = 4, RequireRestart = false)]
        [SettingPropertyGroup("Combat")]
        public float MovementSpeedBonus { get; set; } = 0.10f;

        [SettingPropertyFloatingInteger("Parry Window Reduction", 0.0f, 0.5f, "#0%", HintText = "How much shorter the block window is compared to shield.", Order = 5, RequireRestart = false)]
        [SettingPropertyGroup("Combat")]
        public float ParryWindowReduction { get; set; } = 0.30f;

        /// <summary>
        /// Safe accessor that returns defaults if MCM is not available.
        /// </summary>
        public static DualWieldSettings Get()
        {
            return Instance ?? new DualWieldSettings();
        }

        public static void DebugLog(string message)
        {
            if (Get().DebugMode)
                InformationManager.DisplayMessage(new InformationMessage($"[DualWield] {message}", Colors.Cyan));
        }
    }
}
