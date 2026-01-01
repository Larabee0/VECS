using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ComputeShader : DisposableAsset
    {
        private readonly PushConstantsHandler _pushConstantsHandler;

        private readonly int _descriptorSetCount = 0;

        private readonly ConcurrentDictionary<int, ShaderPropertyInfo> _cachedShaderProperties = new();

        private readonly DescriptorSetInfo[] _descriptorSetInfos;
        private readonly VkDescriptorSetLayout[] _descriptorSetLayouts;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipeline _pipline;

        public PushConstantsHandler PushConstantsHandler => _pushConstantsHandler;

        [ThreadStatic]
        private static ComputeShader _lastBoundComputeShader;
        [ThreadStatic]
        private static int _frameIndex;

        public unsafe ComputeShader(string assetName, string shaderName)
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
                _descriptorSetInfos[setIndex] = new DescriptorSetInfo(layout, setBindings, true);
            }

            _pushConstantsHandler = new(spirShader);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(shaderModule, _descriptorSetLayouts, _pushConstantsHandler);

            VkComputePipelineCreateInfo computePipelineInfo = new()
            {
                layout = _pipelineLayout,
                stage = shaderModule.ShaderStageCreateInfo,
                flags = VkPipelineCreateFlags.DescriptorBufferEXT
            };

            _pipline = GPUPipelineUtil.CreateComputePipeline(shaderModule, computePipelineInfo);
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
        public bool LookUpProperty(string property, out ShaderPropertyInfo propertyInfo)
        {
            return LookUpProperty(property.GetHashCode(), out propertyInfo);
        }

        public bool LookUpProperty(int propertyId, out ShaderPropertyInfo propertyInfo)
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

            propertyInfo = ShaderPropertyInfo.Invalid;
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

        public void SetStorageBuffer(string property, uint variant, GPUBuffer buffer)
        {
            if (LookUpProperty(property, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex, propertyInfo.BindPoint, variant, buffer);
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

        public unsafe void WriteToBuffer<T>(uint variant, ShaderPropertyInfo propertyInfo, T element) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var buffer = GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);

            uint offset = propertyOffset + (buffer.UInstanceSize32 * variant);

            var hostPtr = (IntPtr)buffer.HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);

            NativeMemory.Copy(&element, (void*)hostPtr, maxSize);
        }

        public unsafe void WriteArrayToBuffer<T>(uint variant, ShaderPropertyInfo propertyInfo, T[] array) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) * array.Length > maxSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var buffer = GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);

            uint offset = propertyOffset + (buffer.UInstanceSize32 * variant);
            var hostPtr = (IntPtr)buffer.HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);
            fixed (T* arrayPtr = array)
            {
                NativeMemory.Copy(arrayPtr, (void*)hostPtr, maxSize);
            }
        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, uint setId, uint workGroupCountX, uint workGroupCountY = 1, uint workGroupCountZ = 1)
        {
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            ulong* offsets = stackalloc ulong[_descriptorSetCount];
            uint* indices = stackalloc uint[_descriptorSetCount];


            for (uint i = 0; i < _descriptorSetCount; i++)
            {
                _descriptorSetInfos[i].WriteUniforms(frameIndex,setId);
                _descriptorSetInfos[i].WriteFromBuffers(frameIndex);
                var buffer = _descriptorSetInfos[i].DescriptorBuffers[frameIndex];
                buffer.Flush();
                bindingInfo[i] = buffer.BindingInfo;
                offsets[i] = buffer.AlignedSize * setId;
                indices[i] = i;
            }
            if(frameIndex != _frameIndex || this != _lastBoundComputeShader)
            {
                _lastBoundComputeShader = this;
                _frameIndex = frameIndex;
            }
            GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _pipline);
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Compute, 0, (uint)_descriptorSetCount, offsets, indices);

            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, setId);
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
        }

        public static ComputeShader GetOrCreate(string shaderName)
        {
            var shader = AssetDataBase<ComputeShader>.GetNamedSilentFail(shaderName);

            if (shader == null)
            {
                shader = new ComputeShader(shaderName, shaderName);
                AssetDataBase<ComputeShader>.Add(shader);
            }

            return shader;
        }

        public static Vector2UInt CompensateForWorkGroupLimits(uint totalInvocations)
        {
            var workGroupY = (uint)(int)MathF.Ceiling((float)totalInvocations / (float)GraphicsDevice.MaxWorkGroupX);
            var workGroupX = (uint)Math.Min(totalInvocations, GraphicsDevice.MaxWorkGroupX);

            return new(workGroupX,workGroupY);
        }
    }
}
