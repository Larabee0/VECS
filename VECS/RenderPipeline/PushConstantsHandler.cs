using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public class PushConstantsHandler
    {
        private PushConstantsInfo[][] _pushConstants;
        private readonly int _pushConstantsCount = 0;

        public PushConstantsInfo[] PrimaryPushConstants => _pushConstants[0];

        public bool HasPushConstrants => PrimaryPushConstants != null && _pushConstantsCount > 0;

        public int Count => _pushConstantsCount;
        public uint UCount => (uint)_pushConstantsCount;


        public PushConstantsHandler(params SpvReflectShaderModule[] modules)
        {
            _pushConstants = [GPUPipelineUtil.GetPushConstants(modules)];
            if (PrimaryPushConstants != null)
            {
                _pushConstantsCount = PrimaryPushConstants.Length;
            }
            EnsureCapacity(MaterialV2.MAX_VARIANTS);
        }

        public PushConstantsHandler(params ShaderModule[] modules)
        {
            _pushConstants = [GPUPipelineUtil.GetPushConstants(modules)];
            if (PrimaryPushConstants != null)
            {
                _pushConstantsCount = PrimaryPushConstants.Length;
            }
            EnsureCapacity(MaterialV2.MAX_VARIANTS);
        }

        public void EnsureCapacity(int count)
        {
            if (_pushConstants.Length >= count)
            {
                return;
            }

            var currentCount = _pushConstants.Length;
            Array.Resize(ref _pushConstants, count);

            for (int i = currentCount; i < count; i++)
            {
                _pushConstants[i] = new PushConstantsInfo[_pushConstantsCount];
                for (int j = 0; j < _pushConstantsCount; j++)
                {
                    _pushConstants[i][j] = new(PrimaryPushConstants[j]);
                }
            }
        }

        public PushConstantsInfo[] GetSecondary(int id)
        {
            return _pushConstants[id];
        }
        public PushConstantsInfo[] GetSecondary(uint id)
        {
            return _pushConstants[id];
        }
    }

    public static class PushConstantsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantInt(this Material material ,string property, int value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantInt(this Material material, string property,int id, int value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantFloat(this Material material ,string property, float value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantFloat(this Material material ,string property, int id, float value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector2(this Material material ,string property, Vector2 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector2(this Material material ,string property, int id, Vector2 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector4(this Material material ,string property, Vector4 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector4(this Material material ,string property,int id,  Vector4 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix3x2(this Material material ,string property, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix3x2(this Material material ,string property,int id,  Matrix3x2 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix4x4(this Material material ,string property, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix4x4(this Material material ,string property, int id, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property,id, value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantInt(this PushConstantsHandler handler, string property, int value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantInt(this PushConstantsHandler handler, string property,int id,  int value)
        {
            WriteToPushConstantBuffer(handler, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUInt(this PushConstantsHandler handler, string property, uint value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUInt(this PushConstantsHandler handler, string property, int id, uint value)
        {
            WriteToPushConstantBuffer(handler, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantFloat(this PushConstantsHandler handler, string property, float value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantFloat(this PushConstantsHandler handler, string property,int id,  float value)
        {
            WriteToPushConstantBuffer(handler, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector2(this PushConstantsHandler handler, string property, Vector2 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector2(this PushConstantsHandler handler, string property,int id,  Vector2 value)
        {
            WriteToPushConstantBuffer(handler, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector4(this PushConstantsHandler handler, string property, Vector4 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector4(this PushConstantsHandler handler, string property,int id,  Vector4 value)
        {
            WriteToPushConstantBuffer(handler, property, id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix3x2(this PushConstantsHandler handler, string property, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix3x2(this PushConstantsHandler handler, string property,int id, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(handler, property, id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix4x4(this PushConstantsHandler handler, string property, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix4x4(this PushConstantsHandler handler, string property,int id, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(handler, property,id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUniform<T>(this PushConstantsHandler handler, string property, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUniform<T>(this PushConstantsHandler handler, string property, int id, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(handler, property, id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteToPushConstantBuffer<T>(PushConstantsHandler handler, string property, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(handler, property, 0, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteToPushConstantBuffer<T>(PushConstantsHandler handler, string property, int id, T value) where T : unmanaged
        {
            for (int i = 0; i < handler.Count; i++)
            {
                if (handler.GetSecondary(id)[i].WriteToPushConstantBuffer(property, value))
                {
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, RendererFrameInfo rendererFrameInfo, VkPipelineLayout pipelineLayout)
        {
            BindPushConstants(handler, rendererFrameInfo, pipelineLayout, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, RendererFrameInfo rendererFrameInfo, VkPipelineLayout pipelineLayout, int id)
        {
            for (int i = 0; i < handler.Count; i++)
            {
                handler.GetSecondary(id)[i].PushConstants(rendererFrameInfo, pipelineLayout);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, VkCommandBuffer commandBuffer, VkPipelineLayout pipelineLayout)
        {
            BindPushConstants(handler, commandBuffer, pipelineLayout, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, VkCommandBuffer commandBuffer, VkPipelineLayout pipelineLayout, int id)
        {
            for (int i = 0; i < handler.Count; i++)
            {
                handler.GetSecondary(id)[i].PushConstants(commandBuffer, pipelineLayout);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, VkCommandBuffer commandBuffer, VkPipelineLayout pipelineLayout, uint id)
        {
            for (int i = 0; i < handler.Count; i++)
            {
                handler.GetSecondary(id)[i].PushConstants(commandBuffer, pipelineLayout);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void PopulateLayout(this PushConstantsHandler handler, VkPushConstantRange* pLayouts)
        {
            for (int i = 0; i < handler.Count; i++)
            {
                pLayouts[i] = handler.PrimaryPushConstants[i].VkPushConstantRange;
            }
        }
    }
}