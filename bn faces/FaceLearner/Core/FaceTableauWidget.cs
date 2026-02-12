using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace FaceLearner.Core
{
    /// <summary>
    /// Widget with HeroVisualCode and camera properties
    /// </summary>
    public class FaceTableauWidget : TextureWidget
    {
        private string _heroVisualCode;
        private float _zoomLevel = 1.0f;
        private float _heightOffset = 0f;
        private float _horizontalOffset = 0f;
        private float _characterScale = 1.0f;

        [Editor(false)]
        public string HeroVisualCode
        {
            get => _heroVisualCode;
            set
            {
                if (value != _heroVisualCode)
                {
                    _heroVisualCode = value;
                    OnPropertyChanged(value, "HeroVisualCode");
                    SetTextureProviderProperty("HeroVisualCode", value);
                }
            }
        }
        
        [Editor(false)]
        public float ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (value != _zoomLevel)
                {
                    _zoomLevel = value;
                    OnPropertyChanged(value, "ZoomLevel");
                    SetTextureProviderProperty("ZoomLevel", value);
                }
            }
        }
        
        [Editor(false)]
        public float HeightOffset
        {
            get => _heightOffset;
            set
            {
                if (value != _heightOffset)
                {
                    _heightOffset = value;
                    OnPropertyChanged(value, "HeightOffset");
                    SetTextureProviderProperty("HeightOffset", value);
                }
            }
        }
        
        [Editor(false)]
        public float HorizontalOffset
        {
            get => _horizontalOffset;
            set
            {
                if (value != _horizontalOffset)
                {
                    _horizontalOffset = value;
                    OnPropertyChanged(value, "HorizontalOffset");
                    SetTextureProviderProperty("HorizontalOffset", value);
                }
            }
        }
        
        [Editor(false)]
        public float CharacterScale
        {
            get => _characterScale;
            set
            {
                if (value != _characterScale)
                {
                    _characterScale = value;
                    OnPropertyChanged(value, "CharacterScale");
                    SetTextureProviderProperty("CharacterScale", value);
                }
            }
        }

        public FaceTableauWidget(UIContext context) : base(context)
        {
            base.TextureProviderName = "FaceTableauTextureProvider";
            _isRenderRequestedPreviousFrame = true;
        }

        protected override void OnMousePressed()
        {
            SetTextureProviderProperty("CurrentlyRotating", true);
        }

        protected override void OnMouseReleased()
        {
            SetTextureProviderProperty("CurrentlyRotating", false);
        }
    }
}
