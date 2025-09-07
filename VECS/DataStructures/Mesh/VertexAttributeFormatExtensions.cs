using System;
using System.Numerics;
using Vortice.Vulkan;

namespace VECS
{
    public static class VertexAttributeFormatExtensions
    {
        public static unsafe uint GetAttributeFloatSize(this VertexAttributeFormat format)
        {
            return format switch
            {
                VertexAttributeFormat.Float1 => GetAttributeByteSize(format) / sizeof(float),
                VertexAttributeFormat.Float2 => GetAttributeByteSize(format) / sizeof(float),
                VertexAttributeFormat.Float3 => GetAttributeByteSize(format) / sizeof(float),
                VertexAttributeFormat.Float4 => GetAttributeByteSize(format) / sizeof(float),
                _ => throw new NotImplementedException(),
            };
        }
        public static unsafe uint GetAttributeByteSize(this VertexAttributeFormat format)
        {
            return format switch
            {
                VertexAttributeFormat.Float1 => sizeof(float),
                VertexAttributeFormat.Float2 => (uint)sizeof(Vector2),
                VertexAttributeFormat.Float3 => (uint)sizeof(Vector3),
                VertexAttributeFormat.Float4 => (uint)sizeof(Vector4),
                _ => throw new NotImplementedException(),
            };
        }

        public static VertexAttributeFormat GetAttributeFromByteSize(this uint byteCount)
        {
            return byteCount switch
            {
                1=> VertexAttributeFormat.Byte,
                4 => VertexAttributeFormat.Float1,
                8 => VertexAttributeFormat.Float2,
                12 => VertexAttributeFormat.Float3,
                16 => VertexAttributeFormat.Float4,
                _ => throw new NotImplementedException(),
            };
        }

        public static unsafe VkFormat GetVkFormat(this VertexAttributeFormat format)
        {
            return format switch
            {
                VertexAttributeFormat.Float1 => VkFormat.R32Sfloat,
                VertexAttributeFormat.Float2 => VkFormat.R32G32Sfloat,
                VertexAttributeFormat.Float3 => VkFormat.R32G32B32Sfloat,
                VertexAttributeFormat.Float4 => VkFormat.R32G32B32A32Sfloat,
                _ => VkFormat.Undefined
            };
        }
    }
}