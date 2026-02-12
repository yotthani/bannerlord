using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using HarmonyLib;
using HeirOfNumenor.Features.FormationPresets.Data;

namespace HeirOfNumenor.Features.FormationPresets
{
    /// <summary>
    /// Manages formation preset saving, loading, and application.
    /// Works with Mission.Current.PlayerTeam formations.
    /// </summary>
    public static class FormationPresetManager
    {
        private static List<HoNFormationPreset> _presets = new List<HoNFormationPreset>();
        private static HoNFormationPreset _currentPreset = null;

        public static IReadOnlyList<HoNFormationPreset> Presets => _presets;
        public static HoNFormationPreset CurrentPreset => _currentPreset;

        /// <summary>
        /// Saves the current formation configuration as a new preset.
        /// Tracks formation classes, captains, and hero troops.
        /// </summary>
        public static HoNFormationPreset SaveCurrentAsPreset(OrderOfBattleVM oobVM, string name)
        {
            try
            {
                if (oobVM == null) return null;

                var preset = new HoNFormationPreset(name);
                
                var allFormationsField = AccessTools.Field(typeof(OrderOfBattleVM), "_allFormations");
                var allFormations = allFormationsField?.GetValue(oobVM) as List<OrderOfBattleFormationItemVM>;
                if (allFormations == null) return null;

                int captainCount = 0;
                int troopCount = 0;
                int formationCount = 0;

                foreach (var formationVM in allFormations)
                {
                    if (formationVM?.Formation == null) continue;
                    
                    int formationIndex = (int)formationVM.Formation.FormationIndex;
                    
                    // Save formation class/type
                    if (formationVM.HasFormation)
                    {
                        var deploymentClass = formationVM.GetOrderOfBattleClass();
                        preset.FormationClasses[formationIndex] = (int)deploymentClass;
                        formationCount++;
                    }
                    else
                    {
                        preset.FormationClasses[formationIndex] = -1; // Unset
                    }
                    
                    // Save captain
                    if (formationVM.HasCaptain && formationVM.Captain?.Agent != null)
                    {
                        var captainAgent = formationVM.Captain.Agent;
                        var characterObject = captainAgent.Character as CharacterObject;
                        var hero = characterObject?.HeroObject;
                        
                        if (hero?.CharacterObject?.StringId != null)
                        {
                            string heroId = hero.CharacterObject.StringId;
                            preset.HeroFormationAssignments[heroId] = formationIndex;
                            preset.CaptainHeroIds.Add(heroId);
                            captainCount++;
                        }
                    }

                    // Save hero troops
                    if (formationVM.HeroTroops != null)
                    {
                        foreach (var heroVM in formationVM.HeroTroops)
                        {
                            if (heroVM?.Agent == null) continue;
                            
                            var characterObject = heroVM.Agent.Character as CharacterObject;
                            var hero = characterObject?.HeroObject;
                            
                            if (hero?.CharacterObject?.StringId != null)
                            {
                                preset.HeroFormationAssignments[hero.CharacterObject.StringId] = formationIndex;
                                troopCount++;
                            }
                        }
                    }
                }

                _presets.Add(preset);
                _currentPreset = preset;

                // Save to campaign behavior
                FormationPresetCampaignBehavior.Instance?.MarkDirty();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"Saved preset '{name}' ({formationCount} formations, {captainCount} captains, {troopCount} troops)", Colors.Green));

                return preset;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Error saving preset: {ex.Message}", Colors.Red));
                return null;
            }
        }

        /// <summary>
        /// Updates an existing preset with current configuration.
        /// </summary>
        public static bool UpdatePreset(OrderOfBattleVM oobVM, HoNFormationPreset preset)
        {
            if (preset == null || oobVM == null) return false;

            try
            {
                var allFormationsField = AccessTools.Field(typeof(OrderOfBattleVM), "_allFormations");
                var allFormations = allFormationsField?.GetValue(oobVM) as List<OrderOfBattleFormationItemVM>;
                if (allFormations == null) return false;

                // Clear existing data
                preset.HeroFormationAssignments.Clear();
                preset.CaptainHeroIds.Clear();
                preset.FormationClasses.Clear();
                preset.CreatedAt = DateTime.Now;

                int captainCount = 0;
                int troopCount = 0;
                int formationCount = 0;

                foreach (var formationVM in allFormations)
                {
                    if (formationVM?.Formation == null) continue;
                    
                    int formationIndex = (int)formationVM.Formation.FormationIndex;
                    
                    // Save formation class
                    if (formationVM.HasFormation)
                    {
                        var deploymentClass = formationVM.GetOrderOfBattleClass();
                        preset.FormationClasses[formationIndex] = (int)deploymentClass;
                        formationCount++;
                    }
                    else
                    {
                        preset.FormationClasses[formationIndex] = -1;
                    }
                    
                    // Save captain
                    if (formationVM.HasCaptain && formationVM.Captain?.Agent != null)
                    {
                        var captainAgent = formationVM.Captain.Agent;
                        var characterObject = captainAgent.Character as CharacterObject;
                        var hero = characterObject?.HeroObject;
                        
                        if (hero?.CharacterObject?.StringId != null)
                        {
                            string heroId = hero.CharacterObject.StringId;
                            preset.HeroFormationAssignments[heroId] = formationIndex;
                            preset.CaptainHeroIds.Add(heroId);
                            captainCount++;
                        }
                    }

                    // Save hero troops
                    if (formationVM.HeroTroops != null)
                    {
                        foreach (var heroVM in formationVM.HeroTroops)
                        {
                            if (heroVM?.Agent == null) continue;
                            
                            var characterObject = heroVM.Agent.Character as CharacterObject;
                            var hero = characterObject?.HeroObject;
                            
                            if (hero?.CharacterObject?.StringId != null)
                            {
                                preset.HeroFormationAssignments[hero.CharacterObject.StringId] = formationIndex;
                                troopCount++;
                            }
                        }
                    }
                }

                _currentPreset = preset;
                FormationPresetCampaignBehavior.Instance?.MarkDirty();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"Updated preset '{preset.Name}' ({formationCount} formations, {captainCount} captains, {troopCount} troops)", Colors.Green));

                return true;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Error updating preset: {ex.Message}", Colors.Red));
                return false;
            }
        }

        /// <summary>
        /// Loads a preset and applies it to the current mission.
        /// Sets formation classes, then assigns captains and troops.
        /// </summary>
        public static bool LoadPreset(OrderOfBattleVM oobVM, HoNFormationPreset preset)
        {
            if (preset == null || oobVM == null) return false;

            try
            {
                var allHeroesField = AccessTools.Field(typeof(OrderOfBattleVM), "_allHeroes");
                var allHeroes = allHeroesField?.GetValue(oobVM) as List<OrderOfBattleHeroItemVM>;

                var allFormationsField = AccessTools.Field(typeof(OrderOfBattleVM), "_allFormations");
                var allFormations = allFormationsField?.GetValue(oobVM) as List<OrderOfBattleFormationItemVM>;

                var unassignedProp = AccessTools.Property(typeof(OrderOfBattleVM), "UnassignedHeroes");
                var unassignedHeroes = unassignedProp?.GetValue(oobVM) as MBBindingList<OrderOfBattleHeroItemVM>;

                if (allHeroes == null || allFormations == null) return false;

                // PHASE 1: Set formation classes first
                int formationsSet = 0;
                foreach (var formationVM in allFormations)
                {
                    if (formationVM?.Formation == null) continue;
                    
                    int formationIndex = (int)formationVM.Formation.FormationIndex;
                    int savedClass = preset.GetFormationClass(formationIndex);
                    
                    if (savedClass >= 0)
                    {
                        SetFormationClass(formationVM, (DeploymentFormationClass)savedClass);
                        formationsSet++;
                    }
                }

                // Build lookup of heroId -> heroVM
                var heroLookup = new Dictionary<string, OrderOfBattleHeroItemVM>();
                foreach (var h in allHeroes)
                {
                    if (h?.Agent == null) continue;
                    var characterObject = h.Agent.Character as CharacterObject;
                    var hero = characterObject?.HeroObject;
                    if (hero?.CharacterObject?.StringId != null)
                    {
                        heroLookup[hero.CharacterObject.StringId] = h;
                    }
                }

                int captainsAssigned = 0;
                int troopsAssigned = 0;

                // PHASE 2: Clear existing assignments for heroes we'll reassign
                foreach (var kvp in preset.HeroFormationAssignments)
                {
                    if (heroLookup.TryGetValue(kvp.Key, out var heroVM))
                    {
                        if (heroVM.IsLeadingAFormation && heroVM.CurrentAssignedFormationItem != null)
                        {
                            heroVM.CurrentAssignedFormationItem.UnassignCaptain();
                        }
                        else if (heroVM.CurrentAssignedFormationItem != null)
                        {
                            heroVM.CurrentAssignedFormationItem.RemoveHeroTroop(heroVM);
                        }
                    }
                }

                // PHASE 3: Assign captains first
                foreach (var kvp in preset.HeroFormationAssignments)
                {
                    string heroId = kvp.Key;
                    int formationIndex = kvp.Value;

                    if (!preset.IsCaptain(heroId)) continue;
                    if (!heroLookup.TryGetValue(heroId, out var heroVM)) continue;

                    var targetFormationVM = allFormations.FirstOrDefault(f => 
                        f?.Formation != null && (int)f.Formation.FormationIndex == formationIndex);
                    
                    if (targetFormationVM == null) continue;

                    unassignedHeroes?.Remove(heroVM);
                    targetFormationVM.Captain = heroVM;
                    captainsAssigned++;
                }

                // PHASE 4: Assign troops
                foreach (var kvp in preset.HeroFormationAssignments)
                {
                    string heroId = kvp.Key;
                    int formationIndex = kvp.Value;

                    if (preset.IsCaptain(heroId)) continue;
                    if (!heroLookup.TryGetValue(heroId, out var heroVM)) continue;

                    var targetFormationVM = allFormations.FirstOrDefault(f => 
                        f?.Formation != null && (int)f.Formation.FormationIndex == formationIndex);
                    
                    if (targetFormationVM == null) continue;

                    unassignedHeroes?.Remove(heroVM);
                    targetFormationVM.AddHeroTroop(heroVM);
                    troopsAssigned++;
                }

                _currentPreset = preset;

                InformationManager.DisplayMessage(new InformationMessage(
                    $"Loaded '{preset.Name}' ({formationsSet} formations, {captainsAssigned} captains, {troopsAssigned} troops)", Colors.Green));

                return true;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Error loading preset: {ex.Message}", Colors.Red));
                return false;
            }
        }

        /// <summary>
        /// Sets a formation's class via the selector.
        /// </summary>
        private static void SetFormationClass(OrderOfBattleFormationItemVM formVM, DeploymentFormationClass targetClass)
        {
            try
            {
                var selectorProp = AccessTools.Property(typeof(OrderOfBattleFormationItemVM), "FormationClassSelector");
                var selector = selectorProp?.GetValue(formVM);
                
                if (selector == null) return;

                var selectorType = selector.GetType();
                var itemsListProp = AccessTools.Property(selectorType, "ItemList");
                var itemsList = itemsListProp?.GetValue(selector) as System.Collections.IList;
                
                if (itemsList == null) return;

                for (int i = 0; i < itemsList.Count; i++)
                {
                    var item = itemsList[i];
                    var formClassField = AccessTools.Field(item.GetType(), "FormationClass");
                    if (formClassField != null)
                    {
                        var formClass = (DeploymentFormationClass)formClassField.GetValue(item);
                        if (formClass == targetClass)
                        {
                            var selectedIndexProp = AccessTools.Property(selectorType, "SelectedIndex");
                            selectedIndexProp?.SetValue(selector, i);
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Silently fail - formation class setting is best-effort
            }
        }

        /// <summary>
        /// Deletes a preset.
        /// </summary>
        public static bool DeletePreset(HoNFormationPreset preset)
        {
            if (preset == null) return false;

            if (_presets.Remove(preset))
            {
                if (_currentPreset == preset)
                    _currentPreset = null;
                
                FormationPresetCampaignBehavior.Instance?.MarkDirty();
                
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Deleted preset '{preset.Name}'", Colors.Yellow));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Deletes a preset by name.
        /// </summary>
        public static bool DeletePreset(string name)
        {
            var preset = _presets.FirstOrDefault(p => p.Name == name);
            return preset != null && DeletePreset(preset);
        }

        /// <summary>
        /// Gets preset by ID.
        /// </summary>
        public static HoNFormationPreset GetPresetById(string id)
        {
            return _presets.FirstOrDefault(p => p.Id == id);
        }

        /// <summary>
        /// Loads preset data from campaign behavior storage.
        /// </summary>
        public static void LoadFromCampaignData(List<HoNFormationPreset> presets)
        {
            if (presets != null)
            {
                _presets = new List<HoNFormationPreset>(presets);
            }
        }

        /// <summary>
        /// Gets preset data for campaign behavior storage.
        /// </summary>
        public static List<HoNFormationPreset> GetPresetsForSaving()
        {
            return new List<HoNFormationPreset>(_presets);
        }

        /// <summary>
        /// Clears all presets (for testing/reset).
        /// </summary>
        public static void ClearAll()
        {
            _presets.Clear();
            _currentPreset = null;
        }
    }
}
