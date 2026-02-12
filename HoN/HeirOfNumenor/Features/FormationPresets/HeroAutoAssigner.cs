using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using HarmonyLib;
using HeirOfNumenor.Features.CompanionRoles;

namespace HeirOfNumenor.Features.FormationPresets
{
    /// <summary>
    /// Handles automatic assignment of heroes to formations based on their combat role.
    /// </summary>
    public static class HeroAutoAssigner
    {
        /// <summary>
        /// Auto-assigns all unassigned heroes to formations based on their combat role.
        /// First assigns captains (best matching heroes with banners), then troops.
        /// Uses the OrderOfBattleVM methods to properly update both UI and data.
        /// </summary>
        /// <param name="oobVM">The Order of Battle ViewModel</param>
        /// <param name="resetExisting">If true, clears all existing hero assignments first</param>
        public static void AutoAssignHeroes(OrderOfBattleVM oobVM, bool resetExisting = false)
        {
            try
            {
                if (oobVM == null) return;

                // Get VM data
                var allHeroesField = AccessTools.Field(typeof(OrderOfBattleVM), "_allHeroes");
                var allHeroes = allHeroesField?.GetValue(oobVM) as List<OrderOfBattleHeroItemVM>;
                if (allHeroes == null || allHeroes.Count == 0) return;

                var allFormationsField = AccessTools.Field(typeof(OrderOfBattleVM), "_allFormations");
                var allFormations = allFormationsField?.GetValue(oobVM) as List<OrderOfBattleFormationItemVM>;
                if (allFormations == null || allFormations.Count == 0) return;

                var unassignedProp = AccessTools.Property(typeof(OrderOfBattleVM), "UnassignedHeroes");
                var unassignedHeroes = unassignedProp?.GetValue(oobVM) as MBBindingList<OrderOfBattleHeroItemVM>;

                // If reset requested, clear all existing assignments first
                if (resetExisting)
                {
                    ClearAllHeroAssignments(allFormations, unassignedHeroes);
                }

                if (unassignedHeroes == null || unassignedHeroes.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "All heroes are already assigned.", Colors.Yellow));
                    return;
                }

                // Check for mounted heroes among unassigned
                var mountedHeroes = unassignedHeroes.Where(h => h?.Agent != null && h.Agent.HasMount).ToList();
                
                // Get active formations with defined class
                var activeFormations = allFormations
                    .Where(f => f != null && f.HasFormation)
                    .ToList();

                // Check if we have mounted formations
                bool hasMountedFormation = activeFormations.Any(f => IsFormationMounted(f.GetOrderOfBattleClass()));

                // Check for inactive/unset formations that could become cavalry
                var inactiveFormations = allFormations
                    .Where(f => f != null && !f.HasFormation)
                    .ToList();

                // If we have mounted heroes but no mounted formation, handle it
                if (mountedHeroes.Count > 0 && !hasMountedFormation)
                {
                    if (inactiveFormations.Count > 0)
                    {
                        // Ask user if they want to create a cavalry formation
                        InformationManager.ShowInquiry(new InquiryData(
                            "Mounted Heroes Detected",
                            $"You have {mountedHeroes.Count} mounted hero(es) but no cavalry formation.\n\nWould you like to set an empty formation to Cavalry?",
                            true, true,
                            "Create Cavalry", "Skip Mounted",
                            () => {
                                // Set first inactive formation to Cavalry
                                SetFormationToCavalry(inactiveFormations[0]);
                                // Now do the actual assignment
                                DoAssignment(oobVM, allFormations, unassignedHeroes, false);
                            },
                            () => {
                                // Skip mounted heroes, assign only foot soldiers
                                DoAssignment(oobVM, allFormations, unassignedHeroes, true);
                            }
                        ));
                        return; // Wait for user response
                    }
                    else
                    {
                        // No inactive formations available
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"{mountedHeroes.Count} mounted hero(es) will remain unassigned (no cavalry formation available).", 
                            Colors.Yellow));
                        // Proceed to assign only foot soldiers
                        DoAssignment(oobVM, allFormations, unassignedHeroes, true);
                        return;
                    }
                }

                // Normal assignment
                DoAssignment(oobVM, allFormations, unassignedHeroes, false);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Error during auto-assign: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Sets a formation to Cavalry type.
        /// </summary>
        private static void SetFormationToCavalry(OrderOfBattleFormationItemVM formVM)
        {
            try
            {
                // Access the FormationClassSelector and set it to Cavalry
                var selectorProp = AccessTools.Property(typeof(OrderOfBattleFormationItemVM), "FormationClassSelector");
                var selector = selectorProp?.GetValue(formVM);
                
                if (selector != null)
                {
                    // Find the Cavalry option in the selector
                    var selectorType = selector.GetType();
                    var itemsListProp = AccessTools.Property(selectorType, "ItemList");
                    var itemsList = itemsListProp?.GetValue(selector) as System.Collections.IList;
                    
                    if (itemsList != null)
                    {
                        for (int i = 0; i < itemsList.Count; i++)
                        {
                            var item = itemsList[i];
                            var formClassField = AccessTools.Field(item.GetType(), "FormationClass");
                            if (formClassField != null)
                            {
                                var formClass = (DeploymentFormationClass)formClassField.GetValue(item);
                                if (formClass == DeploymentFormationClass.Cavalry)
                                {
                                    // Select this item
                                    var selectedIndexProp = AccessTools.Property(selectorType, "SelectedIndex");
                                    selectedIndexProp?.SetValue(selector, i);
                                    
                                    InformationManager.DisplayMessage(new InformationMessage(
                                        $"Created Cavalry formation.", Colors.Green));
                                    return;
                                }
                            }
                        }
                    }
                }
                
                // Fallback message
                InformationManager.DisplayMessage(new InformationMessage(
                    "Could not automatically create cavalry formation. Please create one manually.", Colors.Yellow));
            }
            catch (Exception)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Could not create cavalry formation automatically.", Colors.Yellow));
            }
        }

        /// <summary>
        /// Performs the actual hero assignment.
        /// </summary>
        /// <param name="oobVM">The OOB ViewModel</param>
        /// <param name="allFormations">All formation VMs</param>
        /// <param name="unassignedHeroes">List of unassigned heroes</param>
        /// <param name="skipMounted">If true, don't assign mounted heroes</param>
        private static void DoAssignment(OrderOfBattleVM oobVM, List<OrderOfBattleFormationItemVM> allFormations, 
            MBBindingList<OrderOfBattleHeroItemVM> unassignedHeroes, bool skipMounted)
        {
            // Get active formations (has a class defined OR has troops)
            var activeFormations = allFormations
                .Where(f => f != null && (f.HasFormation || (f.Formation != null && f.Formation.CountOfUnits > 0)))
                .ToList();

            if (activeFormations.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "No active formations to assign heroes to.", Colors.Yellow));
                return;
            }

            var heroesToAssign = unassignedHeroes.ToList();
            
            // Filter out mounted heroes if requested
            if (skipMounted)
            {
                heroesToAssign = heroesToAssign.Where(h => h?.Agent == null || !h.Agent.HasMount).ToList();
            }

            int captainsAssigned = 0;
            int troopsAssigned = 0;
            int skippedMounted = 0;

            // PHASE 1: Assign captains to formations that need them
            var formationsNeedingCaptain = activeFormations
                .Where(f => !f.HasCaptain)
                .ToList();

            foreach (var formVM in formationsNeedingCaptain)
            {
                if (!heroesToAssign.Any()) break;

                var formationClassType = formVM.GetOrderOfBattleClass();
                bool formationIsMounted = IsFormationMounted(formationClassType);
                
                // Find best captain candidate for this formation type
                OrderOfBattleHeroItemVM bestCandidate = null;
                int bestScore = int.MinValue;

                foreach (var heroVM in heroesToAssign)
                {
                    if (heroVM?.Agent == null) continue;
                    
                    bool heroIsMounted = heroVM.Agent.HasMount;
                    
                    // Skip mounted heroes for foot formations and vice versa
                    if (heroIsMounted != formationIsMounted) continue;

                    int score = CalculateHeroFormationScore(heroVM, formationClassType);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCandidate = heroVM;
                    }
                }

                if (bestCandidate != null)
                {
                    formVM.Captain = bestCandidate;
                    heroesToAssign.Remove(bestCandidate);
                    unassignedHeroes.Remove(bestCandidate);
                    captainsAssigned++;
                }
            }

            // PHASE 2: Assign remaining heroes as troops
            foreach (var heroVM in heroesToAssign.ToList())
            {
                if (heroVM == null || heroVM.Agent == null) continue;

                bool heroIsMounted = heroVM.Agent.HasMount;

                // Find best matching formation for this hero's mount status
                OrderOfBattleFormationItemVM targetFormationVM = null;
                int bestScore = int.MinValue;

                foreach (var formVM in activeFormations)
                {
                    var formationClassType = formVM.GetOrderOfBattleClass();
                    bool formationIsMounted = IsFormationMounted(formationClassType);
                    
                    // Only consider formations matching mount status
                    if (heroIsMounted != formationIsMounted) continue;

                    int score = CalculateHeroFormationScore(heroVM, formationClassType);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        targetFormationVM = formVM;
                    }
                }

                if (targetFormationVM != null)
                {
                    targetFormationVM.AddHeroTroop(heroVM);
                    unassignedHeroes.Remove(heroVM);
                    troopsAssigned++;
                }
                else if (heroIsMounted)
                {
                    skippedMounted++;
                }
            }

            // Build result message
            string message = $"Auto-assigned {captainsAssigned} captains and {troopsAssigned} troops.";
            if (skippedMounted > 0)
            {
                message += $" ({skippedMounted} mounted hero(es) skipped - no cavalry formation)";
            }
            
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Green));
        }

        /// <summary>
        /// Calculates a score for how well a hero matches a formation.
        /// Higher score = better match. Assumes mount compatibility is already checked.
        /// 
        /// Priority order:
        /// 1. Role match (highest) - for mixed formations, "robust" roles win (Infantry > Ranged, Cavalry > HorseArcher)
        /// 2. Banner match (tiebreaker)
        /// 3. Character level (tiebreaker)
        /// 4. Role-relevant skills (tiebreaker)
        /// </summary>
        private static int CalculateHeroFormationScore(OrderOfBattleHeroItemVM heroVM, DeploymentFormationClass formationClass)
        {
            int score = 0;
            
            var agent = heroVM.Agent;
            var characterObject = agent.Character as TaleWorlds.CampaignSystem.CharacterObject;
            var hero = characterObject?.HeroObject;

            bool hasBanner = heroVM.BannerOfHero != null;
            
            CompanionRoleDetector.CombatRole role = CompanionRoleDetector.CombatRole.ShieldInfantry;
            if (hero != null)
            {
                role = CompanionRoleDetector.GetPrimaryRole(hero);
            }

            // ROLE COMPATIBILITY - Primary factor (1000 base to ensure it always wins)
            score += GetRoleMatchScore(role, formationClass) * 10; // 1000 for perfect match, 500 for secondary

            // BANNER - Tiebreaker 1 (up to 50 points)
            if (hasBanner)
            {
                score += 20; // Bonus for having any banner
                if (IsBannerMatchingDeploymentClass(heroVM.BannerOfHero, formationClass))
                {
                    score += 30; // Additional bonus for matching banner
                }
            }

            // CHARACTER LEVEL - Tiebreaker 2 (up to ~40 points for high level chars)
            if (hero != null)
            {
                // Level typically ranges from 1-40+, divide by 10 to get 0-4 points, then multiply
                score += Math.Min(hero.Level, 40); // Cap at 40 points
            }

            // ROLE-RELEVANT SKILLS - Tiebreaker 3 (up to ~30 points)
            if (hero != null)
            {
                score += GetRelevantSkillBonus(hero, role, formationClass);
            }

            // Main agent (player) - very small preference
            if (agent.IsMainAgent)
            {
                score += 5;
            }

            return score;
        }

        /// <summary>
        /// Gets bonus points based on role-relevant skills.
        /// </summary>
        private static int GetRelevantSkillBonus(Hero hero, CompanionRoleDetector.CombatRole role, DeploymentFormationClass formationClass)
        {
            try
            {
                int skillTotal = 0;

                // Get skills relevant to the role
                switch (role)
                {
                    case CompanionRoleDetector.CombatRole.ShieldInfantry:
                        skillTotal = hero.GetSkillValue(DefaultSkills.OneHanded) + 
                                     hero.GetSkillValue(DefaultSkills.Athletics);
                        break;

                    case CompanionRoleDetector.CombatRole.TwoHanded:
                        skillTotal = hero.GetSkillValue(DefaultSkills.TwoHanded) + 
                                     hero.GetSkillValue(DefaultSkills.Athletics);
                        break;

                    case CompanionRoleDetector.CombatRole.Polearm:
                        skillTotal = hero.GetSkillValue(DefaultSkills.Polearm) + 
                                     hero.GetSkillValue(DefaultSkills.Athletics);
                        break;

                    case CompanionRoleDetector.CombatRole.Archer:
                        skillTotal = hero.GetSkillValue(DefaultSkills.Bow) + 
                                     hero.GetSkillValue(DefaultSkills.Athletics);
                        break;

                    case CompanionRoleDetector.CombatRole.Crossbow:
                        skillTotal = hero.GetSkillValue(DefaultSkills.Crossbow) + 
                                     hero.GetSkillValue(DefaultSkills.Athletics);
                        break;

                    case CompanionRoleDetector.CombatRole.Skirmisher:
                        skillTotal = hero.GetSkillValue(DefaultSkills.Throwing) + 
                                     hero.GetSkillValue(DefaultSkills.Athletics);
                        break;

                    case CompanionRoleDetector.CombatRole.Cavalry:
                        skillTotal = hero.GetSkillValue(DefaultSkills.Riding) + 
                                     hero.GetSkillValue(DefaultSkills.Polearm);
                        break;

                    case CompanionRoleDetector.CombatRole.HorseArcher:
                        skillTotal = hero.GetSkillValue(DefaultSkills.Riding) + 
                                     hero.GetSkillValue(DefaultSkills.Bow);
                        break;

                    default:
                        skillTotal = hero.GetSkillValue(DefaultSkills.OneHanded);
                        break;
                }

                // Also add Leadership skill as captains benefit from it
                skillTotal += hero.GetSkillValue(DefaultSkills.Leadership);

                // Skills can range from 0-300+, divide to get reasonable bonus
                // Total of 3 skills could be ~900, divide by 30 to get max ~30 points
                return skillTotal / 30;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets role match score. For mixed formations, "robust" roles get priority.
        /// Infantry > Ranged for InfantryAndRanged
        /// Cavalry > HorseArcher for CavalryAndHorseArcher
        /// </summary>
        private static int GetRoleMatchScore(CompanionRoleDetector.CombatRole role, DeploymentFormationClass formationClass)
        {
            bool isInfantryRole = role == CompanionRoleDetector.CombatRole.ShieldInfantry ||
                                  role == CompanionRoleDetector.CombatRole.TwoHanded ||
                                  role == CompanionRoleDetector.CombatRole.Polearm;
            
            bool isRangedRole = role == CompanionRoleDetector.CombatRole.Archer ||
                                role == CompanionRoleDetector.CombatRole.Crossbow ||
                                role == CompanionRoleDetector.CombatRole.Skirmisher;
            
            bool isCavalryRole = role == CompanionRoleDetector.CombatRole.Cavalry;
            bool isHorseArcherRole = role == CompanionRoleDetector.CombatRole.HorseArcher;

            switch (formationClass)
            {
                case DeploymentFormationClass.Infantry:
                    return isInfantryRole ? 100 : 0;

                case DeploymentFormationClass.Ranged:
                    return isRangedRole ? 100 : 0;

                case DeploymentFormationClass.Cavalry:
                    return isCavalryRole ? 100 : 0;

                case DeploymentFormationClass.HorseArcher:
                    return isHorseArcherRole ? 100 : 0;

                case DeploymentFormationClass.InfantryAndRanged:
                    // Infantry is the "robust" type for mixed foot formations
                    if (isInfantryRole) return 100;  // Infantry wins
                    if (isRangedRole) return 50;     // Ranged is acceptable but lower priority
                    return 0;

                case DeploymentFormationClass.CavalryAndHorseArcher:
                    // Cavalry is the "robust" type for mixed mounted formations
                    if (isCavalryRole) return 100;      // Cavalry wins
                    if (isHorseArcherRole) return 50;   // HorseArcher is acceptable but lower priority
                    return 0;

                case DeploymentFormationClass.Unset:
                    return 50; // Any role is acceptable for unset

                default:
                    return 50; // Unknown formation - accept any
            }
        }


        /// <summary>
        /// Checks if formation is for mounted units.
        /// </summary>
        private static bool IsFormationMounted(DeploymentFormationClass formationClass)
        {
            return formationClass == DeploymentFormationClass.Cavalry || 
                   formationClass == DeploymentFormationClass.HorseArcher ||
                   formationClass == DeploymentFormationClass.CavalryAndHorseArcher;
        }

        /// <summary>
        /// Checks if a banner's effect type matches a deployment formation class.
        /// </summary>
        private static bool IsBannerMatchingDeploymentClass(ItemObject banner, DeploymentFormationClass formationClass)
        {
            if (banner == null) return false;

            try
            {
                var bannerComponent = banner.ItemComponent as BannerComponent;
                if (bannerComponent?.BannerEffect == null) return false;

                string effectId = bannerComponent.BannerEffect.StringId?.ToLowerInvariant() ?? "";

                switch (formationClass)
                {
                    case DeploymentFormationClass.Infantry:
                    case DeploymentFormationClass.InfantryAndRanged:
                        return effectId.Contains("infantry") || effectId.Contains("melee");

                    case DeploymentFormationClass.Ranged:
                        return effectId.Contains("ranged") || effectId.Contains("archer") || effectId.Contains("bow");

                    case DeploymentFormationClass.Cavalry:
                    case DeploymentFormationClass.CavalryAndHorseArcher:
                        return effectId.Contains("cavalry") || effectId.Contains("mounted") || effectId.Contains("horse");

                    case DeploymentFormationClass.HorseArcher:
                        return effectId.Contains("horse") || effectId.Contains("mounted");

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clears all hero assignments from formations.
        /// </summary>
        private static void ClearAllHeroAssignments(List<OrderOfBattleFormationItemVM> allFormations, MBBindingList<OrderOfBattleHeroItemVM> unassignedHeroes)
        {
            foreach (var formVM in allFormations)
            {
                if (formVM == null) continue;

                // Clear captain
                if (formVM.HasCaptain && formVM.Captain != null)
                {
                    var captain = formVM.Captain;
                    formVM.UnassignCaptain();
                    if (unassignedHeroes != null && !unassignedHeroes.Contains(captain))
                    {
                        unassignedHeroes.Add(captain);
                    }
                }

                // Clear hero troops
                if (formVM.HeroTroops != null)
                {
                    var troops = formVM.HeroTroops.ToList();
                    foreach (var troop in troops)
                    {
                        formVM.RemoveHeroTroop(troop);
                        if (unassignedHeroes != null && !unassignedHeroes.Contains(troop))
                        {
                            unassignedHeroes.Add(troop);
                        }
                    }
                }
            }
        }
    }
}
