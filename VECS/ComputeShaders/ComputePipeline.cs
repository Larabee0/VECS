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
        private readonly VkPipeline _computePipline;


        internal ComputeVariant[] _computeVariants;
        private readonly ConcurrentQueue<uint> _freeVariantIndices = new();
        private readonly ConcurrentQueue<ComputeVariant> _variantsToAdd = new();
        private uint _variantCount;
        public int VariantCount => _computeVariants.Length;
        internal static bool _descriptorReWrite = false;

        private readonly UniformBuffer _uniformBuffer;
        private readonly uint _uniformSize = 0;
        private readonly bool _hasUniforms = false;
        private readonly VkBufferUsageFlags _uniformFlags = VkBufferUsageFlags.None;
        public uint UniformBufferSize => _uniformSize;
        public bool HasUniforms => _hasUniforms;
        public VkBufferUsageFlags UniformFlags => _uniformFlags;
        public PushConstantsHandler PushConstantsHandler => _pushConstantsHandler;

        private readonly static ConcurrentDictionary<int, int> _lastBoundComputePipeline = new(Environment.ProcessorCount, Environment.ProcessorCount * 2);

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
                GraphicsDevice.SetObjectName(VkObjectType.DescriptorSetLayout, layout.Handle, string.Format("{0}_Set_{1}", AssetName, setIndex));
                _descriptorSetLayouts[setIndex] = layout;
                _descriptorSetInfos[setIndex] = new DescriptorSetInfo(layout, setBindings, true, _uniformSize, 1);
                _uniformSize += _descriptorSetInfos[setIndex].UnifromBufferSize;
                _uniformFlags |= _descriptorSetInfos[setIndex].UniformBufferFlags;
                _hasUniforms |= _descriptorSetInfos[setIndex]._uniformCount > 0;
            }
            
            if (_uniformSize > 0)
            {
                _uniformSize = (uint)GPUBufferExtensions.GetAlignment(_uniformSize, VkBufferUsageFlags.UniformBuffer);
                _uniformBuffer = new(_uniformSize, 1, _uniformFlags,_descriptorSetInfos);
                _uniformBuffer.SetDebugName(string.Format("{0}_UniformBuffer",AssetName));
            }

            _pushConstantsHandler = new(spirShader);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVert(shaderModule, _descriptorSetLayouts, _pushConstantsHandler);

            VkComputePipelineCreateInfo computePipelineInfo = new()
            {
                layout = _pipelineLayout,
                stage = shaderModule.ShaderStageCreateInfo,
                flags = VkPipelineCreateFlags.DescriptorBufferEXT
            };

            _computePipline = GPUPipelineUtil.CreateComputePipeline(computePipelineInfo);
            GraphicsDevice.SetObjectName(VkObjectType.Pipeline, _computePipline.Handle, AssetName);
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

            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                result[setIndex] = new DescriptorSetInfo(_descriptorSetLayouts[setIndex], _descriptorSetInfos[setIndex].DescriptorBindings, true, _descriptorSetInfos[setIndex].UnifromBufferOffset, 1);
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
                    _uniformBuffer.SetDebugName(string.Format("{0}_UniformBuffer", AssetName));

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
                return propertyInfo != ShaderProperties.Invalid;
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
                setInfo.WriteDescriptors(Presenter.FrameIndex,propertyInfo.BindPoint, variant, buffer[Presenter.FrameIndex]);
            }
        }

        public void SetStorageBuffer(int propertyId, uint variant, SwapChainBuffer buffer)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.FrameIndex, propertyInfo.BindPoint, variant, buffer[Presenter.FrameIndex]);
            }
        }

        public void SetStorageBuffer(int propertyId, uint variant, GPUBuffer buffer)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.FrameIndex, propertyInfo.BindPoint, variant, buffer);
            }
        }

        public void SetStorageBuffer(string property, uint variant, GPUBuffer buffer)
        {
            if (LookUpProperty(property, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.FrameIndex, propertyInfo.BindPoint, variant, buffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(int propertyId, uint variant, VkDescriptorImageInfo imageInfo, VkDescriptorType imageType)
        {
            if(LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.Image)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.FrameIndex, propertyInfo.BindPoint, variant, imageInfo, imageType);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(int propertyId, uint variant, Texture texture)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.Image)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.FrameIndex, propertyInfo.BindPoint, variant, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt(string property, uint variant, int value)
        {
            WriteToUniformBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUInt(string property, uint variant, uint value)
        {
            WriteToUniformBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUInt(int propertyId, uint variant, uint value)
        {
            WriteToUniformBuffer(propertyId, variant, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(string property, uint variant, float value)
        {
            WriteToUniformBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector2(string property, uint variant, Vector2 value)
        {
            WriteToUniformBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVector4(string property, uint variant, Vector4 value)
        {
            WriteToUniformBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix3x2( string property, uint variant, Matrix3x2 value)
        {
            WriteToUniformBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMatrix4x4(string property, uint variant, Matrix4x4 value)
        {
            WriteToUniformBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform<T>(string property, uint variant, T value) where T : unmanaged
        {
            WriteToUniformBuffer(property, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform<T>(int propertyId, uint variant, T value) where T : unmanaged
        {
            WriteToUniformBuffer(propertyId, variant, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToUniformBuffer<T>(string property, uint variant, T value) where T : unmanaged
        {
            if(LookUpProperty(property,out var propertyInfo))
            {
                WriteToUniformBuffer(variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToUniformBuffer<T>(int propertyId, uint variant, T value) where T : unmanaged
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                WriteToUniformBuffer(variant, propertyInfo, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToUniformBuffer<T>(uint variant, ShaderProperty propertyInfo, T element) where T : unmanaged
        {
            _uniformBuffer.WriteToUniformBuffer(variant, propertyInfo, element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteToUniformBuffer<T>(void* uniform, ShaderProperty propertyInfo, T element) where T : unmanaged
        {
            _uniformBuffer.WriteToUniformBuffer(uniform, propertyInfo, element);
        }

        public void WriteArrayToUniformBuffer<T>(uint variant, ShaderProperty propertyInfo, T[] array) where T : unmanaged
        {
            _uniformBuffer.WriteArrayToBuffer<T>(variant, propertyInfo, array);
        }

        private void WriteUniformToDescriptorBuffers(ComputeVariant computeVariant)
        {
            if (!HasUniforms) return;
            var variant = computeVariant.VariantIndex;
            var startOffset = variant * UniformBufferSize;
            for (uint i = 0; i < DescriptorSetCount; i++)
            {
                var setInfo = _descriptorSetInfos[i];

                for (uint j = 0; j < setInfo.BindingCount; j++)
                {
                    var binding = setInfo.DescriptorBindings[j];

                    if (!binding.UniformBuffer) continue;

                    var internalOffset = InternalUniformBufferOffset(binding.DescriptorSetIndex, binding.BindPoint);
                    var global = EngineBuffers.TryGetBuffer(binding.Id);

                    for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                    {
                        VkDescriptorAddressInfoEXT addressRange;
                        if (global != null)
                        {
                            addressRange = global[frameIndex].GetBufferAddressRangeBytes();
                        }
                        else
                        {
                            addressRange = _uniformBuffer.Buffer[frameIndex].GetBufferAddressRangeBytes(startOffset + internalOffset, binding.BufferSize);
                        }

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

            Dispatch(commandBuffer, setId, bindingInfo, offsets, indices, workGroupCountX, workGroupCountY, workGroupCountZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Dispatch(VkCommandBuffer commandBuffer, uint pushConstantIndex, VkDescriptorBufferBindingInfoEXT* bindingInfo, ulong* offsets, uint* indices, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            var threadID = Environment.CurrentManagedThreadId;
            bool init = _lastBoundComputePipeline.TryGetValue(threadID, out var shaderHash);
            
            if(!init || shaderHash != Hash|| shaderHash == int.MaxValue)
            {
                GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _computePipline);
                _lastBoundComputePipeline.AddOrUpdate(threadID, Hash,(a, b) => Hash);
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
            

            GraphicsDevice.DeviceAPI.vkDestroyPipeline(_computePipline);

            for (int i = 0; i < _descriptorSetCount; i++)
            {
                _descriptorSetInfos[i]?.Dispose();
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(_descriptorSetLayouts[i], null);
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
        internal static void UpdateComputeShaders(RendererFrameInfo frameInfo)
        {
            foreach (var item in _lastBoundComputePipeline)
            {
                _lastBoundComputePipeline[item.Key] = int.MaxValue;
            }
            var count = AssetDataBase<ComputePipeline>.AssetCount;
            var readingList = AssetDataBase<ComputePipeline>.AllAssetsListForReading;
            readingList.ForEach(m => Update(m, frameInfo));
            _descriptorReWrite = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SwapChainBuffer GetBuffer(DescriptorBinding descriptorBinding)
        {
            return GetBuffer(descriptorBinding.DescriptorSetIndex, descriptorBinding.BindPoint);
        }
        private static void Update(ComputePipeline pipeline, RendererFrameInfo frameInfo)
        {
            if (pipeline.VariantCount == 0) return;

            for (uint i = 0; i < pipeline.DescriptorSetCount; i++)
            {
                var bindings = pipeline.GetDescriptorBindings(i);
                for (uint j = 0; j < bindings.Length; j++)
                {
                    var binding = bindings[j];
                    if (binding.StorageBuffer && (pipeline.GetBuffer(binding) == null || pipeline.GetBuffer(binding).IsDisposed))
                    {
                        pipeline._descriptorSetInfos[i].SetStorageBuffer(EngineBuffers.TryGetBuffer(binding.Id), binding.BindPoint);
                    }
                    if ((_descriptorReWrite || frameInfo.NewSwapChain) && binding.Image)
                    {
                        var texture = EngineTextures.TryGetTexture(binding.Id);
                        if (texture == null) continue;
                        for (int k = 0; k < pipeline.VariantCount; k++)
                        {
                            var variant = pipeline._computeVariants[k];
                            if (variant == null) continue;
                            variant.SetTextures(binding.DescriptorSetIndex, binding.BindPoint, texture);
                        }
                    }
                }
            }

            bool forceDescriptorWrite = pipeline.AllocNewVariants();
            forceDescriptorWrite |= frameInfo.NewSwapChain;
            forceDescriptorWrite |= _descriptorReWrite;
            if (forceDescriptorWrite)
            {
                for (int i = 0; i < pipeline.VariantCount; i++)
                {
                    var variant = pipeline._computeVariants[i];
                    if (variant == null) continue;
                    ComputeVariant.UpdateVariant(variant);
                    pipeline.WriteUniformToDescriptorBuffers(variant);
                }
            }

            for (int i = 0; i < pipeline._descriptorSetInfos.Length; i++)
            {
                pipeline._descriptorSetInfos[i].WriteFromBuffers(frameInfo.FrameIndex);
            }

            pipeline._uniformBuffer?.WriteToGPU(frameInfo.FrameIndex);
        }
    }
}
