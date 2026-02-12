using System;
using System.Linq;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace HeirOfNumenor.Features.FiefManagement
{
    /// <summary>
    /// Custom navigation element for Fief Management in the MapBar.
    /// </summary>
    public class FiefManagementNavigationElement
    {
        private const string FEATURE_NAME = "FiefManagement";
        
        public string Id => "FiefManagement";
        public TextObject Name => new TextObject("Fief Management");
        public string IconPath => "SPGeneral\\MapOverlay\\Settlement\\settlement_icon_frame";
        
        public bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableFiefManagement; }
                catch { return true; }
            }
        }

        public void OnClick()
        {
            SafeExecutor.Execute(FEATURE_NAME, "OnClick", () =>
            {
                if (!IsEnabled) return;
                
                if (!FiefManagementHelper.CanOpenFiefManagement())
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "You don't own any fiefs yet!", Colors.Yellow));
                    return;
                }

                FiefManagementHelper.OpenFiefManagement();
            });
        }
    }

    /// <summary>
    /// Harmony patches to add Fief Management button to MapBar.
    /// </summary>
    [HarmonyPatch]
    public static class FiefManagementMapBarPatches
    {
        private const string FEATURE_NAME = "FiefManagement";
        private static MapNavigationVM _mapNavVM;
        private static bool _buttonAdded = false;

        /// <summary>
        /// Check if Fief Management is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableFiefManagement; }
                catch { return true; }
            }
        }

        /* DISABLED - Constructor signature might not match all game versions
        /// <summary>
        /// Patch MapNavigationVM constructor to add our button.
        /// </summary>
        [HarmonyPatch(typeof(MapNavigationVM), MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(INavigationHandler), typeof(Func<MapBarShortcuts>) })]
        [HarmonyPostfix]
        public static void MapNavigationVM_Constructor_Postfix(MapNavigationVM __instance)
        {
            SafeExecutor.WrapPatch(FEATURE_NAME, "Constructor", () =>
            {
                if (!IsEnabled) return;
                
                _mapNavVM = __instance;
                _buttonAdded = false;
            });
        }
        */

        /// <summary>
        /// Hook into MapScreen update to check for F6 hotkey.
        /// </summary>
        [HarmonyPatch(typeof(MapScreen), "OnFrameTick")]
        [HarmonyPostfix]
        public static void MapScreen_OnFrameTick_Postfix(MapScreen __instance)
        {
            SafeExecutor.WrapPatch(FEATURE_NAME, "Update", () =>
            {
                if (!IsEnabled) return;
                
                // Check for F6 hotkey
                if (Input.IsKeyPressed(InputKey.F6))
                {
                    if (FiefManagementHelper.CanOpenFiefManagement())
                    {
                        FiefManagementHelper.OpenFiefManagement();
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "You don't own any fiefs yet!", Colors.Yellow));
                    }
                }
            });
        }
    }

    /// <summary>
    /// ViewModel for Fief Management navigation item.
    /// </summary>
    public class FiefManagementNavItemVM : ViewModel
    {
        private const string FEATURE_NAME = "FiefManagement";
        
        private string _shortcutText;
        private string _nameText;
        private bool _isEnabled;

        public FiefManagementNavItemVM()
        {
            try
            {
                ShortcutText = "F6";
                NameText = "Fief Management";
                IsEnabled = FiefManagementHelper.CanOpenFiefManagement();
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "NavItemVM Constructor", ex);
            }
        }

        [DataSourceProperty]
        public string ShortcutText
        {
            get => _shortcutText;
            set
            {
                if (_shortcutText != value)
                {
                    _shortcutText = value;
                    OnPropertyChangedWithValue(value, nameof(ShortcutText));
                }
            }
        }

        [DataSourceProperty]
        public string NameText
        {
            get => _nameText;
            set
            {
                if (_nameText != value)
                {
                    _nameText = value;
                    OnPropertyChangedWithValue(value, nameof(NameText));
                }
            }
        }

        [DataSourceProperty]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChangedWithValue(value, nameof(IsEnabled));
                }
            }
        }

        public void ExecuteAction()
        {
            SafeExecutor.Execute(FEATURE_NAME, "ExecuteAction", () =>
            {
                if (FiefManagementHelper.CanOpenFiefManagement())
                {
                    FiefManagementHelper.OpenFiefManagement();
                }
            });
        }

        public void RefreshState()
        {
            SafeExecutor.Execute(FEATURE_NAME, "RefreshState", () =>
            {
                IsEnabled = FiefManagementHelper.CanOpenFiefManagement();
            });
        }
    }

    /// <summary>
    /// Helper class for Fief Management operations.
    /// </summary>
    public static class FiefManagementHelper
    {
        private const string FEATURE_NAME = "FiefManagement";

        /// <summary>
        /// Check if the player can open fief management (owns at least one fief).
        /// </summary>
        public static bool CanOpenFiefManagement()
        {
            try
            {
                if (!FiefManagementMapBarPatches.IsEnabled) return false;
                
                var playerClan = Clan.PlayerClan;
                if (playerClan == null) return false;

                return Settlement.All?.Any(s => 
                    s?.OwnerClan == playerClan && 
                    (s.IsTown || s.IsCastle)) == true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Opens the Fief Management screen.
        /// </summary>
        public static void OpenFiefManagement()
        {
            SafeExecutor.Execute(FEATURE_NAME, "OpenScreen", () =>
            {
                var state = Game.Current?.GameStateManager?.CreateState<FiefManagementState>();
                if (state != null)
                {
                    Game.Current.GameStateManager.PushState(state);
                }
            });
        }

        /// <summary>
        /// Gets all player-owned fiefs.
        /// </summary>
        public static System.Collections.Generic.List<Settlement> GetPlayerFiefs()
        {
            var fiefs = new System.Collections.Generic.List<Settlement>();

            SafeExecutor.Execute(FEATURE_NAME, "GetFiefs", () =>
            {
                var playerClan = Clan.PlayerClan;
                if (playerClan == null) return;

                foreach (var settlement in Settlement.All ?? Enumerable.Empty<Settlement>())
                {
                    try
                    {
                        if (settlement?.OwnerClan == playerClan && 
                            (settlement.IsTown || settlement.IsCastle))
                        {
                            fiefs.Add(settlement);
                        }
                    }
                    catch { }
                }

                // Sort: towns first, then by name
                fiefs.Sort((a, b) =>
                {
                    if (a.IsTown && !b.IsTown) return -1;
                    if (!a.IsTown && b.IsTown) return 1;
                    return string.Compare(a.Name?.ToString(), b.Name?.ToString(), StringComparison.Ordinal);
                });
            });

            return fiefs;
        }
    }

    /// <summary>
    /// Harmony patch to intercept GameStateScreenManager.CreateScreen for FiefManagementState.
    /// The [GameStateScreen] attribute doesn't work in mods, so we must intercept manually.
    /// </summary>
    [HarmonyPatch(typeof(GameStateScreenManager), "CreateScreen")]
    public static class FiefManagement_CreateScreen_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(GameState state, ref ScreenBase __result)
        {
            if (state is FiefManagementState fiefState)
            {
                __result = new GauntletFiefManagementScreen(fiefState);
                return false;
            }
            return true;
        }
    }
}
