using System;
using System.Numerics;

namespace VECS.ECS.Transforms
{
    /// <summary>
    /// stores the local to world matrix for an entity.
    /// </summary>
    public struct LocalToWorld : IComponent, IRenderBuffer
    {
        public readonly static Type BufferElementType = typeof(ModelMatrices);
        public unsafe readonly static uint BufferElementSize = (uint)sizeof(ModelMatrices);
        public readonly Type ElementType => BufferElementType;
        public readonly uint ElementSize => BufferElementSize;
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Matrix4x4 Value;

        public readonly IRenderBufferElement RenderBufferData => new ModelMatrices(Value);

        public readonly unsafe void CopyIn(void* ptr)
        {
            ((ModelMatrices*)ptr)[0] = new ModelMatrices(Value);
        }
    }
}
