using System;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class TextureSampler : DisposableAsset
    {
        protected readonly int _samplerId;
        protected readonly VkSamplerCreateInfo _samplerCreateInfo;
        internal VkSampler _textureSampler;

        public int SamplerId => _samplerId;
        public VkSampler VkSampler => _textureSampler;

        public VkSamplerCreateInfo SamplerCreateInfo => _samplerCreateInfo;

        public VkFilter MinFilter
        {
            get => _samplerCreateInfo.minFilter;
        }
        public VkFilter MagFilter
        {
            get => _samplerCreateInfo.magFilter;
        }
        public VkSamplerAddressMode WrapModeU
        {
            get => _samplerCreateInfo.addressModeU;
        }
        public VkSamplerAddressMode WrapModeV
        {
            get => _samplerCreateInfo.addressModeV;
        }
        public VkSamplerAddressMode WrapModeW
        {
            get => _samplerCreateInfo.addressModeW;
        }
        public bool AnisotropyEnable
        {
            get => _samplerCreateInfo.anisotropyEnable;
        }
        public float MaxAnisotropy
        {
            get => _samplerCreateInfo.maxAnisotropy;
        }
        public VkBorderColor BorderColour
        {
            get => _samplerCreateInfo.borderColor;
        }
        public bool UnnormalisedCoordinates
        {
            get => _samplerCreateInfo.unnormalizedCoordinates;
        }
        public bool CompareEnable
        {
            get => _samplerCreateInfo.compareEnable;
        }
        public VkCompareOp CompareOp
        {
            get => _samplerCreateInfo.compareOp;
        }
        public VkSamplerMipmapMode MipMapMode
        {
            get => _samplerCreateInfo.mipmapMode;
        }
        public float MipMapBias
        {
            get => _samplerCreateInfo.mipLodBias;
        }
        public float MinMipLOD
        {
            get => _samplerCreateInfo.minLod;
        }
        public float MaxMipLOD
        {
            get => _samplerCreateInfo.maxLod;
        }

        public unsafe TextureSampler(VkSamplerCreateInfo samplerCreateInfo)
        {
            _samplerCreateInfo = samplerCreateInfo;
            _samplerId = ShaderProperties.Hash((byte*)&samplerCreateInfo, (uint)sizeof(VkSamplerCreateInfo));
            AssetName = string.Format("Sampler_{0}", _samplerId);
            GraphicsDevice.DeviceAPI.vkCreateSampler(samplerCreateInfo, out _textureSampler);
            AssetDataBase<TextureSampler>.Add(this);
        }

        // public virtual VkSamplerCreateInfo GetSamplerCreateInfo()
        // {
        //     return new VkSamplerCreateInfo()
        //     {
        //         magFilter = MagFilter,
        //         minFilter = MinFilter,
        // 
        //         addressModeU = WrapModeU,
        //         addressModeV = WrapModeV,
        //         addressModeW = WrapModeW,
        // 
        //         anisotropyEnable = AnisotropyEnable,
        //         
        //         maxAnisotropy = MaxAnisotropy,
        // 
        //         borderColor = BorderColour,
        //         unnormalizedCoordinates = UnnormalisedCoordinates,
        //         compareEnable = CompareEnable,
        //         compareOp = CompareOp,
        //         mipmapMode = MipMapMode,
        //         mipLodBias = MipMapBias,
        //         minLod = MinMipLOD,
        //         maxLod = MaxMipLOD
        //     };
        // }

        public override void Dispose()
        {
            if(IsDisposed) return; 

            GC.SuppressFinalize(this);
            TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, VkImageView.Null, _textureSampler);
            _disposed = true;
            GC.ReRegisterForFinalize(this);
        }
    }
}
