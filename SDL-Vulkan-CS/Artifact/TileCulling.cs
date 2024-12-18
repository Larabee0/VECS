using SDL_Vulkan_CS.ECS;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.Artifact
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
