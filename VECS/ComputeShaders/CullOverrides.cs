using System;

namespace VECS
{
    [Flags]
    public enum CullOverrides : int
    {
        None = 0,
        NoCull = 1,
        NoDepth = 2
    }
}
