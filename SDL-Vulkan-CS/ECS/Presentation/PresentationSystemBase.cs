using System;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Base presentation system defines extra update calls when the frame render cycle occurs which parses in extra data for renderering
    /// </summary>
    public abstract class PresentationSystemBase : SystemBase
    {

        public PresentationSystemBase()
        {
            if (Presenter.Instance == null)
            {
                throw new Exception("Cannot Create render system when the presenter is uninitialised!");
            }

        }

        public abstract void OnPresent(EntityManager entityManager, RendererFrameInfo rendererFrameInfo);

        public virtual void OnPostPresentation(EntityManager entityManager)
        {

        }
    }
}
