using System.Numerics;
using System.Runtime.InteropServices;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ShaderAABB
    {
        public Vector4 Min;
        public Vector4 Max;

        public ShaderAABB(AABB bounds, CullOverrides cullOverride)
        {
            Min = new(bounds.Min, (int)cullOverride);
            Max = new(bounds.Max, 0);
        }
    }
}