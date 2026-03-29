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

        public virtual void OnPreShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo) { }
        public virtual void OnShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo) { }
        public virtual void OnPostShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo) { }

        public virtual void OnPreOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo) { }
        public virtual void OnOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo) { }
        public virtual void OnPostOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo) { }

        public virtual void OnPreTransparentPass(EntityManager entityManager, RendererFrameInfo frameInfo) { }
        public virtual void OnTransparentPass(EntityManager entityManager, RendererFrameInfo frameInfo) { }
        public virtual void OnPostTransparentPass(EntityManager entityManager, RendererFrameInfo frameInfo) { }

        public virtual void OnPostAA(EntityManager entityManager, RendererFrameInfo frameInfo) { }

        public virtual void OnPostPresentation(EntityManager entityManager) { }

    }
}
