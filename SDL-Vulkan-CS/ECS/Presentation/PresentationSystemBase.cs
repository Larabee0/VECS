using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public abstract class PresentationSystemBase : SystemBase
    {
        public abstract void OnPresentation(EntityManager entityManager, RendererFrameInfo rendererFrameInfo);

        public virtual void OnPostPresentation(EntityManager entityManager)
        {
            
        }
    }
}
