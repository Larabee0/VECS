using System;
using Vortice.Vulkan;

namespace VECS
{
    /// <summary>
    /// global information for render systems to use in the rendering of objects.
    /// 
    /// Most importantly the command buffer for recording render commands
    /// 
    /// The frame index is included to allows local object specific buffer access
    /// 
    /// the global descriptor set is needed for render systems to bind it to their pipelines
    /// 
    /// The frame descriptor pool is needed for arbitary data to be sent to the shaders by the 
    /// render system pipelines.
    /// 
    /// </summary>
    public readonly struct RendererFrameInfo
    {
        public readonly int FrameIndex;
        public readonly int CameraCount;
        public readonly int MainCamera;
        public readonly float DeltaTime;

        public readonly VkCommandBuffer CommandBuffer;
        public readonly CullData CullData;
        public readonly LightingInfo LightingInfo;

        public readonly BufferMAXCAMS<CameraInfo> CameraInfo;
        public readonly BufferMAXCAMS<CameraInverseInfo> CameraInverseInfo;
        public readonly BufferMAXCAMS<AdditionalCameraInfo> AdditionalCameraInfo;
        public readonly BufferMAXCAMS<OrthographicInfo> OrthographicInfo;
        public readonly BufferMAXLIGHTS<PointLightUniform> PointLights;
        public readonly BufferMAXLIGHTS<SpotLightUniform> SpotLights;

        public RendererFrameInfo(
            int frameIndex,
            int cameraCount,
            int mainCamera,
            float deltaTime,
            VkCommandBuffer commandBuffer,
            CullData cullData,
            LightingInfo lightingInfo,
            BufferMAXCAMS<CameraInfo> cameraInfo,
            BufferMAXCAMS<CameraInverseInfo> cameraInverseInfo,
            BufferMAXCAMS<AdditionalCameraInfo> additionalCameraInfo,
            BufferMAXCAMS<OrthographicInfo> orthographicInfo,
            BufferMAXLIGHTS<PointLightUniform> pointLights,
            BufferMAXLIGHTS<SpotLightUniform> spotLights)
        {
            FrameIndex = frameIndex;
            CameraCount = cameraCount;
            MainCamera = mainCamera;
            DeltaTime = deltaTime;

            CommandBuffer = commandBuffer;
            CullData = cullData;
            LightingInfo = lightingInfo;

            CameraInfo = cameraInfo;
            CameraInverseInfo = cameraInverseInfo;
            AdditionalCameraInfo = additionalCameraInfo;
            OrthographicInfo = orthographicInfo;
            PointLights = pointLights;
            SpotLights = spotLights;
        }

        public static bool operator ==(RendererFrameInfo left, RendererFrameInfo right)
        {
            return left.FrameIndex == right.FrameIndex && left.DeltaTime == right.DeltaTime;
        }

        public static bool operator !=(RendererFrameInfo left, RendererFrameInfo right) => !(left == right);

        public readonly bool Equals(RendererFrameInfo other)
        {
            return this == other;
        }

        public readonly override bool Equals(object obj)
        {
            return (obj is RendererFrameInfo other) && Equals(other);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(FrameIndex, DeltaTime);
        }
    }
}
