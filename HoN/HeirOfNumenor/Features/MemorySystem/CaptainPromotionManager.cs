using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace HeirOfNumenor.Features.MemorySystem
{
    /// <summary>
    /// Manages the rare promotion of legendary Virtual Captains to real Hero companions.
    /// This is a very special event that should feel meaningful and earned.
    /// Now requires user confirmation before promotion.
    /// </summary>
    public class CaptainPromotionManager
    {
        private const string FEATURE_NAME = "CaptainPromotion";

        // Pending promotions waiting for user confirmation
        private static readonly Queue<PendingPromotion> _pendingPromotions = new Queue<PendingPromotion>();
        private static bool _isShowingInquiry = false;

        // Character templates for different cultures
        private static readonly Dictionary<string, string[]> CultureTemplates = new Dictionary<string, string[]>
        {
            { "gondor", new[] { "aserai_wanderer", "empire_wanderer" } },
            { "rohan", new[] { "khuzait_wanderer", "battania_wanderer" } },
            { "elf", new[] { "battania_wanderer", "empire_wanderer" } },
            { "dwarf", new[] { "sturgia_wanderer", "vlandia_wanderer" } },
            { "mordor", new[] { "aserai_wanderer", "khuzait_wanderer" } },
            { "isengard", new[] { "sturgia_wanderer", "vlandia_wanderer" } },
            { "default", new[] { "empire_wanderer", "vlandia_wanderer" } }
        };

        /// <summary>
        /// Check if a captain is eligible for promotion to companion.
        /// </summary>
        public static bool IsEligibleForPromotion(HoNVirtualCaptain captain)
        {
            if (captain == null || !captain.IsAlive)
                return false;

            try
            {
                var settings = ModSettings.Get();
                if (!settings.EnableCaptainPromotion)
                    return false;

                // Must have survived required number of battles
                if (captain.BattlesSurvived < settings.BattlesForCaptainPromotion)
                    return false;

                // Must have high experience
                if (captain.Experience < 80f)
                    return false;

                // Check promotion requirements from HoNVirtualCaptain
                if (!captain.MeetsPromotionRequirements(settings.BattlesForCaptainPromotion))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Roll for promotion after a battle. Returns true if promotion should occur.
        /// </summary>
        public static bool RollForPromotion(HoNVirtualCaptain captain)
        {
            if (!IsEligibleForPromotion(captain))
                return false;

            try
            {
                var settings = ModSettings.Get();
                float chance = settings.CaptainPromotionChance;

                // Bonus chance based on experience above 80
                float expBonus = (captain.Experience - 80f) / 100f;
                chance += expBonus * 0.02f; // Up to +2% at 100 exp

                // Bonus for surviving many battles beyond requirement
                int extraBattles = captain.BattlesSurvived - settings.BattlesForCaptainPromotion;
                chance += extraBattles * 0.001f; // +0.1% per extra battle

                // Bonus from promotion score
                float scoreBonus = captain.GetPromotionScore() * 0.0005f; // Up to +5%
                chance += scoreBonus;

                // Cap at 15%
                chance = Math.Min(chance, 0.15f);

                return MBRandom.RandomFloat < chance;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Queue a captain for promotion and show user confirmation dialog.
        /// </summary>
        public static void RequestPromotionConfirmation(HoNVirtualCaptain captain, Action<bool> onComplete = null)
        {
            if (captain == null || !captain.IsAlive)
            {
                onComplete?.Invoke(false);
                return;
            }

            _pendingPromotions.Enqueue(new PendingPromotion
            {
                Captain = captain,
                OnComplete = onComplete
            });

            // Show dialog if not already showing one
            ProcessPendingPromotions();
        }

        /// <summary>
        /// Process the queue of pending promotions.
        /// </summary>
        private static void ProcessPendingPromotions()
        {
            if (_isShowingInquiry || _pendingPromotions.Count == 0)
                return;

            var pending = _pendingPromotions.Peek();
            ShowPromotionConfirmation(pending.Captain, (accepted) =>
            {
                _pendingPromotions.Dequeue();
                pending.OnComplete?.Invoke(accepted);
                _isShowingInquiry = false;
                
                // Process next in queue
                ProcessPendingPromotions();
            });
        }

        /// <summary>
        /// Show the user confirmation dialog for captain promotion.
        /// </summary>
        private static void ShowPromotionConfirmation(HoNVirtualCaptain captain, Action<bool> onDecision)
        {
            _isShowingInquiry = true;

            try
            {
                string title = "A Legend Seeks to Rise";
                
                string description = $"{captain.GetFullName()} has proven themselves through {captain.BattlesSurvived} battles " +
                                   $"and seeks to join your companions as a full hero.\n\n" +
                                   $"Experience: {captain.Experience:F0}%\n" +
                                   $"Leadership: {captain.LeadershipLevel}\n" +
                                   $"Tactics: {captain.TacticsLevel}\n" +
                                   $"Combat: {captain.CombatLevel}\n\n" +
                                   $"Traits:\n" +
                                   $"  Valor: {captain.Valor:+#;-#;0}\n" +
                                   $"  Honor: {captain.Honor:+#;-#;0}\n\n" +
                                   $"Accept {captain.Name} as a companion?";

                InformationManager.ShowInquiry(new InquiryData(
                    title,
                    description,
                    true,  // affirmative button shown
                    true,  // negative button shown
                    "Welcome to the company!",
                    "Perhaps another time",
                    () => OnPromotionAccepted(captain, onDecision),
                    () => OnPromotionDeclined(captain, onDecision)));
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, $"Failed to show promotion dialog: {ex.Message}");
                _isShowingInquiry = false;
                onDecision?.Invoke(false);
            }
        }

        private static void OnPromotionAccepted(HoNVirtualCaptain captain, Action<bool> onDecision)
        {
            try
            {
                Hero newHero = ExecutePromotion(captain);
                bool success = newHero != null;
                
                if (success)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{captain.GetFullName()} has joined your companions!",
                        Colors.Green));
                }
                
                onDecision?.Invoke(success);
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, $"Promotion execution failed: {ex.Message}");
                onDecision?.Invoke(false);
            }
        }

        private static void OnPromotionDeclined(HoNVirtualCaptain captain, Action<bool> onDecision)
        {
            try
            {
                // Captain stays as virtual captain but remembers being passed over
                captain.PromotionConsidered = true;
                
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{captain.GetFullName()} remains leading their troops, perhaps awaiting another opportunity.",
                    Colors.Yellow));
                
                onDecision?.Invoke(false);
            }
            catch
            {
                onDecision?.Invoke(false);
            }
        }

        /// <summary>
        /// Actually execute the promotion (called after user confirms).
        /// </summary>
        private static Hero ExecutePromotion(HoNVirtualCaptain captain)
        {
            if (captain == null || !captain.IsAlive)
                return null;

            try
            {
                ModSettings.DebugLog(FEATURE_NAME, $"Executing promotion for captain {captain.GetFullName()}");

                // Find a suitable character template
                CharacterObject template = GetWandererTemplate(captain.TroopId);
                if (template == null)
                {
                    ModSettings.ErrorLog(FEATURE_NAME, "Could not find wanderer template for captain promotion");
                    return null;
                }

                // Create the new hero
                Hero newHero = HeroCreator.CreateSpecialHero(template, 
                    bornSettlement: null,
                    faction: Clan.PlayerClan, 
                    supporterOfClan: Clan.PlayerClan);

                if (newHero == null)
                {
                    ModSettings.ErrorLog(FEATURE_NAME, "Failed to create hero for promoted captain");
                    return null;
                }

                // Customize the hero
                CustomizePromotedHero(newHero, captain);

                // Add to player party
                AddHeroToParty(newHero);

                // Mark captain as promoted (dead with special message)
                captain.OnDeath((float)CampaignTime.Now.ToDays, "Promoted to Companion");

                return newHero;
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, $"Failed to promote captain: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Legacy method - now shows confirmation dialog instead of auto-promoting.
        /// </summary>
        public static Hero PromoteCaptainToCompanion(HoNVirtualCaptain captain)
        {
            // Show confirmation dialog - actual promotion happens in callback
            RequestPromotionConfirmation(captain, null);
            return null; // Hero is created asynchronously after user confirms
        }

        private static CharacterObject GetWandererTemplate(string troopId)
        {
            string id = troopId.ToLowerInvariant();
            string[] templates = null;

            // Find culture-appropriate template
            foreach (var kvp in CultureTemplates)
            {
                if (id.Contains(kvp.Key))
                {
                    templates = kvp.Value;
                    break;
                }
            }

            if (templates == null)
                templates = CultureTemplates["default"];

            // Try to find a valid template
            foreach (string templateId in templates)
            {
                try
                {
                    var character = MBObjectManager.Instance.GetObject<CharacterObject>(templateId);
                    if (character != null)
                        return character;
                }
                catch { }
            }

            // Ultimate fallback - any wanderer
            try
            {
                foreach (var character in CharacterObject.All)
                {
                    if (character.Occupation == Occupation.Wanderer)
                        return character;
                }
            }
            catch { }

            return null;
        }

        private static void CustomizePromotedHero(Hero hero, HoNVirtualCaptain captain)
        {
            // Set name from captain
            TextObject firstName = new TextObject(captain.Name);
            hero.SetName(firstName, firstName);

            // Transfer captain skills/traits to hero
            TransferCaptainExperience(hero, captain);

            // Give them some equipment based on their role
            GiveRoleAppropriateEquipment(hero, captain.TroopId);
        }

        private static void TransferCaptainExperience(Hero hero, HoNVirtualCaptain captain)
        {
            // Use captain's actual skill levels now
            float leadershipXp = captain.LeadershipLevel * 150f;
            float tacticsXp = captain.TacticsLevel * 150f;
            float combatBase = captain.CombatLevel * 100f;

            // Add XP to relevant skills based on troop type
            string id = captain.TroopId.ToLowerInvariant();

            if (id.Contains("archer") || id.Contains("bow"))
            {
                hero.AddSkillXp(DefaultSkills.Bow, (int)(combatBase * 1.5f));
                hero.AddSkillXp(DefaultSkills.Athletics, (int)(combatBase * 0.5f));
            }
            else if (id.Contains("cavalry") || id.Contains("rider") || id.Contains("knight"))
            {
                hero.AddSkillXp(DefaultSkills.Riding, (int)(combatBase * 1.5f));
                hero.AddSkillXp(DefaultSkills.Polearm, (int)(combatBase * 0.5f));
            }
            else if (id.Contains("crossbow"))
            {
                hero.AddSkillXp(DefaultSkills.Crossbow, (int)(combatBase * 1.5f));
                hero.AddSkillXp(DefaultSkills.Athletics, (int)(combatBase * 0.5f));
            }
            else // Infantry
            {
                hero.AddSkillXp(DefaultSkills.OneHanded, (int)(combatBase * 1.0f));
                hero.AddSkillXp(DefaultSkills.Athletics, (int)(combatBase * 0.5f));
            }

            // Leadership and Tactics from captain's actual levels
            hero.AddSkillXp(DefaultSkills.Leadership, (int)leadershipXp);
            hero.AddSkillXp(DefaultSkills.Tactics, (int)tacticsXp);

            // Scouting if they had it
            if (captain.ScoutingLevel > 20)
            {
                hero.AddSkillXp(DefaultSkills.Scouting, captain.ScoutingLevel * 100);
            }
        }

        private static void GiveRoleAppropriateEquipment(Hero hero, string troopId)
        {
            // Hero already has equipment from template
            // The native system handles equipment generation
        }

        private static void AddHeroToParty(Hero hero)
        {
            try
            {
                if (MobileParty.MainParty != null)
                {
                    // Make them a companion
                    hero.ChangeState(Hero.CharacterStates.Active);
                    AddCompanionAction.Apply(Clan.PlayerClan, hero);
                    
                    // Add to party
                    MobileParty.MainParty.AddElementToMemberRoster(hero.CharacterObject, 1);
                }
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, $"Failed to add hero to party: {ex.Message}");
            }
        }

        /// <summary>
        /// Get extra companion limit from settings.
        /// </summary>
        public static int GetAdditionalCompanionLimit()
        {
            try
            {
                return ModSettings.Get().AdditionalCompanionLimit;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Pending promotion data.
        /// </summary>
        private class PendingPromotion
        {
            public HoNVirtualCaptain Captain { get; set; }
            public Action<bool> OnComplete { get; set; }
        }
    }
}
