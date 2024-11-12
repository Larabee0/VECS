using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public  struct Rotation : IComponent
    {
        public static int ComponentId { get; set; }

        public Quaternion Value;
    }
}
