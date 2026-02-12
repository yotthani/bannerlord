using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.TroopStatus
{
    /// <summary>
    /// Campaign behavior for troop status system.
    /// Handles daily updates, battle events, and save/load.
    /// </summary>
    public class TroopStatusCampaignBehavior : CampaignBehaviorBase
    {
        private const string FEATURE_NAME = "TroopStatus";
        
        private static TroopStatusCampaignBehavior _instance;
        public static TroopStatusCampaignBehavior Instance => _instance;

        /// <summary>
        /// Check if the TroopStatus system is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableTroopStatus; }
                catch { return true; } // Default enabled if settings fail
            }
        }

        // Save data
        [SaveableField(1)]
        private Dictionary<string, HoNTroopStatusData> _troopStatusData;

        public TroopStatusCampaignBehavior()
        {
            _instance = this;
            _troopStatusData = new Dictionary<string, HoNTroopStatusData>();
        }

        public override void RegisterEvents()
        {
            try
            {
                // Core events
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
                
                // Battle events
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
                
                // Recruitment events
                CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
                CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);
                
                // Hero events (for companion status tracking)
                CampaignEvents.HeroLevelledUp.AddNonSerializedListener(this, OnHeroLevelledUp);
                CampaignEvents.HeroWounded.AddNonSerializedListener(this, OnHeroWounded);
                
                // Army/Party events
                CampaignEvents.ArmyCreated.AddNonSerializedListener(this, OnArmyCreated);
                CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Failed to register events", ex);
            }
        }

        #region New Event Handlers

        private void OnHeroLevelledUp(Hero hero, bool shouldNotify)
        {
            if (!IsEnabled) return;
            
            SafeExecutor.Execute(FEATURE_NAME, "HeroLevelledUp", () =>
            {
                // When companions level up, boost troop morale
                if (hero?.Clan == Clan.PlayerClan && hero != Hero.MainHero)
                {
                    // Companions leveling up inspires nearby troops
                    ApplyBondingChange(2f, "Inspired by companion's growth");
                }
            });
        }

        private void OnHeroWounded(Hero woundedHero)
        {
            if (!IsEnabled) return;
            
            SafeExecutor.Execute(FEATURE_NAME, "HeroWounded", () =>
            {
                // If a companion is wounded, troops become fearful
                if (woundedHero?.Clan == Clan.PlayerClan && woundedHero != Hero.MainHero)
                {
                    ApplyFearChange(5f, "Companion wounded in battle");
                }
            });
        }

        private void OnArmyCreated(Army army)
        {
            if (!IsEnabled) return;
            
            SafeExecutor.Execute(FEATURE_NAME, "ArmyCreated", () =>
            {
                // Joining an army boosts morale
                if (army?.LeaderParty?.LeaderHero?.Clan == Clan.PlayerClan)
                {
                    ApplyBondingChange(5f, "Joining a great host");
                    ApplyFearChange(-3f, "Strength in numbers");
                }
            });
        }

        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
        {
            if (!IsEnabled) return;
            
            SafeExecutor.Execute(FEATURE_NAME, "ArmyDispersed", () =>
            {
                if (!isPlayersArmy) return;

                // Army dispersal effects depend on reason
                switch (reason)
                {
                    case Army.ArmyDispersionReason.DismissalRequestedWithInfluence:
                        // Normal disbandment - slight frustration
                        ApplyFrustrationChange(2f, "Army disbanded");
                        break;
                    case Army.ArmyDispersionReason.CohesionDepleted:
                        // Cohesion collapse - major frustration
                        ApplyFrustrationChange(8f, "Army collapsed from poor leadership");
                        break;
                    case Army.ArmyDispersionReason.FoodProblem:
                        // Starvation - fear and frustration
                        ApplyFearChange(5f, "Starving army dispersed");
                        ApplyFrustrationChange(10f, "No food for the troops");
                        break;
                }
            });
        }

        private void ApplyBondingChange(float change, string reason)
        {
            foreach (var data in _troopStatusData.Values)
            {
                data.ModifyStatus(HoNTroopStatusType.Bonding, change);
            }
            ModSettings.DebugLog(FEATURE_NAME, $"Bonding {(change >= 0 ? "+" : "")}{change}: {reason}");
        }

        private void ApplyFearChange(float change, string reason)
        {
            foreach (var data in _troopStatusData.Values)
            {
                data.ModifyStatus(HoNTroopStatusType.Fear, change);
            }
            ModSettings.DebugLog(FEATURE_NAME, $"Fear {(change >= 0 ? "+" : "")}{change}: {reason}");
        }

        private void ApplyFrustrationChange(float change, string reason)
        {
            foreach (var data in _troopStatusData.Values)
            {
                data.ModifyStatus(HoNTroopStatusType.Frustration, change);
            }
            ModSettings.DebugLog(FEATURE_NAME, $"Frustration {(change >= 0 ? "+" : "")}{change}: {reason}");
        }

        #endregion

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("TroopStatus_Data", ref _troopStatusData);
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Save/Load error", ex);
            }

            _troopStatusData ??= new Dictionary<string, HoNTroopStatusData>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                _instance = this;
                
                if (!IsEnabled)
                {
                    ModSettings.DebugLog($"{FEATURE_NAME} is disabled in settings.");
                    return;
                }

                // Initialize the manager with saved data
                TroopStatusManager.Instance?.Initialize(_troopStatusData);
                
                // Sync with current party
                TroopStatusManager.Instance?.SyncWithPartyRoster();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] Loaded. Tracking {_troopStatusData?.Count ?? 0} troop types.",
                    Colors.Cyan));
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Session launch error", ex);
            }
        }

        #region Daily Tick

        private void OnDailyTick()
        {
            if (!IsEnabled) return;
            
            try
            {
                var manager = TroopStatusManager.Instance;
                if (manager == null) return;
                
                // Sync roster counts
                manager.SyncWithPartyRoster();

                var settings = ModSettings.Get();
                
                // Increment days for active troops
                foreach (var data in manager.GetAllData().Values)
                {
                    if (data?.CurrentCount > 0)
                    {
                        data.DaysInParty += 1f;
                    }
                }

                // Apply natural status changes (fear decay, bonding growth, etc.)
                manager.ApplyDailyNaturalChanges();

                // Check for desertion due to high fear
                if (settings.EnableDesertion)
                {
                    ProcessDesertion(settings);
                }

                // Update saved data reference
                _troopStatusData = manager.GetAllData();
            }
            catch (Exception ex)
            {
                if (ModSettings.Get().SafeMode)
                {
                    ModSettings.ErrorLog(FEATURE_NAME, "Daily tick error", ex);
                }
            }
        }

        private void ProcessDesertion(ModSettings settings)
        {
            try
            {
                var party = MobileParty.MainParty;
                if (party == null) return;

                var manager = TroopStatusManager.Instance;
                if (manager == null) return;
                
                float desertionThreshold = settings.FearDesertionThreshold;
                var terrifiedTroops = manager.GetTroopsAboveFear(desertionThreshold);
                if (terrifiedTroops == null) return;

                foreach (var data in terrifiedTroops)
                {
                    if (data == null || data.CurrentCount <= 0) continue;
                    
                    // Desertion chance based on fear level above threshold
                    float excessFear = data.Fear - desertionThreshold;
                    float desertionChance = excessFear * 0.02f; // 2% per point above threshold

                    // Bonding reduces desertion chance
                    desertionChance *= (1f - data.Bonding / 200f); // Up to 50% reduction

                    if (MBRandom.RandomFloat < desertionChance)
                    {
                        // Desert 1-3 troops based on fear
                        int desertCount = Math.Min(data.CurrentCount, MBRandom.RandomInt(1, 4));
                        
                        var troop = GetCharacterObject(data.TroopId);
                        if (troop != null && desertCount > 0)
                        {
                            party.MemberRoster.AddToCounts(troop, -desertCount);
                            data.CurrentCount -= desertCount;
                            
                            data.AddEvent($"{desertCount} deserted due to fear");

                            InformationManager.DisplayMessage(new InformationMessage(
                                $"{desertCount} {troop.Name} have deserted in fear!",
                                Colors.Red));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Desertion processing error", ex);
            }
        }

        #endregion

        #region Battle Events

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!IsEnabled) return;
            
            try
            {
                // Only process battles involving player
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;

                var party = MobileParty.MainParty;
                if (party == null) return;

                bool isVictory = mapEvent.WinningSide == mapEvent.PlayerSide;
                var manager = TroopStatusManager.Instance;
                if (manager == null) return;
                
                var settings = ModSettings.Get();
                
                if (isVictory)
                {
                    ApplyBattleVictoryEffects(manager, settings);
                }
                else
                {
                    ApplyBattleDefeatEffects(manager, settings);
                }

                // Survivors get battle experience bonus to bonding
                ApplyBattleSurvivorBonus(manager, isVictory, settings);
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Battle event error", ex);
            }
        }

        private void ApplyBattleVictoryEffects(TroopStatusManager manager, ModSettings settings)
        {
            try
            {
                foreach (var data in manager.GetAllData().Values)
                {
                    if (data?.CurrentCount > 0)
                    {
                        // Victory reduces fear
                        float fearChange = -5f * settings.FearGainMultiplier;
                        manager.ModifyStatus(data.TroopId, HoNTroopStatusType.Fear, fearChange);
                        
                        // Victory increases bonding slightly
                        float bondingGain = 1f * settings.BondingGainMultiplier;
                        manager.ModifyStatus(data.TroopId, HoNTroopStatusType.Bonding, bondingGain);
                        
                        // Increase battle experience
                        manager.ModifyStatus(data.TroopId, HoNTroopStatusType.BattleExperience, 2f);
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Victory effects error", ex);
            }
        }

        private void ApplyBattleDefeatEffects(TroopStatusManager manager, ModSettings settings)
        {
            try
            {
                foreach (var data in manager.GetAllData().Values)
                {
                    if (data?.CurrentCount > 0)
                    {
                        // Defeat increases fear (reduced by bonding)
                        float baseFear = 10f * settings.FearGainMultiplier;
                        float fearGain = baseFear * data.CalculateFearResistance();
                        manager.ModifyStatus(data.TroopId, HoNTroopStatusType.Fear, fearGain);
                        
                        // Defeat increases frustration
                        manager.ModifyStatus(data.TroopId, HoNTroopStatusType.Frustration, 5f);
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Defeat effects error", ex);
            }
        }

        private void ApplyBattleSurvivorBonus(TroopStatusManager manager, bool isVictory, ModSettings settings)
        {
            try
            {
                foreach (var data in manager.GetAllData().Values)
                {
                    if (data?.CurrentCount > 0)
                    {
                        // Surviving a battle together increases bonding
                        float bondingGain = (isVictory ? 2f : 1f) * settings.BondingGainMultiplier;
                        manager.ModifyStatus(data.TroopId, HoNTroopStatusType.Bonding, bondingGain);
                        
                        data.AddEvent(isVictory ? "Survived victorious battle" : "Survived defeat");
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Survivor bonus error", ex);
            }
        }

        #endregion

        #region Recruitment Events

        private void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero recruitmentSource, 
            CharacterObject troop, int count)
        {
            if (!IsEnabled) return;
            
            try
            {
                if (recruiter != Hero.MainHero) return;
                HandleRecruitment(troop, count);
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Troop recruit event error", ex);
            }
        }

        private void OnUnitRecruited(CharacterObject troop, int count)
        {
            if (!IsEnabled) return;
            
            try
            {
                HandleRecruitment(troop, count);
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Unit recruit event error", ex);
            }
        }

        private void HandleRecruitment(CharacterObject troop, int count)
        {
            try
            {
                if (troop == null || troop.IsHero || count <= 0) return;

                var manager = TroopStatusManager.Instance;
                if (manager == null) return;
                
                var data = manager.GetTroopStatus(troop.StringId);

                if (data != null && data.CurrentCount > 0)
                {
                    // Existing troops - dilute bonding with new recruits
                    int oldCount = data.CurrentCount;
                    float oldBonding = data.Bonding;
                    
                    // New recruits have 0 bonding - calculate weighted average
                    float newBonding = (oldBonding * oldCount) / (oldCount + count);
                    
                    data.Bonding = newBonding;
                    data.CurrentCount = oldCount + count;
                    
                    data.AddEvent($"Recruited {count} new troops (bonding diluted {oldBonding:F0} → {newBonding:F0})");

                    if (oldBonding - newBonding >= 5f)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"{troop.Name} bonding diluted by new recruits ({oldBonding:F0}% → {newBonding:F0}%)",
                            Colors.Yellow));
                    }
                }
                else
                {
                    // First time recruiting this troop type
                    manager.GetOrCreateTroopStatus(troop.StringId, count);
                }
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Recruitment handling error", ex);
            }
        }

        #endregion

        #region Ring Integration Hook

        /// <summary>
        /// Called by Ring System when ring corruption affects troops.
        /// </summary>
        public void ApplyRingCorruptionToTroops(float corruptionLevel, bool hasOneRing)
        {
            if (!IsEnabled) return;
            
            try
            {
                var manager = TroopStatusManager.Instance;
                if (manager == null) return;
                
                var settings = ModSettings.Get();
                
                // Base fear from ring corruption
                float baseFear = corruptionLevel * 0.1f * settings.FearGainMultiplier;
                
                // One Ring is especially terrifying
                if (hasOneRing)
                {
                    baseFear *= 2f;
                }

                foreach (var data in manager.GetAllData().Values)
                {
                    if (data?.CurrentCount > 0)
                    {
                        // Apply fear (reduced by bonding)
                        float actualFear = baseFear * data.CalculateFearResistance();
                        manager.ModifyStatus(data.TroopId, HoNTroopStatusType.Fear, actualFear);
                        
                        // Increase ring exposure
                        float exposureGain = corruptionLevel * 0.05f;
                        if (hasOneRing) exposureGain *= 3f;
                        manager.ModifyStatus(data.TroopId, HoNTroopStatusType.RingExposure, exposureGain);
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "Ring corruption error", ex);
            }
        }

        #endregion

        #region Helpers

        private CharacterObject GetCharacterObject(string troopId)
        {
            try
            {
                return TaleWorlds.ObjectSystem.MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// SaveableTypeDefiner for troop status data.
    /// </summary>
    public class TroopStatusSaveableTypeDefiner : SaveableTypeDefiner
    {
        public TroopStatusSaveableTypeDefiner() : base(726900001) { } // Unique ID

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(HoNTroopStatusData), 101);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<string, HoNTroopStatusData>));
        }
    }
}
