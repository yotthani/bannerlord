using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace LOTRAOM.FactionMap
{
    /// <summary>
    /// Harmony patches to inject our FactionSelectionVM into the CharacterCreationCultureStage.
    ///
    /// The prefab is replaced by placing our version at the same path:
    /// GUI/Prefabs/CharacterCreation/CharacterCreationCultureStage.xml
    ///
    /// We only need to patch the ViewModel creation to use our FactionSelectionVM.
    /// </summary>
    public static class CultureStageViewPatches
    {
        private static FactionSelectionVM _factionVM;
        private static object _originalDataSource;

        // Possible namespaces for CharacterCreationCultureStageView in different BL versions
        private static readonly string[] PossibleTypeNames = new[]
        {
            "SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView",
            "SandBox.View.CharacterCreation.CharacterCreationCultureStageView",
        };

        private static Type FindCultureStageViewType()
        {
            foreach (var typeName in PossibleTypeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type != null)
                {
                    return type;
                }
            }
            SubModule.LogError("CharacterCreationCultureStageView type not found in any namespace!");
            return null;
        }

        /// <summary>
        /// Patch the constructor to inject our ViewModel after the movie loads.
        /// </summary>
        [HarmonyPatch]
        public static class ConstructorPatch
        {
            static bool Prepare()
            {
                return FindCultureStageViewType() != null;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var type = FindCultureStageViewType();
                if (type == null) return null;

                var ctors = type.GetConstructors();
                if (ctors.Length > 0)
                    return ctors[0];

                return null;
            }

            /// <summary>
            /// After the original constructor runs, we inject our ViewModel.
            /// The prefab is already replaced, so it will use our layout.
            /// We need to rebind to our ViewModel for the new properties.
            /// </summary>
            static void Postfix(object __instance)
            {
                try
                {
                    // Reset all static widget state from any previous session
                    Widgets.PolygonWidget.ResetSession();

                    // Get the GauntletLayer from the original
                    var layerField = AccessTools.Field(__instance.GetType(), "GauntletLayer");
                    var gauntletLayer = layerField?.GetValue(__instance) as GauntletLayer;

                    if (gauntletLayer == null)
                    {
                        SubModule.LogError("GauntletLayer not found in CultureStageView");
                        return;
                    }

                    // Get the original movie
                    var movieField = AccessTools.Field(__instance.GetType(), "_movie");
                    var originalMovie = movieField?.GetValue(__instance) as GauntletMovieIdentifier;

                    // Get original data source
                    var dataSourceField = AccessTools.Field(__instance.GetType(), "_dataSource");
                    _originalDataSource = dataSourceField?.GetValue(__instance);

                    if (originalMovie == null)
                    {
                        SubModule.LogError("Original movie not found");
                        return;
                    }

                    // Release the original movie
                    gauntletLayer.ReleaseMovie(originalMovie);

                    // Create callback that sets the culture and advances
                    Action<CultureObject> onCultureConfirmed = (culture) =>
                    {
                        if (culture == null)
                        {
                            SubModule.LogError("Culture is null!");
                            return;
                        }

                        SetCultureOnCharacterCreation(culture, __instance);

                        // Advance to next stage
                        var nextStageMethod = AccessTools.Method(__instance.GetType(), "NextStage");
                        nextStageMethod?.Invoke(__instance, null);
                    };

                    // Create callback to go back to the previous stage
                    Action onPreviousStage = () =>
                    {
                        var prevStageMethod = AccessTools.Method(__instance.GetType(), "PreviousStage");
                        prevStageMethod?.Invoke(__instance, null);
                    };

                    // Create our ViewModel
                    _factionVM = new FactionSelectionVM(onCultureConfirmed, onPreviousStage);

                    // Copy relevant properties from original ViewModel
                    if (_originalDataSource != null)
                    {
                        var titleProp = AccessTools.Property(_originalDataSource.GetType(), "Title");
                        if (titleProp != null)
                        {
                            _factionVM.Title = titleProp.GetValue(_originalDataSource) as string ?? "Wähle deine Kultur";
                        }
                    }

                    // Load custom brushes BEFORE movie (must be explicit for mod brushes)
                    // Root element MUST be <Brushes> not <ResourceDictionary>!
                    try
                    {
                        UIResourceManager.BrushFactory.LoadBrushFile("FactionMap");
                    }
                    catch (Exception brushEx)
                    {
                        SubModule.LogError($"Could not load FactionMap brushes: {brushEx.Message}");
                    }

                    // Load sprite categories BEFORE movie — even AlwaysLoad categories
                    // may need explicit loading in mod context
                    try
                    {
                        // ui_group1 contains frame_9, tooltip_frame_white_9, rounded_canvas_9, etc.
                        var spriteData = UIResourceManager.SpriteData;
                        if (spriteData != null && spriteData.SpriteCategories.ContainsKey("ui_group1"))
                        {
                            var cat = spriteData.SpriteCategories["ui_group1"];
                            if (!cat.IsLoaded)
                                cat.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
                        }
                        else
                        {
                            SubModule.LogError("SpriteData or ui_group1 category not found!");
                        }
                    }
                    catch (Exception spriteEx)
                    {
                        SubModule.LogError($"Could not load sprite category: {spriteEx.Message}");
                    }

                    // Load our movie with our ViewModel
                    var newMovie = gauntletLayer.LoadMovie("CharacterCreationCultureStage", _factionVM);
                    movieField?.SetValue(__instance, newMovie);

                    SubModule.Log($"FactionMap injected (movie={newMovie != null})");
                }
                catch (Exception ex)
                {
                    SubModule.LogError($"CultureStageView constructor patch error: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        private static void SetCultureOnCharacterCreation(CultureObject culture, object viewInstance)
        {
            try
            {
                // Get CharacterCreation instance via GameStateManager
                var activeState = TaleWorlds.Core.GameStateManager.Current?.ActiveState;
                if (activeState == null)
                {
                    SubModule.LogError("No active game state");
                    return;
                }

                // Get CharacterCreationManager from CharacterCreationState
                var charCreationProp = AccessTools.Property(activeState.GetType(), "CharacterCreationManager");
                var charCreation = charCreationProp?.GetValue(activeState);
                if (charCreation == null)
                {
                    SubModule.LogError("CharacterCreationManager not found in active state");
                    return;
                }

                // Get the CharacterCreationContent
                var contentProp = AccessTools.Property(charCreation.GetType(), "CharacterCreationContent");
                object content = contentProp?.GetValue(charCreation);
                if (content == null)
                {
                    var contentField = AccessTools.Field(charCreation.GetType(), "_characterCreationContent");
                    content = contentField?.GetValue(charCreation);
                }

                if (content != null)
                {
                    // Try to call SetSelectedCulture
                    var contentType = content.GetType();
                    var setCultureMethod = AccessTools.Method(contentType, "SetSelectedCulture");

                    if (setCultureMethod != null)
                    {
                        var parameters = setCultureMethod.GetParameters();
                        if (parameters.Length == 2)
                        {
                            setCultureMethod.Invoke(content, new object[] { culture, charCreation });
                        }
                        else if (parameters.Length == 1)
                        {
                            setCultureMethod.Invoke(content, new object[] { culture });
                        }
                    }
                    else
                    {
                        SubModule.LogError("SetSelectedCulture method not found");
                    }
                }
                else
                {
                    SubModule.LogError("CharacterCreationContent not found");
                }

                // Also set via the original view's dataSource if available
                if (_originalDataSource != null)
                {
                    var culturesField = AccessTools.Field(_originalDataSource.GetType(), "_cultures");
                    var culturesList = culturesField?.GetValue(_originalDataSource) as System.Collections.IList;

                    if (culturesList != null)
                    {
                        foreach (var cultureVM in culturesList)
                        {
                            var cultureIdProp = AccessTools.Property(cultureVM.GetType(), "CultureID");
                            var cultureId = cultureIdProp?.GetValue(cultureVM) as string;

                            if (cultureId == culture.StringId)
                            {
                                var selectMethod = AccessTools.Method(cultureVM.GetType(), "ExecuteSelectCulture");
                                selectMethod?.Invoke(cultureVM, null);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SubModule.LogError($"SetCultureOnCharacterCreation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch Tick to handle our input.
        /// </summary>
        [HarmonyPatch]
        public static class TickPatch
        {
            static bool Prepare()
            {
                return FindCultureStageViewType() != null;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var type = FindCultureStageViewType();
                return AccessTools.Method(type, "Tick", new[] { typeof(float) });
            }

            static void Postfix(object __instance, float dt)
            {
                if (_factionVM == null) return;

                // Poll hover state each frame for tooltip display
                _factionVM.Tick();

                // Get the GauntletLayer to check input
                var layerField = AccessTools.Field(__instance.GetType(), "GauntletLayer");
                var gauntletLayer = layerField?.GetValue(__instance) as GauntletLayer;

                if (gauntletLayer == null) return;

                // Enter to confirm (if playable faction selected)
                if (gauntletLayer.Input.IsHotKeyReleased("Confirm") && _factionVM.CanConfirm)
                {
                    _factionVM.ExecuteConfirm();
                }
            }
        }

        /// <summary>
        /// Patch OnFinalize to cleanup.
        /// </summary>
        [HarmonyPatch]
        public static class FinalizePatch
        {
            static bool Prepare()
            {
                return FindCultureStageViewType() != null;
            }

            static System.Reflection.MethodBase TargetMethod()
            {
                var type = FindCultureStageViewType();
                return AccessTools.Method(type, "OnFinalize");
            }

            static void Prefix()
            {
                _factionVM?.OnFinalize();
                _factionVM = null;
                _originalDataSource = null;
            }
        }
    }
}
