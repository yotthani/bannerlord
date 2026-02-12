using System;
using TaleWorlds.Library;

namespace FaceLearner.Core
{
    /// <summary>
    /// Body section accordion VM with sliders
    /// </summary>
    public class BodySectionVM : ViewModel
    {
        private bool _isExpanded = false;
        private readonly string _name;
        private readonly Action<BodySectionVM> _onToggle;
        
        public BodySectionVM(string name, Action<BodySectionVM> onToggle = null)
        {
            _name = name;
            _onToggle = onToggle;
            Sliders = new MBBindingList<SliderVM>();
        }
        
        [DataSourceProperty] 
        public string Name => _name;
        
        [DataSourceProperty] 
        public string HeaderText => _isExpanded ? $"[-] {_name}" : $"[+] {_name}";
        
        [DataSourceProperty] 
        public MBBindingList<SliderVM> Sliders { get; }
        
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
