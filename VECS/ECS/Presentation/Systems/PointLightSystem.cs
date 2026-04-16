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
                .WithNone(typeof(Prefab),typeof(UpdateShadow),  typeof(UpdateLight))
                .Build();

            _pointLightUpdateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(PointLight), typeof(LocalToWorld), typeof(UpdateLight))
                .WithNone(typeof(Prefab), typeof(DoNotRender),typeof(ShadowInfo))
                .Build();

            _pointLightShadowQuery = new EntityQuery(entityManager)
                .WithAll(typeof(PointLight), typeof(LocalToWorld), typeof(ShadowInfo))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _pointLightShadowQuery = new EntityQuery(entityManager)
                .WithAll(typeof(PointLight), typeof(LocalToWorld),  typeof(ShadowInfo), typeof(UpdateShadow))
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

                entityManager.AddComponent<UpdateLight>(entities[i]);

                if (entityManager.HasComponent<ShadowInfo>(entities[i]))
                {
                    entityManager.AddComponent<UpdateShadow>(entities[i]);
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

                    hostBuffer.Realloc((uint)entities.Count);
                    frameInfo.PointLightShadowCount = entities.Count;
                    UpdatePLBuffer(entityManager, ref plCount, entities, hostBuffer.HostBuffer);
                }
                if (_pointLightUpdateQuery.HasEntities)
                {
                    var entities = _pointLightUpdateQuery.GetEntities();
                    
                    hostBuffer.Realloc((uint)(frameInfo.PointLightShadowCount + entities.Count));

                    frameInfo.PointLightCount = entities.Count;
                    UpdatePLBuffer(entityManager, ref plCount, entities, hostBuffer.HostBuffer);
                }

                frameInfo.PointLightCount += frameInfo.PointLightShadowCount;
                frameInfo.PointLightShadowCount = Math.Min((int)SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS, frameInfo.PointLightShadowCount);

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
            for (; i < Math.Min(PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS,entities.Count); i++)
            {
                entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo);
                
                Debug.Assert(shadowInfo.Resolution > 2);
                bool textureChanged = Presenter.Instance.PLShadows.SetShadowTexture(i, shadowInfo.Resolution);
                if (textureChanged)
                {
                    entityManager.AddComponent<UpdateShadow>(entities[i]);
                }
                reassignTextures |= textureChanged;
            }

            for (; i < PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS; i++)
            {
                reassignTextures |= Presenter.Instance.PLShadows.SetShadowTexture(i, 8);
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
                plShadows.AssignShadowTextures(ShaderProperties.PLShadowImageId);
            }

            plShadows.PreShadowPass(frameInfo);
            int i = 0;

            for (; i < Math.Min(PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS, entities.Count); i++)
            {
                if (!entityManager.HasComponent<UpdateShadow>(entities[i])
                    || !entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo)) continue;

                if(shadowInfo.UpdateBehaviour == ShadowUpdate.OnDemand)
                {
                    entityManager.RemoveComponent<UpdateShadow>(entities[i]);
                }

                plShadows.PointLightShadowPass(frameInfo, i, hostBuffer.HostBuffer[i]);

            }

            for (; i < PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS; i++)
            {
                plShadows.ClearImage(frameInfo,i);
            }
        }
    }
}
