using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Stores the screen aspect ratio for the access by the camera system
    /// </summary>
    public struct FrameInfo : IComponent
    {
        public static int ComponentId { get; set; }

        public float screenAspect;
    }
}
