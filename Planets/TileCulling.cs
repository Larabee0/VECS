using VECS;
using VECS.ECS;
using System.Numerics;

namespace Planets
{
    public static class TileCullingExtensions
    {
        public static Vector3 AverageNormal(this Mesh mesh)
        {
            Vector3 normal = Vector3.Zero;

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                normal += mesh.Vertices[i].Normal;
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
