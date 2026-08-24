using System;

namespace VECS.ECS.Presentation
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

        public virtual void OnPostAA(EntityManager entityManager, RendererFrameInfo frameInfo) { }

    }
}
