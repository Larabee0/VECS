using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
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
        internal readonly static SwapChainBuffer<DirectionalLightUniform> DirectionalLightBuffer;
        internal readonly static SwapChainBuffer<PointLightUniform> PointLightBuffer;
        internal readonly static SwapChainBuffer<SpotLightUniform> SpotLightBuffer;

        internal readonly static SwapChainBuffer<Matrix4x4> DirectionalLightMatsBuffer;
        internal readonly static SwapChainBuffer<Matrix4x4> PointLightMatsBuffer;
        internal readonly static SwapChainBuffer<Matrix4x4> SpotLightMatsBuffer;

        internal readonly static ConcurrentDictionary<int, SwapChainBuffer> _engineBuffers = new();

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
            _engineBuffers.AddOrUpdate(propertyId,buffer, (key, value) =>
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
            DirectionalLightBuffer = new(1, BufferUsageFlags, true);
            PointLightBuffer = new(PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS, BufferUsageFlags, true);
            SpotLightBuffer = new(SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS, BufferUsageFlags, true);



            DirectionalLightMatsBuffer = new(DirectionalLightShadows.MAX_CASCADE_COUNT, BufferUsageFlags, true);
            PointLightMatsBuffer = new(PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS*6, BufferUsageFlags, true);
            SpotLightMatsBuffer = new(SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS, BufferUsageFlags, true);

            CameraInfoBuffer.SetDebugName("CameraInfoBuffer");
            CameraInverseInfoBuffer.SetDebugName("CameraInverseInfoBuffer");
            AddtionalCameraInfoBuffer.SetDebugName("AddtionalCameraInfoBuffer");
            OrthopgrahicInfoBuffer.SetDebugName("OrthopgrahicInfoBuffer");

            LightingInfoBuffer.SetDebugName("LightingInfoBuffer");
            DirectionalLightBuffer.SetDebugName("DirectionalLightBuffer");
            PointLightBuffer.SetDebugName("PointLightBuffer");
            SpotLightBuffer.SetDebugName("SpotLightBuffer");

            DirectionalLightMatsBuffer.SetDebugName("DirectionalLightMatsBuffer");
            PointLightMatsBuffer.SetDebugName("PointLightMatsBuffer");
            SpotLightMatsBuffer.SetDebugName("SpotLightMatsBuffer");

            AddEngineBuffer(ShaderProperties.CameraInfoId, CameraInfoBuffer);
            AddEngineBuffer(ShaderProperties.CameraInverseId, CameraInverseInfoBuffer);
            AddEngineBuffer(ShaderProperties.AdditionalCameraInfoId, AddtionalCameraInfoBuffer);
            AddEngineBuffer(ShaderProperties.OrthographicInfoId, OrthopgrahicInfoBuffer);

            AddEngineBuffer(ShaderProperties.LightingInfoId, LightingInfoBuffer);
            AddEngineBuffer(ShaderProperties.DirectionalLightsBufferId, DirectionalLightBuffer);
            AddEngineBuffer(ShaderProperties.PointLightsBufferId, PointLightBuffer);
            AddEngineBuffer(ShaderProperties.SpotLightsBufferId, SpotLightBuffer);

            AddEngineBuffer(ShaderProperties.DirShadowMatsId, DirectionalLightMatsBuffer);
            AddEngineBuffer(ShaderProperties.PLShadowMatsId, PointLightMatsBuffer);
            AddEngineBuffer(ShaderProperties.SLShadowMatsId, SpotLightMatsBuffer);
        }

        public static void UpdateCameras(EntityManager entityManager, int frameIndex)
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
            GPUBufferExtensions.WriteFromHostDelayed(CameraInfoBuffer, frameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(CameraInverseInfoBuffer,frameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(AddtionalCameraInfoBuffer,frameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(OrthopgrahicInfoBuffer,frameIndex);
        }

        public static unsafe LightingInfo UpdateLights(EntityManager entityManager, int frameIndex)
        {
            LightingInfo lightingInfo = default;
            
            if(entityManager.GetComponent(Presenter.Instance.FrameInfoEntity, out DirectionalLightFrameInfo dirFrameInfo))
            {
                lightingInfo.NumDirectionalLights = dirFrameInfo.DirectionalLightCount;
                lightingInfo.NumDirectionalLightShadows = dirFrameInfo.DirectionalLightShadowCount;
            }

            if (entityManager.GetComponent(Presenter.Instance.FrameInfoEntity, out PointLightFrameInfo plFrameInfo))
            {
                lightingInfo.NumPointLights = plFrameInfo.PointLightCount;
                lightingInfo.NumPointLightShadows = plFrameInfo.PointLightShadowCount;
            }

            if(entityManager.GetComponent(Presenter.Instance.FrameInfoEntity, out SpotLightFrameInfo slFrameInfo))
            {
                lightingInfo.NumSpotLightShadows = slFrameInfo.SpotLightShadowCount;
                lightingInfo.NumSpotLights = slFrameInfo.SpotLightCount;
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
            DirectionalLightBuffer.Dispose();
            PointLightBuffer.Dispose();
            SpotLightBuffer.Dispose();

            DirectionalLightMatsBuffer.Dispose();
            PointLightMatsBuffer.Dispose();
            SpotLightMatsBuffer.Dispose();

            _engineBuffers.Clear();
        }
    }
}
