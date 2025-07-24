using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public class PushConstantsHandler
    {
        private readonly PushConstantsInfo[] _pushConstants;
        private readonly int _pushConstantsCount = 0;

        public PushConstantsInfo[] PushConstants => _pushConstants;

        public bool HasPushConstrants => _pushConstants != null && _pushConstantsCount > 0;

        public int Count => _pushConstantsCount;
        public uint UCount => (uint)_pushConstantsCount;


        public PushConstantsHandler(params SpvReflectShaderModule[] modules)
        {
            _pushConstants = GPUPipelineUtil.GetPushConstants(modules);
            if (_pushConstants != null)
            {
                _pushConstantsCount = _pushConstants.Length;
            }
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
        public static void SetPushConstantFloat(this Material material ,string property, float value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector2(this Material material ,string property, Vector2 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector4(this Material material ,string property, Vector4 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix3x2(this Material material ,string property, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix4x4(this Material material ,string property, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(material.PushConstants, property, value);
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantInt(this PushConstantsHandler handler, string property, int value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantFloat(this PushConstantsHandler handler, string property, float value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector2(this PushConstantsHandler handler, string property, Vector2 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantVector4(this PushConstantsHandler handler, string property, Vector4 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix3x2(this PushConstantsHandler handler, string property, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantMatrix4x4(this PushConstantsHandler handler, string property, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPushConstantUniform<T>(this PushConstantsHandler handler, string property, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(handler, property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteToPushConstantBuffer<T>(PushConstantsHandler handler, string property, T value) where T : unmanaged
        {
            for (int i = 0; i < handler.Count; i++)
            {
                if (handler.PushConstants[i].WriteToPushConstantBuffer(property, value))
                {
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindPushConstants(this PushConstantsHandler handler, RendererFrameInfo rendererFrameInfo, VkPipelineLayout pipelineLayout)
        {
            for (int i = 0; i < handler.Count; i++)
            {
                handler.PushConstants[i].PushConstants(rendererFrameInfo, pipelineLayout);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void PopulateLayout(this PushConstantsHandler handler, VkPushConstantRange* pLayouts)
        {
            for (int i = 0; i < handler.Count; i++)
            {
                pLayouts[i] = handler.PushConstants[i].VkPushConstantRange;
            }
        }
    }
}