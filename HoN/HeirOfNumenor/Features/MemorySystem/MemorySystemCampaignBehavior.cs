using System;
using System.Collections.Generic;
using System.Linq;
using HeirOfNumenor.Features.TroopStatus;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.MemorySystem
{
    /// <summary>
    /// Campaign behavior for the Memory System.
    /// Manages virtual captains, tracks bonding milestones, and integrates with TroopStatus.
    /// </summary>
    public class MemorySystemCampaignBehavior : CampaignBehaviorBase
    {
        private const string FEATURE_NAME = "MemorySystem";
        
        private static MemorySystemCampaignBehavior _instance;
        public static MemorySystemCampaignBehavior Instance => _instance;

        /// <summary>
        /// Check if the Memory System is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableMemorySystem; }
                catch { return true; }
            }
        }

        [SaveableField(1)]
        private Dictionary<string, HoNVirtualCaptain> _activeCaptains;

        [SaveableField(2)]
        private List<HoNVirtualCaptain> _fallenCaptains;

        [SaveableField(3)]
        private float _campaignDays;

        [SaveableField(4)]
        private Dictionary<string, float> _releasedTroops;

        private const int MaxFallenCaptains = 50;

        public MemorySystemCampaignBehavior()
        {
            _instance = this;
            _activeCaptains = new Dictionary<string, HoNVirtualCaptain>();
            _fallenCaptains = new List<HoNVirtualCaptain>();
            _releasedTroops = new Dictionary<string, float>();
            _campaignDays = 0f;
        }

        public override void RegisterEvents()
        {
            SafeExecutor.Execute(FEATURE_NAME, "RegisterEvents", () =>
            {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
                // Note: OnTroopDismissalEvent doesn't exist - dismissal handled via roster change monitoring
            });
        }

        public override void SyncData(IDataStore dataStore)
        {
            SafeExecutor.Execute(FEATURE_NAME, "SyncData", () =>
            {
                dataStore.SyncData("Memory_ActiveCaptains", ref _activeCaptains);
                dataStore.SyncData("Memory_FallenCaptains", ref _fallenCaptains);
                dataStore.SyncData("Memory_CampaignDays", ref _campaignDays);
                dataStore.SyncData("Memory_ReleasedTroops", ref _releasedTroops);
            });

            // Ensure collections are initialized
            _activeCaptains ??= new Dictionary<string, HoNVirtualCaptain>();
            _fallenCaptains ??= new List<HoNVirtualCaptain>();
            _releasedTroops ??= new Dictionary<string, float>();
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
                int activeCaptains = _activeCaptains?.Values.Count(c => c?.IsAlive == true) ?? 0;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] Loaded. {activeCaptains} active captains, {_fallenCaptains?.Count ?? 0} fallen.",
                    Colors.Cyan));
            });
        }

        #region Properties

        public IReadOnlyDictionary<string, HoNVirtualCaptain> ActiveCaptains => _activeCaptains;
        public IReadOnlyList<HoNVirtualCaptain> FallenCaptains => _fallenCaptains;
        public float CampaignDays => _campaignDays;
        
        /// <summary>
        /// Gets the count of currently active (alive) virtual captains.
        /// </summary>
        public int GetActiveCaptainCount()
        {
            if (_activeCaptains == null) return 0;
            return _activeCaptains.Values.Count(c => c?.IsAlive == true);
        }

        #endregion

        #region Daily Tick

        private void OnDailyTick()
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "DailyTick", () =>
            {
                _campaignDays += 1f;

                var settings = ModSettings.Get();
                
                // Check for new captain promotions
                CheckCaptainPromotions(settings);

                // Check for captain deaths (if troop count dropped to 0)
                if (settings.EnableCaptainDeath)
                {
                    CheckCaptainDeaths();
                }

                // Process bonding penalties for previously released troops
                ProcessReleasedTroopPenalties();
            });
        }

        private void CheckCaptainPromotions(ModSettings settings)
        {
            SafeExecutor.Execute(FEATURE_NAME, "CheckPromotions", () =>
            {
                var statusManager = TroopStatusManager.Instance;
                if (statusManager == null) return;

                int bondingThreshold = settings.CaptainBondingThreshold;
                int minTroops = settings.MinTroopsForCaptain;

                foreach (var data in statusManager.GetAllData().Values)
                {
                    if (data == null) continue;
                    
                    // Skip if already has captain
                    if (_activeCaptains.TryGetValue(data.TroopId, out var existingCaptain) && existingCaptain?.IsAlive == true)
                        continue;

                    // Check promotion criteria (using settings)
                    if (data.CurrentCount >= minTroops && data.Bonding >= bondingThreshold)
                    {
                        PromoteCaptain(data.TroopId);
                    }
                }
            });
        }

        private void PromoteCaptain(string troopId)
        {
            SafeExecutor.Execute(FEATURE_NAME, "PromoteCaptain", () =>
            {
                var captain = new HoNVirtualCaptain(troopId, _campaignDays);
                _activeCaptains[troopId] = captain;

                var troop = GetCharacterObject(troopId);
                string troopName = troop?.Name?.ToString() ?? troopId;

                InformationManager.DisplayMessage(new InformationMessage(
                    $"★ {captain.GetFullName()} has emerged as captain of your {troopName}!",
                    Colors.Yellow));

                // Add event to troop status
                var statusManager = TroopStatusManager.Instance;
                var statusData = statusManager?.GetTroopStatus(troopId);
                statusData?.AddEvent($"Captain {captain.Name} promoted");
            });
        }

        private void CheckCaptainDeaths()
        {
            SafeExecutor.Execute(FEATURE_NAME, "CheckDeaths", () =>
            {
                var statusManager = TroopStatusManager.Instance;
                if (statusManager == null) return;

                var captainsToRemove = new List<string>();

                foreach (var kvp in _activeCaptains)
                {
                    if (kvp.Value?.IsAlive != true) continue;

                    var statusData = statusManager.GetTroopStatus(kvp.Key);
                    
                    // Captain dies if troop count reaches 0
                    if (statusData == null || statusData.CurrentCount <= 0)
                    {
                        var captain = kvp.Value;
                        captain.OnDeath(_campaignDays, "All troops fell in battle");
                        
                        _fallenCaptains.Add(captain);
                        captainsToRemove.Add(kvp.Key);

                        var troop = GetCharacterObject(kvp.Key);
                        string troopName = troop?.Name?.ToString() ?? kvp.Key;

                        InformationManager.DisplayMessage(new InformationMessage(
                            $"† Captain {captain.GetFullName()} has fallen with the last of the {troopName}!",
                            Colors.Red));
                    }
                }

                // Cleanup
                foreach (var troopId in captainsToRemove)
                {
                    _activeCaptains.Remove(troopId);
                }

                // Limit fallen captain history
                while (_fallenCaptains.Count > MaxFallenCaptains)
                {
                    _fallenCaptains.RemoveAt(0);
                }
            });
        }

        #endregion

        #region Battle Events

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "BattleEnded", () =>
            {
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;

                bool isVictory = mapEvent.WinningSide == mapEvent.PlayerSide;
                var settings = ModSettings.Get();
                float bonusMultiplier = settings.CaptainBonusMultiplier;

                // Update surviving captains
                foreach (var captain in _activeCaptains.Values.SafeWhere(c => c.IsAlive))
                {
                    var statusData = TroopStatusManager.Instance?.GetTroopStatus(captain.TroopId);
                    if (statusData != null && statusData.CurrentCount > 0)
                    {
                        captain.OnBattleSurvived(isVictory);
                    }
                }

                // Check for captain deaths in heavy casualties
                if (!isVictory && settings.EnableCaptainDeath)
                {
                    CheckBattleCaptainDeaths(bonusMultiplier);
                }

                // Check for captain promotion after victory
                if (isVictory && settings.EnableCaptainPromotion)
                {
                    CheckCaptainPromotion();
                }
            });
        }

        /// <summary>
        /// Check if any captain should be promoted to companion.
        /// This is a rare and special event.
        /// </summary>
        private void CheckCaptainPromotion()
        {
            SafeExecutor.Execute(FEATURE_NAME, "CaptainPromotion", () =>
            {
                // Only one promotion per battle maximum
                HoNVirtualCaptain candidateForPromotion = null;
                
                foreach (var kvp in _activeCaptains)
                {
                    var captain = kvp.Value;
                    if (captain == null || !captain.IsAlive) continue;

                    // Check eligibility and roll
                    if (CaptainPromotionManager.RollForPromotion(captain))
                    {
                        candidateForPromotion = captain;
                        break; // Only one per battle
                    }
                }

                if (candidateForPromotion != null)
                {
                    // Attempt promotion
                    Hero newCompanion = CaptainPromotionManager.PromoteCaptainToCompanion(candidateForPromotion);
                    
                    if (newCompanion != null)
                    {
                        // Remove from active captains (PromoteCaptainToCompanion marks as dead with special message)
                        _activeCaptains.Remove(candidateForPromotion.TroopId);
                        
                        ModSettings.DebugLog(FEATURE_NAME, 
                            $"Captain {candidateForPromotion.GetFullName()} promoted to companion {newCompanion.Name}!");
                    }
                }
            });
        }

        private void CheckBattleCaptainDeaths(float bonusMultiplier)
        {
            SafeExecutor.Execute(FEATURE_NAME, "BattleDeaths", () =>
            {
                var statusManager = TroopStatusManager.Instance;
                if (statusManager == null) return;

                var captainsToCheck = _activeCaptains.ToList();
                
                foreach (var kvp in captainsToCheck)
                {
                    if (kvp.Value?.IsAlive != true) continue;

                    var statusData = statusManager.GetTroopStatus(kvp.Key);
                    if (statusData == null) continue;

                    // Heavy casualties (more than 50% lost)
                    float casualtyRate = 1f - (statusData.CurrentCount / (float)Math.Max(1, statusData.CurrentCount + 5));
                    
                    if (casualtyRate > 0.5f)
                    {
                        // 30% chance captain fell in heavy fighting
                        if (MBRandom.RandomFloat < 0.3f)
                        {
                            var captain = kvp.Value;
                            captain.OnDeath(_campaignDays, "Fell leading a desperate charge");
                            
                            _fallenCaptains.Add(captain);
                            _activeCaptains.Remove(kvp.Key);

                            var troop = GetCharacterObject(kvp.Key);
                            string troopName = troop?.Name?.ToString() ?? kvp.Key;

                            InformationManager.DisplayMessage(new InformationMessage(
                                $"† Captain {captain.GetFullName()} fell leading the {troopName} in desperate battle!",
                                Colors.Red));

                            // Massive morale hit to surviving troops
                            statusManager.ModifyStatus(kvp.Key, HoNTroopStatusType.Fear, 20f);
                            statusData.AddEvent($"Captain {captain.Name} fell in battle");
                        }
                    }
                }
            });
        }

        #endregion

        #region Troop Release

        private void OnTroopDismissed(MobileParty party, TroopRoster roster, TroopRosterElement element)
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "TroopDismissed", () =>
            {
                if (party != MobileParty.MainParty) return;
                if (element.Character == null || element.Character.IsHero) return;

                string troopId = element.Character.StringId;
                var statusManager = TroopStatusManager.Instance;
                var statusData = statusManager?.GetTroopStatus(troopId);

                if (statusData != null && statusData.Bonding > 0)
                {
                    // Record the bonding level for penalty if re-recruited
                    _releasedTroops[troopId] = statusData.Bonding;

                    // Reduce bonding based on how many were released
                    float bondingLoss = (element.Number / (float)Math.Max(1, statusData.CurrentCount)) * statusData.Bonding * 0.5f;
                    statusManager.ModifyStatus(troopId, HoNTroopStatusType.Bonding, -bondingLoss);

                    statusData.AddEvent($"Released {element.Number} troops (bonding -{bondingLoss:F0})");

                    if (bondingLoss >= 10f)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"Your {element.Character.Name} feel abandoned... (Bonding -{bondingLoss:F0}%)",
                            Colors.Yellow));
                    }

                    // Check if captain lost all troops
                    if (_activeCaptains.TryGetValue(troopId, out var captain) && captain?.IsAlive == true)
                    {
                        var remainingData = statusManager.GetTroopStatus(troopId);
                        if (remainingData == null || remainingData.CurrentCount <= 0)
                        {
                            captain.OnDeath(_campaignDays, "Disbanded - troops released");
                            _fallenCaptains.Add(captain);
                            _activeCaptains.Remove(troopId);

                            InformationManager.DisplayMessage(new InformationMessage(
                                $"Captain {captain.GetFullName()} disbands as the last troops are released.",
                                Colors.Yellow));
                        }
                    }
                }
            });
        }

        private void ProcessReleasedTroopPenalties()
        {
            SafeExecutor.Execute(FEATURE_NAME, "ProcessPenalties", () =>
            {
                if (_releasedTroops == null || _releasedTroops.Count == 0) return;
                
                // Slowly forget released troops (decay penalty over time)
                var toRemove = new List<string>();
                var keys = _releasedTroops.Keys.ToList();
                
                foreach (var key in keys)
                {
                    // Decay by 1% per day
                    _releasedTroops[key] = _releasedTroops[key] - 1f;
                    if (_releasedTroops[key] <= 0)
                    {
                        toRemove.Add(key);
                    }
                }

                foreach (var key in toRemove)
                {
                    _releasedTroops.Remove(key);
                }
            });
        }

        #endregion

        #region Queries

        public HoNVirtualCaptain GetCaptain(string troopId)
        {
            try
            {
                if (_activeCaptains.TryGetValue(troopId, out var captain) && captain?.IsAlive == true)
                    return captain;
            }
            catch { }
            return null;
        }

        public bool HasCaptain(string troopId)
        {
            return GetCaptain(troopId) != null;
        }

        public (float morale, float damage, float defense, float fearResist) GetTotalCaptainBonuses()
        {
            float morale = 0f, damage = 0f, defense = 0f, fearResist = 0f;

            try
            {
                var multiplier = ModSettings.Get().CaptainBonusMultiplier;
                
                foreach (var captain in _activeCaptains.Values.SafeWhere(c => c.IsAlive))
                {
                    morale += captain.GetMoraleBonus() * multiplier;
                    damage += captain.GetDamageBonus() * multiplier;
                    defense += captain.GetDefenseBonus() * multiplier;
                    fearResist += captain.GetFearResistanceBonus() * multiplier;
                }
            }
            catch { }

            return (morale, damage, defense, fearResist);
        }

        public string GetSummary()
        {
            try
            {
                int activeCaptains = _activeCaptains?.Values.Count(c => c?.IsAlive == true) ?? 0;
                var bonuses = GetTotalCaptainBonuses();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Active Captains: {activeCaptains}");
                sb.AppendLine($"Fallen Heroes: {_fallenCaptains?.Count ?? 0}");
                
                if (activeCaptains > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Total Bonuses:");
                    sb.AppendLine($"  Morale: +{bonuses.morale:F0}%");
                    sb.AppendLine($"  Damage: +{bonuses.damage * 100:F0}%");
                    sb.AppendLine($"  Defense: +{bonuses.defense * 100:F0}%");
                    sb.AppendLine($"  Fear Resist: +{bonuses.fearResist * 100:F0}%");
                }

                return sb.ToString();
            }
            catch
            {
                return "Memory System data unavailable";
            }
        }

        public List<string> GetCaptainDetails()
        {
            var details = new List<string>();
            
            try
            {
                foreach (var captain in _activeCaptains.Values.SafeWhere(c => c.IsAlive).OrderByDescending(c => c.Experience))
                {
                    var troop = GetCharacterObject(captain.TroopId);
                    string troopName = troop?.Name?.ToString() ?? captain.TroopId;
                    
                    details.Add($"★ {captain.GetFullName()} - Captain of {troopName}");
                    details.Add($"   Battles: {captain.BattlesSurvived}, Exp: {captain.Experience:F0}%");
                }
            }
            catch { }

            return details;
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
    /// SaveableTypeDefiner for memory system data.
    /// </summary>
    public class MemorySystemSaveableTypeDefiner : SaveableTypeDefiner
    {
        public MemorySystemSaveableTypeDefiner() : base(726900201) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(HoNVirtualCaptain), 101);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<string, HoNVirtualCaptain>));
            ConstructContainerDefinition(typeof(List<HoNVirtualCaptain>));
        }
    }
}
