using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace VECS.ECS.Presentation
{
    public class DirectionalLightSystem : SystemBase
    {
        private EntityQuery _directionalLightCreateQuery;
        private EntityQuery _directionalLightUpdateQuery;
        private EntityQuery _directionalLightShadowQuery;

        private DirectionalLightShadows _directionalLightShadows;

        public override void OnCreate(EntityManager entityManager)
        {
            _directionalLightCreateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectionalLight))
                .WithNone(typeof(Prefab), typeof(UpdateShadow), typeof(UpdateLight))
                .Build();

            _directionalLightUpdateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectionalLight), typeof(UpdateLight))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _directionalLightShadowQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectionalLight), typeof(ShadowInfo), typeof(UpdateShadow))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _directionalLightShadows = new();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            CreateDirectionalLights(entityManager);
            UpdateDirectionalLights(entityManager);
        }

        private void CreateDirectionalLights(EntityManager entityManager)
        {
            if (!_directionalLightCreateQuery.HasEntities) return;

            var entities = _directionalLightCreateQuery.GetEntities();

            for (int i = 0; i < entities.Count; i++)
            {
                if (!entityManager.HasComponent<DirectionalLight>(entities[i])) continue;

                entityManager.AddComponent<UpdateLight>(entities[i]);

                if (entityManager.HasComponent<ShadowInfo>(entities[i]))
                {
                    entityManager.AddComponent<UpdateShadow>(entities[i]);
                }
            }
            _directionalLightUpdateQuery.MarkStaleNow();
        }

        private void UpdateDirectionalLights(EntityManager entityManager)
        {
            DirectionalLightFrameInfo frameInfo = new();
            if (_directionalLightShadowQuery.HasEntities || _directionalLightUpdateQuery.HasEntities)
            {
                int dirShadowCount = 0;
                int dirCount = 0;
                var shadowHostBuffer = (SwapChainBuffer<DirectionalLightShadowUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.DirectionalLightShadowBufferId);
                var lightHostBuffer = (SwapChainBuffer<DirectionalLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.DirectionalLightsBufferId);

                if (_directionalLightShadowQuery.HasEntities)
                {
                    var entities = _directionalLightShadowQuery.GetEntities();

                    shadowHostBuffer.Realloc((uint)entities.Count);
                    frameInfo.DirectionalLightShadowCount = entities.Count;
                    UpdateDLShadowBuffer(entityManager, ref dirShadowCount, dirCount, entities, shadowHostBuffer.HostBuffer);
                }
                if (_directionalLightUpdateQuery.HasEntities)
                {
                    var entities = _directionalLightUpdateQuery.GetEntities();

                    shadowHostBuffer.Realloc((uint)(frameInfo.DirectionalLightShadowCount + entities.Count));

                    frameInfo.DirectionalLightCount = entities.Count;
                    UpdateDLBuffer(entityManager, ref dirCount, entities, lightHostBuffer.HostBuffer);
                }

                frameInfo.DirectionalLightCount += frameInfo.DirectionalLightShadowCount;
                frameInfo.DirectionalLightShadowCount = Math.Min(1, frameInfo.DirectionalLightShadowCount);
                shadowHostBuffer.SetBuffersDirty(true);
                lightHostBuffer.SetBuffersDirty(true);
            }
            entityManager.AddComponent(Presenter.Instance.FrameInfoEntity, frameInfo);
        }

        private static void UpdateDLShadowBuffer(EntityManager entityManager, ref int dirCount, int lightIndex, List<Entity> entities, Span<DirectionalLightShadowUniform> hostBuffer)
        {
            var cameras = entityManager.GetAllEntitiesWithComponent<Camera>();
            if (cameras == null) return;
            for (int i = 0; i < entities.Count; i++)
            {
                if (!entityManager.GetComponent(entities[i], out DirectionalLight directionalLight)) continue;

                for (int j = 0; j < Math.Min(cameras.Count, Presenter.MAX_CAMERAS); j++, dirCount++)
                {
                    hostBuffer[dirCount] = DirectionalLightShadows.GetDirectionalLight(directionalLight.Value, lightIndex, new(entityManager.GetComponent<Camera>(cameras[i])));
                }

                
            }
        }

        private static void UpdateDLBuffer(EntityManager entityManager, ref int dirCount, List<Entity> entities, Span<DirectionalLightUniform> hostBuffer)
        {
            for (int i = 0; i < entities.Count; i++, dirCount++)
            {
                if (!entityManager.GetComponent(entities[i], out DirectionalLight directionalLight)) continue;

                hostBuffer[dirCount] = directionalLight.Value;
            }
        }

        public override void OnPrePresent(EntityManager entityManager)
        {

            if (!_directionalLightShadowQuery.HasEntities && !_directionalLightUpdateQuery.HasEntities) return;

            _directionalLightShadows.ReassignTextures = false;
            var entities = _directionalLightShadowQuery.GetEntities();
            int i = 0;
            for (; i < Math.Min(1, entities.Count); i++)
            {
                bool hasShadowInfo = entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo);

                Debug.Assert(shadowInfo.Resolution > 2);
                bool textureChanged = _directionalLightShadows.SetShadowTexture(i, shadowInfo.Resolution);
                if (textureChanged && shadowInfo.UpdateBehaviour == ShadowUpdate.OnDemand && !entityManager.HasComponent<UpdateShadow>(entities[i]))
                {
                    //_directionalLightShadows.UpdateShadow.Enqueue(i);
                }
                else if(textureChanged && shadowInfo.UpdateBehaviour != ShadowUpdate.OnDemand && !entityManager.HasComponent<UpdateShadow>(entities[i]))
                {
                    entityManager.AddComponent<UpdateShadow>(entities[i]);
                }
                _directionalLightShadows.ReassignTextures |= textureChanged;
                if (!entityManager.HasComponent<UpdateShadow>(entities[i]) || !hasShadowInfo) continue;
                if (shadowInfo.UpdateBehaviour == ShadowUpdate.OnDemand)
                {
                    entityManager.RemoveComponent<UpdateShadow>(entities[i]);
                }
                //_directionalLightShadows.UpdateShadow.Enqueue(i);
            }

            for (; i < 1; i++)
            {
                _directionalLightShadows.ReassignTextures |= _directionalLightShadows.SetShadowTexture(i, 8);
                _directionalLightShadows.Clear = true;
            }
        }
    }
}
