using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace LOTRAOM.FactionMap.Widgets
{
    /// <summary>
    /// Simple Sprite wrapper around a runtime-loaded TwoDimension.Texture.
    /// Equivalent to the internal SpriteFromTexture but accessible from mod code.
    /// Used to set Widget.Sprite to a dynamically loaded texture.
    /// </summary>
    public class RuntimeSprite : Sprite
    {
        private Texture _texture;

        public override Texture Texture => _texture;

        public RuntimeSprite(Texture texture, int width, int height)
            : base("RuntimeSprite", width, height, SpriteNinePatchParameters.Empty)
        {
            _texture = texture;
        }

        public override Vec2 GetMinUvs()
        {
            return Vec2.Zero;
        }

        public override Vec2 GetMaxUvs()
        {
            return Vec2.One;
        }
    }
}
