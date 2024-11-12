using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public struct FrameInfo : IComponent
    {
        public static int ComponentId { get; set; }

        public float screenAspect;
    }
}
