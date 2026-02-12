using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.TroopStatus
{
    /// <summary>
    /// Holds all status values for a single troop type (CharacterObject).
    /// Status values range from 0-100.
    /// </summary>
    public class HoNTroopStatusData
    {
        public const float MinValue = 0f;
        public const float MaxValue = 100f;

        /// <summary>
        /// The troop type ID (CharacterObject.StringId)
        /// </summary>
        [SaveableField(1)]
        public string TroopId;

        /// <summary>
        /// Current count of this troop type in party.
        /// Used for dilution calculations.
        /// </summary>
        [SaveableField(2)]
        public int CurrentCount;

        /// <summary>
        /// Days this troop type has been in party (weighted by survival).
        /// </summary>
        [SaveableField(3)]
        public float DaysInParty;

        /// <summary>
        /// Status values dictionary.
        /// </summary>
        [SaveableField(4)]
        private Dictionary<int, float> _statusValues;

        /// <summary>
        /// History of significant events for this troop type.
        /// </summary>
        [SaveableField(5)]
        private List<string> _eventHistory;

        /// <summary>
        /// Maximum history entries to keep.
        /// </summary>
        private const int MaxHistoryEntries = 20;

        public HoNTroopStatusData()
        {
            _statusValues = new Dictionary<int, float>();
            _eventHistory = new List<string>();
            InitializeDefaults();
        }

        public HoNTroopStatusData(string troopId, int count) : this()
        {
            TroopId = troopId;
            CurrentCount = count;
        }

        private void InitializeDefaults()
        {
            foreach (HoNTroopStatusType statusType in Enum.GetValues(typeof(HoNTroopStatusType)))
            {
                _statusValues[(int)statusType] = statusType.GetDefaultValue();
            }
        }

        #region Status Access

        /// <summary>
        /// Gets the current value of a status (0-100).
        /// </summary>
        public float GetStatus(HoNTroopStatusType statusType)
        {
            if (_statusValues == null)
                _statusValues = new Dictionary<int, float>();
                
            return _statusValues.TryGetValue((int)statusType, out float value) 
                ? value 
                : statusType.GetDefaultValue();
        }

        /// <summary>
        /// Sets a status to a specific value (clamped 0-100).
        /// </summary>
        public void SetStatus(HoNTroopStatusType statusType, float value)
        {
            if (_statusValues == null)
                _statusValues = new Dictionary<int, float>();
                
            _statusValues[(int)statusType] = MathF.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// Modifies a status by a delta amount (clamped 0-100).
        /// Returns the actual change applied.
        /// </summary>
        public float ModifyStatus(HoNTroopStatusType statusType, float delta)
        {
            float oldValue = GetStatus(statusType);
            float newValue = MathF.Clamp(oldValue + delta, MinValue, MaxValue);
            SetStatus(statusType, newValue);
            return newValue - oldValue;
        }

        /// <summary>
        /// Gets status as a percentage (0.0 - 1.0).
        /// </summary>
        public float GetStatusPercent(HoNTroopStatusType statusType)
        {
            return GetStatus(statusType) / MaxValue;
        }

        /// <summary>
        /// Checks if a status has reached a threshold.
        /// </summary>
        public bool IsStatusAbove(HoNTroopStatusType statusType, float threshold)
        {
            return GetStatus(statusType) >= threshold;
        }

        /// <summary>
        /// Checks if a status is below a threshold.
        /// </summary>
        public bool IsStatusBelow(HoNTroopStatusType statusType, float threshold)
        {
            return GetStatus(statusType) < threshold;
        }

        #endregion

        #region Convenience Properties

        /// <summary>
        /// Current fear level (0-100).
        /// </summary>
        public float Fear
        {
            get => GetStatus(HoNTroopStatusType.Fear);
            set => SetStatus(HoNTroopStatusType.Fear, value);
        }

        /// <summary>
        /// Current frustration level (0-100).
        /// </summary>
        public float Frustration
        {
            get => GetStatus(HoNTroopStatusType.Frustration);
            set => SetStatus(HoNTroopStatusType.Frustration, value);
        }

        /// <summary>
        /// Current bonding level (0-100).
        /// </summary>
        public float Bonding
        {
            get => GetStatus(HoNTroopStatusType.Bonding);
            set => SetStatus(HoNTroopStatusType.Bonding, value);
        }

        /// <summary>
        /// Current loyalty level (0-100).
        /// </summary>
        public float Loyalty
        {
            get => GetStatus(HoNTroopStatusType.Loyalty);
            set => SetStatus(HoNTroopStatusType.Loyalty, value);
        }

        /// <summary>
        /// Current battle experience level (0-100).
        /// </summary>
        public float BattleExperience
        {
            get => GetStatus(HoNTroopStatusType.BattleExperience);
            set => SetStatus(HoNTroopStatusType.BattleExperience, value);
        }

        /// <summary>
        /// Current ring exposure level (0-100).
        /// </summary>
        public float RingExposure
        {
            get => GetStatus(HoNTroopStatusType.RingExposure);
            set => SetStatus(HoNTroopStatusType.RingExposure, value);
        }

        #endregion

        #region Status Thresholds

        /// <summary>
        /// Fear thresholds for effects.
        /// </summary>
        public static class FearThresholds
        {
            public const float Uneasy = 30f;      // Start showing warnings
            public const float Frightened = 50f;  // Performance penalty
            public const float Terrified = 70f;   // May flee in battle
            public const float Desertion = 85f;   // Will desert
        }

        /// <summary>
        /// Bonding thresholds for effects.
        /// </summary>
        public static class BondingThresholds
        {
            public const float Acquainted = 20f;     // Know the player
            public const float Friendly = 40f;       // Small bonuses
            public const float Loyal = 60f;          // Good bonuses, resistance
            public const float Devoted = 75f;        // Captain threshold
            public const float Legendary = 90f;      // Maximum bonuses
        }

        /// <summary>
        /// Gets a description of the current fear state.
        /// </summary>
        public string GetFearState()
        {
            float fear = Fear;
            if (fear >= FearThresholds.Desertion) return "Deserting";
            if (fear >= FearThresholds.Terrified) return "Terrified";
            if (fear >= FearThresholds.Frightened) return "Frightened";
            if (fear >= FearThresholds.Uneasy) return "Uneasy";
            return "Steady";
        }

        /// <summary>
        /// Gets a description of the current bonding state.
        /// </summary>
        public string GetBondingState()
        {
            float bonding = Bonding;
            if (bonding >= BondingThresholds.Legendary) return "Legendary";
            if (bonding >= BondingThresholds.Devoted) return "Devoted";
            if (bonding >= BondingThresholds.Loyal) return "Loyal";
            if (bonding >= BondingThresholds.Friendly) return "Friendly";
            if (bonding >= BondingThresholds.Acquainted) return "Acquainted";
            return "New";
        }

        #endregion

        #region Event History

        /// <summary>
        /// Adds an event to the history.
        /// </summary>
        public void AddEvent(string eventDescription)
        {
            if (_eventHistory == null)
                _eventHistory = new List<string>();
                
            _eventHistory.Add($"[Day {(int)DaysInParty}] {eventDescription}");
            
            // Keep history manageable
            while (_eventHistory.Count > MaxHistoryEntries)
                _eventHistory.RemoveAt(0);
        }

        /// <summary>
        /// Gets the event history.
        /// </summary>
        public IReadOnlyList<string> GetEventHistory()
        {
            return _eventHistory ?? new List<string>();
        }

        #endregion

        #region Calculations

        /// <summary>
        /// Calculates effective morale based on all statuses.
        /// </summary>
        public float CalculateEffectiveMorale()
        {
            // Base morale
            float morale = 50f;

            // Positive factors
            morale += Bonding * 0.3f;       // Up to +30 from bonding
            morale += Loyalty * 0.2f - 10f; // Up to +10 from loyalty (centered at 50)

            // Negative factors
            morale -= Fear * 0.4f;          // Up to -40 from fear
            morale -= Frustration * 0.3f;   // Up to -30 from frustration

            return MathF.Clamp(morale, 0f, 100f);
        }

        /// <summary>
        /// Calculates effective morale with full breakdown for tooltips.
        /// Uses native ExplainedNumber pattern for integration.
        /// </summary>
        public ExplainedNumber CalculateEffectiveMoraleExplained(bool includeDescription = false)
        {
            var result = new ExplainedNumber(50f, includeDescription, 
                new TextObject("{=hon_base_morale}Base Morale"));

            // Positive factors
            if (Bonding > 0f)
            {
                result.Add(Bonding * 0.3f, new TextObject("{=hon_bonding}Unit Cohesion"));
            }
            
            float loyaltyEffect = Loyalty * 0.2f - 10f;
            if (System.Math.Abs(loyaltyEffect) > 0.5f)
            {
                result.Add(loyaltyEffect, new TextObject("{=hon_loyalty}Troop Loyalty"));
            }

            // Battle experience provides confidence
            if (BattleExperience > 30f)
            {
                result.Add((BattleExperience - 30f) * 0.15f, 
                    new TextObject("{=hon_experience}Battle Experience"));
            }

            // Negative factors
            if (Fear > 0f)
            {
                result.Add(-Fear * 0.4f, new TextObject("{=hon_fear}Fear"));
            }

            if (Frustration > 0f)
            {
                result.Add(-Frustration * 0.3f, new TextObject("{=hon_frustration}Frustration"));
            }

            // Ring corruption exposure
            if (RingExposure > 10f)
            {
                result.Add(-RingExposure * 0.1f, 
                    new TextObject("{=hon_ring_exposure}Dark Influence"));
            }

            result.LimitMin(0f);
            result.LimitMax(100f);

            return result;
        }

        /// <summary>
        /// Calculates resistance to fear based on bonding.
        /// Returns a multiplier (0.5 - 1.0) for fear gain.
        /// </summary>
        public float CalculateFearResistance()
        {
            // High bonding = more resistance to fear
            // At 0 bonding: 1.0x fear gain (no resistance)
            // At 100 bonding: 0.5x fear gain (50% resistance)
            return 1.0f - (Bonding / MaxValue * 0.5f);
        }

        /// <summary>
        /// Calculates combat bonus from bonding.
        /// Returns a percentage bonus (0-15%).
        /// </summary>
        public float CalculateCombatBonus()
        {
            // 0-15% bonus based on bonding
            return Bonding / MaxValue * 0.15f;
        }

        #endregion

        /// <summary>
        /// Creates a debug string of all status values.
        /// </summary>
        public string ToDebugString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Troop: {TroopId} (Count: {CurrentCount}, Days: {DaysInParty:F1})");
            foreach (var kvp in _statusValues)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value:F1}");
            }
            return sb.ToString();
        }
    }
}
