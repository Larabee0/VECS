using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ComputePipeline : DisposableAsset
    {
        private readonly PushConstantsHandler _pushConstantsHandler;

        private readonly int _descriptorSetCount = 0;

        public int DescriptorSetCount => _descriptorSetCount;

        private readonly ConcurrentDictionary<int, ShaderProperty> _cachedShaderProperties = new();

        internal readonly DescriptorSetInfo[] _descriptorSetInfos;
        private readonly VkDescriptorSetLayout[] _descriptorSetLayouts;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipeline _pipline;


        internal ComputeVariant[] _computeVariants;
        private readonly ConcurrentQueue<uint> _freeVariantIndices = new();
        private readonly ConcurrentQueue<ComputeVariant> _variantsToAdd = new();
        private uint _variantCount;
        public int VariantCount => _computeVariants.Length;

        private readonly UniformBuffer _uniformBuffer;
        private readonly uint _uniformSize = 0;
        private readonly VkBufferUsageFlags _uniformFlags = VkBufferUsageFlags.None;
        public uint UniformBufferSize => _uniformSize;
        public VkBufferUsageFlags UniformFlags => _uniformFlags;
        public PushConstantsHandler PushConstantsHandler => _pushConstantsHandler;

        [ThreadStatic]
        private static ComputePipeline _lastBoundComputeShader;
        [ThreadStatic]
        private static int _frameIndex;

        public unsafe ComputePipeline(string assetName, string shaderName)
        {
            AssetName = assetName;
            var shaderModule = AssetDataBase<ShaderModule>.GetNamed(shaderName);
            var spirShader = shaderModule.SpvShaderModule;
            var descriptorSetBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(spirShader);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);

            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            
            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);
                _descriptorSetLayouts[setIndex] = layout;
                _descriptorSetInfos[setIndex] = new DescriptorSetInfo(layout, setBindings, true, _uniformSize, 1);
                _uniformSize += _descriptorSetInfos[setIndex].UnifromBufferSize;
                _uniformFlags |= _descriptorSetInfos[setIndex].UniformBufferFlags;
            }
            
            if (_uniformSize > 0)
            {
                _uniformBuffer = new(_uniformSize, 1, _uniformFlags);
            }

            _pushConstantsHandler = new(spirShader);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVert(shaderModule, _descriptorSetLayouts, _pushConstantsHandler);

            VkComputePipelineCreateInfo computePipelineInfo = new()
            {
                layout = _pipelineLayout,
                stage = shaderModule.ShaderStageCreateInfo,
                flags = VkPipelineCreateFlags.DescriptorBufferEXT
            };

            _pipline = GPUPipelineUtil.CreateComputePipeline(shaderModule, computePipelineInfo);

            _computeVariants = [new ComputeVariant("Default", this, false)];
            _variantsToAdd.TryDequeue(out var variant);

            if (_uniformSize > 0)
            {
                NativeMemory.AlignedFree(variant.pUniformBuffer);
                variant.pUniformBuffer = _uniformBuffer.Buffer.HostPtr;
                variant.localUniformAllocation = false;
            }
        }

        internal DescriptorSetInfo[] GetTemporaryDescriptorSetInfos()
        {
            DescriptorSetInfo[] result = new DescriptorSetInfo[_descriptorSetCount];
            uint _uniformSize = 0;

            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                result[setIndex] = new DescriptorSetInfo(_descriptorSetLayouts[setIndex], _descriptorSetInfos[setIndex].DescriptorBindings, true, _uniformSize, 1);
                _uniformSize += result[setIndex].UnifromBufferSize;
            }
            return result;
        }

        public uint GetNextVariantIndex()
        {
            if (_freeVariantIndices.TryDequeue(out var index))
                return index;
            return Interlocked.Add(ref _variantCount, 1) - 1;
        }

        public void RemoveVariant(ComputeVariant variant)
        {
            _freeVariantIndices.Enqueue(variant.VariantIndex);
            _computeVariants[variant.VariantIndex] = null;
        }

        public void AddVariant(ComputeVariant variant)
        {
            _variantsToAdd.Enqueue(variant);
        }

        public ComputeVariant GetOrCreateVariant(uint index, bool allowTmpAllocation = true)
        {
            if (index < _computeVariants.Length && _computeVariants[index] != null)
            {
                return _computeVariants[index];
            }
            return Create(string.Format("VARAINT_{0}", index), allowTmpAllocation);
        }

        public ComputeVariant Create(string name, bool allowTmpAllocation = false)
        {
            return new ComputeVariant(name, this, true, allowTmpAllocation);
        }

        public ComputeVariant Default()
        {
            return _computeVariants[0];
        }

        private unsafe bool AllocNewVariants()
        {
            if (!_variantsToAdd.IsEmpty)
            {
                Array.Resize(ref _computeVariants, (int)_variantCount);
                bool reassignUniformPtrs = false;
                for (int i = 0; i < _descriptorSetCount; i++)
                {
                    _descriptorSetInfos[i].SetVariantLength((uint)VariantCount);
                }
                if (_uniformSize > 0)
                {
                    _uniformBuffer.UpdateUniformCount((uint)VariantCount);
                    reassignUniformPtrs = true;
                }
                while (_variantsToAdd.TryDequeue(out var variant))
                {
                    Debug.Assert(_computeVariants[variant.VariantIndex] == null, "Attempting to replace active material!");
                    _computeVariants[variant.VariantIndex] = variant;
                    if (_uniformSize > 0 && variant.localUniformAllocation)
                    {
                        void* pipelineAlloc = _uniformBuffer.UniformAddresses[variant.VariantIndex];
                        if (variant._allowTmpBufferAllocation)
                        {
                            void* localAllocation = variant.pUniformBuffer;
                            Buffer.MemoryCopy(localAllocation, pipelineAlloc, _uniformSize, _uniformSize);
                        }
                        variant.pUniformBuffer = pipelineAlloc;
                    }
                    variant.CopyDescriptorBindings();
                    variant.DiposeTemporaryBuffers();
                    variant.localUniformAllocation = false;
                }

                if (reassignUniformPtrs)
                {
                    for (int i = 0; i < VariantCount; i++)
                    {
                        if (_computeVariants[i] == null) continue;
                        _computeVariants[i].pUniformBuffer = _uniformBuffer.UniformAddresses[i];
                    }
                }
                return true;
            }
            return false;
        }

        public void SetUniformBufferLength(uint length)
        {
            // if (_uniformLength == length) return;
            // _uniformLength = Math.Max(1, length);
            // for (uint i = 0; i < _descriptorSetCount; i++)
            // {
            //     _descriptorSetInfos[i].SetVariantLength(length);
            //     var bindings = GetDescriptorBindings(i);
            //     for (int j = 0; j < bindings.Length; j++)
            //     {
            //         if (bindings[j].UniformBuffer)
            //         {
            //             GetBuffer(i, bindings[j].BindPoint).SetUsedInstanceCount(_uniformLength);
            //         }
            //     }
            // }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DescriptorBinding[] GetDescriptorBindings(uint setIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwapChainBuffer GetBuffer(uint set, uint bindingPoint)
        {
            return _descriptorSetInfos[set].GetBuffer(bindingPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint InternalUniformBufferOffset(uint set, uint bindPoint)
        {
            var setInfo = _descriptorSetInfos[set];
            return setInfo.UnifromBufferOffset + setInfo.SetUniformBufferOffsets[bindPoint];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint InternalUniformBufferOffset(ShaderProperty propertyInfo)
        {
            return InternalUniformBufferOffset(propertyInfo.SetIndex, propertyInfo.BindPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LookUpProperty(string property, out ShaderProperty propertyInfo)
        {
            return LookUpProperty(property.GetHashCode(), out propertyInfo);
        }

        public bool LookUpProperty(int propertyId, out ShaderProperty propertyInfo)
        {
            if (_cachedShaderProperties.TryGetValue(propertyId, out propertyInfo))
            {
                return true;
            }

            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var bindings = GetDescriptorBindings(setIndex);
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    var descriptorBinding = bindings[bindingIndex];
                    if(descriptorBinding.Id == propertyId)
                    {
                        propertyInfo = new(descriptorBinding, null);
                        _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
                        return true;
                    }
                    var property = descriptorBinding.GetProperty(propertyId);
                    if (property != null)
                    {
                        propertyInfo = new(descriptorBinding, property);
                        _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
                        return true;
                    }
                }
            }

            Console.WriteLine("ComputeShader '{0}' has no shader property matching propertyId: '{1}' -> '{2}'", AssetName, propertyId, propertyId.GetPropertyIdString());

            propertyInfo = ShaderProperties.Invalid;
            _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
            return false;
        }

        public void SetStorageBuffer(string property, uint variant, SwapChainBuffer buffer)
        {
            if(LookUpProperty(property,out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex,propertyInfo.BindPoint, variant, buffer[Presenter.Instance.FrameIndex]);
            }
        }

        public void SetStorageBuffer(int propertyId, uint variant, SwapChainBuffer buffer)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex, propertyInfo.BindPoint, variant, buffer[Presenter.Instance.FrameIndex]);
            }
        }

        public void SetStorageBuffer(int propertyId, uint variant, GPUBuffer buffer)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex, propertyInfo.BindPoint, variant, buffer);
            }
        }

        public void SetStorageBuffer(string property, uint variant, GPUBuffer buffer)
        {
            if (LookUpProperty(property, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex, propertyInfo.BindPoint, variant, buffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(int propertyId, uint variant, VkDescriptorImageInfo imageInfo, VkDescriptorType imageType)
        {
            if(LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.Image)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex, propertyInfo.BindPoint, variant, imageInfo, imageType);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(int propertyId, uint variant, Texture texture)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.Image)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex, propertyInfo.BindPoint, variant, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt(string property, uint variant, int value)
        {
            WriteToBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUInt(string property, uint variant, uint value)
        {
            WriteToBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUInt(int propertyId, uint variant, uint value)
        {
            WriteToBuffer(propertyId, variant, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(string property, uint variant, float value)
        {
            WriteToBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector2(string property, uint variant, Vector2 value)
        {
            WriteToBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector4(string property, uint variant, Vector4 value)
        {
            WriteToBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix3x2( string property, uint variant, Matrix3x2 value)
        {
            WriteToBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix4x4(string property, uint variant, Matrix4x4 value)
        {
            WriteToBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform<T>(string property, uint variant, T value) where T : unmanaged
        {
            WriteToBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform<T>(int propertyId, uint variant, T value) where T : unmanaged
        {
            WriteToBuffer(propertyId, variant, value);
        }

        public void WriteToBuffer<T>(string property, uint variant, T value) where T : unmanaged
        {
            if(LookUpProperty(property,out var propertyInfo))
            {
                WriteToBuffer(variant, propertyInfo, value);
            }
        }

        public void WriteToBuffer<T>(int propertyId, uint variant, T value) where T : unmanaged
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                WriteToBuffer(variant, propertyInfo, value);
            }
        }

        public unsafe void WriteToBuffer<T>(uint variant, ShaderProperty propertyInfo, T element) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            if (variant >= VariantCount)
            {
                throw new InvalidOperationException("Cannot write property to uniform buffer, variant not allocated!");
            }

            var buffer = _uniformBuffer.Buffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;

            var hostPtr = (byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant));

            Buffer.MemoryCopy(&element, hostPtr, maxSize, maxSize);
        }

        public unsafe void WriteArrayToBuffer<T>(uint variant, ShaderProperty propertyInfo, T[] array) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) * array.Length > maxSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var buffer = GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);

            uint offset = propertyOffset + (buffer.UInstanceSize32 * variant);
            

            var hostPtr = (byte*)buffer.HostPtr + offset;
            fixed (T* arrayPtr = array)
            {
                Buffer.MemoryCopy(arrayPtr, hostPtr, maxSize, maxSize);
            }
        }

        private unsafe void WriteUniformToDescriptorBuffers(ComputeVariant material)
        {
            if (UniformBufferSize == 0) return;
            var variant = material.VariantIndex;
            var startOffset = variant * UniformBufferSize;
            for (uint i = 0; i < DescriptorSetCount; i++)
            {
                var setInfo = _descriptorSetInfos[i];

                for (uint j = 0; j < setInfo.BindingCount; j++)
                {
                    var binding = setInfo.DescriptorBindings[j];

                    if (!binding.UniformBuffer) continue;

                    var internalOffset = InternalUniformBufferOffset(binding.DescriptorSetIndex, binding.BindPoint);

                    for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                    {
                        var addressRange = _uniformBuffer.Buffer[frameIndex].GetBufferAddressRangeBytes(startOffset + internalOffset, binding.BufferSize);

                        setInfo.DescriptorBuffers[frameIndex].SetBufferBinding(addressRange, binding.DescriptorType, variant, binding.BindPoint);
                    }
                }
            }
        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, uint setId, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            ulong* offsets = stackalloc ulong[_descriptorSetCount];
            uint* indices = stackalloc uint[_descriptorSetCount];

            for (uint i = 0; i < _descriptorSetCount; i++)
            {
                var buffer = _descriptorSetInfos[i].DescriptorBuffers[frameIndex];
                bindingInfo[i] = buffer.BindingInfo;
                offsets[i] = buffer.AlignedSize * setId;
                indices[i] = i;
            }

            Dispatch(commandBuffer, frameIndex, setId, bindingInfo, offsets, indices, workGroupCountX, workGroupCountY, workGroupCountZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, uint pushConstantIndex, VkDescriptorBufferBindingInfoEXT* bindingInfo, ulong* offsets, uint* indices, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            if (frameIndex != _frameIndex || this != _lastBoundComputeShader)
            {
                GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _pipline);
                _lastBoundComputeShader = this;
                _frameIndex = frameIndex;
            }
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Compute, 0, (uint)_descriptorSetCount, offsets, indices);

            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, pushConstantIndex);
            GraphicsDevice.DeviceAPI.vkCmdDispatch(commandBuffer, workGroupCountX, workGroupCountY, workGroupCountZ);
        }

        public override unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            

            GraphicsDevice.DeviceAPI.vkDestroyPipeline(GraphicsDevice.Device, _pipline);

            for (int i = 0; i < _descriptorSetCount; i++)
            {
                _descriptorSetInfos[i]?.Dispose();
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _descriptorSetLayouts[i], null);
            }
            _uniformBuffer?.Dispose();
        }

        public static ComputePipeline GetOrCreate(string shaderName)
        {
            var shader = AssetDataBase<ComputePipeline>.GetNamedSilentFail(shaderName);

            if (shader == null)
            {
                shader = new ComputePipeline(shaderName, shaderName);
                AssetDataBase<ComputePipeline>.Add(shader);
            }

            return shader;
        }

        public static Vector2UInt CompensateForWorkGroupLimits(uint totalInvocations)
        {
            var workGroupY = (uint)(int)MathF.Ceiling((float)totalInvocations / (float)GraphicsDevice.MaxWorkGroupX);
            var workGroupX = (uint)Math.Min(totalInvocations, GraphicsDevice.MaxWorkGroupX);

            return new(workGroupX,workGroupY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateComputeShaders(int frameIndex)
        {
            var count = AssetDataBase<ComputePipeline>.AssetCount;
            var readingList = AssetDataBase<ComputePipeline>.AllAssetsListForReading;
            readingList.ForEach(m => Update(m, frameIndex));
        }

        private static void Update(ComputePipeline pipeline, int frameIndex)
        {
            if (pipeline.VariantCount == 0) return;

            bool forceDescriptorWrite = pipeline.AllocNewVariants();
            if (forceDescriptorWrite)
            {
                for (int i = 0; i < pipeline.VariantCount; i++)
                {
                    var variant = pipeline._computeVariants[i];
                    if (variant == null) continue;
                    pipeline.WriteUniformToDescriptorBuffers(variant);
                }
            }

            for (int i = 0; i < pipeline._descriptorSetInfos.Length; i++)
            {
                pipeline._descriptorSetInfos[i].WriteFromBuffers(frameIndex);
            }

            if (pipeline.UniformBufferSize > 0)
            {
                pipeline._uniformBuffer.Buffer.WriteFromHostToBuffer(frameIndex);
            }

        }
    }
}
