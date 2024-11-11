using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public class LocalToWorldSystem : SystemBase
    {
        private EntityQuery _addLTWQuery;
        private EntityQuery _ltwQuery;
        public override void OnCreate(EntityManager entityManager)
        {
            EntityQuery addLtw = new EntityQuery(entityManager).WithAny(typeof(Translation), typeof(Rotation), typeof(Scale)).WithNone(typeof(LocalToWorld)).Build();

            EntityQuery ltw = new EntityQuery(entityManager).WithAll(typeof(LocalToWorld)).WithAny(typeof(Translation),typeof(Rotation),typeof(Scale)).Build();
            _ltwQuery = ltw;
            _addLTWQuery = addLtw;
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_ltwQuery.HasEntities)
            {
                _addLTWQuery.GetEntities().ForEach(e =>
                {
                    entityManager.AddComponent<LocalToWorld>(e);
                });
            }

            if(_addLTWQuery.HasEntities)
            {
                _ltwQuery.GetEntities().ForEach(e =>
                {
                    Vector3 translation = entityManager.GetComponent(e, out Translation t) ? t.Value : Vector3.Zero;
                    Quaternion rotation = entityManager.GetComponent(e, out Rotation r) ? r.Value : Quaternion.Identity;
                    Vector3 scale = entityManager.GetComponent(e, out Scale s) ? s.Value : Vector3.One;

                    entityManager.SetComponent<LocalToWorld>(e, new() { Value = TransformExtensions.TRS(translation, rotation, scale) });

                });
            }

        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            _ltwQuery.MarkStale();
            _addLTWQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            
        }
    }
}
