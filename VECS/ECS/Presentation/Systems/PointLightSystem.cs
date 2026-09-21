using System;
using System.Collections.Generic;
using System.Diagnostics;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{
    public class PointLightSystem : SystemBase
    {
        private EntityQuery _pointLightCreateQuery;
        private EntityQuery _pointLightUpdateQuery;
        private EntityQuery _pointLightShadowQuery;

        private PointLightShadows _pointLightShadows;

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

            _pointLightShadows = new();
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
            if (!_pointLightShadowQuery.HasEntities && !_pointLightUpdateQuery.HasEntities) return;

            _pointLightShadows.ReassignTextures = false;
            var entities = _pointLightShadowQuery.GetEntities();
            int i = 0;
            for (; i < Math.Min(PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS,entities.Count); i++)
            {
                bool hasShadowInfo = entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo);
                
                Debug.Assert(shadowInfo.Resolution > 2);
                bool textureChanged = _pointLightShadows.SetShadowTexture(i, shadowInfo.Resolution);
                if (textureChanged && shadowInfo.UpdateBehaviour == ShadowUpdate.OnDemand && !entityManager.HasComponent<UpdateShadow>(entities[i]))
                {
                    _pointLightShadows.UpdateShadow.Enqueue(i);
                }
                else if (textureChanged && shadowInfo.UpdateBehaviour != ShadowUpdate.OnDemand && !entityManager.HasComponent<UpdateShadow>(entities[i]))
                {
                    entityManager.AddComponent<UpdateShadow>(entities[i]);
                }
                _pointLightShadows.ReassignTextures |= textureChanged;
                if (!entityManager.HasComponent<UpdateShadow>(entities[i]) || !hasShadowInfo) continue;
                if (shadowInfo.UpdateBehaviour == ShadowUpdate.OnDemand)
                {
                    entityManager.RemoveComponent<UpdateShadow>(entities[i]);
                }
                _pointLightShadows.UpdateShadow.Enqueue(i);
            }

            for (; i < PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS; i++)
            {
                _pointLightShadows.ReassignTextures |= _pointLightShadows.SetShadowTexture(i, 8);
                _pointLightShadows.ClearShadow.Enqueue(i);
            }
        }
    }
}
