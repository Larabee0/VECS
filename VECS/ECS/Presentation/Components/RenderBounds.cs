using System;
using System.Numerics;

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

    public readonly struct WorldRenderBoundsRenderBuffer : IRenderBuffer
    {
        public readonly static Type BufferElementType = typeof(ShaderAABB);
        public readonly static int BoundsBufferShaderPropertyId = "boundsBuffer".GetShaderPropertyId();
        public unsafe readonly static uint BufferElementSize = (uint)sizeof(ShaderAABB);
        public readonly Type ElementType => BufferElementType;
        public readonly uint ElementSize => BufferElementSize;
        public readonly int BufferShaderPropertyId => BoundsBufferShaderPropertyId;

        public readonly int ComponentId => WorldRenderBounds.ComponentId;

        public readonly unsafe void CopyIn(void* ptr, IComponent component)
        {
            var cast = (WorldRenderBounds)component;
            ((ShaderAABB*)ptr)[0] = cast.Value;
        }

        public unsafe void DefaultIn(void* ptr)
        {
            ((ShaderAABB*)ptr)[0] = (ShaderAABB)AABB.FromCenterSize(Vector3.Zero,Vector3.One);
        }
    }

    public struct WorldRenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public ShaderAABB Value;

        public WorldRenderBounds(AABB bounds, CullOverrides cullOverrides)
        {
            Value = new(bounds, cullOverrides);
        }
    }
}
