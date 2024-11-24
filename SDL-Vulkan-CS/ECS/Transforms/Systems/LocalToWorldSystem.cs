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
        public override void OnCreate(EntityManager entityManager)
        {
            // any entity with a translation, rotation or scale component should get a LTW component added to it automatically.
            _addLTWQuery = new EntityQuery(entityManager)
                .WithAny(typeof(Translation), typeof(Rotation), typeof(Scale))
                .WithNone(typeof(LocalToWorld))
                .Build();

            // local to world update query
            _ltwQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld))
                .WithAny(typeof(Translation), typeof(Rotation), typeof(Scale))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_addLTWQuery.HasEntities) // updates and checks if the query has entities. NoAlloc check
            {
                _addLTWQuery.GetEntities().ForEach(e =>
                {
                    entityManager.AddComponent<LocalToWorld>(e);
                });
            }

            if (_ltwQuery.HasEntities) // updates and checks if the query has entities. NoAlloc check
            {
                // compute a ltw matrisx for each entity matching the query.
                // defaults are assume for entities missing t r s components
                _ltwQuery.GetEntities().ForEach(e =>
                {
                    Vector3 translation = entityManager.GetComponent(e, out Translation t) ? t.Value : Vector3.Zero;
                    Vector3 rotation = entityManager.GetComponent(e, out Rotation r) ? r.Value : Vector3.Zero;
                    Vector3 scale = entityManager.GetComponent(e, out Scale s) ? s.Value : Vector3.One;

                    entityManager.SetComponent<LocalToWorld>(e, new() { Value = TransformExtensions.TRS(translation, rotation, scale) });

                });
            }

        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            // mark the qurues as stale for next frame
            _ltwQuery.MarkStale();
            _addLTWQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {

        }
    }
}
