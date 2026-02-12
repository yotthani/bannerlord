using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using BannerlordThemeSwitcher.Behaviors;
using BannerlordThemeSwitcher.Patches;
using System;
using System.IO;

namespace BannerlordThemeSwitcher
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;
        private ThemeManager _themeManager;
        
        private const string HarmonyId = "com.bannerlord.themeswitcher";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            try
            {
                Debug.Print("[ThemeSwitcher] === OnSubModuleLoad START ===");
                
                // Initialize Harmony for patches
                _harmony = new Harmony(HarmonyId);
                CharacterCreationPatches.ApplyPatch(_harmony);
                BrushRendererPatch.ApplyPatch(_harmony);
                
                // Initialize theme manager
                _themeManager = new ThemeManager();
                _themeManager.DiscoverThemes();
                
                // Initialize BrushModifier (will use ColorScheme from themes at runtime)
                BrushModifier.Initialize();
                
                Debug.Print("[ThemeSwitcher] === OnSubModuleLoad COMPLETE ===");
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] CRITICAL ERROR in OnSubModuleLoad: {ex}");
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll(HarmonyId);
            _themeManager?.Dispose();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign)
            {
                var campaignStarter = gameStarterObject as CampaignGameStarter;
                if (campaignStarter != null)
                {
                    campaignStarter.AddBehavior(new ThemeKingdomBehavior());
                }
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            InformationManager.DisplayMessage(new InformationMessage(
                "=== Theme Switcher LOADED ===",
                Colors.Cyan));
            
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[ThemeSwitcher] Themes: {_themeManager?.AvailableThemes.Count ?? 0}",
                    Colors.Green));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[ThemeSwitcher] Error: {ex.Message}",
                    Colors.Red));
            }
        }
    }
}
