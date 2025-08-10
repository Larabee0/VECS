using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS.Presentation;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ModelBounds
    {
        public Vector4 Min;
        public Vector4 Max;

        public ModelBounds(RenderBounds renderBounds)
        {
            Min = new(renderBounds.Bounds.Min, renderBounds.Radius);
            Max = new(renderBounds.Bounds.Max, renderBounds.Radius);
        }

        public ModelBounds(WorldRenderBounds worldRenderBounds) : this()
        {
            float maxRadius = MathF.Max(worldRenderBounds.Radius.X, MathF.Max(worldRenderBounds.Radius.Y, worldRenderBounds.Radius.Z));
            Min = new(worldRenderBounds.Bounds.Min, maxRadius);
            Max = new(worldRenderBounds.Bounds.Max, maxRadius);
        }
    }
}