using System;
using System.Collections.Generic;
using System.Diagnostics;
using VECS.LowLevel;

namespace VECS.ECS.Presentation
{
    public class DirectionalLightSystem : PresentationSystemBase
    {
        private EntityQuery _directionalLightCreateQuery;
        private EntityQuery _directionalLightUpdateQuery;
        private EntityQuery _directionalLightShadowQuery;

        private DirectionalLightShadows _directionalLightShadows;

        bool reassignTextures = false;

        public override void OnCreate(EntityManager entityManager)
        {
            _directionalLightCreateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectionalLight))
                .WithNone(typeof(Prefab), typeof(UpdateShadow), typeof(UpdateLight))
                .Build();

            _directionalLightUpdateQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectionalLight), typeof(UpdateLight))
                .WithNone(typeof(Prefab), typeof(DoNotRender), typeof(ShadowInfo))
                .Build();

            //_directionalLightShadowQuery = new EntityQuery(entityManager)
            //    .WithAll(typeof(DirectionalLight), typeof(ShadowInfo))
            //    .WithNone(typeof(Prefab), typeof(DoNotRender))
            //    .Build();

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
                int dirCount = 0;
                var hostBuffer = (SwapChainBuffer<DirectionalLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.DirectionalLightsBufferId);

                if (_directionalLightShadowQuery.HasEntities)
                {
                    var entities = _directionalLightShadowQuery.GetEntities();

                    hostBuffer.Realloc((uint)entities.Count);
                    frameInfo.DirectionalLightShadowCount = entities.Count;
                    UpdateDLBuffer(entityManager, ref dirCount, entities, hostBuffer.HostBuffer);
                }
                if (_directionalLightUpdateQuery.HasEntities)
                {
                    var entities = _directionalLightUpdateQuery.GetEntities();

                    hostBuffer.Realloc((uint)(frameInfo.DirectionalLightShadowCount + entities.Count));

                    frameInfo.DirectionalLightCount = entities.Count;
                    UpdateDLBuffer(entityManager, ref dirCount, entities, hostBuffer.HostBuffer);
                }

                frameInfo.DirectionalLightCount += frameInfo.DirectionalLightShadowCount;
                frameInfo.DirectionalLightShadowCount = Math.Min(1, frameInfo.DirectionalLightShadowCount);
                hostBuffer.SetBuffersDirty(true);
            }
            entityManager.AddComponent(Presenter.Instance.FrameInfoEntity, frameInfo);
        }

        private static void UpdateDLBuffer(EntityManager entityManager, ref int dirCount, List<Entity> entities, Span<DirectionalLightUniform> hostBuffer)
        {
            for (int i = 0; i < entities.Count; i++, dirCount++)
            {
                if (!entityManager.GetComponent(entities[i], out DirectionalLight directionalLight)) continue;

                if (dirCount == 0 && entityManager.SingletonEntity<MainCamera>(out Entity mainCamera) && entityManager.GetComponent(mainCamera, out Camera camera))
                {
                    hostBuffer[dirCount] = DirectionalLightShadows.GetDirectionalLight(directionalLight.Value, new(camera));
                }
                else
                {
                    hostBuffer[dirCount] = directionalLight.Value;
                }
            }
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (!_directionalLightShadowQuery.HasEntities) return;
            reassignTextures = false;
            var entities = _directionalLightShadowQuery.GetEntities();
            int i = 0;
            for (; i < Math.Min(1, entities.Count); i++)
            {
                entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo);

                Debug.Assert(shadowInfo.Resolution > 2);
                bool textureChanged = _directionalLightShadows.SetShadowTexture(i, shadowInfo.Resolution);
                if (textureChanged)
                {
                    entityManager.AddComponent<UpdateShadow>(entities[i]);
                }
                reassignTextures |= textureChanged;
            }

            for (; i < 1; i++)
            {
                reassignTextures |= _directionalLightShadows.SetShadowTexture(i, 8);
            }
        }

        public override void OnShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (!_directionalLightUpdateQuery.HasEntities && !_directionalLightShadowQuery.HasEntities) return;

            var hostBuffer = (SwapChainBuffer<DirectionalLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.DirectionalLightsBufferId);
            GPUBufferExtensions.WriteFromHostDelayed(hostBuffer, Presenter.FrameIndex);

            var entities = _directionalLightShadowQuery.GetEntities();

            if (reassignTextures)
            {
                _directionalLightShadows.AssignShadowTextures(ShaderProperties.DirShadowImageId);
            }

            _directionalLightShadows.PreShadowPass(frameInfo);
            int i = 0;

            for (; i < Math.Min(1, entities.Count); i++)
            {
                if (!entityManager.HasComponent<UpdateShadow>(entities[i])
                    || !entityManager.GetComponent(entities[i], out ShadowInfo shadowInfo)) continue;

                if (shadowInfo.UpdateBehaviour == ShadowUpdate.OnDemand)
                {
                    entityManager.RemoveComponent<UpdateShadow>(entities[i]);
                }

                _directionalLightShadows.DirectionalShadowPass(frameInfo, hostBuffer.HostBuffer[i]);

            }

            for (; i < 1; i++)
            {
                _directionalLightShadows.ClearImage(frameInfo, i);
            }
        }
    }
}
