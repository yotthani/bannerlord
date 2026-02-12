extern alias SNV;
using System;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

using EngineTexture = TaleWorlds.Engine.Texture;
using TwoDimTexture = TaleWorlds.TwoDimension.Texture;

namespace LOTRAOM.FactionMap.Widgets
{
    /// <summary>
    /// Displays a faction artwork image in the detail panel.
    /// Uses the base Widget.OnRender() by setting Sprite — same approach as PolygonWidget.
    ///
    /// GOLDEN RULES:
    ///   - Never touch the Brush
    ///   - Never call base.OnHoverBegin()
    /// </summary>
    public class FactionImageWidget : ImageWidget
    {
        private string _imageId = "";
        private string _loadedImageId = "";
        private bool _textureLoaded;
        private bool _loadFailed;

        public FactionImageWidget(UIContext context) : base(context)
        {
            OverrideDefaultStateSwitchingEnabled = true;
        }

        [Editor(false)]
        public string ImageId
        {
            get => _imageId;
            set
            {
                if (_imageId != value)
                {
                    _imageId = value;
                    if (_loadedImageId != value)
                    {
                        _textureLoaded = false;
                        _loadFailed = false;
                        _loadedImageId = "";
                    }
                    OnPropertyChanged(value, nameof(ImageId));
                }
            }
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            TryLoadTexture();
        }

        // Let base Widget.OnRender() handle rendering — it draws Sprite automatically
        // No override needed!

        private void TryLoadTexture()
        {
            if (_textureLoaded || _loadFailed || string.IsNullOrEmpty(_imageId))
                return;

            try
            {
                string modPath = SubModule.ModulePath;
                if (string.IsNullOrEmpty(modPath)) { _loadFailed = true; return; }

                string file = System.IO.Path.Combine(modPath, "GUI", "SpriteData", "FactionMap", $"{_imageId}.png");
                if (!System.IO.File.Exists(file))
                {
                    _loadFailed = true;
                    SubModule.LogError($"[FactionImage] File not found: {file}");
                    return;
                }

                string fileName = System.IO.Path.GetFileName(file);
                string folder = System.IO.Path.GetDirectoryName(file);

                EngineTexture engineTex = EngineTexture.LoadTextureFromPath(fileName, folder);
                if (engineTex == null)
                {
                    _loadFailed = true;
                    SubModule.LogError($"[FactionImage] LoadTextureFromPath returned null for {_imageId}");
                    return;
                }

                var twoDimTex = new TwoDimTexture(new TaleWorlds.Engine.GauntletUI.EngineTexture(engineTex));
                Sprite = new RuntimeSprite(twoDimTex, twoDimTex.Width, twoDimTex.Height);
                _textureLoaded = true;
                _loadedImageId = _imageId;

            }
            catch (Exception ex)
            {
                _loadFailed = true;
                SubModule.LogError($"[FactionImage] Error: {ex.Message}");
            }
        }
    }
}
