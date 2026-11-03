using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS.Transforms;
using VECS.LowLevel;

namespace VECS.ECS.Presentation
{
    public class DebugDrawUtilities : SystemBase
    {
        private EntityQuery _renderBoundsQuery;
        private EntityQuery _cameraQuery;
        private bool _drawBounds = false;
        private bool _drawCameraFustrums = false;

        public override void OnCreate(EntityManager entityManager)
        {
            _renderBoundsQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectSubMeshIndex), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();
            _cameraQuery = new EntityQuery(entityManager)
                .WithAll(typeof(Camera), typeof(LocalToWorld))
                .WithNone(typeof(Prefab), typeof(MainCamera))
                .Build();
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F1))
            {
                _drawBounds = !_drawBounds;
            }

            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F2))
            {
                _drawCameraFustrums = !_drawCameraFustrums;
            }

            QueueBoundingBoxes(entityManager);
            QueueCameraFustrums(entityManager);
        }

        internal unsafe void QueueBoundingBoxes(EntityManager entityManager)
        {
            if (!_drawBounds || !_renderBoundsQuery.HasEntities) return;
            var entities = _renderBoundsQuery.GetEntities();
            HashSet<DirectMesh> readMeshlets = new();
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                AABB bounds = entityManager.GetComponent<WorldRenderBounds>(entity).Value;
                //DebugDrawer.DrawWireCube(bounds.Center, bounds.Size, Quaternion.Identity);
                if (GraphicsDevice.MeshShading)
                {
                    if (entityManager.GetComponent<RenderMesh>(entity, out var renderMesh) && entityManager.GetComponent<LocalToWorld>(entity, out var ltw))
                    {
                        var directMesh = AssetDataBase<DirectMesh>.GetHashed(renderMesh.MeshHash);
                        readMeshlets.Add(directMesh);
                        //directMesh._meshletBuffer.ReadToHostBuffer();
                        var submesh = directMesh.DirectSubMeshes[renderMesh.SubMesh];
                        var meshletBounds = directMesh._meshletBoundsBuffer.HostBuffer;
                        var meshlets = directMesh._meshletBuffer.HostBuffer;
                        var meshletData = submesh.MeshletData;
                        var ltwM = ltw.Value;
                        for (int j = 0; j < meshletData.MeshletCount; j++)
                        {
                            //var meshletBound = meshletBounds[meshletData.MeshletOffset + j];
                            var meshletBound = AABB.Transform(ltwM, meshletBounds[meshletData.MeshletOffset + j]);

                            DebugDrawer.DrawWireCube(meshletBound.Center, meshletBound.Size,Quaternion.Identity);
                        }
                        
                    }
                }
            }

            foreach (var item in readMeshlets)
            {
                item._meshletBuffer.ReadToHostBuffer();
            }
        }

        internal void QueueCameraFustrums(EntityManager entityManager)
        {
            if (!_drawCameraFustrums || !_cameraQuery.HasEntities || !SwapChain.SwapChainInitialised) return;

            var cameras = _cameraQuery.GetEntities();
            for (int i = 0; i < cameras.Count; i++)
            {
                var cam = cameras[i];
                var ltw = entityManager.GetComponent<LocalToWorld>(cam).Value;
                if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.Space) && entityManager.SingletonEntity<MainCamera>(out Entity mainCamera))
                {
                    ltw = entityManager.GetComponent<LocalToWorld>(mainCamera).Value;
                    entityManager.SetComponent(cam, new LocalToWorld() { Value = ltw });
                }
                var fustrum = entityManager.GetComponent<Camera>(cam);
                Matrix4x4 projection = fustrum.ProjectionMatrix;
                DebugDrawer.DrawFustrum(projection, ltw, Colour.White);
            }
        }

        public override void OnDestroy(EntityManager entityManager)
        {
        }
    }
}
