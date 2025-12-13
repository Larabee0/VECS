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

        public ModelBounds(Vector4 min, Vector4 max)
        {
            Min = min;
            Max = max;
        }

        public ModelBounds(Vector3 min, Vector3 max)
        {
            Min = new(min, 1);
            Max = new(max, 1);
        }

        public ModelBounds(Bounds bounds)
        {
            Min = new(bounds.Min, 1);
            Max = new(bounds.Max, 1);
        }

        public ModelBounds(RenderBounds renderBounds)
        {
            Min = new(renderBounds.Value.Min, 1);
            Max = new(renderBounds.Value.Max, 1);
        }

        public ModelBounds(WorldRenderBounds worldRenderBounds)
        {
            Min = new(worldRenderBounds.Value.Min, 1);
            Max = new(worldRenderBounds.Value.Max, 1);
        }
    }
}