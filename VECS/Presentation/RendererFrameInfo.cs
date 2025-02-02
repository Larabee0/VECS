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
    public struct RendererFrameInfo
    {
        public static readonly RendererFrameInfo Null = new() { FrameIndex = -1, DeltaTime = -1 };

        public int FrameIndex;
        public float DeltaTime;
        public VkCommandBuffer CommandBuffer;
        public GlobalUbo Ubo;
        public GPUBuffer<GlobalUbo.WriteableUBO> UboBuffer;
        public VkDescriptorSet GlobalDescriptorSet;
        public DescriptorPool FrameDescriptorPool;
        public List<VkBufferMemoryBarrier> PostCullBarriers;
        public VkDescriptorImageInfo DepthPyramid;
        public int DepthPyramidWidth;
        public int DepthPyramidHeight;
        public static bool operator ==(RendererFrameInfo left, RendererFrameInfo right)
        {
            return left.FrameIndex == right.FrameIndex && left.DeltaTime == right.DeltaTime;
        }

        public static bool operator !=(RendererFrameInfo left, RendererFrameInfo right) => !(left == right);

        public readonly bool Equals(RendererFrameInfo other)
        {
            return this == other;
        }

        public override readonly bool Equals(object obj)
        {
            return (obj is RendererFrameInfo other) && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(FrameIndex, DeltaTime);
        }
    }
}
