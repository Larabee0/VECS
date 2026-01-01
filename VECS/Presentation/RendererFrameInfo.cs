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
        public readonly float DeltaTime;
        public readonly VkCommandBuffer CommandBuffer;
        public readonly CullData CullData;

        public readonly CameraInfo CameraInfo;
        public readonly CameraInverseInfo CameraInverseInfo;
        public readonly AdditionalCameraInfo AdditionalCameraInfo;
        public readonly OrthographicInfo OrthographicInfo;
        public readonly LightingInfo LightingInfo;
        public readonly BufferMAXLIGHTS<PointLightUniform> PointLights;

        public RendererFrameInfo(int frameIndex, float deltaTime, VkCommandBuffer commandBuffer, CullData cullData, CameraInfo cameraInfo, CameraInverseInfo cameraInverseInfo, AdditionalCameraInfo additionalCameraInfo, OrthographicInfo orthographicInfo, LightingInfo lightingInfo, BufferMAXLIGHTS<PointLightUniform> pointLights)
        {
            FrameIndex = frameIndex;
            DeltaTime = deltaTime;
            CommandBuffer = commandBuffer;
            CullData = cullData;
            CameraInfo = cameraInfo;
            CameraInverseInfo = cameraInverseInfo;
            AdditionalCameraInfo = additionalCameraInfo;
            OrthographicInfo = orthographicInfo;
            LightingInfo = lightingInfo;
            PointLights = pointLights;
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
