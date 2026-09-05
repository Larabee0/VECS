using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using Vortice.Vulkan;

namespace VECS
{
    public static class EngineBuffers
    {
        private const VkBufferUsageFlags BufferUsageFlags = VkBufferUsageFlags.StorageBuffer;

        private readonly static SwapChainBuffer<CameraData> CameraDataBuffer;


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
            Pipeline._descriptorReWrite = true;
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
            CameraDataBuffer = new(Presenter.MAX_CAMERAS * 2, BufferUsageFlags, true);

            LightingInfoBuffer = new(1, GPUBufferExtensions.GetAlignment((uint)sizeof(LightingInfo), VkBufferUsageFlags.UniformBuffer), VkBufferUsageFlags.UniformBuffer, true);
            DirectionalLightBuffer = new(1, BufferUsageFlags, true);
            PointLightBuffer = new(PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS, BufferUsageFlags, true);
            SpotLightBuffer = new(SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS, BufferUsageFlags, true);



            DirectionalLightMatsBuffer = new(DirectionalLightShadows.MAX_CASCADE_COUNT, BufferUsageFlags, true);
            PointLightMatsBuffer = new(PointLightShadows.MAX_POINT_LIGHT_SHADOW_CASTERS * 6, BufferUsageFlags, true);
            SpotLightMatsBuffer = new(SpotLightShadows.MAX_SPOT_LIGHT_SHADOW_CASTERS, BufferUsageFlags, true);

            CameraDataBuffer.SetDebugName("CameraDataBuffer");

            LightingInfoBuffer.SetDebugName("LightingInfoBuffer");
            DirectionalLightBuffer.SetDebugName("DirectionalLightBuffer");
            PointLightBuffer.SetDebugName("PointLightBuffer");
            SpotLightBuffer.SetDebugName("SpotLightBuffer");

            DirectionalLightMatsBuffer.SetDebugName("DirectionalLightMatsBuffer");
            PointLightMatsBuffer.SetDebugName("PointLightMatsBuffer");
            SpotLightMatsBuffer.SetDebugName("SpotLightMatsBuffer");

            AddEngineBuffer(ShaderProperties.CameraDataId, CameraDataBuffer);

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
            if (cameras == null) return;
            var cameraCount = Math.Min(cameras.Count, Presenter.MAX_CAMERAS);
            int mainCamera = -1;
            Camera camera;
            CameraOrthographic orthCam;
            for (int i = 0; i < cameraCount; i++)
            {
                var entity = cameras[i];
                camera = entityManager.GetComponent<Camera>(entity);
                if (mainCamera == -1 && entityManager.HasComponent<MainCamera>(entity))
                {
                    mainCamera = i;
                }
                if (entityManager.HasComponent<CameraPerspective>(entity))
                {
                    CameraDataBuffer.HostBuffer[i] = new(camera);
                }
                else if (entityManager.HasComponent<CameraOrthographic>(entity, out var signature))
                {
                    orthCam = entityManager.GetComponent<CameraOrthographic>(signature);
                    CameraDataBuffer.HostBuffer[i] = new(camera, orthCam);
                }
            }

            CameraDataBuffer.SetBuffersDirty(true);
            GPUBufferExtensions.WriteFromHostDelayed(CameraDataBuffer, frameIndex);
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
            CameraDataBuffer.Dispose();

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
