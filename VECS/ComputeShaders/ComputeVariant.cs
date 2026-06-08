using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ComputeVariant : DisposableAsset
    {
        internal unsafe void* pUniformBuffer;
        internal bool localUniformAllocation;

        private readonly ComputePipeline _computePipeline;
        private readonly uint _variantIndex;
        private readonly ITextureProvider[][] _textures;

        private GPUBuffer _tempUniformBuffer;
        private DescriptorSetInfo[] _tempDescriptorSetInfos;
        internal bool _allowTmpBufferAllocation;

        public uint VariantIndex => _variantIndex;

        public PushConstantsHandler PushConstantsHandler => _computePipeline.PushConstantsHandler;

        internal unsafe ComputeVariant(string name, ComputePipeline pipeline, bool localUniformAlloc = true, bool allowTmpBufferAllocation = true)
        {
            AssetName = pipeline.AssetName + '.' + name;
            _variantIndex = pipeline.GetNextVariantIndex();
            _computePipeline = pipeline;
            _allowTmpBufferAllocation = allowTmpBufferAllocation;
            localUniformAllocation = localUniformAlloc && pipeline.UniformBufferSize > 0;


            if (localUniformAlloc && allowTmpBufferAllocation && pipeline.UniformBufferSize > 0)
            {
                AllocateTemporaryBuffers();
            }
            else
            {
                pUniformBuffer = null;
            }


            _textures = new ITextureProvider[pipeline.DescriptorSetCount][];
            for (int i = 0; i < pipeline.DescriptorSetCount; i++)
            {
                if (pipeline._descriptorSetInfos[i].HasImages)
                {
                    _textures[i] = new ITextureProvider[pipeline._descriptorSetInfos[i].ImageCount];
                    for (int j = 0; j < pipeline._descriptorSetInfos[i].BindingCount; j++)
                    {
                        var binding = pipeline._descriptorSetInfos[i].DescriptorBindings[j];
                        if (!binding.Image) continue;
                        var imageIndex = pipeline._descriptorSetInfos[i].BindingPointToImageIndex[binding.BindPoint];
                        var engineTexture = EngineTextures.TryGetTexture(binding.Id);
                        if (engineTexture != null)
                        {
                            _textures[i][imageIndex] = engineTexture;
                        }
                        else if (binding.VkSetLayoutBinding.descriptorCount > 1)
                        {
                            var fill = _textures[i][imageIndex] = new BindingArrayTexture((int)binding.VkSetLayoutBinding.descriptorCount);

                            for (int k = 0; k < fill.ImageCount; k++)
                            {
                                fill.SetTexture(EngineTextures.MissingTexture, k);
                            }
                        }
                        else
                        {
                            _textures[i][imageIndex] = (SingleTexture)EngineTextures.MissingTexture;
                        }
                        WriteTexturesToDescriptorBuffer(binding.DescriptorSetIndex, binding.BindPoint, _textures[i][imageIndex]);
                    }
                }
            }

            _computePipeline.AddVariant(this);
            AssetDataBase<ComputeVariant>.Add(this);
        }

        public unsafe void AllocateTemporaryBuffers()
        {
            if (!localUniformAllocation) return;
            if (!_allowTmpBufferAllocation) return;

            _tempDescriptorSetInfos = _computePipeline.GetTemporaryDescriptorSetInfos();
            _tempUniformBuffer = new(_computePipeline.UniformBufferSize, 1, _computePipeline.UniformFlags, true, false, false);
            pUniformBuffer = _tempUniformBuffer.HostPtr;
        }

        public void DiposeTemporaryBuffers()
        {
            _tempUniformBuffer?.EnqueueForDisposal();
            if(_tempDescriptorSetInfos != null)
            {
                for (int i = 0; i < _tempDescriptorSetInfos.Length; i++)
                {
                    _tempDescriptorSetInfos[i]?.Dispose();
                }
                _tempDescriptorSetInfos = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LookUpProperty(int propertyId, out ShaderProperty propertyInfo)
        {
            return _computePipeline.LookUpProperty(propertyId, out propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUInt(int propertyId, uint value)
        {
            WriteToBuffer(propertyId, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt(int propertyId, int value)
        {
            WriteToBuffer(propertyId, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(int propertyId, float value)
        {
            WriteToBuffer(propertyId, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector2(int propertyId, Vector2 value)
        {
            WriteToBuffer(propertyId, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform<T>(int propertyId, T value) where T : unmanaged
        {
            WriteToBuffer(propertyId, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBuffer<T>(int propertyId, T value) where T : unmanaged
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                WriteToBuffer(propertyInfo, value);
            }
        }
        public unsafe void WriteToBuffer<T>(ShaderProperty propertyInfo, T element) where T : unmanaged
        {
            _computePipeline.WriteToUniformBuffer(pUniformBuffer, propertyInfo,element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DescriptorSetInfo GetDescriptorInfo(uint setIndex)
        {
            if (localUniformAllocation)
            {
                return _tempDescriptorSetInfos[setIndex];
            }
            else
            {
                return _computePipeline._descriptorSetInfos[setIndex];
            }
        }

        public void SetStorageBuffer(int propertyId, SwapChainBuffer buffer)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = GetDescriptorInfo(propertyInfo.SetIndex);
                uint variant = localUniformAllocation ? 0 : VariantIndex;
                for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
                {
                    setInfo.WriteDescriptors(i, propertyInfo.BindPoint, variant, buffer[i]);
                }
            }
        }

        public void SetStorageBuffer(int propertyId, GPUBuffer buffer)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = GetDescriptorInfo(propertyInfo.SetIndex);
                uint variant = localUniformAllocation ? 0 : VariantIndex;
                for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
                {
                    setInfo.WriteDescriptors(i, propertyInfo.BindPoint, variant, buffer);
                }
            }
        }

        private unsafe void WriteTexturesToDescriptorBuffer(uint setIndex, uint bindingIndex, ITextureProvider textures)
        {
            var setInfo = GetDescriptorInfo(setIndex);
            uint variant = localUniformAllocation ? 0 : VariantIndex;

            VkDescriptorImageInfo* imageInfos = stackalloc VkDescriptorImageInfo[textures.ImageCount];

            for (int i = 0; i < textures.ImageCount; i++)
            {
                imageInfos[i] = textures.GetTexture(i).ImageInfo;
            }

            SetTextures(bindingIndex, setInfo, variant, imageInfos, (uint)textures.ImageCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void SetTextures(uint bindingIndex, DescriptorSetInfo setInfo, uint variant, VkDescriptorImageInfo* imageInfos, uint imageCount)
        {
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                setInfo.WriteDescriptors(i, bindingIndex, variant, imageInfos, imageCount, setInfo.DescriptorBindings[bindingIndex].DescriptorType);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetTexturesUnsafe(int propertyId, VkDescriptorImageInfo* imageInfos, uint imageCount)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.Image)
            {
                var setInfo = GetDescriptorInfo(propertyInfo.SetIndex);
                uint variant = localUniformAllocation ? 0 : VariantIndex;
                SetTextures(propertyInfo.BindPoint, setInfo, variant, imageInfos, imageCount);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTextures(int propertyId, ITextureProvider textureProvider)
        {
            if (textureProvider == null) return;
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.Image)
            {
                SetTextures(propertyInfo.SetIndex, propertyInfo.BindPoint, textureProvider);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTextures(uint setIndex, uint bindingIndex, ITextureProvider textures)
        {
            if (textures == null) return;
            int imageIndex = _computePipeline._descriptorSetInfos[setIndex].BindingPointToImageIndex[bindingIndex];
            _textures[setIndex][imageIndex] = textures;
            WriteTexturesToDescriptorBuffer(setIndex, bindingIndex, textures);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(int propertyId, Texture texture)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.Image)
            {
                SetTexture(propertyInfo.SetIndex, propertyInfo.BindPoint, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(uint setIndex, uint bindingIndex, Texture texture, int index = 0)
        {
            int imageIndex = _computePipeline._descriptorSetInfos[setIndex].BindingPointToImageIndex[bindingIndex];

            if (_textures[setIndex][imageIndex].First == texture) return;
            _textures[setIndex][imageIndex].SetTexture(texture, index);
            WriteTexturesToDescriptorBuffer(setIndex, bindingIndex, _textures[setIndex][imageIndex]);
        }

        private void WriteUniformToDescriptorBuffers()
        {
            if (!_computePipeline.HasUniforms) return;
            for (uint i = 0; i < _computePipeline.DescriptorSetCount; i++)
            {
                var setInfo = _tempDescriptorSetInfos[i];

                for (uint j = 0; j < setInfo.BindingCount; j++)
                {
                    var binding = setInfo.DescriptorBindings[j];

                    if (!binding.UniformBuffer) continue;

                    var internalOffset = _computePipeline.InternalUniformBufferOffset(binding.DescriptorSetIndex, binding.BindPoint);
                    var global = EngineBuffers.TryGetBuffer(binding.Id);
                    VkDescriptorAddressInfoEXT addressRange;
                    if (global != null)
                    {
                        addressRange = global[0].GetBufferAddressRangeBytes();
                    }
                    else
                    {
                        addressRange = _tempUniformBuffer.GetBufferAddressRangeBytes(internalOffset, binding.BufferSize);
                    }

                    for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                    {
                        setInfo.DescriptorBuffers[frameIndex].SetBufferBinding(addressRange, binding.DescriptorType, 0, binding.BindPoint);
                    }
                }
            }
        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            if (localUniformAllocation)
            {
                var descriptorSetCount = _computePipeline.DescriptorSetCount;
                VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[descriptorSetCount];
                ulong* offsets = stackalloc ulong[descriptorSetCount];
                uint* indices = stackalloc uint[descriptorSetCount];
                GPUBufferExtensions.WriteFromHostDelayed(_tempUniformBuffer, 0, _computePipeline.UniformBufferSize);
                WriteUniformToDescriptorBuffers();
                for (uint i = 0; i < descriptorSetCount; i++)
                {
                    _tempDescriptorSetInfos[i].WriteFromBuffers(frameIndex);
                    var buffer = _tempDescriptorSetInfos[i].DescriptorBuffers[frameIndex];
                    bindingInfo[i] = buffer.BindingInfo;
                    offsets[i] = 0;
                    indices[i] = i;
                }

                _computePipeline.Dispatch(commandBuffer, VariantIndex, bindingInfo, offsets, indices, workGroupCountX, workGroupCountY, workGroupCountZ);
            }
            else
            {
                _computePipeline.Dispatch(commandBuffer,frameIndex,VariantIndex,workGroupCountX, workGroupCountY, workGroupCountZ);
            }
        }

        public override unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
            _tempUniformBuffer?.EnqueueForDisposal();
            _tempUniformBuffer = null;
            _computePipeline.RemoveVariant(this);
            if (localUniformAllocation)
            {
                NativeMemory.AlignedFree(pUniformBuffer);
                localUniformAllocation = false;
            }
            pUniformBuffer = null;

            GC.ReRegisterForFinalize(this);
        }
    }
}
