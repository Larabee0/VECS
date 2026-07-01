namespace VECS.ECS.Presentation
{
    public class LightUpdateSystem :SystemBase
    {
        private EntityQuery _lightUpdateQuery;
        public override void OnCreate(EntityManager entityManager)
        {
            _lightUpdateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(UpdateLight))
                .WithNone(typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (!_lightUpdateQuery.HasEntities) return;
            _lightUpdateQuery.GetEntities().ForEach(e =>
            {
                if (entityManager.HasComponent<ShadowInfo>(e, out int sig) )
                {
                    if (entityManager.GetComponent<ShadowInfo>(sig).UpdateBehaviour == ShadowUpdate.Always)
                    {
                        if (!entityManager.HasComponent<UpdateShadow>(e))
                        {
                            entityManager.AddComponent<UpdateShadow>(e);
                            //entityManager.DiryQueries();
                        }
                    } 
                }
            });
        }
    }
}
