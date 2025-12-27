using System.Numerics;
using System.Runtime.InteropServices;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct ModelMatrices
    {
        public Matrix4x4 ModelMatrix;
        public Matrix4x4 NormalMatrix;

        public ModelMatrices(Matrix4x4 transformMatrix)
        {
            ModelMatrix = transformMatrix;

            if (Matrix4x4.Invert(transformMatrix, out NormalMatrix))
            {
                NormalMatrix = Matrix4x4.Transpose(NormalMatrix);
            }
        }

        public static implicit operator ModelMatrices(Matrix4x4 m) => new(m);
    }
}