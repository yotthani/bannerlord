using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.FiefManagement
{
    /// <summary>
    /// ViewModel for a single building in a fief.
    /// </summary>
    public class FiefBuildingVM : ViewModel
    {
        private Building _building;
        private Town _town;
        private Action<FiefBuildingVM> _onAddToQueue;

        private string _name;
        private string _description;
        private string _levelText;
        private string _progressText;
        private string _costText;
        private int _currentLevel;
        private int _maxLevel;
        private bool _canUpgrade;
        private bool _isInQueue;
        private bool _isMaxLevel;
        private float _progressPercent;
        private string _buildingTypeId;
        private HintViewModel _hint;

        public FiefBuildingVM(Building building, Town town, Action<FiefBuildingVM> onAddToQueue)
        {
            _building = building;
            _town = town;
            _onAddToQueue = onAddToQueue;
            _hint = new HintViewModel();

            RefreshValues();
        }

        public override void RefreshValues()
        {
            base.RefreshValues();

            if (_building == null) return;

            var buildingType = _building.BuildingType;
            
            Name = buildingType.Name?.ToString() ?? "Unknown";
            Description = buildingType.Explanation?.ToString() ?? "";
            BuildingTypeId = buildingType.StringId;
            
            CurrentLevel = _building.CurrentLevel;
            MaxLevel = 3; // Standard max level
            
            IsMaxLevel = CurrentLevel >= MaxLevel;
            LevelText = IsMaxLevel ? "MAX" : $"Level {CurrentLevel}";

            // Check if in queue
            IsInQueue = _town.BuildingsInProgress.Contains(_building);

            // Check if can upgrade
            CanUpgrade = !IsMaxLevel && !IsInQueue && CanAffordUpgrade();

            // Progress for current construction
            if (IsInQueue && _town.CurrentBuilding == _building)
            {
                float progress = _building.BuildingProgress;
                float needed = _building.GetConstructionCost();
                ProgressPercent = needed > 0 ? (progress / needed) * 100f : 0f;
                ProgressText = $"Building: {ProgressPercent:F0}%";
            }
            else if (IsInQueue)
            {
                ProgressText = "In Queue";
                ProgressPercent = 0f;
            }
            else
            {
                ProgressText = "";
                ProgressPercent = 0f;
            }

            // Cost for next level
            if (!IsMaxLevel)
            {
                int cost = GetUpgradeCost();
                CostText = $"Cost: {cost} gold";
            }
            else
            {
                CostText = "";
            }

            // Hint with full details
            UpdateHint();
        }

        private void UpdateHint()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Name);
            sb.AppendLine(Description);
            sb.AppendLine();
            sb.AppendLine($"Current Level: {CurrentLevel}/{MaxLevel}");
            
            if (!IsMaxLevel)
            {
                sb.AppendLine($"Upgrade Cost: {GetUpgradeCost()} gold");
                sb.AppendLine($"Construction: {_building.GetConstructionCost()} progress needed");
            }

            if (IsInQueue)
            {
                int queuePosition = GetQueuePosition(_building) + 1;
                sb.AppendLine($"Queue Position: {queuePosition}");
            }

            Hint = new HintViewModel(new TextObject(sb.ToString()));
        }
        
        private int GetQueuePosition(Building building)
        {
            int index = 0;
            foreach (var b in _town.BuildingsInProgress)
            {
                if (b == building) return index;
                index++;
            }
            return -1;
        }

        private bool CanAffordUpgrade()
        {
            // Check if player has enough gold for the upgrade
            int cost = GetUpgradeCost();
            return Hero.MainHero.Gold >= cost;
        }

        private int GetUpgradeCost()
        {
            // Base cost increases per level
            int baseCost = 5000;
            return baseCost * (CurrentLevel + 1);
        }

        public void ExecuteAddToQueue()
        {
            _onAddToQueue?.Invoke(this);
        }

        public Building Building => _building;
        public Town Town => _town;

        #region Data Source Properties

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        [DataSourceProperty]
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        [DataSourceProperty]
        public string LevelText
        {
            get => _levelText;
            set
            {
                if (_levelText != value)
                {
                    _levelText = value;
                    OnPropertyChanged(nameof(LevelText));
                }
            }
        }

        [DataSourceProperty]
        public string ProgressText
        {
            get => _progressText;
            set
            {
                if (_progressText != value)
                {
                    _progressText = value;
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        [DataSourceProperty]
        public string CostText
        {
            get => _costText;
            set
            {
                if (_costText != value)
                {
                    _costText = value;
                    OnPropertyChanged(nameof(CostText));
                }
            }
        }

        [DataSourceProperty]
        public int CurrentLevel
        {
            get => _currentLevel;
            set
            {
                if (_currentLevel != value)
                {
                    _currentLevel = value;
                    OnPropertyChanged(nameof(CurrentLevel));
                }
            }
        }

        [DataSourceProperty]
        public int MaxLevel
        {
            get => _maxLevel;
            set
            {
                if (_maxLevel != value)
                {
                    _maxLevel = value;
                    OnPropertyChanged(nameof(MaxLevel));
                }
            }
        }

        [DataSourceProperty]
        public bool CanUpgrade
        {
            get => _canUpgrade;
            set
            {
                if (_canUpgrade != value)
                {
                    _canUpgrade = value;
                    OnPropertyChanged(nameof(CanUpgrade));
                }
            }
        }

        [DataSourceProperty]
        public bool IsInQueue
        {
            get => _isInQueue;
            set
            {
                if (_isInQueue != value)
                {
                    _isInQueue = value;
                    OnPropertyChanged(nameof(IsInQueue));
                }
            }
        }

        [DataSourceProperty]
        public bool IsMaxLevel
        {
            get => _isMaxLevel;
            set
            {
                if (_isMaxLevel != value)
                {
                    _isMaxLevel = value;
                    OnPropertyChanged(nameof(IsMaxLevel));
                }
            }
        }

        [DataSourceProperty]
        public float ProgressPercent
        {
            get => _progressPercent;
            set
            {
                if (_progressPercent != value)
                {
                    _progressPercent = value;
                    OnPropertyChanged(nameof(ProgressPercent));
                }
            }
        }

        [DataSourceProperty]
        public string BuildingTypeId
        {
            get => _buildingTypeId;
            set
            {
                if (_buildingTypeId != value)
                {
                    _buildingTypeId = value;
                    OnPropertyChanged(nameof(BuildingTypeId));
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel Hint
        {
            get => _hint;
            set
            {
                if (_hint != value)
                {
                    _hint = value;
                    OnPropertyChanged(nameof(Hint));
                }
            }
        }

        #endregion
    }
}
