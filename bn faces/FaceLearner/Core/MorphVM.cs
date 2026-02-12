using System;
using TaleWorlds.Library;

namespace FaceLearner.Core
{
    /// <summary>
    /// Morph VM for face morphs
    /// </summary>
    public class MorphVM : ViewModel
    {
        private float _value;
        private readonly int _index;
        private readonly string _name;
        private readonly Action<int, float> _onChanged;
        
        public MorphVM(string name, int index, float initial, Action<int, float> onChanged)
        {
            _name = name;
            _index = index;
            _value = initial;
            _onChanged = onChanged;
        }
        
        [DataSourceProperty] 
        public string Name => _name;
        
        [DataSourceProperty] 
        public int Index => _index;
        
        [DataSourceProperty] 
        public float Value 
        { 
            get => _value; 
            set 
            { 
                if (_value != value) 
                { 
                    _value = MathF.Clamp(value, -1f, 5f);  // Extended range like learning
                    OnPropertyChanged(nameof(Value)); 
                    OnPropertyChanged(nameof(ValueText)); 
                    _onChanged?.Invoke(_index, _value); 
                } 
            } 
        }
        
        [DataSourceProperty] 
        public string ValueText => $"{_value:F2}";
        
        /// <summary>
        /// Set value without triggering callback (for external updates)
        /// </summary>
        public void SetValueSilent(float value)
        {
            _value = MathF.Clamp(value, -1f, 5f);  // Extended range
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(ValueText));
        }
        
        public void ExecuteIncrease() => Value += 0.05f;
        public void ExecuteDecrease() => Value -= 0.05f;
    }
}
