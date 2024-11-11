using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public class SystemBase
    {
        public bool AlwaysUpdate;

        public List<EntityQuery> Queries = [];


        public virtual void OnCreate(EntityManager entityManager)
        {

        }

        public virtual void OnDestroy(EntityManager entityManager)
        {

        }

        public virtual void OnUpdate(EntityManager entityManager)
        {

        }

        public virtual void OnPostUpdate(EntityManager entityManager)
        {

        }

    }
}
