using System;
using Vortice.Vulkan;

namespace VECS
{
    public class TextureVariant : Texture
    {
        public Texture Source;

        public TextureVariant(Texture source, TextureSampler sampler)
        {
            Source = source;
            _textureSampler = sampler;

            AssetName = $"TexVar_{source.AssetName}_Sampler_{sampler.AssetName}";

            UpdateDescriptor();
            AssetDataBase<TextureVariant>.Add(this);
        }

        internal override void UpdateDescriptor()
        {
            _imageInfo = new()
            {
                imageLayout = Source.ImageLayout,
                imageView = Source._imageView,
                sampler = TextureSampler
            };
        }

        public override void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            return;
        }

        public override void Reload()
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            return;
        }
    }
}
