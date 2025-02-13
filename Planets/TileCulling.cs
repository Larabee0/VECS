using VECS;
using VECS.ECS;
using System.Numerics;

namespace Planets
{
    public static class TileCullingExtensions
    {
        public static Vector3 AverageNormal(this DirectSubMesh mesh)
        {
            Vector3 normal = Vector3.Zero;

            var normalBuffer = mesh.TryGetVertexDataSpan<Vector3>(VertexAttribute.Normal);
            if (normalBuffer.IsEmpty) return Vector3.Zero;

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                normal += normalBuffer[i];
            }

            return Vector3.Normalize(normal /= mesh.VertexCount);
        }
    }

    public struct TileNormalVector : IComponent
    {
        public static int ComponentId { get; set; }
        public int Id => ComponentId;

        public Vector3 Value;
    }
}
