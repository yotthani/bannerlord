using System;
using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.FormationPresets.Data
{
    /// <summary>
    /// Represents a saved formation preset - formation types and hero assignments.
    /// Tracks formation classes, captains, and hero troops for all 8 formations.
    /// </summary>
    public class HoNFormationPreset
    {
        [SaveableField(1)]
        private string _name;

        [SaveableField(2)]
        private DateTime _createdAt;

        /// <summary>
        /// Maps hero StringId to formation index (0-7).
        /// </summary>
        [SaveableField(3)]
        private Dictionary<string, int> _heroFormationAssignments;

        /// <summary>
        /// Set of hero StringIds that are captains (not just troops).
        /// If a hero is in HeroFormationAssignments but not in CaptainHeroIds, they're a troop.
        /// </summary>
        [SaveableField(4)]
        private HashSet<string> _captainHeroIds;

        /// <summary>
        /// Maps formation index (0-7) to DeploymentFormationClass (stored as int).
        /// -1 = Unset, 0 = Infantry, 1 = Ranged, 2 = Cavalry, 3 = HorseArcher, 
        /// 4 = InfantryAndRanged, 5 = CavalryAndHorseArcher
        /// </summary>
        [SaveableField(5)]
        private Dictionary<int, int> _formationClasses;

        /// <summary>
        /// Unique identifier for this preset.
        /// </summary>
        [SaveableField(6)]
        private string _id;

        public string Id
        {
            get => _id;
            set => _id = value;
        }

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => _createdAt = value;
        }

        public Dictionary<string, int> HeroFormationAssignments
        {
            get => _heroFormationAssignments;
            set => _heroFormationAssignments = value;
        }

        public HashSet<string> CaptainHeroIds
        {
            get => _captainHeroIds;
            set => _captainHeroIds = value;
        }

        public Dictionary<int, int> FormationClasses
        {
            get => _formationClasses;
            set => _formationClasses = value;
        }

        /// <summary>
        /// Check if a hero is assigned as captain (vs hero troop).
        /// </summary>
        public bool IsCaptain(string heroId)
        {
            return _captainHeroIds != null && _captainHeroIds.Contains(heroId);
        }

        /// <summary>
        /// Gets formation class for a formation index.
        /// Returns -1 (Unset) if not stored.
        /// </summary>
        public int GetFormationClass(int formationIndex)
        {
            if (_formationClasses != null && _formationClasses.TryGetValue(formationIndex, out int classValue))
            {
                return classValue;
            }
            return -1; // Unset
        }

        public HoNFormationPreset()
        {
            _id = Guid.NewGuid().ToString();
            _heroFormationAssignments = new Dictionary<string, int>();
            _captainHeroIds = new HashSet<string>();
            _formationClasses = new Dictionary<int, int>();
            _createdAt = DateTime.Now;
        }

        public HoNFormationPreset(string name) : this()
        {
            _name = name;
        }

        /// <summary>
        /// Gets a summary string for display.
        /// </summary>
        public string GetSummary()
        {
            int captainCount = _captainHeroIds?.Count ?? 0;
            int troopCount = (_heroFormationAssignments?.Count ?? 0) - captainCount;
            int formationCount = 0;
            if (_formationClasses != null)
            {
                foreach (var kvp in _formationClasses)
                {
                    if (kvp.Value >= 0) formationCount++; // Not Unset
                }
            }
            return $"{formationCount} formations, {captainCount} captains, {troopCount} troops";
        }
    }

    /// <summary>
    /// Defines saveable types for formation presets.
    /// </summary>
    public class FormationPresetSaveableTypeDefiner : SaveableTypeDefiner
    {
        public FormationPresetSaveableTypeDefiner() : base(726900601) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(HoNFormationPreset), 101);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<HoNFormationPreset>));
        }
    }
}
