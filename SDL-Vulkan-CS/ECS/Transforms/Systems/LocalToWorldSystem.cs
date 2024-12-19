using System.Numerics;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Operates on transform components to compute a local to world matrix
    /// </summary>
    public class LocalToWorldSystem : SystemBase
    {
        private EntityQuery _addLTWQuery;
        private EntityQuery _ltwQuery;
        private EntityQuery _ltwChildQuery;
        public override void OnCreate(EntityManager entityManager)
        {
            // any entity with a translation, rotation or scale component should get a LTW component added to it automatically.
            _addLTWQuery = new EntityQuery(entityManager)
                .WithAny(typeof(Translation), typeof(Rotation), typeof(Scale), typeof(Parent),typeof(Children))
                .WithNone(typeof(LocalToWorld), typeof(Prefab))
                .Build();

            // local to world update query
            _ltwQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld))
                .WithAny(typeof(Translation), typeof(Rotation), typeof(Scale), typeof(Children))
                .WithNone(typeof(Parent),typeof(Prefab))
                .Build();
            _ltwChildQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(Children))
                .WithNone(typeof(Prefab),typeof(Parent))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_addLTWQuery.HasEntities) // updates and checks if the query has entities. NoAlloc check
            {
                _addLTWQuery.GetEntities().ForEach(e => entityManager.AddComponent<LocalToWorld>(e));
            }

            if (_ltwQuery.HasEntities) // updates and checks if the query has entities. NoAlloc check
            {
                // compute a ltw matrix for each entity matching the query.
                // defaults are assume for entities missing t r s components
                _ltwQuery.GetEntities().ForEach(e => entityManager.SetComponent<LocalToWorld>(e, new() { Value = ComputeLocalTRS(entityManager, e) }));
            }

            if(_ltwChildQuery.HasEntities)
            {
                // _ltwChildQuery.GetEntities().ForEach(e =>
                // {
                //     Entity parent = entityManager.GetComponent<Parent>(e).Value;
                //     Matrix4x4 localToWorld = entityManager.GetComponent<LocalToWorld>(parent).Value * ComputeLocalTRS(entityManager, e);
                //     entityManager.SetComponent<LocalToWorld>(e, new() { Value = localToWorld  });
                // });
                _ltwChildQuery.GetEntities().ForEach(e => UpdateHierachy(entityManager, e));
            }
        }

        private static void UpdateHierachy(EntityManager entityManager, Entity root)
        {
            if(entityManager.HasComponent<Children>(root,out var sig))
            {
                var children = entityManager.GetComponent<Children>(sig).Value;
                var ltw = entityManager.GetComponent<LocalToWorld>(root).Value;
                if (children != null)
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        var childLTP = ComputeLocalTRS(entityManager, children[i]);

                        var childLTW =  childLTP*ltw;
                        entityManager.SetComponent<LocalToWorld>(children[i], new() { Value = childLTW });
                        UpdateHierachy(entityManager, children[i]);
                    }
                }
            }
        }

        private static Matrix4x4 ComputeLocalTRS(EntityManager entityManager, Entity e)
        {
            Vector3 translation = entityManager.GetComponent(e, out Translation t) ? t.Value : Vector3.Zero;
            Vector3 rotation = entityManager.GetComponent(e, out Rotation r) ? r.Value : Vector3.Zero;
            Vector3 scale = entityManager.GetComponent(e, out Scale s) ? s.Value : Vector3.One;
            var trsMatrix = TransformExtensions.TRS(translation, rotation, scale);
            return trsMatrix;
        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            // mark the qurues as stale for next frame
            _ltwChildQuery.MarkStale();
            _ltwQuery.MarkStale();
            _addLTWQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {

        }
    }
}
