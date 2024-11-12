using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public struct LocalToWorld : IComponent
    {
        public static int ComponentId { get; set; }

        public Matrix4x4 Value;
    }
}
