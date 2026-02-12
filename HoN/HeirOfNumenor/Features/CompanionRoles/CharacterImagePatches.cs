using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Party;
using System;
using System.Collections.Generic;

namespace HeirOfNumenor.Features.CompanionRoles
{
    /// <summary>
    /// Patches widget classes to add role indicator overlays on character portraits.
    /// Uses native StringItemWithHintVM pattern for proper icon display.
    /// </summary>
    public static class CharacterImagePatches
    {
        private const string FEATURE_NAME = "CompanionRoles";
        
        // Track widgets we've already processed
        private static readonly HashSet<int> _processedWidgets = new HashSet<int>();

        /// <summary>
        /// Check if Companion Roles feature is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableCompanionRoles; }
                catch { return true; }
            }
        }

        /// <summary>
        /// Check if role icons should be shown.
        /// </summary>
        public static bool ShowRoleIcons
        {
            get
            {
                try { return IsEnabled && ModSettings.Get().ShowRoleIcons; }
                catch { return true; }
            }
        }

        // Sprite paths for different roles (following native pattern)
        private static readonly Dictionary<CompanionRoleDetector.CombatRole, string> RoleSpriteNames = new Dictionary<CompanionRoleDetector.CombatRole, string>
        {
            { CompanionRoleDetector.CombatRole.Archer, "General\\TroopTypeIcons\\icon_troop_type_ranged" },
            { CompanionRoleDetector.CombatRole.Crossbow, "General\\TroopTypeIcons\\icon_troop_type_ranged" },
            { CompanionRoleDetector.CombatRole.ShieldInfantry, "General\\TroopTypeIcons\\icon_troop_type_infantry" },
            { CompanionRoleDetector.CombatRole.TwoHanded, "General\\TroopTypeIcons\\icon_troop_type_infantry" },
            { CompanionRoleDetector.CombatRole.Polearm, "General\\TroopTypeIcons\\icon_troop_type_infantry" },
            { CompanionRoleDetector.CombatRole.Cavalry, "General\\TroopTypeIcons\\icon_troop_type_cavalry" },
            { CompanionRoleDetector.CombatRole.HorseArcher, "General\\TroopTypeIcons\\icon_troop_type_horse_archer" },
            { CompanionRoleDetector.CombatRole.Skirmisher, "General\\TroopTypeIcons\\icon_troop_type_ranged" },
        };

        /// <summary>
        /// Patch PartyCharacterVM to inject role icon data using native pattern.
        /// </summary>
        [HarmonyPatch(typeof(PartyCharacterVM))]
        public static class PartyCharacterVM_Patches
        {
            /// <summary>
            /// Patch RefreshValues to add role indicator.
            /// </summary>
            [HarmonyPatch("RefreshValues")]
            [HarmonyPostfix]
            public static void RefreshValues_Postfix(PartyCharacterVM __instance)
            {
                if (!ShowRoleIcons) return;
                
                SafeExecutor.WrapPatch(FEATURE_NAME, "RefreshValues", () =>
                {
                    UpdateRoleIndicator(__instance);
                });
            }

            private static void UpdateRoleIndicator(PartyCharacterVM vm)
            {
                try
                {
                    // Only for heroes
                    if (vm.Character == null || !vm.Character.IsHero)
                        return;

                    Hero hero = vm.Character.HeroObject;
                    if (hero == null || hero == Hero.MainHero)
                        return;

                    // Only show for companions and clan members
                    if (!hero.IsPlayerCompanion && hero.Clan != Clan.PlayerClan)
                        return;

                    var role = CompanionRoleDetector.GetPrimaryRole(hero);
                    if (role == CompanionRoleDetector.CombatRole.Unknown)
                        return;

                    // Try to set TypeIconData via reflection (native property)
                    if (RoleSpriteNames.TryGetValue(role, out string spritePath))
                    {
                        string hintText = GetRoleHint(role, hero);
                        
                        // Use reflection to set TypeIconData (StringItemWithHintVM)
                        var typeIconProp = AccessTools.Property(typeof(PartyCharacterVM), "TypeIconData");
                        if (typeIconProp != null)
                        {
                            var stringItemType = AccessTools.TypeByName("TaleWorlds.Core.ViewModelCollection.Generic.StringItemWithHintVM");
                            if (stringItemType != null)
                            {
                                var textHint = new TextObject(hintText);
                                var constructor = AccessTools.Constructor(stringItemType, new Type[] { typeof(string), typeof(TextObject) });
                                if (constructor != null)
                                {
                                    var iconData = constructor.Invoke(new object[] { spritePath, textHint });
                                    typeIconProp.SetValue(vm, iconData);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModSettings.DebugLog(FEATURE_NAME, $"UpdateRoleIndicator error: {ex.Message}");
                }
            }

            private static string GetRoleHint(CompanionRoleDetector.CombatRole role, Hero hero)
            {
                string baseHint = role switch
                {
                    CompanionRoleDetector.CombatRole.Archer => "Best suited for: Archer formations",
                    CompanionRoleDetector.CombatRole.Crossbow => "Best suited for: Crossbow formations",
                    CompanionRoleDetector.CombatRole.ShieldInfantry => "Best suited for: Shieldwall formations",
                    CompanionRoleDetector.CombatRole.TwoHanded => "Best suited for: Heavy infantry",
                    CompanionRoleDetector.CombatRole.Polearm => "Best suited for: Polearm formations",
                    CompanionRoleDetector.CombatRole.Cavalry => "Best suited for: Cavalry charges",
                    CompanionRoleDetector.CombatRole.HorseArcher => "Best suited for: Horse archer tactics",
                    CompanionRoleDetector.CombatRole.Skirmisher => "Best suited for: Skirmisher tactics",
                    _ => "Combat specialist"
                };

                bool isMounted = CompanionRoleDetector.IsMounted(hero);
                if (isMounted && role != CompanionRoleDetector.CombatRole.Cavalry && 
                    role != CompanionRoleDetector.CombatRole.HorseArcher)
                {
                    baseHint += " (Has mount)";
                }

                return baseHint;
            }
        }

        /// <summary>
        /// Patch PartyTroopTupleButtonWidget for direct widget overlay (fallback approach).
        /// </summary>
        [HarmonyPatch(typeof(PartyTroopTupleButtonWidget), "RefreshState")]
        public static class PartyTroopTupleButtonWidget_RefreshState_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(PartyTroopTupleButtonWidget __instance)
            {
                SafeExecutor.WrapPatch(FEATURE_NAME, "RefreshState", () =>
                {
                    if (!ShowRoleIcons) return;
                    
                    // Skip if main hero
                    if (__instance.IsMainHero)
                        return;

                    // Check if this is a hero by looking up the character
                    string characterId = __instance.CharacterID;
                    if (string.IsNullOrEmpty(characterId)) return;
                    
                    Hero hero = FindHeroByCharacterId(characterId);
                    if (hero == null) return; // Not a hero

                    AddRoleIndicatorOverlay(__instance, characterId);
                });
            }
        }

        private static void AddRoleIndicatorOverlay(PartyTroopTupleButtonWidget parentWidget, string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;

            int widgetHash = parentWidget.GetHashCode();

            Hero hero = FindHeroByCharacterId(characterId);
            if (hero == null || hero == Hero.MainHero) return;

            if (!hero.IsPlayerCompanion && hero.Clan != Clan.PlayerClan)
                return;

            var role = CompanionRoleDetector.GetPrimaryRole(hero);
            if (role == CompanionRoleDetector.CombatRole.Unknown)
                return;

            // Find the Main widget (Id="Main") which contains the character image
            Widget mainWidget = FindWidgetById(parentWidget, "Main");
            if (mainWidget == null)
            {
                // Fallback: try to find by structure (widget_2 is typically 3rd child)
                mainWidget = parentWidget.ChildCount > 2 ? parentWidget.GetChild(2) : null;
            }
            
            Widget targetContainer = mainWidget ?? parentWidget;

            // Check for existing indicator
            Widget existingIndicator = FindIndicatorRecursive(targetContainer);
            if (existingIndicator != null)
            {
                UpdateIndicatorContent(existingIndicator, role, hero);
                existingIndicator.IsVisible = true;
                return;
            }

            // Skip if already processed
            if (_processedWidgets.Contains(widgetHash))
                return;

            try
            {
                string roleText = CompanionRoleDetector.GetRoleShortText(role);
                bool isMounted = CompanionRoleDetector.IsMounted(hero);
                
                // Get role color for background
                uint roleColor = CompanionRoleDetector.GetRoleColor(role);

                // Create container for indicator (positioned at top-left of character image)
                var containerWidget = new Widget(parentWidget.Context);
                containerWidget.WidthSizePolicy = SizePolicy.Fixed;
                containerWidget.HeightSizePolicy = SizePolicy.Fixed;
                containerWidget.SuggestedWidth = 24;
                containerWidget.SuggestedHeight = 16;
                containerWidget.HorizontalAlignment = HorizontalAlignment.Left;
                containerWidget.VerticalAlignment = VerticalAlignment.Top;
                containerWidget.MarginLeft = 2;
                containerWidget.MarginTop = 2;
                containerWidget.Id = "RoleIndicatorContainer";

                // Create background brush widget
                var bgWidget = new BrushWidget(parentWidget.Context);
                bgWidget.WidthSizePolicy = SizePolicy.StretchToParent;
                bgWidget.HeightSizePolicy = SizePolicy.StretchToParent;
                try
                {
                    // Try to use a dark background brush
                    var bgBrush = parentWidget.Context.GetBrush("SPGeneral.Tooltip.Background");
                    if (bgBrush != null)
                        bgWidget.Brush = bgBrush;
                }
                catch { }
                containerWidget.AddChild(bgWidget);

                // Create text indicator
                var textWidget = new TextWidget(parentWidget.Context);
                textWidget.WidthSizePolicy = SizePolicy.StretchToParent;
                textWidget.HeightSizePolicy = SizePolicy.StretchToParent;
                textWidget.HorizontalAlignment = HorizontalAlignment.Center;
                textWidget.VerticalAlignment = VerticalAlignment.Center;
                textWidget.Text = roleText + (isMounted ? "⚔" : "");
                textWidget.Id = "RoleIndicatorText";
                
                // Apply text brush
                try
                {
                    var textBrush = parentWidget.Context.GetBrush("Party.TroopCount.Text");
                    if (textBrush != null)
                        textWidget.Brush = textBrush;
                }
                catch { }

                containerWidget.AddChild(textWidget);
                targetContainer.AddChild(containerWidget);
                _processedWidgets.Add(widgetHash);
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog(FEATURE_NAME, $"AddRoleIndicatorOverlay error: {ex.Message}");
            }
        }

        private static Widget FindWidgetById(Widget parent, string id)
        {
            // Use common utility
            return CommonUtilities.FindWidgetById(parent, id);
        }

        private static Widget FindIndicatorRecursive(Widget parent)
        {
            if (parent.Id == "RoleIndicatorContainer")
                return parent;

            foreach (var child in parent.Children)
            {
                if (child.Id == "RoleIndicatorContainer")
                    return child;
                
                // Only go one level deep to avoid performance issues
            }
            return null;
        }

        private static void UpdateIndicatorContent(Widget indicator, CompanionRoleDetector.CombatRole role, Hero hero)
        {
            // Find text widget inside container
            foreach (var child in indicator.Children)
            {
                if (child is TextWidget textWidget)
                {
                    string roleText = CompanionRoleDetector.GetRoleShortText(role);
                    bool isMounted = CompanionRoleDetector.IsMounted(hero);
                    textWidget.Text = roleText + (isMounted ? "⚔" : "");
                    return;
                }
            }
        }

        private static Hero FindHeroByCharacterId(string characterId)
        {
            // Use common utility
            return CommonUtilities.FindHeroByCharacterId(characterId);
        }

        /// <summary>
        /// Clear processed widgets when entering new screens.
        /// </summary>
        public static void ClearProcessedWidgets()
        {
            _processedWidgets.Clear();
        }
    }
}
