using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{

    public class GenericRenderSystem : PresentationSystemBase
    {
        public const uint MAX_DRAWS = 2000;
        private EntityQuery _renderEntityQuery;
        private EntityQuery _renderBloomEntityQuery;

        private FustrumCull _cullCompute;

        private ForwardInternal _forwardData;
        private ShadowInternal _shadowData;

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _renderBloomEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld),typeof(RenderMesh), typeof(WorldRenderBounds), typeof(BloomTag))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();


            _cullCompute = new();

            _forwardData = new(_cullCompute);
            _shadowData = new(_cullCompute);
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            _forwardData?.Dispose();
            _shadowData?.Dispose();

            _cullCompute?.Dispose();
        }

        private void ResetMeshes()
        {
            int meshCount = DirectMesh.DirectMeshes.Count;
            for(int i = 0; i < meshCount; i++)
            {
                _forwardData.ResetMesh(i);
                _shadowData.ResetMesh(i);
            }
        }

        public override void OnCull(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            ResetMeshes();

            _forwardData.GenerateDrawCmds(rendererFrameInfo,entityManager,entities);
            
        }

        public unsafe override void OnShadowPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            _shadowData.GenerateDrawCmds(rendererFrameInfo,entityManager, entities);
        }

        public override void OnBloomGlow(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            _forwardData.ExecuteBloomDrawCmds(rendererFrameInfo);
        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            _forwardData.ExecuteDrawCmds(rendererFrameInfo);
        }
    }
}
