using System;

namespace VECS.ECS.Presentation
{
    public struct RenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public AABB Value;
        public bool Valid;

        public RenderBounds(AABB bounds, bool valid)
        {
            Value = bounds;
            Valid = valid;
        }

        public RenderBounds(ShaderAABB bounds, bool valid)
        {
            Value = bounds;
            Valid = valid;
        }
    }

    public struct WorldRenderBounds : IComponent, IRenderBuffer
    {
        public readonly static Type BufferElementType = typeof(ShaderAABB);
        public unsafe readonly static uint BufferElementSize = (uint)sizeof(ShaderAABB);
        public readonly Type ElementType => BufferElementType;
        public readonly uint ElementSize => BufferElementSize;
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public readonly IRenderBufferElement RenderBufferData => Value;


        public ShaderAABB Value;

        public WorldRenderBounds(AABB bounds, CullOverrides cullOverrides)
        {
            Value = new(bounds, cullOverrides);
        }

        public readonly unsafe void CopyIn(void* ptr)
        {
            ((ShaderAABB*)ptr)[0] = Value;
        }
    }
}
