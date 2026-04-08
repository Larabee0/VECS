using System;
using System.Collections.Generic;
using System.Diagnostics;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{
    public class SpotLightSystem : PresentationSystemBase
    {
        private EntityQuery _spotLightCreateQuery;
        private EntityQuery _spotLightUpdateQuery;
        private EntityQuery _spotLightShadowQuery;

        bool reassignTextures = false;

        public override void OnCreate(EntityManager entityManager)
        {
            _spotLightCreateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(SpotLight), typeof(LocalToWorld))
                .WithNone(typeof(Prefab), typeof(UpdateShadow), typeof(UpdateLight))
                .Build();

            _spotLightUpdateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(SpotLight), typeof(LocalToWorld), typeof(UpdateLight))
                .WithNone(typeof(Prefab), typeof(DoNotRender), typeof(ShadowInfo))
                .Build();

            _spotLightShadowQuery = new EntityQuery(entityManager)
                .WithAll(typeof(SpotLight), typeof(LocalToWorld), typeof(ShadowInfo))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _spotLightShadowQuery = new EntityQuery(entityManager)
                .WithAll(typeof(SpotLight), typeof(LocalToWorld), typeof(ShadowInfo), typeof(UpdateShadow))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            CreateSpotLights(entityManager);
            UpdateSpotLights(entityManager);
        }

        private void CreateSpotLights(EntityManager entityManager)
        {
            if (!_spotLightCreateQuery.HasEntities) return;

            var entities = _spotLightCreateQuery.GetEntities();

            for (int i = 0; i < entities.Count; i++)
            {
                if (!entityManager.HasComponent<SpotLight>(entities[i])) continue;

                entityManager.AddComponent<UpdateLight>(entities[i]);

                if (entityManager.HasComponent<ShadowInfo>(entities[i]))
                {
                    entityManager.AddComponent<UpdateShadow>(entities[i]);
                }
            }
            _spotLightUpdateQuery.MarkStaleNow();
        }

        private void UpdateSpotLights(EntityManager entityManager)
        {
            SpotLightFrameInfo frameInfo = new();
            if (_spotLightShadowQuery.HasEntities || _spotLightUpdateQuery.HasEntities)
            {
                int slCount = 0;
                var hostBuffer = (SwapChainBuffer<SpotLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.SpotLightsBufferId);

                if (_spotLightShadowQuery.HasEntities)
                {
                    var entities = _spotLightShadowQuery.GetEntities();

                    frameInfo.SpotLightShadowCount = entities.Count;
                    UpdateSLBuffer(entityManager, ref slCount, entities, hostBuffer.HostBuffer);
                }
                if (_spotLightUpdateQuery.HasEntities)
                {
                    var entities = _spotLightUpdateQuery.GetEntities();

                    hostBuffer.Realloc((uint)(frameInfo.SpotLightShadowCount + entities.Count));

                    frameInfo.SpotLightCount = entities.Count;
                    UpdateSLBuffer(entityManager, ref slCount, entities, hostBuffer.HostBuffer);
                }

                frameInfo.SpotLightCount += frameInfo.SpotLightShadowCount;

                hostBuffer.SetBuffersDirty(true);
            }
            entityManager.AddComponent(Presenter.Instance.FrameInfoEntity, frameInfo);
        }

        private static void UpdateSLBuffer(EntityManager entityManager, ref int slCount, List<Entity> entities, Span<SpotLightUniform> hostBuffer)
        {
            for (int i = 0; i < entities.Count; i++, slCount++)
            {
                if (entityManager.GetComponent(entities[i], out LocalToWorld ltw) && entityManager.GetComponent(entities[i], out SpotLight spotLight))
                {
                    hostBuffer[slCount] = new(ltw.Value.Translation,ltw.Value.Forward(), spotLight);
                    hostBuffer[i].LightSpace = SpotLightShadows.GetSpaceMatrix(hostBuffer[i], out _, out _, out _);
                }
            }
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (!_spotLightShadowQuery.HasEntities) return;
            reassignTextures = false;
            var entities = _spotLightShadowQuery.GetEntities();
            int i = 0;
            for (; i < Math.Min(SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS, entities.Count); i++)
            {
                entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo);

                Debug.Assert(shadowInfo.Resolution > 2);
                bool textureChanged = Presenter.Instance.SLShadows.SetShadowTexture(i, shadowInfo.Resolution);
                if (textureChanged)
                {
                    entityManager.AddComponent<UpdateShadow>(entities[i]);
                }
                reassignTextures |= textureChanged;
            }

            for (; i < SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS; i++)
            {
                reassignTextures |= Presenter.Instance.SLShadows.SetShadowTexture(i, 8);
            }
        }

        public override void OnShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (!_spotLightUpdateQuery.HasEntities && !_spotLightShadowQuery.HasEntities) return;

            var hostBuffer = (SwapChainBuffer<SpotLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.SpotLightsBufferId);
            GPUBufferExtensions.WriteFromHostDelayed(hostBuffer, frameInfo.FrameIndex);

            var entities = _spotLightShadowQuery.GetEntities();
            var slShadows = Presenter.Instance.SLShadows;

            if (reassignTextures)
            {
                slShadows.AssignDirShadowTexture(ShaderProperties.SLShadowImageId);
            }

            slShadows.PreShadowPass(frameInfo);
            int i = 0;

            for (; i < Math.Min(SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS, entities.Count); i++)
            {
                if (!entityManager.HasComponent<UpdateShadow>(entities[i])
                    || !entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo)) continue;

                if (shadowInfo.UpdateBehaviour == ShadowUpdate.OnDemand)
                {
                    entityManager.RemoveComponent<UpdateShadow>(entities[i]);
                }

                slShadows.SpotLightShadowPass(frameInfo, i, hostBuffer.HostBuffer[i]);

            }

            for (; i < SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS; i++)
            {
                slShadows.ClearImage(frameInfo, i);
            }
        }
    }
}
