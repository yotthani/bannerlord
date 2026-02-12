using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.TroopStatus.UI
{
    /// <summary>
    /// ViewModel for a single status indicator icon.
    /// </summary>
    public class StatusIndicatorItemVM : ViewModel
    {
        private HoNTroopStatusType _statusType;
        private float _value;
        private string _spriteName;
        private string _colorHex;
        private bool _isVisible;
        private int _size;
        private HintViewModel _hint;

        public StatusIndicatorItemVM(HoNTroopStatusType statusType)
        {
            _statusType = statusType;
            var iconDef = StatusIconMapper.GetIconDefinition(statusType);
            
            if (iconDef != null)
            {
                _spriteName = iconDef.SpriteName;
                _colorHex = iconDef.DefaultColorHex;
                _size = iconDef.Size;
            }
            else
            {
                _spriteName = "BlankWhiteSquare_9";
                _colorHex = "#808080FF";
                _size = 16;
            }

            _isVisible = false;
            _hint = new HintViewModel();
        }

        public void UpdateValue(float value, string stateName = "")
        {
            _value = value;
            var iconDef = StatusIconMapper.GetIconDefinition(_statusType);
            
            if (iconDef != null)
            {
                // Update visibility
                IsVisible = value >= iconDef.ShowThreshold;

                // Update color based on value
                ColorHex = iconDef.GetColorHexForValue(value);

                // Update tooltip
                string tooltip = string.Format(iconDef.TooltipFormat, value, stateName);
                Hint = new HintViewModel(new TextObject(tooltip));
            }
        }

        [DataSourceProperty]
        public string SpriteName
        {
            get => _spriteName;
            set
            {
                if (_spriteName != value)
                {
                    _spriteName = value;
                    OnPropertyChanged(nameof(SpriteName));
                }
            }
        }

        [DataSourceProperty]
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                if (_colorHex != value)
                {
                    _colorHex = value;
                    OnPropertyChanged(nameof(ColorHex));
                }
            }
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
        }

        [DataSourceProperty]
        public int Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    OnPropertyChanged(nameof(Size));
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

        [DataSourceProperty]
        public float Value => _value;
    }

    /// <summary>
    /// ViewModel for all status indicators for a single troop type.
    /// </summary>
    public class TroopStatusIndicatorsVM : ViewModel
    {
        private string _troopId;
        private MBBindingList<StatusIndicatorItemVM> _indicators;
        private bool _hasAnyStatus;
        private bool _isHero;

        // Individual indicator references for direct binding
        private StatusIndicatorItemVM _fearIndicator;
        private StatusIndicatorItemVM _bondingIndicator;
        private StatusIndicatorItemVM _frustrationIndicator;
        private StatusIndicatorItemVM _loyaltyIndicator;

        public TroopStatusIndicatorsVM()
        {
            _indicators = new MBBindingList<StatusIndicatorItemVM>();
            InitializeIndicators();
        }

        public TroopStatusIndicatorsVM(string troopId, bool isHero = false) : this()
        {
            _troopId = troopId;
            _isHero = isHero;
            RefreshFromData();
        }

        private void InitializeIndicators()
        {
            // Create indicators for primary statuses in display order
            _fearIndicator = new StatusIndicatorItemVM(HoNTroopStatusType.Fear);
            _bondingIndicator = new StatusIndicatorItemVM(HoNTroopStatusType.Bonding);
            _frustrationIndicator = new StatusIndicatorItemVM(HoNTroopStatusType.Frustration);
            _loyaltyIndicator = new StatusIndicatorItemVM(HoNTroopStatusType.Loyalty);

            _indicators.Add(_fearIndicator);
            _indicators.Add(_bondingIndicator);
            _indicators.Add(_frustrationIndicator);
            _indicators.Add(_loyaltyIndicator);
        }

        public void SetTroopId(string troopId, bool isHero = false)
        {
            _troopId = troopId;
            _isHero = isHero;
            RefreshFromData();
        }

        public void RefreshFromData()
        {
            if (string.IsNullOrEmpty(_troopId) || _isHero)
            {
                HasAnyStatus = false;
                foreach (var indicator in _indicators)
                    indicator.IsVisible = false;
                return;
            }

            var statusData = TroopStatusManager.Instance?.GetTroopStatus(_troopId);
            if (statusData == null)
            {
                HasAnyStatus = false;
                foreach (var indicator in _indicators)
                    indicator.IsVisible = false;
                return;
            }

            // Update each indicator
            _fearIndicator.UpdateValue(statusData.Fear, statusData.GetFearState());
            _bondingIndicator.UpdateValue(statusData.Bonding, statusData.GetBondingState());
            _frustrationIndicator.UpdateValue(statusData.Frustration, GetFrustrationState(statusData.Frustration));
            _loyaltyIndicator.UpdateValue(statusData.Loyalty, GetLoyaltyState(statusData.Loyalty));

            // Check if any are visible
            HasAnyStatus = _fearIndicator.IsVisible || _bondingIndicator.IsVisible || 
                          _frustrationIndicator.IsVisible || _loyaltyIndicator.IsVisible;

            // Notify property changes for individual indicators
            OnPropertyChanged(nameof(FearIndicator));
            OnPropertyChanged(nameof(BondingIndicator));
            OnPropertyChanged(nameof(FrustrationIndicator));
            OnPropertyChanged(nameof(LoyaltyIndicator));
        }

        private string GetFrustrationState(float frustration)
        {
            if (frustration >= 80) return "Enraged";
            if (frustration >= 60) return "Angry";
            if (frustration >= 40) return "Frustrated";
            if (frustration >= 20) return "Annoyed";
            return "Content";
        }

        private string GetLoyaltyState(float loyalty)
        {
            if (loyalty >= 80) return "Devoted";
            if (loyalty >= 60) return "Loyal";
            if (loyalty >= 40) return "Neutral";
            if (loyalty >= 20) return "Wavering";
            return "Disloyal";
        }

        #region Data Source Properties

        [DataSourceProperty]
        public MBBindingList<StatusIndicatorItemVM> Indicators
        {
            get => _indicators;
            set
            {
                if (_indicators != value)
                {
                    _indicators = value;
                    OnPropertyChanged(nameof(Indicators));
                }
            }
        }

        [DataSourceProperty]
        public bool HasAnyStatus
        {
            get => _hasAnyStatus;
            set
            {
                if (_hasAnyStatus != value)
                {
                    _hasAnyStatus = value;
                    OnPropertyChanged(nameof(HasAnyStatus));
                }
            }
        }

        [DataSourceProperty]
        public bool IsHero
        {
            get => _isHero;
            set
            {
                if (_isHero != value)
                {
                    _isHero = value;
                    OnPropertyChanged(nameof(IsHero));
                }
            }
        }

        // Individual indicator properties for direct binding
        [DataSourceProperty]
        public StatusIndicatorItemVM FearIndicator => _fearIndicator;

        [DataSourceProperty]
        public StatusIndicatorItemVM BondingIndicator => _bondingIndicator;

        [DataSourceProperty]
        public StatusIndicatorItemVM FrustrationIndicator => _frustrationIndicator;

        [DataSourceProperty]
        public StatusIndicatorItemVM LoyaltyIndicator => _loyaltyIndicator;

        #endregion
    }

    /// <summary>
    /// Extension for PartyCharacterVM to include status indicators.
    /// </summary>
    public class PartyTroopStatusExtensionVM : ViewModel
    {
        private TroopStatusIndicatorsVM _statusIndicators;

        public PartyTroopStatusExtensionVM()
        {
            _statusIndicators = new TroopStatusIndicatorsVM();
        }

        public void Initialize(string troopId, bool isHero)
        {
            _statusIndicators.SetTroopId(troopId, isHero);
        }

        public void Refresh()
        {
            _statusIndicators.RefreshFromData();
        }

        [DataSourceProperty]
        public TroopStatusIndicatorsVM StatusIndicators
        {
            get => _statusIndicators;
            set
            {
                if (_statusIndicators != value)
                {
                    _statusIndicators = value;
                    OnPropertyChanged(nameof(StatusIndicators));
                }
            }
        }
    }
}
