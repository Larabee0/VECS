using System;
using System.Collections.Generic;

namespace VECS.ECS.Presentation
{
    internal abstract class RenderSystemInternal : IDisposable
    {
        public abstract void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities);

        public abstract void Dispose();
    }
}
