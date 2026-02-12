using System;
using TaleWorlds.Library;

namespace FaceLearner.Core
{
    /// <summary>
    /// Morph category VM (accordion) - uses MorphVM
    /// </summary>
    public class MorphCategoryVM : ViewModel
    {
        private bool _isExpanded = false;
        private readonly string _name;
        private readonly Action<MorphCategoryVM> _onToggle;
        
        public MorphCategoryVM(string name, Action<MorphCategoryVM> onToggle = null)
        {
            _name = name;
            _onToggle = onToggle;
            Morphs = new MBBindingList<MorphVM>();
        }
        
        [DataSourceProperty] 
        public string Name => _name;
        
        [DataSourceProperty] 
        public string HeaderText => _isExpanded ? $"[-] {_name}" : $"[+] {_name}";
        
        [DataSourceProperty] 
        public MBBindingList<MorphVM> Morphs { get; }
        
        [DataSourceProperty]
        public bool IsExpanded
        {
            get => _isExpanded;
            set 
            { 
                if (_isExpanded != value) 
                { 
                    _isExpanded = value; 
                    OnPropertyChanged(nameof(IsExpanded)); 
                    OnPropertyChanged(nameof(HeaderText)); 
                } 
            }
        }
        
        public void ExecuteToggle() => _onToggle?.Invoke(this);
    }
}
