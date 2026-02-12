using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Campaign behavior that manages ring effects, daily updates, and save/load.
    /// </summary>
    public class RingSystemCampaignBehavior : CampaignBehaviorBase
    {
        private const string FEATURE_NAME = "RingSystem";
        
        private static RingSystemCampaignBehavior _instance;
        public static RingSystemCampaignBehavior Instance => _instance;

        /// <summary>
        /// Check if the Ring System is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableRingSystem; }
                catch { return true; }
            }
        }

        // Effect tracker for the player
        private HoNRingEffectTracker _playerEffects;

        // List of currently equipped ring IDs
        private List<string> _equippedRingIds;

        // Owned ring IDs
        private HashSet<string> _ownedRingIds;
        
        // Corruption accumulator for tracking corruption buildup
        private float _corruptionAccumulator;

        public HoNRingEffectTracker PlayerEffects => _playerEffects;
        public IReadOnlyList<string> EquippedRingIds => _equippedRingIds;
        public IReadOnlyCollection<string> OwnedRingIds => _ownedRingIds;

        public const int MaxEquippedRings = 1;

        public RingSystemCampaignBehavior()
        {
            _instance = this;
            _playerEffects = new HoNRingEffectTracker();
            _equippedRingIds = new List<string>();
            _ownedRingIds = new HashSet<string>();
            _corruptionAccumulator = 0f;
        }

        public override void RegisterEvents()
        {
            SafeExecutor.Execute(FEATURE_NAME, "RegisterEvents", () =>
            {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            });
        }

        public override void SyncData(IDataStore dataStore)
        {
            SafeExecutor.Execute(FEATURE_NAME, "SyncData", () =>
            {
                dataStore.SyncData("RingSystem_EquippedRings", ref _equippedRingIds);
                dataStore.SyncData("RingSystem_OwnedRings", ref _ownedRingIds);
                dataStore.SyncData("RingSystem_PlayerEffects", ref _playerEffects);
            });

            // Ensure collections are initialized after load
            _equippedRingIds ??= new List<string>();
            _ownedRingIds ??= new HashSet<string>();
            _playerEffects ??= new HoNRingEffectTracker();
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
                // Debug: Give player some starter rings for testing
                if (_ownedRingIds.Count == 0)
                {
                    GiveStarterRingsForTesting();
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] Loaded. Owned: {_ownedRingIds.Count}, Equipped: {_equippedRingIds.Count}",
                    Colors.Cyan));
            });
        }

        private void GiveStarterRingsForTesting()
        {
            SafeExecutor.Execute(FEATURE_NAME, "StarterRings", () =>
            {
                // Give one of each type for testing
                _ownedRingIds.Add("hon_ring_narya");      // Ring of Fire (Elven)
                _ownedRingIds.Add("hon_ring_dwarf_1");    // First Dwarf ring
                _ownedRingIds.Add("hon_ring_nazgul_1");   // First Mortal ring
                
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] Starter rings granted for testing!", Colors.Green));
            });
        }

        private void OnDailyTick()
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "DailyTick", () =>
            {
                var settings = ModSettings.Get();
                
                // Update effect progression/decay
                _playerEffects?.OnDayPassed();

                // Apply current effects to player stats
                ApplyRingEffects(settings);

                // Process corruption if enabled
                if (settings.EnableRingCorruption)
                {
                    ProcessCorruption(settings);
                }

                // Update threat system if enabled
                if (settings.EnableRingThreats)
                {
                    ProcessThreat(settings);
                }
            });
        }

        private void ApplyRingEffects(ModSettings settings)
        {
            SafeExecutor.Execute(FEATURE_NAME, "ApplyEffects", () =>
            {
                if (Hero.MainHero == null) return;
                if (_equippedRingIds == null || _equippedRingIds.Count == 0) return;

                foreach (var ringId in _equippedRingIds)
                {
                    // Apply skill bonuses using helper method
                    var skillBonuses = RingItemManager.GetSkillBonuses(ringId);
                    foreach (var skillBonus in skillBonuses)
                    {
                        ApplySkillBonus(skillBonus.Key, skillBonus.Value);
                    }

                    // Apply stat modifiers
                    ApplyStatModifiers(ringId);
                }
            });
        }

        private void ApplySkillBonus(string skillId, int bonus)
        {
            // Note: Direct skill modification is complex in Bannerlord
            // This is a placeholder for actual implementation
            ModSettings.DebugLog($"Applied skill bonus: {skillId} +{bonus}");
        }

        private void ApplyStatModifiers(string ringId)
        {
            // Apply attribute modifiers, etc.
            var ring = RingItemManager.GetRing(ringId);
            string ringName = ring?.Name?.ToString() ?? ringId;
            ModSettings.DebugLog($"Applied ring effects from: {ringName}");
        }

        private void ProcessCorruption(ModSettings settings)
        {
            SafeExecutor.Execute(FEATURE_NAME, "Corruption", () =>
            {
                if (_playerEffects == null) return;
                if (_equippedRingIds == null || _equippedRingIds.Count == 0) return;

                float corruptionRate = settings.CorruptionRateMultiplier;
                
                foreach (var ringId in _equippedRingIds)
                {
                    // Corruption gain based on ring type - use helper methods
                    float baseCorruption = RingItemManager.GetCorruptionRate(ringId);
                    float actualCorruption = baseCorruption * corruptionRate;

                    // Add corruption via tracker method
                    _corruptionAccumulator += actualCorruption;

                    // One Ring has special effects
                    if (RingItemManager.IsOneRing(ringId))
                    {
                        _corruptionAccumulator += actualCorruption * 2f;
                    }
                }

                // Notify troop status system about corruption effects
                var troopStatusBehavior = TroopStatus.TroopStatusCampaignBehavior.Instance;
                float totalCorruption = _playerEffects.GetTotalCorruption();
                if (troopStatusBehavior != null && totalCorruption > 10f)
                {
                    bool hasOneRing = _equippedRingIds.Any(id => RingItemManager.IsOneRing(id));
                    troopStatusBehavior.ApplyRingCorruptionToTroops(totalCorruption, hasOneRing);
                }
            });
        }

        private void ProcessThreat(ModSettings settings)
        {
            SafeExecutor.Execute(FEATURE_NAME, "Threat", () =>
            {
                if (_playerEffects == null) return;
                
                var threatSystem = RingThreatSystem.Instance;
                if (threatSystem == null) return;

                float threatMultiplier = settings.ThreatRateMultiplier;
                
                foreach (var ringId in _equippedRingIds)
                {
                    // Use helper method to get threat level
                    float baseThreat = RingItemManager.GetThreatLevel(ringId);
                    float actualThreat = baseThreat * threatMultiplier;

                    threatSystem.AddThreat(actualThreat);
                }

                // Process threat events
                threatSystem.ProcessDailyThreat();
            });
        }

        #region Ring Management

        /// <summary>
        /// Equips a ring if possible.
        /// </summary>
        public bool EquipRing(string ringId)
        {
            if (!IsEnabled) return false;

            return SafeExecutor.Execute(FEATURE_NAME, "EquipRing", () =>
            {
                // Validate ring exists and is owned
                if (string.IsNullOrEmpty(ringId)) return false;
                if (!_ownedRingIds.Contains(ringId)) return false;
                if (_equippedRingIds.Contains(ringId)) return false;
                if (_equippedRingIds.Count >= MaxEquippedRings) return false;

                _equippedRingIds.Add(ringId);

                var ring = RingItemManager.GetRing(ringId);
                string ringName = ring?.Name?.ToString() ?? ringId;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Equipped: {ringName}", Colors.Green));

                return true;
            }, false);
        }

        /// <summary>
        /// Unequips a ring.
        /// </summary>
        public bool UnequipRing(string ringId)
        {
            if (!IsEnabled) return false;

            return SafeExecutor.Execute(FEATURE_NAME, "UnequipRing", () =>
            {
                if (string.IsNullOrEmpty(ringId)) return false;
                if (!_equippedRingIds.Contains(ringId)) return false;

                _equippedRingIds.Remove(ringId);

                var ring = RingItemManager.GetRing(ringId);
                string ringName = ring?.Name?.ToString() ?? ringId;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Unequipped: {ringName}", Colors.Yellow));

                return true;
            }, false);
        }

        /// <summary>
        /// Grants ownership of a ring.
        /// </summary>
        public void AcquireRing(string ringId)
        {
            if (!IsEnabled) return;

            SafeExecutor.Execute(FEATURE_NAME, "AcquireRing", () =>
            {
                if (string.IsNullOrEmpty(ringId)) return;
                if (_ownedRingIds.Contains(ringId)) return;

                _ownedRingIds.Add(ringId);

                var ring = RingItemManager.GetRing(ringId);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"★ Acquired: {ring?.Name?.ToString() ?? ringId}!", Colors.Yellow));
            });
        }

        /// <summary>
        /// Checks if a ring is equipped.
        /// </summary>
        public bool IsRingEquipped(string ringId)
        {
            try
            {
                return _equippedRingIds?.Contains(ringId) == true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the player owns a ring.
        /// </summary>
        public bool OwnsRing(string ringId)
        {
            try
            {
                return _ownedRingIds?.Contains(ringId) == true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all equipped rings.
        /// </summary>
        public List<ItemObject> GetEquippedRings()
        {
            var result = new List<ItemObject>();
            
            try
            {
                if (_equippedRingIds == null) return result;
                
                foreach (var id in _equippedRingIds)
                {
                    var ring = RingItemManager.GetRing(id);
                    if (ring != null) result.Add(ring);
                }
            }
            catch { }

            return result;
        }

        /// <summary>
        /// Gets all owned rings.
        /// </summary>
        public List<ItemObject> GetOwnedRings()
        {
            var result = new List<ItemObject>();
            
            try
            {
                if (_ownedRingIds == null) return result;
                
                foreach (var id in _ownedRingIds)
                {
                    var ring = RingItemManager.GetRing(id);
                    if (ring != null) result.Add(ring);
                }
            }
            catch { }

            return result;
        }

        #endregion

        #region Queries

        /// <summary>
        /// Gets a summary of current ring effects.
        /// </summary>
        public string GetEffectsSummary()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                
                sb.AppendLine($"Owned Rings: {_ownedRingIds?.Count ?? 0}");
                sb.AppendLine($"Equipped: {_equippedRingIds?.Count ?? 0}/{MaxEquippedRings}");
                
                if (_playerEffects != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Corruption: {_playerEffects.GetTotalCorruption():F1}%");
                    sb.AppendLine($"Power: {_playerEffects.GetTotalRingPower():F1}%");
                }

                return sb.ToString();
            }
            catch
            {
                return "Ring System data unavailable";
            }
        }

        #endregion
    }

    /// <summary>
    /// SaveableTypeDefiner for ring system data.
    /// </summary>
    public class RingSystemSaveableTypeDefiner : SaveableTypeDefiner
    {
        public RingSystemSaveableTypeDefiner() : base(726900101) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(HoNRingEffectTracker), 101);
        }

        // NOTE: Do NOT register common container types like Dictionary<string, float>
        // They are already registered by the game and re-registering causes crashes
    }
}
