using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using System.Collections.Generic;

namespace BannerlordThemeSwitcher
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "BannerlordThemeSwitcher";
        public override string DisplayName => "Theme Switcher";
        public override string FolderName => "BannerlordThemeSwitcher";
        public override string FormatType => "json2";

        // ═══════════════════════════════════════════════════════════════════
        // THEME SWITCHING
        // ═══════════════════════════════════════════════════════════════════

        [SettingPropertyGroup("Theme Switching", GroupOrder = 0)]
        [SettingPropertyBool(
            "Auto-switch by Kingdom",
            Order = 0,
            RequireRestart = false,
            HintText = "Automatically switch UI theme based on player's kingdom. When disabled, uses manual selection.")]
        public bool AutoSwitchByKingdom { get; set; } = true;

        [SettingPropertyGroup("Theme Switching", GroupOrder = 0)]
        [SettingPropertyDropdown(
            "Manual Theme",
            Order = 1,
            RequireRestart = false,
            HintText = "Select theme manually. Only used when auto-switch is disabled.")]
        public Dropdown<string> ManualThemeDropdown { get; set; } = new Dropdown<string>(
            new string[]
            {
                "Default",
                "Vlandia",
                "Battania",
                "Empire",
                "Sturgia",
                "Aserai",
                "Khuzait",
                "Naval"
            },
            0);

        // ═══════════════════════════════════════════════════════════════════
        // DEBUG OPTIONS
        // ═══════════════════════════════════════════════════════════════════

        [SettingPropertyGroup("Debug", GroupOrder = 99)]
        [SettingPropertyBool(
            "Show Current Theme",
            Order = 0,
            RequireRestart = false,
            HintText = "Display current theme name in messages (debug).")]
        public bool DebugShowTheme { get; set; } = false;

        [SettingPropertyGroup("Debug", GroupOrder = 99)]
        [SettingPropertyBool(
            "Log Brush Lookups",
            Order = 1,
            RequireRestart = false,
            HintText = "Log brush interception to debug file (performance impact).")]
        public bool DebugLogBrushLookups { get; set; } = false;

        // ═══════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the currently selected manual theme ID.
        /// </summary>
        public string GetManualThemeId()
        {
            return ManualThemeDropdown?.SelectedValue ?? "Default";
        }

        /// <summary>
        /// Refreshes the dropdown with discovered themes.
        /// </summary>
        public void RefreshThemeDropdown(IEnumerable<string> themeIds)
        {
            var themes = new List<string>(themeIds);
            if (!themes.Contains("Default"))
                themes.Insert(0, "Default");

            int currentIndex = themes.IndexOf(GetManualThemeId());
            if (currentIndex < 0) currentIndex = 0;

            ManualThemeDropdown = new Dropdown<string>(themes, currentIndex);
        }
    }
}
