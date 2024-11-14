using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// interface defines a component. Component implmenetation must define a static ComponentId
    /// This should be marked abstract when generating new components otherwise this is not enforced.
    /// </summary>
    public interface IComponent
    {
        public static int ComponentId { get; }
    }
}
