using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BannerlordThemeSwitcher.Patches
{
    /// <summary>
    /// Harmony patches for character creation to apply themes during culture selection.
    /// </summary>
    public static class CharacterCreationPatches
    {
        /// <summary>
        /// Manually apply the patch
        /// </summary>
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                // Patch SetSelectedCulture for culture selection
                var original = typeof(CharacterCreationContent).GetMethod("SetSelectedCulture",
                    BindingFlags.Instance | BindingFlags.Public);
                
                if (original == null)
                {
                    Debug.Print("[ThemeSwitcher] ERROR: Could not find CharacterCreationContent.SetSelectedCulture!");
                    return;
                }
                
                Debug.Print($"[ThemeSwitcher] Found SetSelectedCulture: {original}");
                
                var postfix = typeof(CharacterCreationPatches).GetMethod("SetSelectedCulture_Postfix",
                    BindingFlags.Static | BindingFlags.Public);
                
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                Debug.Print("[ThemeSwitcher] CharacterCreation patch applied successfully!");
                
                // Patch for leaving character creation (back to main menu)
                // Try to find the screen that handles going back
                PatchCharacterCreationExit(harmony);
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] ERROR applying CharacterCreation patch: {ex}");
            }
        }

        private static void PatchCharacterCreationExit(Harmony harmony)
        {
            try
            {
                // Patch MBInitialScreenBase.OnDeactivate to catch return to main menu
                var initialScreenType = typeof(MBGameManager).Assembly.GetType("TaleWorlds.MountAndBlade.MBInitialScreenBase");
                if (initialScreenType != null)
                {
                    var onActivate = initialScreenType.GetMethod("OnActivate", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (onActivate != null)
                    {
                        var postfix = typeof(CharacterCreationPatches).GetMethod("MainMenuActivate_Postfix",
                            BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(onActivate, postfix: new HarmonyMethod(postfix));
                        Debug.Print("[ThemeSwitcher] MainMenu activate patch applied!");
                    }
                }
                
                // Also try InitialState
                var initialStateType = typeof(MBGameManager).Assembly.GetType("TaleWorlds.MountAndBlade.InitialState");
                if (initialStateType != null)
                {
                    var onActivate = initialStateType.GetMethod("OnActivate",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (onActivate != null)
                    {
                        var postfix = typeof(CharacterCreationPatches).GetMethod("MainMenuActivate_Postfix",
                            BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(onActivate, postfix: new HarmonyMethod(postfix));
                        Debug.Print("[ThemeSwitcher] InitialState activate patch applied!");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Error patching main menu return: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix - called when main menu screen activates (returning from character creation)
        /// </summary>
        public static void MainMenuActivate_Postfix()
        {
            try
            {
                Debug.Print("[ThemeSwitcher] Main menu activated - resetting to Default theme");
                var themeManager = ThemeManager.Instance;
                if (themeManager != null && themeManager.CurrentThemeId != "Default")
                {
                    themeManager.ApplyTheme("Default");
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Error resetting theme: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix - called after SetSelectedCulture
        /// </summary>
        public static void SetSelectedCulture_Postfix(CultureObject culture, CharacterCreationManager characterCreationManager)
        {
            try
            {
                Debug.Print("[ThemeSwitcher] =====================================");
                Debug.Print("[ThemeSwitcher] SetSelectedCulture_Postfix CALLED!");
                
                if (culture == null)
                {
                    Debug.Print("[ThemeSwitcher] Culture is null");
                    return;
                }

                var cultureId = culture.StringId;
                Debug.Print($"[ThemeSwitcher] Culture selected: {cultureId}");

                var themeManager = ThemeManager.Instance;
                if (themeManager == null)
                {
                    Debug.Print("[ThemeSwitcher] ERROR: ThemeManager.Instance is null!");
                    return;
                }

                Debug.Print($"[ThemeSwitcher] ThemeManager has {themeManager.AvailableThemes.Count} themes");
                
                // List all available themes
                foreach (var t in themeManager.AvailableThemes)
                {
                    Debug.Print($"[ThemeSwitcher]   Available theme: {t.Id} bound to: {string.Join(", ", t.BoundKingdoms)}");
                }

                var themeId = themeManager.GetThemeForKingdom(cultureId);
                Debug.Print($"[ThemeSwitcher] GetThemeForKingdom({cultureId}) returned: {themeId}");
                
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[ThemeSwitcher] Culture: {cultureId} -> Theme: {themeId}",
                    Colors.Yellow));
                
                themeManager.ApplyTheme(themeId);
                Debug.Print("[ThemeSwitcher] =====================================");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Error in SetSelectedCulture patch: {ex}");
            }
        }
    }
}
