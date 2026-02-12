using System;
using System.Reflection;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Static manager for Ring screen operations.
    /// </summary>
    public static class RingScreenManager
    {
        public static bool IsOpen => Game.Current?.GameStateManager?.ActiveState is HoNRingState;

        /// <summary>
        /// Opens the Ring screen by pushing HoNRingState onto the game state stack.
        /// </summary>
        public static void OpenRingScreen()
        {
            if (IsOpen) return;

            try
            {
                var gameStateManager = Game.Current?.GameStateManager;
                if (gameStateManager == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[RingSystem] GameStateManager is null!", Colors.Red));
                    return;
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] Creating HoNRingState...", Colors.Cyan));
                
                var ringState = gameStateManager.CreateState<HoNRingState>();
                
                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] Pushing HoNRingState...", Colors.Cyan));
                
                gameStateManager.PushState(ringState);
                
                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] PushState completed!", Colors.Green));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] Error opening: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Closes the Ring screen by popping HoNRingState from the game state stack.
        /// </summary>
        public static void CloseRingScreen()
        {
            if (!IsOpen) return;

            try
            {
                Game.Current?.GameStateManager?.PopState();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] Error closing: {ex.Message}", Colors.Red));
            }
        }
    }

    /// <summary>
    /// Injects Ring navigation item into both MapNavigationVM (UI) and MapNavigationHandler (logic).
    /// </summary>
    public static class RingNavigationInjector
    {
        private const string RingsItemId = "Rings";
        private static bool _itemAdded = false;
        private static RingNavigationElement _navigationElement;

        /// <summary>
        /// Adds the Rings navigation item to MapNavigationVM.NavigationItems.
        /// </summary>
        public static void AddRingsNavigationItem(MapNavigationVM navigationVM)
        {
            if (_itemAdded) return;
            if (navigationVM == null) return;

            try
            {
                var navigationItems = navigationVM.NavigationItems;
                if (navigationItems == null) return;

                // Check if already added
                foreach (var item in navigationItems)
                {
                    if (item.ItemId == RingsItemId)
                    {
                        _itemAdded = true;
                        return;
                    }
                }

                // Create navigation element (shared between UI and handler)
                _navigationElement = new RingNavigationElement();

                // Create MapNavigationItemVM using INavigationElement
                var ringsItem = new MapNavigationItemVM(_navigationElement);

                // Insert before the last item
                int insertIndex = navigationItems.Count > 0 ? navigationItems.Count - 1 : 0;
                navigationItems.Insert(insertIndex, ringsItem);
                _itemAdded = true;

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] Added Rings button at index {insertIndex}! Count: {navigationItems.Count}", Colors.Green));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] AddRingsNavigationItem failed: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Injects our navigation element into the MapNavigationHandler's _elements array.
        /// This is critical for IsAnyElementActive() to work properly.
        /// </summary>
        public static void InjectIntoNavigationHandler(MapScreen mapScreen)
        {
            if (_navigationElement == null) return;

            try
            {
                object handler = null;
                
                // Try NavigationHandler property first
                var navHandlerProp = typeof(MapScreen).GetProperty("NavigationHandler", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (navHandlerProp != null)
                {
                    handler = navHandlerProp.GetValue(mapScreen);
                }
                
                // Try _navigationHandler field
                if (handler == null)
                {
                    var navHandlerField = typeof(MapScreen).GetField("_navigationHandler",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (navHandlerField != null)
                    {
                        handler = navHandlerField.GetValue(mapScreen);
                    }
                }
                
                // Search all fields for INavigationHandler
                if (handler == null)
                {
                    var fields = typeof(MapScreen).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        var value = field.GetValue(mapScreen);
                        if (value is INavigationHandler)
                        {
                            handler = value;
                            break;
                        }
                    }
                }

                if (handler == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[RingSystem] Could not find NavigationHandler on MapScreen", Colors.Yellow));
                    return;
                }
                
                InjectElementIntoHandler(handler);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] InjectIntoNavigationHandler error: {ex.Message}", Colors.Red));
            }
        }

        private static void InjectElementIntoHandler(object handler)
        {
            if (handler == null) return;

            try
            {
                // Search for _elements field in the type hierarchy
                FieldInfo elementsField = null;
                Type currentType = handler.GetType();
                
                while (currentType != null && elementsField == null)
                {
                    elementsField = currentType.GetField("_elements",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    currentType = currentType.BaseType;
                }

                if (elementsField == null)
                {
                    // Try without DeclaredOnly as fallback
                    elementsField = handler.GetType().GetField("_elements",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (elementsField == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[RingSystem] Could not find _elements field in {handler.GetType().Name}", Colors.Yellow));
                    return;
                }

                var elements = elementsField.GetValue(handler) as INavigationElement[];
                if (elements == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[RingSystem] _elements is null", Colors.Yellow));
                    return;
                }

                // Check if already added
                foreach (var elem in elements)
                {
                    if (elem is RingNavigationElement)
                    {
                        return; // Already injected
                    }
                }

                // Create new array with our element added
                var newElements = new INavigationElement[elements.Length + 1];
                Array.Copy(elements, newElements, elements.Length);
                newElements[elements.Length] = _navigationElement;

                // Set the new array
                elementsField.SetValue(handler, newElements);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] Injected into NavigationHandler! Elements: {newElements.Length}", Colors.Green));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] InjectElementIntoHandler error: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Tries to inject from MapBarVM by finding MapNavigation property.
        /// </summary>
        public static void TryInjectFromMapBarVM(MapBarVM mapBarVM)
        {
            if (_itemAdded) return;
            if (mapBarVM == null) return;

            try
            {
                var mapBarType = mapBarVM.GetType();
                
                // Try property first
                var mapNavProp = mapBarType.GetProperty("MapNavigation", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (mapNavProp != null)
                {
                    var mapNavVM = mapNavProp.GetValue(mapBarVM) as MapNavigationVM;
                    if (mapNavVM != null)
                    {
                        AddRingsNavigationItem(mapNavVM);
                        return;
                    }
                }

                // Try fields
                var fields = mapBarType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    var value = field.GetValue(mapBarVM);
                    if (value is MapNavigationVM navVM)
                    {
                        AddRingsNavigationItem(navVM);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] TryInjectFromMapBarVM error: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Resets the injection state.
        /// </summary>
        public static void Reset()
        {
            _itemAdded = false;
            _navigationElement = null;
        }
    }

    /// <summary>
    /// Harmony patches to integrate Ring System.
    /// </summary>
    [HarmonyPatch]
    public static class RingSystemPatches
    {
        /// <summary>
        /// Patch GauntletMapBarGlobalLayer.Initialize to inject our navigation item.
        /// This is the primary injection point for the MapBar Rings button.
        /// </summary>
        [HarmonyPatch(typeof(GauntletMapBarGlobalLayer), "Initialize")]
        public static class GauntletMapBarGlobalLayer_Initialize_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(GauntletMapBarGlobalLayer __instance, MapBarVM dataSource)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] GauntletMapBarGlobalLayer.Initialize patch fired!", Colors.Cyan));
                    
                // Inject into UI ViewModel
                RingNavigationInjector.TryInjectFromMapBarVM(dataSource);
                
                // Also inject into the NavigationHandler for IsAnyElementActive() to work
                // Get _mapScreen field from GauntletMapBarGlobalLayer
                try
                {
                    var mapScreenField = typeof(GauntletMapBarGlobalLayer).GetField("_mapScreen",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (mapScreenField != null)
                    {
                        var mapScreen = mapScreenField.GetValue(__instance) as MapScreen;
                        if (mapScreen != null)
                        {
                            RingNavigationInjector.InjectIntoNavigationHandler(mapScreen);
                        }
                    }
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[RingSystem] Error getting MapScreen: {ex.Message}", Colors.Red));
                }
            }
        }

        /// <summary>
        /// Patch GameStateScreenManager.CreateScreen to return our screen for HoNRingState.
        /// This is the key fix - the framework can't find [GameStateScreen] in mod assemblies,
        /// so we intercept and provide the screen ourselves.
        /// </summary>
        [HarmonyPatch(typeof(GameStateScreenManager), "CreateScreen")]
        public static class GameStateScreenManager_CreateScreen_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(GameState state, ref ScreenBase __result)
            {
                // Only intercept for our HoNRingState
                if (state is HoNRingState ringState)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[RingSystem] CreateScreen intercepted - creating GauntletRingScreen", Colors.Cyan));
                    
                    __result = new GauntletRingScreen(ringState);
                    return false; // Skip original method
                }
                
                return true; // Let original method handle other states
            }
        }

        /// <summary>
        /// Patch MapScreen.OnFrameTick for R key hotkey.
        /// </summary>
        [HarmonyPatch(typeof(MapScreen), "OnFrameTick")]
        public static class MapScreen_OnFrameTick_Patch
        {
            private static bool _injectionAttempted = false;
            
            [HarmonyPostfix]
            public static void Postfix(MapScreen __instance)
            {
                try
                {
                    // Try to inject navigation button on first frame tick if not done yet
                    if (!_injectionAttempted)
                    {
                        _injectionAttempted = true;
                        
                        // Try to find MapBarVM through reflection
                        try
                        {
                            // Look for any field containing MapBarVM
                            var fields = __instance.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            foreach (var field in fields)
                            {
                                var value = field.GetValue(__instance);
                                if (value is MapBarVM mapBarVM)
                                {
                                    RingNavigationInjector.TryInjectFromMapBarVM(mapBarVM);
                                    InformationManager.DisplayMessage(new InformationMessage(
                                        $"[RingSystem] Found MapBarVM in field {field.Name}", Colors.Cyan));
                                    break;
                                }
                            }
                            
                            // Also inject into navigation handler
                            RingNavigationInjector.InjectIntoNavigationHandler(__instance);
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"[RingSystem] Navigation injection failed: {ex.Message}", Colors.Red));
                        }
                    }
                    
                    if (!RingScreenManager.IsOpen &&
                        !__instance.IsInArmyManagement &&
                        !__instance.IsInTownManagement &&
                        !__instance.IsInHideoutTroopManage &&
                        !__instance.IsEscapeMenuOpened &&
                        !__instance.IsMarriageOfferPopupActive)
                    {
                        bool noModifiers = !Input.IsKeyDown(InputKey.LeftControl) && 
                                          !Input.IsKeyDown(InputKey.RightControl) &&
                                          !Input.IsKeyDown(InputKey.LeftShift) &&
                                          !Input.IsKeyDown(InputKey.RightShift) &&
                                          !Input.IsKeyDown(InputKey.LeftAlt) &&
                                          !Input.IsKeyDown(InputKey.RightAlt);

                        if (noModifiers && Input.IsKeyPressed(InputKey.R))
                        {
                            RingScreenManager.OpenRingScreen();
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Patch MapScreen.OnFinalize to reset state.
        /// </summary>
        [HarmonyPatch(typeof(MapScreen), "OnFinalize")]
        public static class MapScreen_OnFinalize_Patch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                // Reset injection flag when map screen closes
                typeof(MapScreen_OnFrameTick_Patch)
                    .GetField("_injectionAttempted", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.SetValue(null, false);
                    
                RingNavigationInjector.Reset();
            }
        }
    }
}
