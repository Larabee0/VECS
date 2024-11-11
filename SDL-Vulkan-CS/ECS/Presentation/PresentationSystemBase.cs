using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public class PresentationSystemBase : SystemBase
    {
        private Entity _frameInfoEntity;

        public override void OnCreate(EntityManager entityManager)
        {
            _frameInfoEntity = entityManager.CreateEntity();
            entityManager.AddComponent<FrameInfo>(_frameInfoEntity);
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            
        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            
        }

        public void OnPresentation(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            
        }

        public void OnPostPresentation(EntityManager entityManager)
        {
            
        }
    }
}
