using System;
using TaleWorlds.Library;

namespace FaceLearner.Core
{
    /// <summary>
    /// Slider VM for body parameters
    /// </summary>
    public class SliderVM : ViewModel
    {
        private float _value;
        private readonly float _min;
        private readonly float _max;
        private readonly float _step = 0.05f;
        private readonly string _name;
        private readonly Action<float> _onChanged;
        
        public SliderVM(string name, float initial, float min, float max, Action<float> onChanged)
        {
            _name = name;
            _value = initial;
            _min = min;
            _max = max;
            _onChanged = onChanged;
        }
        
        [DataSourceProperty] 
        public string Name => _name;
        
        [DataSourceProperty] 
        public float Value 
        { 
            get => _value; 
            set 
            { 
                if (_value != value) 
                { 
                    _value = MathF.Clamp(value, _min, _max); 
                    OnPropertyChanged(nameof(Value)); 
                    OnPropertyChanged(nameof(ValueText)); 
                    _onChanged?.Invoke(_value); 
                } 
            } 
        }
        
        [DataSourceProperty] 
        public string ValueText => _max > 50f ? $"{_value:F0}" : $"{_value:F2}";
        
        [DataSourceProperty] 
        public float MinValue => _min;
        
        [DataSourceProperty] 
        public float MaxValue => _max;
        
        public void ExecuteIncrease() => Value += _step * (_max - _min);
        public void ExecuteDecrease() => Value -= _step * (_max - _min);
    }
}
