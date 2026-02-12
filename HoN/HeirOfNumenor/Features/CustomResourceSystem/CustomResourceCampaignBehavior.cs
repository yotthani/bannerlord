using System;
using System.Collections.Generic;
using System.Linq;
using HeirOfNumenor.Features.TroopStatus;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.CustomResourceSystem
{
    /// <summary>
    /// Campaign behavior for the Custom Resource System.
    /// Tracks resource satisfaction per troop culture and applies effects.
    /// </summary>
    public class CustomResourceCampaignBehavior : CampaignBehaviorBase
    {
        private const string FEATURE_NAME = "CulturalNeeds";
        
        private static CustomResourceCampaignBehavior _instance;
        public static CustomResourceCampaignBehavior Instance => _instance;

        /// <summary>
        /// Check if the Cultural Needs system is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableCulturalNeeds; }
                catch { return true; }
            }
        }

        [SaveableField(1)]
        private Dictionary<string, Dictionary<string, HoNResourceState>> _cultureHoNResourceStates;

        [SaveableField(2)]
        private HashSet<string> _activeRequirements;

        [SaveableField(3)]
        private Dictionary<string, float> _gracePeriodDays;

        public CustomResourceCampaignBehavior()
        {
            _instance = this;
            _cultureHoNResourceStates = new Dictionary<string, Dictionary<string, HoNResourceState>>();
            _activeRequirements = new HashSet<string>();
            _gracePeriodDays = new Dictionary<string, float>();
        }

        public override void RegisterEvents()
        {
            SafeExecutor.Execute(FEATURE_NAME, "RegisterEvents", () =>
            {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
                CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
                CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
            });
        }

        public override void SyncData(IDataStore dataStore)
        {
            SafeExecutor.Execute(FEATURE_NAME, "SyncData", () =>
            {
                dataStore.SyncData("CustomRes_States", ref _cultureHoNResourceStates);
                dataStore.SyncData("CustomRes_Active", ref _activeRequirements);
                dataStore.SyncData("CustomRes_Grace", ref _gracePeriodDays);
            });

            _cultureHoNResourceStates ??= new Dictionary<string, Dictionary<string, HoNResourceState>>();
            _activeRequirements ??= new HashSet<string>();
            _gracePeriodDays ??= new Dictionary<string, float>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _instance = this;

            if (!IsEnabled)
            {
                ModSettings.DebugLog($"{FEATURE_NAME} is disabled in settings.");
                return;
            }

            SafeExecutor.Execute(FEATURE_NAME, "SessionLaunched", () =>
            {
                // Load configuration
                ResourceConfigLoader.LoadConfiguration();

                // Initialize states for cultures in party
                InitializeCultureStates();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] Loaded. Tracking {_cultureHoNResourceStates.Count} cultures.",
                    Colors.Cyan));
            });
        }

        private void InitializeCultureStates()
        {
            SafeExecutor.Execute(FEATURE_NAME, "InitStates", () =>
            {
                var party = MobileParty.MainParty;
                if (party?.MemberRoster == null) return;

                // Get unique cultures from party
                var cultures = new HashSet<string>();
                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    if (element.Character?.Culture != null)
                    {
                        cultures.Add(element.Character.Culture.StringId);
                    }
                }

                // Initialize states for each culture
                foreach (var cultureId in cultures)
                {
                    if (!_cultureHoNResourceStates.ContainsKey(cultureId))
                    {
                        InitializeCultureHoNResourceStates(cultureId);
                    }
                }
            });
        }

        private void InitializeCultureHoNResourceStates(string cultureId)
        {
            SafeExecutor.Execute(FEATURE_NAME, "InitCultureStates", () =>
            {
                var profile = ResourceConfigLoader.GetCultureProfile(cultureId);
                if (profile == null) return;

                var states = new Dictionary<string, HoNResourceState>();

                foreach (var requirement in profile.Requirements)
                {
                    var resource = ResourceConfigLoader.GetResource(requirement.ResourceId);
                    if (resource != null)
                    {
                        states[requirement.ResourceId] = new HoNResourceState
                        {
                            CurrentValue = resource.InitialValue,
                            DaysSinceLastSatisfied = 0,
                            AccumulatedValue = 0,
                            IsSatisfied = true
                        };
                    }
                }

                _cultureHoNResourceStates[cultureId] = states;
            });
        }

        #region Daily Tick

        private void OnDailyTick()
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "DailyTick", () =>
            {
                var settings = ModSettings.Get();
                float decayMultiplier = settings.NeedDecayMultiplier;

                if (decayMultiplier <= 0) return; // Decay disabled

                // Update resource decay
                ProcessDailyDecay(decayMultiplier);

                // Process requirements and apply effects
                ProcessRequirements(settings);
            });
        }

        private void ProcessDailyDecay(float decayMultiplier)
        {
            SafeExecutor.Execute(FEATURE_NAME, "Decay", () =>
            {
                foreach (var cultureKvp in _cultureHoNResourceStates)
                {
                    var profile = ResourceConfigLoader.GetCultureProfile(cultureKvp.Key);
                    if (profile == null) continue;

                    foreach (var requirement in profile.Requirements)
                    {
                        var resource = ResourceConfigLoader.GetResource(requirement.ResourceId);
                        if (resource == null) continue;

                        if (!cultureKvp.Value.TryGetValue(requirement.ResourceId, out var state))
                            continue;

                        // Apply decay based on resource mode
                        switch (resource.Mode)
                        {
                            case SatisfactionMode.Decay:
                                state.CurrentValue = Math.Max(resource.MinValue,
                                    state.CurrentValue - (resource.DailyRate * decayMultiplier));
                                break;

                            case SatisfactionMode.DaysSince:
                                state.DaysSinceLastSatisfied += 1f;
                                break;

                            case SatisfactionMode.Accumulate:
                                state.AccumulatedValue += resource.DailyRate * decayMultiplier;
                                break;
                        }
                    }
                }
            });
        }

        private void ProcessRequirements(ModSettings settings)
        {
            SafeExecutor.Execute(FEATURE_NAME, "Requirements", () =>
            {
                float effectMultiplier = settings.NeedEffectMultiplier;
                bool showWarnings = settings.ShowNeedWarnings;

                foreach (var cultureKvp in _cultureHoNResourceStates)
                {
                    var profile = ResourceConfigLoader.GetCultureProfile(cultureKvp.Key);
                    if (profile == null) continue;

                    foreach (var requirement in profile.Requirements)
                    {
                        if (!requirement.IsEnabled) continue;

                        if (!cultureKvp.Value.TryGetValue(requirement.ResourceId, out var state))
                            continue;

                        string requirementKey = $"{cultureKvp.Key}:{requirement.ResourceId}";
                        bool wasActive = _activeRequirements.Contains(requirementKey);
                        bool isTriggered = CheckRequirementTriggered(requirement, state);

                        if (isTriggered)
                        {
                            // Handle grace period
                            if (!_gracePeriodDays.ContainsKey(requirementKey))
                            {
                                _gracePeriodDays[requirementKey] = 0f;
                            }

                            _gracePeriodDays[requirementKey] += 1f;

                            if (_gracePeriodDays[requirementKey] >= requirement.GracePeriodDays)
                            {
                                // Grace period over - apply effects
                                if (!wasActive)
                                {
                                    OnRequirementTriggered(cultureKvp.Key, requirement, showWarnings);
                                    _activeRequirements.Add(requirementKey);
                                }

                                ApplyRequirementEffects(cultureKvp.Key, requirement, effectMultiplier);
                            }
                        }
                        else
                        {
                            // Reset grace period
                            _gracePeriodDays.Remove(requirementKey);

                            if (wasActive)
                            {
                                OnRequirementResolved(cultureKvp.Key, requirement, showWarnings);
                                _activeRequirements.Remove(requirementKey);
                            }
                        }
                    }
                }
            });
        }

        private bool CheckRequirementTriggered(CultureResourceRequirement requirement, HoNResourceState state)
        {
            try
            {
                var resource = ResourceConfigLoader.GetResource(requirement.ResourceId);
                if (resource == null) return false;

                float checkValue = resource.Mode switch
                {
                    SatisfactionMode.Decay => state.CurrentValue,
                    SatisfactionMode.DaysSince => state.DaysSinceLastSatisfied,
                    SatisfactionMode.Accumulate => state.AccumulatedValue,
                    SatisfactionMode.Binary => state.IsSatisfied ? 1f : 0f,
                    _ => 0f
                };

                return requirement.Condition switch
                {
                    TriggerCondition.Below => checkValue < requirement.Threshold,
                    TriggerCondition.Above => checkValue > requirement.Threshold,
                    TriggerCondition.Equals => Math.Abs(checkValue - requirement.Threshold) < 0.01f,
                    TriggerCondition.DaysExceeds => state.DaysSinceLastSatisfied > requirement.Threshold,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        private void OnRequirementTriggered(string cultureId, CultureResourceRequirement requirement, bool showWarnings)
        {
            SafeExecutor.Execute(FEATURE_NAME, "Triggered", () =>
            {
                if (!showWarnings) return;

                foreach (var effect in requirement.Effects)
                {
                    if (!string.IsNullOrEmpty(effect.TriggerMessage))
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[{cultureId}] {effect.TriggerMessage}",
                            Colors.Yellow));
                    }

                    // Apply one-time trigger magnitude
                    if (Math.Abs(effect.TriggerMagnitude) > 0.01f)
                    {
                        ApplyEffectToTroops(cultureId, effect.TargetStatus, effect.TriggerMagnitude);
                    }
                }
            });
        }

        private void OnRequirementResolved(string cultureId, CultureResourceRequirement requirement, bool showWarnings)
        {
            SafeExecutor.Execute(FEATURE_NAME, "Resolved", () =>
            {
                if (!showWarnings) return;

                foreach (var effect in requirement.Effects)
                {
                    if (!string.IsNullOrEmpty(effect.ResolveMessage))
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[{cultureId}] {effect.ResolveMessage}",
                            Colors.Green));
                    }
                }
            });
        }

        private void ApplyRequirementEffects(string cultureId, CultureResourceRequirement requirement, float effectMultiplier)
        {
            SafeExecutor.Execute(FEATURE_NAME, "ApplyEffects", () =>
            {
                foreach (var effect in requirement.Effects)
                {
                    if (Math.Abs(effect.DailyMagnitude) > 0.01f)
                    {
                        float actualMagnitude = effect.DailyMagnitude * effectMultiplier;
                        ApplyEffectToTroops(cultureId, effect.TargetStatus, actualMagnitude);
                    }
                }
            });
        }

        private void ApplyEffectToTroops(string cultureId, HoNTroopStatusType statusType, float magnitude)
        {
            SafeExecutor.Execute(FEATURE_NAME, "ApplyToTroops", () =>
            {
                var statusManager = TroopStatusManager.Instance;
                if (statusManager == null) return;

                var party = MobileParty.MainParty;
                if (party?.MemberRoster == null) return;

                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    if (element.Character?.Culture?.StringId == cultureId && !element.Character.IsHero)
                    {
                        statusManager.ModifyStatus(element.Character.StringId, statusType, magnitude);
                    }
                }
            });
        }

        #endregion

        #region Event Handlers

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "BattleEnded", () =>
            {
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;

                bool isVictory = mapEvent.WinningSide == mapEvent.PlayerSide;

                // Satisfy "combat" resource for orc cultures
                foreach (var cultureId in _cultureHoNResourceStates.Keys.ToList())
                {
                    SatisfyResource(cultureId, "combat", isVictory ? 60f : 40f);
                    SatisfyResource(cultureId, "meat", isVictory ? 15f : 5f);
                }
            });
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "SettlementEntered", () =>
            {
                if (party != MobileParty.MainParty) return;
                if (settlement == null) return;

                bool isTown = settlement.IsTown;
                bool isCastle = settlement.IsCastle;
                bool isVillage = settlement.IsVillage;

                foreach (var cultureId in _cultureHoNResourceStates.Keys.ToList())
                {
                    // Rest for humans in settlements
                    if (isTown || isCastle || isVillage)
                    {
                        SatisfyResource(cultureId, "rest", 15f);
                    }

                    // Pay in towns (reset days since)
                    if (isTown)
                    {
                        ResetDaysSince(cultureId, "pay");
                        SatisfyResource(cultureId, "beer", 30f);
                    }
                }
            });
        }

        private void OnHourlyTickParty(MobileParty party)
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "HourlyTick", () =>
            {
                if (party != MobileParty.MainParty) return;

                // Check terrain for forest/mountain satisfaction
                CheckTerrainResources(party);
            });
        }

        /// <summary>
        /// Check party position for terrain-based resource satisfaction.
        /// </summary>
        private void CheckTerrainResources(MobileParty party)
        {
            if (party?.CurrentSettlement != null) return; // Only check when on map
            
            try
            {
                // Get terrain type at party's current navigation face
                if (Campaign.Current?.MapSceneWrapper == null) return;
                
                TerrainType terrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);

                // Check each culture in party for terrain-based resources
                foreach (var rosterElement in party.MemberRoster.GetTroopRoster())
                {
                    var character = rosterElement.Character;
                    if (character?.Culture == null) continue;

                    string cultureId = character.Culture.StringId;
                    var profile = ResourceConfigLoader.GetCultureProfile(cultureId);
                    if (profile == null) continue;

                    // Check terrain-based satisfaction
                    // Certain cultures benefit from specific terrains
                    foreach (var requirement in profile.Requirements)
                    {
                        // Check if terrain matches culture preference
                        bool terrainMatches = CheckCultureTerrainMatch(cultureId, terrainType);
                        if (terrainMatches)
                        {
                            // Satisfy a small amount each hour when in correct terrain
                            SatisfyResource(cultureId, requirement.ResourceId, 0.1f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog(FEATURE_NAME, $"Terrain check error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if terrain type matches culture preferences.
        /// </summary>
        private bool CheckCultureTerrainMatch(string cultureId, TerrainType terrain)
        {
            // Culture-specific terrain preferences
            switch (cultureId.ToLowerInvariant())
            {
                case "empire":
                    return terrain == TerrainType.Plain || terrain == TerrainType.RuralArea;
                case "sturgia":
                    return terrain == TerrainType.Snow || terrain == TerrainType.Forest;
                case "aserai":
                    return terrain == TerrainType.Desert || terrain == TerrainType.Dune;
                case "khuzait":
                    return terrain == TerrainType.Steppe || terrain == TerrainType.Plain;
                case "battania":
                    return terrain == TerrainType.Forest || terrain == TerrainType.Swamp;
                case "vlandia":
                    return terrain == TerrainType.Plain || terrain == TerrainType.RuralArea;
                // LOTR cultures
                case "elves":
                case "lothlorien":
                case "rivendell":
                case "mirkwood":
                    return terrain == TerrainType.Forest;
                case "dwarves":
                case "erebor":
                case "ironhills":
                    return terrain == TerrainType.Mountain;
                case "gondor":
                case "rohan":
                    return terrain == TerrainType.Plain;
                case "mordor":
                case "isengard":
                    return terrain == TerrainType.Mountain || terrain == TerrainType.Swamp;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if terrain type matches any of the specified terrain types.
        /// </summary>
        private bool CheckTerrainMatch(TerrainType current, List<string> requiredTerrains)
        {
            if (requiredTerrains == null || requiredTerrains.Count == 0) return false;

            string currentName = current.ToString().ToLowerInvariant();
            
            foreach (var required in requiredTerrains)
            {
                string req = required.ToLowerInvariant();
                
                // Match terrain types
                if (req == "forest" && (current == TerrainType.Forest || currentName.Contains("forest")))
                    return true;
                if (req == "mountain" && (current == TerrainType.Mountain || currentName.Contains("mountain")))
                    return true;
                if (req == "plain" && (current == TerrainType.Plain || currentName.Contains("plain")))
                    return true;
                if (req == "desert" && (current == TerrainType.Desert || currentName.Contains("desert")))
                    return true;
                if (req == "steppe" && (current == TerrainType.Steppe || currentName.Contains("steppe")))
                    return true;
                if (req == "swamp" && (current == TerrainType.Swamp || currentName.Contains("swamp")))
                    return true;
                if (req == "snow" && (current == TerrainType.Snow || currentName.Contains("snow")))
                    return true;
                if (req == "water" && (current == TerrainType.Water || current == TerrainType.River 
                    || current == TerrainType.Lake || current == TerrainType.Fording))
                    return true;
            }
            
            return false;
        }

        #endregion

        #region Resource Management

        public void SatisfyResource(string cultureId, string resourceId, float amount)
        {
            SafeExecutor.Execute(FEATURE_NAME, "Satisfy", () =>
            {
                if (!_cultureHoNResourceStates.TryGetValue(cultureId, out var states)) return;
                if (!states.TryGetValue(resourceId, out var state)) return;

                var resource = ResourceConfigLoader.GetResource(resourceId);
                if (resource == null) return;

                state.CurrentValue = Math.Min(resource.MaxValue, state.CurrentValue + amount);
                state.DaysSinceLastSatisfied = 0;
                state.IsSatisfied = true;
            });
        }

        public void ResetDaysSince(string cultureId, string resourceId)
        {
            SafeExecutor.Execute(FEATURE_NAME, "ResetDays", () =>
            {
                if (!_cultureHoNResourceStates.TryGetValue(cultureId, out var states)) return;
                if (!states.TryGetValue(resourceId, out var state)) return;

                state.DaysSinceLastSatisfied = 0;
            });
        }

        public HoNResourceState GetHoNResourceState(string cultureId, string resourceId)
        {
            try
            {
                if (_cultureHoNResourceStates.TryGetValue(cultureId, out var states))
                {
                    if (states.TryGetValue(resourceId, out var state))
                    {
                        return state;
                    }
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region Queries

        public string GetActiveSummary()
        {
            try
            {
                if (_activeRequirements == null || _activeRequirements.Count == 0)
                    return "No active cultural needs.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Active Cultural Needs:");

                foreach (var req in _activeRequirements)
                {
                    var parts = req.Split(':');
                    if (parts.Length == 2)
                    {
                        sb.AppendLine($"  • {parts[0]}: {parts[1]}");
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return "Cultural needs data unavailable";
            }
        }

        #endregion
    }

    /// <summary>
    /// SaveableTypeDefiner for custom resource system data.
    /// </summary>
    public class CustomResourceSaveableTypeDefiner : SaveableTypeDefiner
    {
        public CustomResourceSaveableTypeDefiner() : base(726900301) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(HoNResourceState), 101);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<string, HoNResourceState>));
            ConstructContainerDefinition(typeof(Dictionary<string, Dictionary<string, HoNResourceState>>));
        }
    }
}
