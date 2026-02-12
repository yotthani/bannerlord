using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace HeirOfNumenor.Features.TroopStatus
{
    /// <summary>
    /// Central manager for all troop status data.
    /// Provides access to status values and handles common operations.
    /// </summary>
    public class TroopStatusManager
    {
        private static TroopStatusManager _instance;
        public static TroopStatusManager Instance => _instance ??= new TroopStatusManager();

        // Status data keyed by troop ID
        private Dictionary<string, HoNTroopStatusData> _troopStatuses;

        // Events for status changes
        public event Action<string, HoNTroopStatusType, float, float> OnStatusChanged;
        public event Action<string, HoNTroopStatusType, float> OnThresholdReached;

        public TroopStatusManager()
        {
            _troopStatuses = new Dictionary<string, HoNTroopStatusData>();
        }

        /// <summary>
        /// Initialize or replace the status dictionary (for save/load).
        /// </summary>
        public void Initialize(Dictionary<string, HoNTroopStatusData> data)
        {
            _troopStatuses = data ?? new Dictionary<string, HoNTroopStatusData>();
            _instance = this;
        }

        /// <summary>
        /// Gets the raw data dictionary for saving.
        /// </summary>
        public Dictionary<string, HoNTroopStatusData> GetAllData()
        {
            return _troopStatuses;
        }

        #region Status Access

        /// <summary>
        /// Gets or creates status data for a troop type.
        /// </summary>
        public HoNTroopStatusData GetOrCreateTroopStatus(string troopId, int initialCount = 0)
        {
            if (string.IsNullOrEmpty(troopId))
                return null;

            if (!_troopStatuses.TryGetValue(troopId, out var data))
            {
                data = new HoNTroopStatusData(troopId, initialCount);
                _troopStatuses[troopId] = data;
            }

            return data;
        }

        /// <summary>
        /// Gets status data for a troop type if it exists.
        /// </summary>
        public HoNTroopStatusData GetTroopStatus(string troopId)
        {
            if (string.IsNullOrEmpty(troopId))
                return null;

            _troopStatuses.TryGetValue(troopId, out var data);
            return data;
        }

        /// <summary>
        /// Gets status data for a CharacterObject.
        /// </summary>
        public HoNTroopStatusData GetTroopStatus(CharacterObject troop)
        {
            return troop != null ? GetTroopStatus(troop.StringId) : null;
        }

        /// <summary>
        /// Checks if we have status data for a troop.
        /// </summary>
        public bool HasTroopStatus(string troopId)
        {
            return !string.IsNullOrEmpty(troopId) && _troopStatuses.ContainsKey(troopId);
        }

        /// <summary>
        /// Gets a specific status value for a troop.
        /// </summary>
        public float GetStatus(string troopId, HoNTroopStatusType statusType)
        {
            var data = GetTroopStatus(troopId);
            return data?.GetStatus(statusType) ?? statusType.GetDefaultValue();
        }

        /// <summary>
        /// Sets a specific status value for a troop.
        /// </summary>
        public void SetStatus(string troopId, HoNTroopStatusType statusType, float value)
        {
            var data = GetOrCreateTroopStatus(troopId);
            if (data != null)
            {
                float oldValue = data.GetStatus(statusType);
                data.SetStatus(statusType, value);
                float newValue = data.GetStatus(statusType);

                if (Math.Abs(oldValue - newValue) > 0.01f)
                {
                    OnStatusChanged?.Invoke(troopId, statusType, oldValue, newValue);
                    CheckThresholds(troopId, statusType, oldValue, newValue);
                }
            }
        }

        /// <summary>
        /// Modifies a status value by a delta.
        /// </summary>
        public float ModifyStatus(string troopId, HoNTroopStatusType statusType, float delta)
        {
            var data = GetOrCreateTroopStatus(troopId);
            if (data != null)
            {
                float oldValue = data.GetStatus(statusType);
                float change = data.ModifyStatus(statusType, delta);
                float newValue = data.GetStatus(statusType);

                if (Math.Abs(change) > 0.01f)
                {
                    OnStatusChanged?.Invoke(troopId, statusType, oldValue, newValue);
                    CheckThresholds(troopId, statusType, oldValue, newValue);
                }

                return change;
            }
            return 0f;
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Applies a status change to all troops in player's party.
        /// </summary>
        public void ApplyToAllPartyTroops(HoNTroopStatusType statusType, float delta)
        {
            var party = MobileParty.MainParty;
            if (party == null) return;

            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                if (element.Character != null && !element.Character.IsHero)
                {
                    ModifyStatus(element.Character.StringId, statusType, delta);
                }
            }
        }

        /// <summary>
        /// Applies natural daily changes to all tracked troops.
        /// </summary>
        public void ApplyDailyNaturalChanges()
        {
            foreach (var data in _troopStatuses.Values)
            {
                foreach (HoNTroopStatusType statusType in Enum.GetValues(typeof(HoNTroopStatusType)))
                {
                    float change = statusType.GetDailyNaturalChange();
                    if (Math.Abs(change) > 0.001f)
                    {
                        ModifyStatus(data.TroopId, statusType, change);
                    }
                }
            }
        }

        /// <summary>
        /// Syncs troop counts with current party roster.
        /// </summary>
        public void SyncWithPartyRoster()
        {
            var party = MobileParty.MainParty;
            if (party == null) return;

            // Get current roster counts
            var rosterCounts = new Dictionary<string, int>();
            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                if (element.Character != null && !element.Character.IsHero)
                {
                    rosterCounts[element.Character.StringId] = element.Number;
                }
            }

            // Update tracked troops
            foreach (var troopId in rosterCounts.Keys)
            {
                var data = GetOrCreateTroopStatus(troopId, rosterCounts[troopId]);
                data.CurrentCount = rosterCounts[troopId];
            }

            // Mark troops no longer in party
            foreach (var data in _troopStatuses.Values)
            {
                if (!rosterCounts.ContainsKey(data.TroopId))
                {
                    data.CurrentCount = 0;
                }
            }
        }

        #endregion

        #region Threshold Checking

        private void CheckThresholds(string troopId, HoNTroopStatusType statusType, float oldValue, float newValue)
        {
            // Check fear thresholds
            if (statusType == HoNTroopStatusType.Fear)
            {
                CheckThreshold(troopId, statusType, oldValue, newValue, HoNTroopStatusData.FearThresholds.Uneasy, "Troops grow uneasy");
                CheckThreshold(troopId, statusType, oldValue, newValue, HoNTroopStatusData.FearThresholds.Frightened, "Troops are frightened!");
                CheckThreshold(troopId, statusType, oldValue, newValue, HoNTroopStatusData.FearThresholds.Terrified, "Troops are TERRIFIED!");
                CheckThreshold(troopId, statusType, oldValue, newValue, HoNTroopStatusData.FearThresholds.Desertion, "Troops may desert!");
            }

            // Check bonding thresholds
            if (statusType == HoNTroopStatusType.Bonding)
            {
                CheckThreshold(troopId, statusType, oldValue, newValue, HoNTroopStatusData.BondingThresholds.Friendly, "Troops have become friendly");
                CheckThreshold(troopId, statusType, oldValue, newValue, HoNTroopStatusData.BondingThresholds.Loyal, "Troops are now loyal!");
                CheckThreshold(troopId, statusType, oldValue, newValue, HoNTroopStatusData.BondingThresholds.Devoted, "Troops are DEVOTED!");
            }
        }

        private void CheckThreshold(string troopId, HoNTroopStatusType statusType, float oldValue, float newValue, float threshold, string message)
        {
            // Crossed threshold going up
            if (oldValue < threshold && newValue >= threshold)
            {
                OnThresholdReached?.Invoke(troopId, statusType, threshold);
                
                var troop = GetCharacterObject(troopId);
                string troopName = troop?.Name?.ToString() ?? troopId;
                
                Color color = statusType.GetDirection() == StatusDirection.Negative 
                    ? Colors.Red 
                    : Colors.Green;
                    
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{troopName}: {message}", color));
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// Gets all troops that have crossed a fear threshold.
        /// </summary>
        public IEnumerable<HoNTroopStatusData> GetTroopsAboveFear(float threshold)
        {
            return _troopStatuses.Values
                .Where(d => d.CurrentCount > 0 && d.Fear >= threshold);
        }

        /// <summary>
        /// Gets all troops that have high bonding.
        /// </summary>
        public IEnumerable<HoNTroopStatusData> GetBondedTroops(float minBonding = 40f)
        {
            return _troopStatuses.Values
                .Where(d => d.CurrentCount > 0 && d.Bonding >= minBonding);
        }

        /// <summary>
        /// Gets the total count of troops at or above a bonding level.
        /// </summary>
        public int GetBondedTroopCount(float minBonding = 40f)
        {
            return _troopStatuses.Values
                .Where(d => d.Bonding >= minBonding)
                .Sum(d => d.CurrentCount);
        }

        /// <summary>
        /// Gets average bonding across all tracked troops.
        /// </summary>
        public float GetAverageBonding()
        {
            var withCount = _troopStatuses.Values.Where(d => d.CurrentCount > 0).ToList();
            if (!withCount.Any()) return 0f;

            float totalWeighted = withCount.Sum(d => d.Bonding * d.CurrentCount);
            int totalCount = withCount.Sum(d => d.CurrentCount);
            
            return totalCount > 0 ? totalWeighted / totalCount : 0f;
        }

        /// <summary>
        /// Gets average fear across all tracked troops.
        /// </summary>
        public float GetAverageFear()
        {
            var withCount = _troopStatuses.Values.Where(d => d.CurrentCount > 0).ToList();
            if (!withCount.Any()) return 0f;

            float totalWeighted = withCount.Sum(d => d.Fear * d.CurrentCount);
            int totalCount = withCount.Sum(d => d.CurrentCount);
            
            return totalCount > 0 ? totalWeighted / totalCount : 0f;
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

        /// <summary>
        /// Clears all troop status data.
        /// </summary>
        public void ClearAll()
        {
            _troopStatuses.Clear();
        }

        /// <summary>
        /// Gets debug info for all tracked troops.
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Troop Status Manager ===");
            sb.AppendLine($"Tracked troop types: {_troopStatuses.Count}");
            sb.AppendLine($"Average Bonding: {GetAverageBonding():F1}");
            sb.AppendLine($"Average Fear: {GetAverageFear():F1}");
            sb.AppendLine();

            foreach (var data in _troopStatuses.Values.Where(d => d.CurrentCount > 0).OrderByDescending(d => d.Bonding))
            {
                sb.AppendLine(data.ToDebugString());
            }

            return sb.ToString();
        }

        #endregion
    }
}
