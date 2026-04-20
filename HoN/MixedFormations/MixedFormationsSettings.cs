using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace MixedFormations
{
    /// <summary>
    /// MCM-backed settings for the Mixed Formations standalone module.
    /// Persisted to Documents/Mount and Blade II Bannerlord/Configs/ModSettings/Global/MixedFormations/.
    /// </summary>
    public class MixedFormationsSettings : AttributeGlobalSettings<MixedFormationsSettings>
    {
        public override string Id => "MixedFormations_v1";
        public override string DisplayName => "Mixed Formations";
        public override string FolderName => "MixedFormations";
        public override string FormatType => "json2";

        [SettingPropertyBool("Enable Mixed Formation Layouts",
            HintText = "Master toggle. When off, formations use vanilla behavior.",
            Order = 0, RequireRestart = false)]
        [SettingPropertyGroup("General")]
        public bool EnableMixedFormationLayouts { get; set; } = true;

        [SettingPropertyInteger("Infantry Row Depth", 1, 10,
            HintText = "Rows of infantry when infantry is in front.",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("Layout Depths")]
        public int InfantryRowDepth { get; set; } = 3;

        [SettingPropertyInteger("Ranged Row Depth", 1, 10,
            HintText = "Rows of ranged units when ranged is in front.",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("Layout Depths")]
        public int RangedRowDepth { get; set; } = 2;

        /// <summary>
        /// Safe accessor that returns defaults if MCM is unavailable.
        /// Mirrors the DualWield module pattern.
        /// </summary>
        public static MixedFormationsSettings Get()
        {
            return Instance ?? new MixedFormationsSettings();
        }
    }
}
