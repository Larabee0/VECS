using System;
using System.Numerics;

namespace VECS.ECS.Transforms
{
    public readonly struct LocalToWorldRenderBuffer : IRenderBuffer
    {
        public readonly static Type BufferElementType = typeof(ModelMatrices);
        public readonly static int MatricesBufferShaderPropertyId = "matricesBuffer".GetShaderPropertyId();
        public unsafe readonly static uint BufferElementSize = (uint)sizeof(ModelMatrices);
        public readonly Type ElementType => BufferElementType;
        public readonly uint ElementSize => BufferElementSize;
        public readonly int BufferShaderPropertyId => MatricesBufferShaderPropertyId;
        public readonly int ComponentId => LocalToWorld.ComponentId;

        public readonly unsafe void CopyIn(void* ptr, IComponent component)
        {
            var cast = (LocalToWorld)component;
            ((ModelMatrices*)ptr)[0] = new ModelMatrices(cast.Value);
        }

        public readonly unsafe void DefaultIn(void* ptr)
        {
            ((ModelMatrices*)ptr)[0] = new ModelMatrices(Matrix4x4.Identity);
        }
    }

    /// <summary>
    /// stores the local to world matrix for an entity.
    /// </summary>
    public struct LocalToWorld : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Matrix4x4 Value;
    }
}
