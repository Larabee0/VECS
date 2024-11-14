using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Base presentation system defines extra update calls when the frame render cycle occurs which parses in extra data for renderering
    /// </summary>
    public abstract class PresentationSystemBase : SystemBase
    {
        public abstract void OnPresent(EntityManager entityManager, RendererFrameInfo rendererFrameInfo);

        public virtual void OnPostPresentation(EntityManager entityManager)
        {
            
        }
    }
}
