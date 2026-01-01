using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public class PushConstantsHandler
    {
        private readonly PushConstantsInfo[] _pushConstantsInfos;
        internal byte[] _bufferInstances;

        private readonly uint _bufferInstanceSize;

        private readonly int _pushConstantsCount;

        public PushConstantsInfo[] PushConstantsInfo => _pushConstantsInfos;

        public bool HasPushConstrants => PushConstantsInfo != null && _pushConstantsCount > 0;
        public int Count => _pushConstantsCount;
        public uint UCount => (uint)_pushConstantsCount;


        public PushConstantsHandler(params SpvReflectShaderModule[] modules)
        {
            _pushConstantsInfos = GPUPipelineUtil.GetPushConstants(modules);
            if (_pushConstantsInfos != null)
            {
                _pushConstantsCount = _pushConstantsInfos.Length;

                for (int i = 0; i < _pushConstantsInfos.Length; i++)
                {
                    _bufferInstanceSize += _pushConstantsInfos[i].BlockSize;
                }

                _bufferInstances = new byte[_bufferInstanceSize];
            }
        }

        public PushConstantsHandler(params ShaderModule[] modules)
        {
            _pushConstantsInfos = GPUPipelineUtil.GetPushConstants(modules);
            if (_pushConstantsInfos != null)
            {
                _pushConstantsCount = _pushConstantsInfos.Length;

                for (int i = 0; i < _pushConstantsInfos.Length; i++)
                {
                    _bufferInstanceSize += _pushConstantsInfos[i].BlockSize;
                }

                _bufferInstances = new byte[_bufferInstanceSize];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int count)
        {
            if (_bufferInstances.Length >= (int)(count * _bufferInstanceSize))
            {
                return;
            }

            Array.Resize(ref _bufferInstances, (int)(count * _bufferInstanceSize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetBufferInstance(int instanceIndex)
        {
            if (_bufferInstances.Length <= (int)(instanceIndex * _bufferInstanceSize))
            {
                EnsureCapacity(instanceIndex + 1);
            }
            return _bufferInstances.AsSpan((int)(instanceIndex * _bufferInstanceSize), (int)_bufferInstanceSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetPushConstantInstance(int instanceIndex, int pushConstantIndex)
        {
            var info = _pushConstantsInfos[pushConstantIndex];
            return GetBufferInstance(instanceIndex).Slice((int)info.BufferOffset, (int)info.BlockSize);
        }
    }

    public static class PushConstantsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantInt(this PushConstantsHandler handler, string property, int value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantInt(this PushConstantsHandler handler, string property, int instanceIndex, int value)
        {
            WriteToPushConstantBuffer(handler, property, instanceIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUInt(this PushConstantsHandler handler, string property, uint value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUInt(this PushConstantsHandler handler, string property, int instanceIndex, uint value)
        {
            WriteToPushConstantBuffer(handler, property, instanceIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantFloat(this PushConstantsHandler handler, string property, float value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantFloat(this PushConstantsHandler handler, string property, int instanceIndex, float value)
        {
            WriteToPushConstantBuffer(handler, property, instanceIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector2(this PushConstantsHandler handler, string property, Vector2 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector2(this PushConstantsHandler handler, string property, int instanceIndex, Vector2 value)
        {
            WriteToPushConstantBuffer(handler, property, instanceIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector4(this PushConstantsHandler handler, string property, Vector4 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector4(this PushConstantsHandler handler, string property, int instanceIndex, Vector4 value)
        {
            WriteToPushConstantBuffer(handler, property, instanceIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix3x2(this PushConstantsHandler handler, string property, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix3x2(this PushConstantsHandler handler, string property, int instanceIndex, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(handler, property, instanceIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix4x4(this PushConstantsHandler handler, string property, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix4x4(this PushConstantsHandler handler, string property, int instanceIndex, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(handler, property, instanceIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUniform<T>(this PushConstantsHandler handler, string property, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUniform<T>(this PushConstantsHandler handler, string property, int instanceIndex, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(handler, property, instanceIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteToPushConstantBuffer<T>(PushConstantsHandler handler, string property, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(handler, property, 0, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteToPushConstantBuffer<T>(PushConstantsHandler handler, string property, int instanceIndex, T value) where T : unmanaged
        {
            for (int i = 0; i < handler.Count; i++)
            {
                if (handler.PushConstantsInfo[i].WriteToPushConstantBuffer(handler.GetPushConstantInstance(instanceIndex, i), property, value))
                {
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, RendererFrameInfo rendererFrameInfo, VkPipelineLayout pipelineLayout, int instanceIndex)
        {
            BindPushConstants(handler, rendererFrameInfo.CommandBuffer, pipelineLayout, instanceIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, VkCommandBuffer commandBuffer, VkPipelineLayout pipelineLayout, uint instanceIndex)
        {
            BindPushConstants(handler, commandBuffer, pipelineLayout, (int)instanceIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, VkCommandBuffer commandBuffer, VkPipelineLayout pipelineLayout, int instanceIndex)
        {
            for (int i = 0; i < handler.Count; i++)
            {
                handler.PushConstantsInfo[i].PushConstants(handler.GetPushConstantInstance(instanceIndex, i), commandBuffer, pipelineLayout);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void PopulateLayout(this PushConstantsHandler handler, VkPushConstantRange* pLayouts)
        {
            for (int i = 0; i < handler.Count; i++)
            {
                pLayouts[i] = handler.PushConstantsInfo[i].VkPushConstantRange;
            }
        }
    }
}