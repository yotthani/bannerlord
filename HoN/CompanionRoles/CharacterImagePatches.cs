using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Party;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Core;

namespace CompanionRoles
{
    /// <summary>
    /// Patches widget classes to add role indicator overlays on character portraits.
    /// All settings are read from CompanionRolesSettings (independent of HoN).
    /// </summary>
    public static class CharacterImagePatches
    {
        private static readonly HashSet<int> _processedWidgets = new HashSet<int>();

        public static bool IsEnabled
        {
            get
            {
                try { return CompanionRolesSettings.Get().EnableCompanionRoles; }
                catch { return true; }
            }
        }

        public static bool ShowRoleIcons
        {
            get
            {
                try { return IsEnabled && CompanionRolesSettings.Get().ShowRoleIcons; }
                catch { return true; }
            }
        }

        private static readonly Dictionary<CompanionRoleDetector.CombatRole, string> RoleSpriteNames =
            new Dictionary<CompanionRoleDetector.CombatRole, string>
        {
            { CompanionRoleDetector.CombatRole.Archer,         "General\\TroopTypeIcons\\icon_troop_type_ranged" },
            { CompanionRoleDetector.CombatRole.Crossbow,       "General\\TroopTypeIcons\\icon_troop_type_ranged" },
            { CompanionRoleDetector.CombatRole.ShieldInfantry, "General\\TroopTypeIcons\\icon_troop_type_infantry" },
            { CompanionRoleDetector.CombatRole.TwoHanded,      "General\\TroopTypeIcons\\icon_troop_type_infantry" },
            { CompanionRoleDetector.CombatRole.Polearm,        "General\\TroopTypeIcons\\icon_troop_type_infantry" },
            { CompanionRoleDetector.CombatRole.Cavalry,        "General\\TroopTypeIcons\\icon_troop_type_cavalry" },
            { CompanionRoleDetector.CombatRole.HorseArcher,    "General\\TroopTypeIcons\\icon_troop_type_horse_archer" },
            { CompanionRoleDetector.CombatRole.Skirmisher,     "General\\TroopTypeIcons\\icon_troop_type_ranged" },
        };

        [HarmonyPatch(typeof(PartyCharacterVM))]
        public static class PartyCharacterVM_Patches
        {
            [HarmonyPatch("RefreshValues")]
            [HarmonyPostfix]
            public static void RefreshValues_Postfix(PartyCharacterVM __instance)
            {
                if (!ShowRoleIcons) return;
                try { UpdateRoleIndicator(__instance); }
                catch (Exception ex) { CompanionRolesLog.Error("RefreshValues", ex); }
            }

            private static void UpdateRoleIndicator(PartyCharacterVM vm)
            {
                if (vm.Character == null || !vm.Character.IsHero) return;

                Hero hero = vm.Character.HeroObject;
                if (hero == null || hero == Hero.MainHero) return;
                if (!hero.IsPlayerCompanion && hero.Clan != Clan.PlayerClan) return;

                var role = CompanionRoleDetector.GetPrimaryRole(hero);
                if (role == CompanionRoleDetector.CombatRole.Unknown) return;

                if (!RoleSpriteNames.TryGetValue(role, out string spritePath)) return;

                string hintText = GetRoleHint(role, hero);

                var typeIconProp = AccessTools.Property(typeof(PartyCharacterVM), "TypeIconData");
                if (typeIconProp == null) return;

                var stringItemType = AccessTools.TypeByName("TaleWorlds.Core.ViewModelCollection.Generic.StringItemWithHintVM");
                if (stringItemType == null) return;

                var textHint = new TextObject(hintText);
                var constructor = AccessTools.Constructor(stringItemType, new Type[] { typeof(string), typeof(TextObject) });
                if (constructor == null) return;

                var iconData = constructor.Invoke(new object[] { spritePath, textHint });
                typeIconProp.SetValue(vm, iconData);
            }

            private static string GetRoleHint(CompanionRoleDetector.CombatRole role, Hero hero)
            {
                string baseHint = role switch
                {
                    CompanionRoleDetector.CombatRole.Archer         => "Best suited for: Archer formations",
                    CompanionRoleDetector.CombatRole.Crossbow       => "Best suited for: Crossbow formations",
                    CompanionRoleDetector.CombatRole.ShieldInfantry => "Best suited for: Shieldwall formations",
                    CompanionRoleDetector.CombatRole.TwoHanded      => "Best suited for: Heavy infantry",
                    CompanionRoleDetector.CombatRole.Polearm        => "Best suited for: Polearm formations",
                    CompanionRoleDetector.CombatRole.Cavalry        => "Best suited for: Cavalry charges",
                    CompanionRoleDetector.CombatRole.HorseArcher    => "Best suited for: Horse archer tactics",
                    CompanionRoleDetector.CombatRole.Skirmisher     => "Best suited for: Skirmisher tactics",
                    _ => "Combat specialist"
                };

                if (CompanionRoleDetector.IsMounted(hero) &&
                    role != CompanionRoleDetector.CombatRole.Cavalry &&
                    role != CompanionRoleDetector.CombatRole.HorseArcher)
                {
                    baseHint += " (Has mount)";
                }

                return baseHint;
            }
        }

        [HarmonyPatch(typeof(PartyTroopTupleButtonWidget), "RefreshState")]
        public static class PartyTroopTupleButtonWidget_RefreshState_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(PartyTroopTupleButtonWidget __instance)
            {
                if (!ShowRoleIcons) return;
                try
                {
                    if (__instance.IsMainHero) return;

                    string characterId = __instance.CharacterID;
                    if (string.IsNullOrEmpty(characterId)) return;

                    Hero hero = FindHeroByCharacterId(characterId);
                    if (hero == null) return;

                    AddRoleIndicatorOverlay(__instance, characterId);
                }
                catch (Exception ex) { CompanionRolesLog.Error("RefreshState", ex); }
            }
        }

        private static void AddRoleIndicatorOverlay(PartyTroopTupleButtonWidget parentWidget, string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;

            int widgetHash = parentWidget.GetHashCode();

            Hero hero = FindHeroByCharacterId(characterId);
            if (hero == null || hero == Hero.MainHero) return;
            if (!hero.IsPlayerCompanion && hero.Clan != Clan.PlayerClan) return;

            var role = CompanionRoleDetector.GetPrimaryRole(hero);
            if (role == CompanionRoleDetector.CombatRole.Unknown) return;

            Widget mainWidget = FindWidgetById(parentWidget, "Main");
            if (mainWidget == null)
            {
                mainWidget = parentWidget.ChildCount > 2 ? parentWidget.GetChild(2) : null;
            }

            Widget targetContainer = mainWidget ?? parentWidget;

            Widget existingIndicator = FindIndicatorRecursive(targetContainer);
            if (existingIndicator != null)
            {
                UpdateIndicatorContent(existingIndicator, role, hero);
                existingIndicator.IsVisible = true;
                return;
            }

            if (_processedWidgets.Contains(widgetHash)) return;

            try
            {
                string roleText = CompanionRoleDetector.GetRoleShortText(role);
                bool isMounted = CompanionRoleDetector.IsMounted(hero);

                var containerWidget = new Widget(parentWidget.Context)
                {
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = 24,
                    SuggestedHeight = 16,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    MarginLeft = 2,
                    MarginTop = 2,
                    Id = "RoleIndicatorContainer"
                };

                var bgWidget = new BrushWidget(parentWidget.Context)
                {
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent
                };
                try
                {
                    var bgBrush = parentWidget.Context.GetBrush("SPGeneral.Tooltip.Background");
                    if (bgBrush != null) bgWidget.Brush = bgBrush;
                }
                catch { }
                containerWidget.AddChild(bgWidget);

                var textWidget = new TextWidget(parentWidget.Context)
                {
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = roleText + (isMounted ? "⚔" : ""),
                    Id = "RoleIndicatorText"
                };
                try
                {
                    var textBrush = parentWidget.Context.GetBrush("Party.TroopCount.Text");
                    if (textBrush != null) textWidget.Brush = textBrush;
                }
                catch { }

                containerWidget.AddChild(textWidget);
                targetContainer.AddChild(containerWidget);
                _processedWidgets.Add(widgetHash);
            }
            catch (Exception ex)
            {
                CompanionRolesLog.Error("AddRoleIndicatorOverlay", ex);
            }
        }

        // ── helpers (ported from HoN CommonUtilities) ───────────────────────────
        private static Widget FindWidgetById(Widget parent, string id)
        {
            if (parent == null || string.IsNullOrEmpty(id)) return null;
            try
            {
                if (parent.Id == id) return parent;
                foreach (var child in parent.Children)
                {
                    var found = FindWidgetById(child, id);
                    if (found != null) return found;
                }
            }
            catch { }
            return null;
        }

        private static Widget FindIndicatorRecursive(Widget parent)
        {
            if (parent.Id == "RoleIndicatorContainer") return parent;
            foreach (var child in parent.Children)
            {
                if (child.Id == "RoleIndicatorContainer") return child;
            }
            return null;
        }

        private static void UpdateIndicatorContent(Widget indicator, CompanionRoleDetector.CombatRole role, Hero hero)
        {
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
            if (string.IsNullOrEmpty(characterId)) return null;
            try
            {
                var character = MBObjectManager.Instance?.GetObject<CharacterObject>(characterId);
                if (character?.IsHero == true) return character.HeroObject;

                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero.CharacterObject?.StringId == characterId) return hero;
                }
            }
            catch { }
            return null;
        }

        public static void ClearProcessedWidgets()
        {
            _processedWidgets.Clear();
        }
    }
}
