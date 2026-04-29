using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            AssetName = string.Format("TexVar_{0}_Sampler_{1}",source.AssetName, sampler.AssetName);

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

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            return;
        }
    }
}
