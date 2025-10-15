using System;
using System.Collections.Generic;
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
    public class RendererFrameInfo
    {
        public int FrameIndex;
        public float DeltaTime;
        public VkCommandBuffer CommandBuffer;
        public VkDescriptorBufferInfo UboBufferInfo;
        public GlobalUbo Ubo;
        public VkDescriptorSet GlobalDescriptorSet;
        public DescriptorPool ApplicationDescriptorPool;
        public DescriptorPool MaterialDescriptorPool;
        public DescriptorPool EntityDescriptorPool;
        public List<VkBufferMemoryBarrier> PostCullBarriers;
        public CullData cullData;

        public CameraInfo CameraInfo;
        public CameraInverseInfo CameraInverseInfo;
        public AdditionalCameraInfo AdditionalCameraInfo;
        public OrthographicInfo OrthographicInfo;
        public LightingInfo LightingInfo;
        public PointLightUniform[] PointLights;

        public DescriptorPool GetDescriptorPool(DescriptorLevel descriptorLevel)
        {
            return descriptorLevel switch
            {
                DescriptorLevel.Game => ApplicationDescriptorPool,
                DescriptorLevel.Material => MaterialDescriptorPool,
                DescriptorLevel.Entity => EntityDescriptorPool,
                _ => null,
            };
        }

        public static bool operator ==(RendererFrameInfo left, RendererFrameInfo right)
        {
            return left.FrameIndex == right.FrameIndex && left.DeltaTime == right.DeltaTime;
        }

        public static bool operator !=(RendererFrameInfo left, RendererFrameInfo right) => !(left == right);

        public bool Equals(RendererFrameInfo other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return (obj is RendererFrameInfo other) && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FrameIndex, DeltaTime);
        }
    }
}
