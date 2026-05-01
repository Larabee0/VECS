namespace VECS.UI
{
    public class NoesisTexture : Noesis.Texture
    {
        public Texture2D Texture;
        public bool TextureInverted;
        public bool AlphaComponent;

        public override uint Width => (uint)Texture.Width;

        public override uint Height => (uint)Texture.Height;

        public override bool HasMipMaps => Texture.MipMapCount > 1;

        public override bool IsInverted => TextureInverted;

        public override bool HasAlpha => AlphaComponent;

        public NoesisTexture(Texture2D texture, bool inverted, bool alphaComponent)
        {
            Texture = texture;
            TextureInverted = inverted;
            AlphaComponent = alphaComponent;
        }
    }
}
