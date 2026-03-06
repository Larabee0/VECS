using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class EngineBuffers
    {
        private const VkBufferUsageFlags BufferUsageFlags = VkBufferUsageFlags.StorageBuffer;

        private readonly static SwapChainBuffer<CameraInfo> CameraInfoBuffer;
        private readonly static SwapChainBuffer<CameraInverseInfo> CameraInverseInfoBuffer;
        private readonly static SwapChainBuffer<AdditionalCameraInfo> AddtionalCameraInfoBuffer;
        private readonly static SwapChainBuffer<OrthographicInfo> OrthopgrahicInfoBuffer;

        private readonly static SwapChainBuffer LightingInfoBuffer;
        private readonly static SwapChainBuffer<PointLightUniform> PointLightBuffer;
        private readonly static SwapChainBuffer<SpotLightUniform> SpotLightBuffer;

        private readonly static ConcurrentDictionary<int, SwapChainBuffer> _engineBuffers = new();

        public static SwapChainBuffer TryGetBuffer(int propertyId)
        {
            _engineBuffers.TryGetValue(propertyId, out var buffer);
            return buffer;
        }

        public static void AddEngineBuffer(int propertyId, SwapChainBuffer buffer)
        {
            if(!_engineBuffers.TryAdd(propertyId, buffer))
            {
                throw new ArgumentException(string.Format("Key {0} already exists in the enginebuffers dictionary, use UpdateEngineBuffer to replace it", propertyId));
            }
        }

        public static void UpdateEngineBuffer(int propertyId, SwapChainBuffer buffer)
        {
            if(!_engineBuffers.TryGetValue(propertyId, out var existing) || !_engineBuffers.TryUpdate(propertyId, buffer, existing))
            {
                throw new KeyNotFoundException(string.Format("Key {0} has no buffer assocaited with it, use AddEngineBuffer to add it", propertyId));
            }
        }

        public static void AddOrUpdateEngineBuffer(int propertyId, SwapChainBuffer buffer)
        {
            _engineBuffers.AddOrUpdate(propertyId,buffer,(int key, SwapChainBuffer value) =>
            {
                if (!value.IsDisposed)
                {
                    value.Dispose();
                }
                return buffer;
            });
        }

        public static void RemoveEngineBuffer(int propertyId, bool disposeAfterRemove = true)
        {
            if(_engineBuffers.TryRemove(propertyId, out var buffer) && disposeAfterRemove)
            {
                buffer.Dispose();
            }
        }

        static unsafe EngineBuffers()
        {
            CameraInfoBuffer = new (Presenter.MAX_CAMERAS, BufferUsageFlags, true);
            CameraInverseInfoBuffer = new (Presenter.MAX_CAMERAS, BufferUsageFlags, true);
            AddtionalCameraInfoBuffer = new (Presenter.MAX_CAMERAS, BufferUsageFlags, true);
            OrthopgrahicInfoBuffer = new (Presenter.MAX_CAMERAS, BufferUsageFlags, true);

            LightingInfoBuffer = new(1, GPUBufferExtensions.GetAlignment((uint)sizeof(LightingInfo), VkBufferUsageFlags.UniformBuffer), VkBufferUsageFlags.UniformBuffer, true);
            PointLightBuffer = new(Presenter.MAX_POINT_LIGHTS, BufferUsageFlags, true);
            SpotLightBuffer = new(Presenter.MAX_POINT_LIGHTS, BufferUsageFlags, true);


            AddEngineBuffer(ShaderProperties.CameraInfoId, CameraInfoBuffer);
            AddEngineBuffer(ShaderProperties.CameraInverseId, CameraInverseInfoBuffer);
            AddEngineBuffer(ShaderProperties.AdditionalCameraInfoId, AddtionalCameraInfoBuffer);
            AddEngineBuffer(ShaderProperties.OrthographicInfoId, OrthopgrahicInfoBuffer);

            AddEngineBuffer(ShaderProperties.LightingInfoId, LightingInfoBuffer);
            AddEngineBuffer(ShaderProperties.PointLightsBufferId, PointLightBuffer);
            AddEngineBuffer(ShaderProperties.SpotLightsBufferId, SpotLightBuffer);
        }

        public unsafe static void UpdateCameras(EntityManager entityManager, int frameIndex)
        {
            var cameras = entityManager.GetAllEntitiesWithComponent<Camera>();
            var cameraCount = Math.Min(cameras.Count, Presenter.MAX_CAMERAS);
            int mainCamera = -1;
            Camera camera;
            float clipNear = 0;
            float clipFar = 0;
            CameraOrthographic orthCam = default;
            bool orth = false;
            for (int i = 0; i < cameraCount; i++)
            {
                var entity = cameras[i];
                camera = entityManager.GetComponent<Camera>(entity);
                if (mainCamera == -1 && entityManager.HasComponent<MainCamera>(entity))
                {
                    mainCamera = i;
                }
                if (entityManager.HasComponent<CameraPerspective>(entity, out var signature))
                {
                    var per = entityManager.GetComponent<CameraPerspective>(signature);
                    clipNear = per.ClipNear;
                    clipFar = per.ClipFar;
                }
                else if (entityManager.HasComponent<CameraOrthographic>(entity, out signature))
                {
                    orthCam = entityManager.GetComponent<CameraOrthographic>(signature);
                    clipNear = orthCam.ClipNear;
                    clipFar = orthCam.ClipFar;
                    orth = true;
                }
                CameraInfoBuffer.HostBuffer[i] = new(camera);
                CameraInverseInfoBuffer.HostBuffer[i] = new(camera);
                AddtionalCameraInfoBuffer.HostBuffer[i] = new(camera.ProjectionMatrix, clipNear, clipFar, SwapChain.ExtentAspectRatio);
                OrthopgrahicInfoBuffer.HostBuffer[i] = new(orth, orthCam);
            }

            CameraInfoBuffer.SetBuffersDirty(true);
            CameraInverseInfoBuffer.SetBuffersDirty(true);
            AddtionalCameraInfoBuffer.SetBuffersDirty(true);
            OrthopgrahicInfoBuffer.SetBuffersDirty(true);
            GPUBufferExtensions.WriteFromHostDelayed( CameraInfoBuffer, frameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(CameraInverseInfoBuffer,frameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(AddtionalCameraInfoBuffer,frameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(OrthopgrahicInfoBuffer,frameIndex);
        }

        public static unsafe LightingInfo UpdateLights(EntityManager entityManager, int frameIndex)
        {
            LightingInfo lightingInfo;
            var dirLights = entityManager.GetAllEntitiesWithComponent<DirectionalLight>();
            var pointLights = entityManager.GetAllEntitiesWithComponent<PointLight>();
            var spotLights = entityManager.GetAllEntitiesWithComponent<SpotLight>();

            if (dirLights != null && dirLights.Count > 0)
            {
                lightingInfo = new(entityManager.GetComponent<DirectionalLight>(dirLights[0]), 0, 0);
                if(entityManager.SingletonEntity<MainCamera>(out Entity mainCameraEntity))
                {

                    Camera camera = entityManager.GetComponent<Camera>(mainCameraEntity);

                    lightingInfo.DirectionalLight = DirectionalLightShadows.GetDirectionalLight(lightingInfo.DirectionalLight, new(camera), new(camera.ProjectionMatrix, camera.ClipNear, camera.ClipFar, 0));
                }
                
            }
            else
            {
                lightingInfo = new()
                {
                    DirectionalLight = new()
                    {
                        Ambient = Vector4.One,
                        Direction = new(0, -1, 0, 0),
                        CascadeSplits = Vector4.Zero,
                        LightSpaceA = Matrix4x4.Identity,
                        LightSpaceB = Matrix4x4.Identity,
                        LightSpaceC = Matrix4x4.Identity,
                        LightSpaceD = Matrix4x4.Identity,
                        CascadeCount = 0
                    }
                };
            }

            if (pointLights != null && pointLights.Count > 0)
            {
                int pointLightCount = Math.Min(pointLights.Count, Presenter.MAX_POINT_LIGHTS);
                lightingInfo.NumPointLights = pointLightCount;

                for (int i = 0; i < pointLightCount; i++)
                {
                    Vector3 position = entityManager.GetComponent<LocalToWorld>(pointLights[i]).Value.Translation;
                    var pointLight = entityManager.GetComponent<PointLight>(pointLights[i]);
                    PointLightBuffer.HostBuffer[i] = new(position, pointLight);
                }

                for (int i = pointLightCount; i < Presenter.MAX_POINT_LIGHTS; i++)
                {
                    PointLightBuffer.HostBuffer[i] = default;
                }

                PointLightBuffer.SetBuffersDirty(true);
                GPUBufferExtensions.WriteFromHostDelayed(PointLightBuffer, frameIndex);
            }

            if (spotLights != null && spotLights.Count > 0)
            {
                int spotLightCount = Math.Min(spotLights.Count, Presenter.MAX_POINT_LIGHTS);
                lightingInfo.NumSpotLights = spotLightCount;

                for (int i = 0; i < spotLightCount; i++)
                {
                    var ltw = entityManager.GetComponent<LocalToWorld>(spotLights[i]).Value;
                    var spotLight = entityManager.GetComponent<SpotLight>(spotLights[i]);
                    SpotLightBuffer.HostBuffer[i] = new(ltw.Translation, ltw.Forward(), spotLight);
                    SpotLightBuffer.HostBuffer[i].LightSpace = SpotLightShadows.GetSpaceMatrix(SpotLightBuffer.HostBuffer[i], out _, out _, out _);
                }

                for (int i = spotLightCount; i < Presenter.MAX_POINT_LIGHTS; i++)
                {
                    SpotLightBuffer.HostBuffer[i] = default;
                }

                SpotLightBuffer.SetBuffersDirty(true);
                GPUBufferExtensions.WriteFromHostDelayed(SpotLightBuffer, frameIndex);

            }

            Buffer.MemoryCopy(&lightingInfo, LightingInfoBuffer.HostPtr, LightingInfoBuffer.InstanceSize32, sizeof(LightingInfo));
            LightingInfoBuffer.SetBuffersDirty(true);
            GPUBufferExtensions.WriteFromHostDelayed(LightingInfoBuffer, frameIndex);
            return lightingInfo;
        }

        public static void CleanUp()
        {

            CameraInfoBuffer.Dispose();
            CameraInverseInfoBuffer.Dispose();
            AddtionalCameraInfoBuffer.Dispose();
            OrthopgrahicInfoBuffer.Dispose();

            LightingInfoBuffer.Dispose();
            PointLightBuffer.Dispose();
            SpotLightBuffer.Dispose();
            _engineBuffers.Clear();
        }
    }
}
