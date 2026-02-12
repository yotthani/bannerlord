using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace HeirOfNumenor
{
    /// <summary>
    /// Common utility methods used across multiple features.
    /// Centralizes repeated patterns to reduce code duplication.
    /// </summary>
    public static class CommonUtilities
    {
        #region Hero Utilities

        /// <summary>
        /// Finds a hero by their character string ID.
        /// </summary>
        public static Hero FindHeroByCharacterId(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return null;

            try
            {
                // First, try direct character lookup
                var character = MBObjectManager.Instance?.GetObject<CharacterObject>(characterId);
                if (character?.IsHero == true)
                    return character.HeroObject;

                // Try from all heroes
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero.CharacterObject?.StringId == characterId)
                        return hero;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Finds a hero by predicate.
        /// </summary>
        public static Hero FindHero(Func<Hero, bool> predicate)
        {
            try
            {
                return Hero.AllAliveHeroes.FirstOrDefault(predicate);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets all companion heroes in the player's party.
        /// </summary>
        public static IEnumerable<Hero> GetPlayerCompanions()
        {
            try
            {
                if (MobileParty.MainParty == null) yield break;

                foreach (var element in MobileParty.MainParty.MemberRoster.GetTroopRoster())
                {
                    if (element.Character?.IsHero == true && 
                        element.Character.HeroObject != Hero.MainHero)
                    {
                        yield return element.Character.HeroObject;
                    }
                }
            }
            finally { }
        }

        /// <summary>
        /// Checks if a hero is a player companion.
        /// </summary>
        public static bool IsPlayerCompanion(Hero hero)
        {
            return hero != null && 
                   hero != Hero.MainHero && 
                   (hero.IsPlayerCompanion || hero.Clan == Clan.PlayerClan);
        }

        #endregion

        #region Widget Utilities

        /// <summary>
        /// Finds a widget by ID recursively.
        /// </summary>
        public static Widget FindWidgetById(Widget parent, string id)
        {
            if (parent == null || string.IsNullOrEmpty(id))
                return null;

            try
            {
                if (parent.Id == id)
                    return parent;

                foreach (var child in parent.Children)
                {
                    var found = FindWidgetById(child, id);
                    if (found != null)
                        return found;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Finds all widgets matching a predicate recursively.
        /// </summary>
        public static IEnumerable<Widget> FindWidgets(Widget parent, Func<Widget, bool> predicate)
        {
            if (parent == null) yield break;

            try
            {
                if (predicate(parent))
                    yield return parent;

                foreach (var child in parent.Children)
                {
                    foreach (var found in FindWidgets(child, predicate))
                        yield return found;
                }
            }
            finally { }
        }

        /// <summary>
        /// Gets or creates a child widget with the specified ID.
        /// </summary>
        public static T GetOrCreateWidget<T>(Widget parent, string id, Func<T> factory) where T : Widget
        {
            var existing = FindWidgetById(parent, id) as T;
            if (existing != null)
                return existing;

            var newWidget = factory();
            if (newWidget != null)
            {
                newWidget.Id = id;
                parent.AddChild(newWidget);
            }
            return newWidget;
        }

        #endregion

        #region Party Utilities

        /// <summary>
        /// Gets the main party, or null if not available.
        /// </summary>
        public static MobileParty MainParty => MobileParty.MainParty;

        /// <summary>
        /// Checks if the main party exists and is valid.
        /// </summary>
        public static bool HasMainParty => MobileParty.MainParty != null;

        /// <summary>
        /// Gets all troops in the main party (non-hero).
        /// </summary>
        public static IEnumerable<(CharacterObject Character, int Count)> GetMainPartyTroops()
        {
            if (!HasMainParty) yield break;

            try
            {
                foreach (var element in MobileParty.MainParty.MemberRoster.GetTroopRoster())
                {
                    if (element.Character != null && !element.Character.IsHero)
                    {
                        yield return (element.Character, element.Number);
                    }
                }
            }
            finally { }
        }

        /// <summary>
        /// Counts total troops (non-hero) in main party.
        /// </summary>
        public static int GetMainPartyTroopCount()
        {
            if (!HasMainParty) return 0;

            try
            {
                return MobileParty.MainParty.MemberRoster.GetTroopRoster()
                    .Where(e => e.Character != null && !e.Character.IsHero)
                    .Sum(e => e.Number);
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Message Utilities

        /// <summary>
        /// Shows a message in the game log.
        /// </summary>
        public static void ShowMessage(string text, Color? color = null)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(text, color ?? Colors.White));
            }
            catch { }
        }

        /// <summary>
        /// Shows a success message (green).
        /// </summary>
        public static void ShowSuccess(string text)
        {
            ShowMessage(text, Colors.Green);
        }

        /// <summary>
        /// Shows a warning message (yellow).
        /// </summary>
        public static void ShowWarning(string text)
        {
            ShowMessage(text, Colors.Yellow);
        }

        /// <summary>
        /// Shows an error message (red).
        /// </summary>
        public static void ShowError(string text)
        {
            ShowMessage(text, Colors.Red);
        }

        /// <summary>
        /// Shows an info message (gray).
        /// </summary>
        public static void ShowInfo(string text)
        {
            ShowMessage(text, Colors.Gray);
        }

        #endregion

        #region Object Utilities

        /// <summary>
        /// Gets an object from MBObjectManager by ID.
        /// </summary>
        public static T GetObject<T>(string id) where T : MBObjectBase
        {
            if (string.IsNullOrEmpty(id))
                return null;

            try
            {
                return MBObjectManager.Instance?.GetObject<T>(id);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets an item by ID.
        /// </summary>
        public static ItemObject GetItem(string itemId)
        {
            return GetObject<ItemObject>(itemId);
        }

        /// <summary>
        /// Gets a character by ID.
        /// </summary>
        public static CharacterObject GetCharacter(string characterId)
        {
            return GetObject<CharacterObject>(characterId);
        }

        #endregion

        #region Math Utilities

        /// <summary>
        /// Clamps an integer value to a range.
        /// </summary>
        public static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        /// <summary>
        /// Clamps a float value to a range.
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        /// <summary>
        /// Normalizes a value from one range to another.
        /// </summary>
        public static float Normalize(float value, float inMin, float inMax, float outMin, float outMax)
        {
            if (inMax - inMin == 0) return outMin;
            float normalized = (value - inMin) / (inMax - inMin);
            return outMin + (normalized * (outMax - outMin));
        }

        /// <summary>
        /// Converts 0-100 scale to 0-5 (focus points style).
        /// </summary>
        public static int ToFocusScale(float percent)
        {
            return Clamp((int)(percent / 20f), 0, 5);
        }

        #endregion

        #region String Utilities

        /// <summary>
        /// Formats a number with sign prefix.
        /// </summary>
        public static string FormatWithSign(float value, string format = "F1")
        {
            return value >= 0 ? $"+{value.ToString(format)}" : value.ToString(format);
        }

        /// <summary>
        /// Formats a number with sign prefix (integer).
        /// </summary>
        public static string FormatWithSign(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString();
        }

        /// <summary>
        /// Truncates a string to max length with ellipsis.
        /// </summary>
        public static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        #endregion
    }
}
