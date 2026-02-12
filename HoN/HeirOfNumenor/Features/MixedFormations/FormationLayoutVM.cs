using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.MixedFormations
{
    public class FormationLayoutVM : ViewModel
    {
        private Formation _formation;
        private MixedFormationLayoutBehavior _behavior;
        private MBBindingList<LayoutOptionVM> _layoutOptions;
        private bool _isVisible;
        private int _selectedIndex;
        
        public FormationLayoutVM(MixedFormationLayoutBehavior behavior)
        {
            _behavior = behavior;
            _layoutOptions = new MBBindingList<LayoutOptionVM>();
            InitializeOptions();
        }
        
        private void InitializeOptions()
        {
            _layoutOptions.Add(new LayoutOptionVM(FormationLayoutType.Vanilla, "Default", this));
            _layoutOptions.Add(new LayoutOptionVM(FormationLayoutType.InfantryFrontRangedBack, "Infantry Front", this));
            _layoutOptions.Add(new LayoutOptionVM(FormationLayoutType.RangedFrontInfantryBack, "Ranged Front", this));
            _layoutOptions.Add(new LayoutOptionVM(FormationLayoutType.RangedWingsInfantryCenter, "Ranged Wings", this));
            _layoutOptions.Add(new LayoutOptionVM(FormationLayoutType.Checkerboard, "Checkerboard", this));
        }
        
        public void SetFormation(Formation formation)
        {
            _formation = formation;
            IsVisible = formation != null;
            if (formation != null)
            {
                var current = _behavior?.GetFormationLayout(formation) ?? FormationLayoutType.Vanilla;
                SelectedIndex = (int)current;
            }
        }
        
        public void SelectLayout(FormationLayoutType layout)
        {
            if (_formation != null && _behavior != null)
            {
                _behavior.SetFormationLayout(_formation, layout);
                SelectedIndex = (int)layout;
            }
        }
        
        [DataSourceProperty]
        public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChangedWithValue(value, nameof(IsVisible)); } }
        
        [DataSourceProperty]
        public MBBindingList<LayoutOptionVM> LayoutOptions => _layoutOptions;
        
        [DataSourceProperty]
        public int SelectedIndex { get => _selectedIndex; set { _selectedIndex = value; OnPropertyChangedWithValue(value, nameof(SelectedIndex)); } }
    }
    
    public class LayoutOptionVM : ViewModel
    {
        private FormationLayoutType _type;
        private string _name;
        private FormationLayoutVM _parent;
        
        public LayoutOptionVM(FormationLayoutType type, string name, FormationLayoutVM parent)
        {
            _type = type;
            _name = name;
            _parent = parent;
        }
        
        public void ExecuteSelect() => _parent.SelectLayout(_type);
        
        [DataSourceProperty]
        public string Name => _name;
    }
}
