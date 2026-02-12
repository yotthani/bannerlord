using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.FiefManagement
{
    /// <summary>
    /// ViewModel for the Fief Management screen.
    /// Allows managing all clan fiefs from anywhere on the map.
    /// Now integrates with native SettlementProjectSelectionVM for building management.
    /// </summary>
    public class FiefManagementVM : ViewModel
    {
        private Action _onClose;
        private List<Settlement> _clanFiefs;
        private int _currentFiefIndex;

        // Current fief data
        private string _fiefName;
        private string _fiefType;
        private string _prosperityText;
        private string _loyaltyText;
        private string _securityText;
        private string _foodText;
        private string _garrisonText;
        private string _goldIncomeText;
        private string _currentBuildingText;
        private string _queueCountText;
        private int _fiefCount;
        private int _currentFiefNumber;
        private bool _hasPreviousFief;
        private bool _hasNextFief;
        private bool _hasAnyFiefs;
        private bool _useNativeProjectSelection;
        
        // Custom building lists (fallback)
        private MBBindingList<FiefBuildingVM> _buildings;
        private MBBindingList<FiefBuildingVM> _buildQueue;
        private HintViewModel _fiefHint;
        
        // Native building selection (preferred)
        private SettlementProjectSelectionVM _projectSelection;
        private SettlementGovernorSelectionVM _governorSelection;
        private bool _isSelectingGovernor;

        public FiefManagementVM(Action onClose)
        {
            _onClose = onClose;
            _buildings = new MBBindingList<FiefBuildingVM>();
            _buildQueue = new MBBindingList<FiefBuildingVM>();
            _fiefHint = new HintViewModel();
            _clanFiefs = new List<Settlement>();
            _currentFiefIndex = 0;
            _useNativeProjectSelection = true; // Try to use native first

            LoadClanFiefs();
            RefreshCurrentFief();
        }

        #region Native Project Selection Integration

        /// <summary>
        /// Gets the native project selection VM for the current settlement.
        /// </summary>
        [DataSourceProperty]
        public SettlementProjectSelectionVM ProjectSelection
        {
            get => _projectSelection;
            set
            {
                if (_projectSelection != value)
                {
                    _projectSelection = value;
                    OnPropertyChangedWithValue(value, nameof(ProjectSelection));
                }
            }
        }

        /// <summary>
        /// Gets the native governor selection VM.
        /// </summary>
        [DataSourceProperty]
        public SettlementGovernorSelectionVM GovernorSelection
        {
            get => _governorSelection;
            set
            {
                if (_governorSelection != value)
                {
                    _governorSelection = value;
                    OnPropertyChangedWithValue(value, nameof(GovernorSelection));
                }
            }
        }

        [DataSourceProperty]
        public bool IsSelectingGovernor
        {
            get => _isSelectingGovernor;
            set
            {
                if (_isSelectingGovernor != value)
                {
                    _isSelectingGovernor = value;
                    OnPropertyChangedWithValue(value, nameof(IsSelectingGovernor));
                }
            }
        }

        [DataSourceProperty]
        public bool UseNativeProjectSelection
        {
            get => _useNativeProjectSelection;
            set
            {
                if (_useNativeProjectSelection != value)
                {
                    _useNativeProjectSelection = value;
                    OnPropertyChangedWithValue(value, nameof(UseNativeProjectSelection));
                }
            }
        }

        /// <summary>
        /// Initialize native VMs for the current settlement.
        /// </summary>
        private void InitializeNativeVMs(Settlement settlement)
        {
            try
            {
                // Clean up old VMs
                _projectSelection?.OnFinalize();
                _governorSelection?.OnFinalize();

                if (settlement?.Town != null)
                {
                    // Create native project selection - this is the key integration!
                    _projectSelection = new SettlementProjectSelectionVM(settlement, OnBuildingQueueChanged);
                    
                    // Create native governor selection
                    _governorSelection = new SettlementGovernorSelectionVM(settlement, OnGovernorSelected);
                    
                    OnPropertyChanged(nameof(ProjectSelection));
                    OnPropertyChanged(nameof(GovernorSelection));
                    
                    UseNativeProjectSelection = true;
                    ModSettings.DebugLog("FiefManagement", $"Initialized native VMs for {settlement.Name}");
                }
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog("FiefManagement", $"Failed to initialize native VMs: {ex.Message}");
                UseNativeProjectSelection = false;
                // Fall back to custom implementation
            }
        }

        private void OnBuildingQueueChanged()
        {
            // Sync with our display
            RefreshBuildingInfo();
            
            // Actually apply the changes to the settlement
            ApplyBuildingChanges();
        }

        private void OnGovernorSelected(Hero governor)
        {
            var currentFief = GetCurrentFief();
            if (currentFief?.Town != null)
            {
                // Apply governor change
                currentFief.Town.Governor = governor;
                IsSelectingGovernor = false;
                RefreshCurrentFief();
                
                InformationManager.DisplayMessage(new InformationMessage(
                    governor != null 
                        ? $"{governor.Name} appointed as Governor of {currentFief.Name}" 
                        : $"Governor removed from {currentFief.Name}",
                    Colors.Green));
            }
        }

        private void ApplyBuildingChanges()
        {
            var currentFief = GetCurrentFief();
            if (currentFief?.Town == null || _projectSelection == null) return;

            try
            {
                // The SettlementProjectSelectionVM manages LocalDevelopmentList
                // We need to sync it back to the town
                var town = currentFief.Town;
                
                // Clear current queue and rebuild from native VM
                town.BuildingsInProgress.Clear();
                foreach (var building in _projectSelection.LocalDevelopmentList)
                {
                    town.BuildingsInProgress.Enqueue(building);
                }

                // Handle daily default
                if (_projectSelection.CurrentDailyDefault?.Building != null)
                {
                    // Set as current default
                    foreach (var b in town.Buildings)
                    {
                        if (b.BuildingType.IsDailyProject)
                        {
                            b.IsCurrentlyDefault = (b == _projectSelection.CurrentDailyDefault.Building);
                        }
                    }
                }

                ModSettings.DebugLog("FiefManagement", $"Applied building changes to {currentFief.Name}");
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog("FiefManagement", $"Failed to apply building changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Open governor selection panel.
        /// </summary>
        public void ExecuteOpenGovernorSelection()
        {
            if (_governorSelection != null)
            {
                IsSelectingGovernor = true;
            }
        }

        #endregion

        private void LoadClanFiefs()
        {
            _clanFiefs.Clear();

            var clan = Clan.PlayerClan;
            if (clan == null) return;

            // Get all settlements owned by player clan (towns and castles)
            foreach (var settlement in Settlement.All)
            {
                if (settlement.OwnerClan == clan && (settlement.IsTown || settlement.IsCastle))
                {
                    _clanFiefs.Add(settlement);
                }
            }

            // Sort by type (towns first) then by name
            _clanFiefs = _clanFiefs
                .OrderByDescending(s => s.IsTown)
                .ThenBy(s => s.Name.ToString())
                .ToList();

            FiefCount = _clanFiefs.Count;
            HasAnyFiefs = _clanFiefs.Count > 0;

            if (_currentFiefIndex >= _clanFiefs.Count)
                _currentFiefIndex = 0;
        }

        private Settlement GetCurrentFief()
        {
            if (!HasAnyFiefs || _currentFiefIndex >= _clanFiefs.Count)
                return null;
            return _clanFiefs[_currentFiefIndex];
        }

        private void RefreshCurrentFief()
        {
            _buildings.Clear();
            _buildQueue.Clear();

            var currentFief = GetCurrentFief();

            if (currentFief == null)
            {
                FiefName = "No Fiefs Owned";
                FiefType = "";
                ProsperityText = "-";
                LoyaltyText = "-";
                SecurityText = "-";
                FoodText = "-";
                GarrisonText = "-";
                GoldIncomeText = "-";
                CurrentBuildingText = "None";
                QueueCountText = "0";
                HasPreviousFief = false;
                HasNextFief = false;
                UseNativeProjectSelection = false;
                return;
            }

            var settlement = _clanFiefs[_currentFiefIndex];
            var town = settlement.Town;

            // Initialize native VMs for this settlement
            InitializeNativeVMs(settlement);

            // Basic info
            FiefName = settlement.Name?.ToString() ?? "Unknown";
            FiefType = settlement.IsTown ? "Town" : "Castle";
            CurrentFiefNumber = _currentFiefIndex + 1;

            // Navigation
            HasPreviousFief = _currentFiefIndex > 0;
            HasNextFief = _currentFiefIndex < _clanFiefs.Count - 1;

            // Stats
            if (town != null)
            {
                ProsperityText = $"{town.Prosperity:F0}";
                LoyaltyText = $"{town.Loyalty:F0}%";
                SecurityText = $"{town.Security:F0}%";
                FoodText = $"{town.FoodStocks:F0}";
                GarrisonText = $"{settlement.Town.GarrisonParty?.MemberRoster.TotalManCount ?? 0}";
                
                // Calculate income (simplified)
                float income = town.Prosperity * 0.1f + town.TradeTaxAccumulated;
                GoldIncomeText = $"{income:F0}/day";

                RefreshBuildingInfo();

                // Load buildings (fallback if native not available)
                if (!UseNativeProjectSelection)
                {
                    LoadBuildings(town);
                    LoadBuildQueue(town);
                }
            }

            // Update hint
            UpdateFiefHint(settlement);
        }

        private void RefreshBuildingInfo()
        {
            var currentFief = GetCurrentFief();
            if (currentFief?.Town == null) return;
            var town = currentFief.Town;

            // Current construction
            if (town.CurrentBuilding != null)
            {
                CurrentBuildingText = town.CurrentBuilding.BuildingType.Name?.ToString() ?? "Unknown";
            }
            else
            {
                CurrentBuildingText = "None";
            }

            // Queue count
            QueueCountText = $"{town.BuildingsInProgress.Count}";
        }

        private void LoadBuildings(Town town)
        {
            foreach (var building in town.Buildings)
            {
                var vm = new FiefBuildingVM(building, town, OnBuildingAddToQueue);
                _buildings.Add(vm);
            }
        }

        private void LoadBuildQueue(Town town)
        {
            foreach (var building in town.BuildingsInProgress)
            {
                var vm = new FiefBuildingVM(building, town, OnBuildingRemoveFromQueue);
                _buildQueue.Add(vm);
            }
        }

        private void UpdateFiefHint(Settlement settlement)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{settlement.Name}");
            sb.AppendLine($"Type: {(settlement.IsTown ? "Town" : "Castle")}");
            sb.AppendLine($"Owner: {settlement.OwnerClan?.Name}");
            
            if (settlement.Town != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Prosperity: {settlement.Town.Prosperity:F0}");
                sb.AppendLine($"Loyalty: {settlement.Town.Loyalty:F0}%");
                sb.AppendLine($"Security: {settlement.Town.Security:F0}%");
            }

            FiefHint = new HintViewModel(new TextObject(sb.ToString()));
        }

        #region Building Queue Management

        private void OnBuildingAddToQueue(FiefBuildingVM buildingVM)
        {
            try
            {
                var building = buildingVM.Building;
                var town = buildingVM.Town;

                if (building == null || town == null) return;
                if (building.CurrentLevel >= 3) return; // Max level
                if (town.BuildingsInProgress.Contains(building)) return; // Already in queue

                // Add to queue
                town.BuildingsInProgress.Enqueue(building);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"Added {building.BuildingType.Name} to construction queue in {town.Settlement.Name}",
                    Colors.Green));

                // Refresh display
                RefreshCurrentFief();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Error adding to queue: {ex.Message}", Colors.Red));
            }
        }

        private void OnBuildingRemoveFromQueue(FiefBuildingVM buildingVM)
        {
            try
            {
                var building = buildingVM.Building;
                var town = buildingVM.Town;

                if (building == null || town == null) return;

                // Can only remove if not currently being built
                if (town.CurrentBuilding == building)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Cannot remove building currently under construction!", Colors.Yellow));
                    return;
                }

                // Remove from queue (need to rebuild queue without this item)
                var newQueue = new Queue<Building>();
                while (town.BuildingsInProgress.Count > 0)
                {
                    var b = town.BuildingsInProgress.Dequeue();
                    if (b != building)
                        newQueue.Enqueue(b);
                }
                while (newQueue.Count > 0)
                    town.BuildingsInProgress.Enqueue(newQueue.Dequeue());

                InformationManager.DisplayMessage(new InformationMessage(
                    $"Removed {building.BuildingType.Name} from queue",
                    Colors.Yellow));

                RefreshCurrentFief();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Error removing from queue: {ex.Message}", Colors.Red));
            }
        }

        #endregion

        #region Commands

        public void ExecuteClose()
        {
            _onClose?.Invoke();
        }

        public void ExecutePreviousFief()
        {
            if (_currentFiefIndex > 0)
            {
                _currentFiefIndex--;
                RefreshCurrentFief();
            }
        }

        public void ExecuteNextFief()
        {
            if (_currentFiefIndex < _clanFiefs.Count - 1)
            {
                _currentFiefIndex++;
                RefreshCurrentFief();
            }
        }

        public void ExecuteRefresh()
        {
            LoadClanFiefs();
            RefreshCurrentFief();
        }

        #endregion

        #region Data Source Properties

        [DataSourceProperty]
        public string FiefName
        {
            get => _fiefName;
            set
            {
                if (_fiefName != value)
                {
                    _fiefName = value;
                    OnPropertyChanged(nameof(FiefName));
                }
            }
        }

        [DataSourceProperty]
        public string FiefType
        {
            get => _fiefType;
            set
            {
                if (_fiefType != value)
                {
                    _fiefType = value;
                    OnPropertyChanged(nameof(FiefType));
                }
            }
        }

        [DataSourceProperty]
        public string ProsperityText
        {
            get => _prosperityText;
            set
            {
                if (_prosperityText != value)
                {
                    _prosperityText = value;
                    OnPropertyChanged(nameof(ProsperityText));
                }
            }
        }

        [DataSourceProperty]
        public string LoyaltyText
        {
            get => _loyaltyText;
            set
            {
                if (_loyaltyText != value)
                {
                    _loyaltyText = value;
                    OnPropertyChanged(nameof(LoyaltyText));
                }
            }
        }

        [DataSourceProperty]
        public string SecurityText
        {
            get => _securityText;
            set
            {
                if (_securityText != value)
                {
                    _securityText = value;
                    OnPropertyChanged(nameof(SecurityText));
                }
            }
        }

        [DataSourceProperty]
        public string FoodText
        {
            get => _foodText;
            set
            {
                if (_foodText != value)
                {
                    _foodText = value;
                    OnPropertyChanged(nameof(FoodText));
                }
            }
        }

        [DataSourceProperty]
        public string GarrisonText
        {
            get => _garrisonText;
            set
            {
                if (_garrisonText != value)
                {
                    _garrisonText = value;
                    OnPropertyChanged(nameof(GarrisonText));
                }
            }
        }

        [DataSourceProperty]
        public string GoldIncomeText
        {
            get => _goldIncomeText;
            set
            {
                if (_goldIncomeText != value)
                {
                    _goldIncomeText = value;
                    OnPropertyChanged(nameof(GoldIncomeText));
                }
            }
        }

        [DataSourceProperty]
        public string CurrentBuildingText
        {
            get => _currentBuildingText;
            set
            {
                if (_currentBuildingText != value)
                {
                    _currentBuildingText = value;
                    OnPropertyChanged(nameof(CurrentBuildingText));
                }
            }
        }

        [DataSourceProperty]
        public string QueueCountText
        {
            get => _queueCountText;
            set
            {
                if (_queueCountText != value)
                {
                    _queueCountText = value;
                    OnPropertyChanged(nameof(QueueCountText));
                }
            }
        }

        [DataSourceProperty]
        public int FiefCount
        {
            get => _fiefCount;
            set
            {
                if (_fiefCount != value)
                {
                    _fiefCount = value;
                    OnPropertyChanged(nameof(FiefCount));
                }
            }
        }

        [DataSourceProperty]
        public int CurrentFiefNumber
        {
            get => _currentFiefNumber;
            set
            {
                if (_currentFiefNumber != value)
                {
                    _currentFiefNumber = value;
                    OnPropertyChanged(nameof(CurrentFiefNumber));
                }
            }
        }

        [DataSourceProperty]
        public bool HasPreviousFief
        {
            get => _hasPreviousFief;
            set
            {
                if (_hasPreviousFief != value)
                {
                    _hasPreviousFief = value;
                    OnPropertyChanged(nameof(HasPreviousFief));
                }
            }
        }

        [DataSourceProperty]
        public bool HasNextFief
        {
            get => _hasNextFief;
            set
            {
                if (_hasNextFief != value)
                {
                    _hasNextFief = value;
                    OnPropertyChanged(nameof(HasNextFief));
                }
            }
        }

        [DataSourceProperty]
        public bool HasAnyFiefs
        {
            get => _hasAnyFiefs;
            set
            {
                if (_hasAnyFiefs != value)
                {
                    _hasAnyFiefs = value;
                    OnPropertyChanged(nameof(HasAnyFiefs));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<FiefBuildingVM> Buildings
        {
            get => _buildings;
            set
            {
                if (_buildings != value)
                {
                    _buildings = value;
                    OnPropertyChanged(nameof(Buildings));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<FiefBuildingVM> BuildQueue
        {
            get => _buildQueue;
            set
            {
                if (_buildQueue != value)
                {
                    _buildQueue = value;
                    OnPropertyChanged(nameof(BuildQueue));
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel FiefHint
        {
            get => _fiefHint;
            set
            {
                if (_fiefHint != value)
                {
                    _fiefHint = value;
                    OnPropertyChanged(nameof(FiefHint));
                }
            }
        }

        #endregion

        #region Cleanup

        public override void OnFinalize()
        {
            base.OnFinalize();
            
            // Clean up native VMs
            _projectSelection?.OnFinalize();
            _governorSelection?.OnFinalize();
            _projectSelection = null;
            _governorSelection = null;
            
            _buildings?.Clear();
            _buildQueue?.Clear();
            _clanFiefs?.Clear();
        }

        #endregion
    }
}
