using System;
using System.Collections.Generic;
using System.Diagnostics;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{
    public class PointLightSystem : PresentationSystemBase
    {
        private EntityQuery _pointLightCreateQuery;
        private EntityQuery _pointLightUpdateQuery;
        private EntityQuery _pointLightShadowQuery;

        bool reassignTextures = false;

        public override void OnCreate(EntityManager entityManager)
        {
            _pointLightCreateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(PointLight),typeof(LocalToWorld))
                .WithNone(typeof(Prefab),typeof(UpdateShadow), typeof(ShadowImage), typeof(UpdatePointLight))
                .Build();

            _pointLightUpdateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(PointLight), typeof(LocalToWorld), typeof(UpdatePointLight))
                .WithNone(typeof(Prefab), typeof(DoNotRender),typeof(ShadowImage))
                .Build();

            _pointLightShadowQuery = new EntityQuery(entityManager)
                .WithAll(typeof(PointLight), typeof(LocalToWorld), typeof(ShadowImage), typeof(ShadowInfo))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _pointLightShadowQuery = new EntityQuery(entityManager)
                .WithAll(typeof(PointLight), typeof(LocalToWorld), typeof(ShadowImage), typeof(ShadowInfo), typeof(UpdateShadow))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            CreatePointLights(entityManager);
            UpdatePointLights(entityManager);
        }

        private void CreatePointLights(EntityManager entityManager)
        {
            if (!_pointLightCreateQuery.HasEntities) return;

            var entities = _pointLightCreateQuery.GetEntities();

            for (int i = 0; i < entities.Count; i++)
            {
                if(!entityManager.HasComponent<PointLight>(entities[i])) continue;

                entityManager.AddComponent<UpdatePointLight>(entities[i]);

                if (entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo))
                {
                    Debug.Assert(shadowInfo.Resolution > 2);
                    ShadowImage shadowImage = new()
                    {
                        ShadowTextureId = PointLightShadows.CreateShadowMap(entities[i], shadowInfo.Resolution).Hash
                    };

                    entityManager.AddComponent<UpdateShadow>(entities[i]);
                    entityManager.AddComponent(entities[i], shadowImage);
                }
            }
            _pointLightUpdateQuery.MarkStaleNow();
        }

        private void UpdatePointLights(EntityManager entityManager)
        {
            PointLightFrameInfo frameInfo = new();
            if (_pointLightShadowQuery.HasEntities || _pointLightUpdateQuery.HasEntities)
            {
                int plCount = 0;
                var hostBuffer = (SwapChainBuffer<PointLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId);
                if (_pointLightShadowQuery.HasEntities)
                {
                    var entities = _pointLightShadowQuery.GetEntities();

                    frameInfo.PointLightShadowCount = entities.Count;
                    UpdatePLBuffer(entityManager, ref plCount, entities, hostBuffer.HostBuffer);
                }
                if (_pointLightUpdateQuery.HasEntities)
                {
                    var entities = _pointLightUpdateQuery.GetEntities();
                    frameInfo.PointLightCount = entities.Count;
                    UpdatePLBuffer(entityManager, ref plCount, entities, hostBuffer.HostBuffer);
                }

                frameInfo.PointLightCount += frameInfo.PointLightShadowCount;

                hostBuffer.SetBuffersDirty(true);
            }
            entityManager.AddComponent(Presenter.Instance.FrameInfoEntity, frameInfo);
        }

        private static void UpdatePLBuffer(EntityManager entityManager, ref int plCount, List<Entity> entities, Span<PointLightUniform> hostBuffer)
        {
            for (int i = 0; i < entities.Count; i++, plCount++)
            {
                if (entityManager.GetComponent(entities[i], out LocalToWorld ltw) && entityManager.GetComponent(entities[i], out PointLight pointLight))
                {
                    hostBuffer[plCount] = new(ltw.Value.Translation,pointLight);
                }
            }
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (!_pointLightShadowQuery.HasEntities) return;
            reassignTextures = false;
            var entities = _pointLightShadowQuery.GetEntities();
            int i = 0;
            for (; i < Math.Min(PointLightShadows.MAX_POINT_LIGHTS_SHADOW_CASTERS,entities.Count); i++)
            {
                entityManager.GetComponent(entities[i], out ShadowImage image);
                entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo);
                
                Debug.Assert(shadowInfo.Resolution > 2);
                var cubemap = AssetDataBase<Cubemap>.GetHashedSilentFail(image.ShadowTextureId);
                if (cubemap == null)
                {
                    cubemap = PointLightShadows.CreateShadowMap(entities[i], shadowInfo.Resolution);
                    image.ShadowTextureId = cubemap.Hash;
                    entityManager.SetComponent(entities[i], image);
                    reassignTextures |= true;
                }
                else if (cubemap.Width != shadowInfo.Resolution)
                {
                    cubemap.Reinitialise(shadowInfo.Resolution);
                    reassignTextures |= true;
                }


                reassignTextures |= Presenter.Instance.PLShadows.SetShadowTexture(i, cubemap);
            }

            for (; i < PointLightShadows.MAX_POINT_LIGHTS_SHADOW_CASTERS; i++)
            {
                reassignTextures |= Presenter.Instance.PLShadows.SetShadowTexture(i, EngineTextures.PointLightShadowEmpty);
            }
        }

        public override void OnShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (!_pointLightUpdateQuery.HasEntities && !_pointLightShadowQuery.HasEntities) return;
            var hostBuffer = (SwapChainBuffer<PointLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId);
            GPUBufferExtensions.WriteFromHostDelayed(hostBuffer, frameInfo.FrameIndex);
            

            var entities = _pointLightShadowQuery.GetEntities();
            var plShadows = Presenter.Instance.PLShadows;

            if (reassignTextures)
            {
                plShadows.AssignDirShadowTexture();
            }

            plShadows.PrePointLightShadowPass(frameInfo);
            for (int i = 0; i < entities.Count; i++)
            {
                if (!entityManager.HasComponent<UpdateShadow>(entities[i])
                    || !entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo)
                    || !entityManager.GetComponent(entities[i], out ShadowImage shadowImage)) continue;

                var cubemap = AssetDataBase<Cubemap>.GetHashedSilentFail(shadowImage.ShadowTextureId);
                if (cubemap == null) continue;

                plShadows.PointLightShadowPass(frameInfo, i, cubemap);

            }
        }
    }
}
