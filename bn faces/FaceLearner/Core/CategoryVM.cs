using System;
using TaleWorlds.Library;

namespace FaceLearner.Core
{
    /// <summary>
    /// Generic category VM for accordion behavior
    /// </summary>
    public class CategoryVM : ViewModel
    {
        private bool _isExpanded;
        private readonly string _name;
        private readonly Action<CategoryVM> _onToggle;
        
        public CategoryVM(string name, Action<CategoryVM> onToggle)
        {
            _name = name;
            _onToggle = onToggle;
        }
        
        [DataSourceProperty]
        public string Name => _name;
        
        [DataSourceProperty]
        public string HeaderText => _isExpanded ? $"[-] {_name}" : $"[+] {_name}";
        
        [DataSourceProperty]
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
                OnPropertyChanged(nameof(HeaderText));
            }
        }
        
        /// <summary>
        /// Called from XML Command.Click - notifies parent for accordion behavior
        /// </summary>
        public void ExecuteToggle()
        {
            _onToggle?.Invoke(this);
        }
    }
}
