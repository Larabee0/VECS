using System;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class Texture2D : Texture
    {
        public readonly static Texture2D MissingTexture;
        public readonly static Texture2D Zeroed;
        public readonly static Texture2D Black;
        public readonly static Texture2D Gray;
        public readonly static Texture2D Normal;
        public readonly static Texture2D Red;
        public readonly static Texture2D White;

        protected GPUBuffer _hostBuffer;

        static Texture2D()
        {
            Colour[] copyFrom = new Colour[4 * 4];
            Array.Fill(copyFrom, Colour.Black);

            var pink = new Colour(251, 60, 249, 255);

            copyFrom[0] = pink;
            copyFrom[1] = pink;
            copyFrom[4] = pink;
            copyFrom[5] = pink;
            
            copyFrom[10] = pink;
            copyFrom[11] = pink;
            copyFrom[14] = pink;
            copyFrom[15] = pink;
            
            MissingTexture = new(4, 4);
            MissingTexture.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.Clear);
            Zeroed = new(4, 4);
            Zeroed.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.Black);
            Black = new(4, 4);
            Black.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, new Vector4(0.5f, 0.5f, 0.5f, 1f).ToVkColor());
            Gray = new(4, 4);
            Gray.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, new Vector4(0.5f, 0.5f, 1f, 1f).ToVkColor());
            Normal = new(4, 4);
            Normal.CopyFromArray(copyFrom);
            
            Array.Fill(copyFrom, Colour.Red);
            Red = new(4, 4);
            Red.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.White);
            White = new(4, 4);
            White.CopyFromArray(copyFrom);
        }
        
        protected Texture2D(){}

        public Texture2D(int width, int height)
        {
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
 
            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
            }

            SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            UpdateDescriptor();
        }

        public Texture2D(int width, int height, VkFormat textureFormat, VkImageUsageFlags usage, uint mipMapCount = 1)
        {
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = textureFormat;
            _useageFlags = usage;
            _mipMapCount = mipMapCount;

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            if (usage.HasFlag(VkImageUsageFlags.DepthStencilAttachment))
            {
                SetImageLayout(VkImageLayout.DepthAttachmentStencilReadOnlyOptimal);
            }
            else
            {
                SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            }

            this.CreateImageView(GetImageViewCreateInfo());

            if (_mipMapCount > 1)
            {
                GenerateMipMaps();
            }

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
            }

            UpdateDescriptor();
        }

        public Texture2D(string filePath)
        {
            var surface = TextureLoader.LoadToSurface(filePath);
            _hostBuffer = TextureLoader.CopySurfaceToStagingBuffer(surface);
            _imageExtent = new(surface.Width, surface.Height, 1);
            _imageImageViewType = VkImageViewType.Image2D;

            this.CreateImage(GetImageCreateInfo());
            this.SetImageLayoutAndAspectFromUsage();
            this.CopyFromBuffer(_hostBuffer);

            if (_mipMapCount > 1)
            {
                GenerateMipMaps();
            }
            
            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler(GetSamplerCreateInfo());
            SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            UpdateDescriptor();
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            _hostBuffer?.Dispose();
            base.Dispose();
        }
    }
}