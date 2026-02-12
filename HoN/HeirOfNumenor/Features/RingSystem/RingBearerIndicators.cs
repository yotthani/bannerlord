using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Party;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Adds ring bearer indicators to various UI elements:
    /// - Map party nameplates
    /// - Encyclopedia hero pages
    /// - Party screen character cards
    /// </summary>
    public static class RingBearerIndicators
    {
        private const string FEATURE_NAME = "RingBearerIndicators";
        private const string RING_ICON = "🔮"; // Ring emoji for text indicators
        
        // Ring colors by type
        private static readonly Color ElvenRingColor = new Color(0.5f, 0.8f, 1.0f); // Light blue
        private static readonly Color DwarfRingColor = new Color(0.9f, 0.7f, 0.2f); // Gold
        private static readonly Color MortalRingColor = new Color(0.6f, 0.2f, 0.2f); // Dark red
        private static readonly Color OneRingColor = new Color(1.0f, 0.5f, 0.0f); // Orange/fire

        /// <summary>
        /// Get ring bearer info for a hero.
        /// </summary>
        public static RingBearerInfo GetRingBearerInfo(Hero hero)
        {
            // Early exit if campaign not ready - prevents crashes during initialization
            if (Campaign.Current == null)
                return null;
                
            if (hero == null)
                return null;

            // Check if MainHero exists before comparing
            if (Hero.MainHero == null)
                return null;

            try
            {
                var behavior = RingSystemCampaignBehavior.Instance;
                if (behavior == null)
                    return null;

                // Currently rings are only tracked for main hero
                // This can be expanded later to track per-hero
                if (hero != Hero.MainHero)
                    return null;

                // Check if any ring is equipped
                var equippedRings = behavior.GetEquippedRings();
                if (equippedRings == null || equippedRings.Count == 0)
                    return null;

                var equippedRing = equippedRings.FirstOrDefault();
                if (equippedRing == null)
                    return null;

                // Get ring details
                var ringId = equippedRing.StringId;
                var race = RingAttributes.GetRingRace(ringId);
                var effectTracker = behavior.PlayerEffects;
                
                float power = effectTracker?.GetRingPower(hero) ?? 0f;
                float corruption = effectTracker?.GetCorruptionLevel(hero) ?? 0f;

                return new RingBearerInfo
                {
                    Hero = hero,
                    RingName = equippedRing.Name?.ToString() ?? "Ring",
                    RingRace = race,
                    PowerLevel = power,
                    CorruptionLevel = corruption,
                    IsOneRing = race == RingAttributes.RingRace.OneRing
                };
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog("RingBearer", $"Error getting ring info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the indicator color for a ring race.
        /// </summary>
        public static Color GetRingColor(RingAttributes.RingRace race)
        {
            return race switch
            {
                RingAttributes.RingRace.Elven => ElvenRingColor,
                RingAttributes.RingRace.Dwarf => DwarfRingColor,
                RingAttributes.RingRace.Mortal => MortalRingColor,
                RingAttributes.RingRace.OneRing => OneRingColor,
                _ => Colors.Gray
            };
        }

        /// <summary>
        /// Get short ring type text.
        /// </summary>
        public static string GetRingTypeText(RingAttributes.RingRace race)
        {
            return race switch
            {
                RingAttributes.RingRace.Elven => "Elven Ring",
                RingAttributes.RingRace.Dwarf => "Dwarven Ring",
                RingAttributes.RingRace.Mortal => "Ring of Men",
                RingAttributes.RingRace.OneRing => "The One Ring",
                _ => "Ring"
            };
        }

        #region Map Nameplate Indicator

        /// <summary>
        /// Patch PartyNameplatesVM to add ring bearer indicator to party nameplates.
        /// </summary>
        [HarmonyPatch]
        public static class PartyNameplate_Patches
        {
            // We'll patch the RefreshDynamicProperties method to add ring indicator to party name/text
            [HarmonyPatch("SandBox.ViewModelCollection.Nameplate.PartyNameplateVM", "RefreshDynamicProperties")]
            [HarmonyPostfix]
            public static void RefreshDynamicProperties_Postfix(object __instance)
            {
                // Early exit if campaign not ready - prevents crashes during initialization
                if (Campaign.Current == null) return;
                if (Hero.MainHero == null) return;
                
                SafeExecutor.WrapPatch(FEATURE_NAME, "NameplateRefresh", () =>
                {
                    // Get the Party property
                    var partyProp = AccessTools.Property(__instance.GetType(), "Party");
                    if (partyProp == null) return;

                    var party = partyProp.GetValue(__instance) as MobileParty;
                    if (party == null) return;

                    // Check if leader is a ring bearer
                    var leader = party.LeaderHero;
                    if (leader == null) return;

                    var ringInfo = GetRingBearerInfo(leader);
                    if (ringInfo == null) return;

                    // Modify the ExtraInfoText to show ring indicator
                    var extraInfoProp = AccessTools.Property(__instance.GetType(), "ExtraInfoText");
                    if (extraInfoProp != null)
                    {
                        string currentText = extraInfoProp.GetValue(__instance) as string ?? "";
                        string ringIndicator = $"{RING_ICON} {GetRingTypeText(ringInfo.RingRace)}";
                        
                        if (!currentText.Contains(RING_ICON))
                        {
                            string newText = string.IsNullOrEmpty(currentText) 
                                ? ringIndicator 
                                : $"{currentText} | {ringIndicator}";
                            extraInfoProp.SetValue(__instance, newText);
                        }
                    }
                });
            }
        }

        #endregion

        #region Encyclopedia Indicator

        /// <summary>
        /// Patch Encyclopedia Hero Page to show ring bearer status.
        /// </summary>
        [HarmonyPatch(typeof(EncyclopediaHeroPageVM))]
        public static class EncyclopediaHeroPage_Patches
        {
            [HarmonyPatch("Refresh")]
            [HarmonyPostfix]
            public static void Refresh_Postfix(EncyclopediaHeroPageVM __instance)
            {
                // Early exit if campaign not ready
                if (Campaign.Current == null) return;
                if (Hero.MainHero == null) return;
                
                SafeExecutor.WrapPatch(FEATURE_NAME, "EncyclopediaRefresh", () =>
                {
                    // Get the hero from the page
                    var objProp = AccessTools.Property(typeof(EncyclopediaHeroPageVM), "Obj");
                    if (objProp == null) return;

                    var hero = objProp.GetValue(__instance) as Hero;
                    if (hero == null) return;

                    var ringInfo = GetRingBearerInfo(hero);
                    if (ringInfo == null) return;

                    // Try to add ring info to InformationText
                    var infoTextProp = AccessTools.Property(typeof(EncyclopediaHeroPageVM), "InformationText");
                    if (infoTextProp != null)
                    {
                        string currentInfo = infoTextProp.GetValue(__instance) as string ?? "";
                        
                        // Build ring bearer info
                        string ringStatus = $"\n\n[Ring Bearer]\nBears: {ringInfo.RingName}";
                        ringStatus += $"\nPower: {ringInfo.PowerLevel:F0}%";
                        if (ringInfo.CorruptionLevel > 0)
                        {
                            ringStatus += $"\nCorruption: {ringInfo.CorruptionLevel:F0}%";
                        }
                        
                        if (!currentInfo.Contains("[Ring Bearer]"))
                        {
                            infoTextProp.SetValue(__instance, currentInfo + ringStatus);
                        }
                    }
                });
            }
        }

        #endregion

        #region Party Screen Indicator

        /// <summary>
        /// Patch PartyCharacterVM to add ring bearer indicator.
        /// </summary>
        [HarmonyPatch(typeof(PartyCharacterVM))]
        public static class PartyCharacterVM_RingIndicator_Patches
        {
            [HarmonyPatch("RefreshValues")]
            [HarmonyPostfix]
            public static void RefreshValues_Postfix(PartyCharacterVM __instance)
            {
                // Early exit if campaign not ready
                if (Campaign.Current == null) return;
                if (Hero.MainHero == null) return;
                
                SafeExecutor.WrapPatch(FEATURE_NAME, "PartyCharacterRefresh", () =>
                {
                    // Only for heroes
                    if (__instance.Character == null || !__instance.Character.IsHero)
                        return;

                    Hero hero = __instance.Character.HeroObject;
                    if (hero == null) return;

                    var ringInfo = GetRingBearerInfo(hero);
                    if (ringInfo == null) return;

                    // Try to modify the Name property to include ring indicator
                    var nameProp = AccessTools.Property(typeof(PartyCharacterVM), "Name");
                    if (nameProp != null)
                    {
                        string currentName = nameProp.GetValue(__instance) as string ?? hero.Name.ToString();
                        
                        if (!currentName.Contains(RING_ICON))
                        {
                            string newName = $"{RING_ICON} {currentName}";
                            nameProp.SetValue(__instance, newName);
                        }
                    }

                    // Also add tooltip hint
                    var transferHintProp = AccessTools.Property(typeof(PartyCharacterVM), "TransferHint");
                    if (transferHintProp != null)
                    {
                        string hintText = $"Ring Bearer: {ringInfo.RingName}\n" +
                                        $"Power: {ringInfo.PowerLevel:F0}%\n" +
                                        $"Corruption: {ringInfo.CorruptionLevel:F0}%";
                        
                        var hint = new BasicTooltipViewModel(() => hintText);
                        // Note: We can't easily replace the hint, so we add to existing if possible
                    }
                });
            }
        }

        /// <summary>
        /// Widget patch to add visual ring indicator to party screen.
        /// </summary>
        [HarmonyPatch(typeof(PartyTroopTupleButtonWidget), "RefreshState")]
        public static class PartyTroopTupleWidget_RingIndicator_Patch
        {
            private static readonly HashSet<int> _processedWidgets = new HashSet<int>();

            [HarmonyPostfix]
            public static void Postfix(PartyTroopTupleButtonWidget __instance)
            {
                // Early exit if campaign not ready
                if (Campaign.Current == null) return;
                if (Hero.MainHero == null) return;
                
                SafeExecutor.WrapPatch(FEATURE_NAME, "WidgetRingIndicator", () =>
                {
                    string characterId = __instance.CharacterID;
                    if (string.IsNullOrEmpty(characterId)) return;

                    int widgetHash = __instance.GetHashCode();

                    // Find the hero - only process if it's a hero character
                    Hero hero = FindHeroByCharacterId(characterId);
                    if (hero == null) return;  // Not a hero or not found

                    var ringInfo = GetRingBearerInfo(hero);
                    
                    // Find or create indicator
                    Widget existingIndicator = FindRingIndicator(__instance);
                    
                    if (ringInfo == null)
                    {
                        // Hide indicator if exists
                        if (existingIndicator != null)
                            existingIndicator.IsVisible = false;
                        return;
                    }

                    if (existingIndicator != null)
                    {
                        // Update and show existing
                        existingIndicator.IsVisible = true;
                        UpdateRingIndicator(existingIndicator, ringInfo);
                        return;
                    }

                    // Skip if already tried to add
                    if (_processedWidgets.Contains(widgetHash))
                        return;

                    // Create new ring indicator
                    try
                    {
                        var indicator = new TextWidget(__instance.Context);
                        indicator.WidthSizePolicy = SizePolicy.CoverChildren;
                        indicator.HeightSizePolicy = SizePolicy.CoverChildren;
                        indicator.HorizontalAlignment = HorizontalAlignment.Right;
                        indicator.VerticalAlignment = VerticalAlignment.Top;
                        indicator.MarginRight = 4;
                        indicator.MarginTop = 4;
                        indicator.Text = RING_ICON;
                        indicator.Id = "RingBearerIndicator";
                        
                        // Try to apply brush
                        try
                        {
                            var brush = __instance.Context.GetBrush("Encyclopedia.SubInfo.ItemName");
                            if (brush != null)
                                indicator.Brush = brush;
                        }
                        catch { }

                        __instance.AddChild(indicator);
                        _processedWidgets.Add(widgetHash);
                    }
                    catch (Exception ex)
                    {
                        ModSettings.DebugLog(FEATURE_NAME, $"Failed to add ring indicator: {ex.Message}");
                    }
                });
            }

            private static Widget FindRingIndicator(Widget parent)
            {
                foreach (var child in parent.Children)
                {
                    if (child.Id == "RingBearerIndicator")
                        return child;
                }
                return null;
            }

            private static void UpdateRingIndicator(Widget indicator, RingBearerInfo info)
            {
                if (indicator is TextWidget textWidget)
                {
                    // Add corruption warning if high
                    string text = RING_ICON;
                    if (info.CorruptionLevel > 50)
                        text += "⚠";
                    textWidget.Text = text;
                }
            }

            private static Hero FindHeroByCharacterId(string characterId)
            {
                // Use common utility
                return CommonUtilities.FindHeroByCharacterId(characterId);
            }

            public static void ClearProcessedWidgets()
            {
                _processedWidgets.Clear();
            }
        }

        #endregion
    }

    /// <summary>
    /// Information about a ring bearer.
    /// </summary>
    public class RingBearerInfo
    {
        public Hero Hero { get; set; }
        public string RingName { get; set; }
        public RingAttributes.RingRace RingRace { get; set; }
        public float PowerLevel { get; set; }
        public float CorruptionLevel { get; set; }
        public bool IsOneRing { get; set; }
    }
}
