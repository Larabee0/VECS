using System;
using System.Numerics;
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
        public readonly int TargetCamera;
        public readonly float DeltaTime;

        public readonly VkCommandBuffer CommandBuffer;
        public readonly CullData CullData;
        public readonly LightingInfo LightingInfo;

        public readonly VkRect2D OutputRect;
        public readonly Vector2UInt DepthPyramidSize;

        public RendererFrameInfo(
            int mainCamera,
            float deltaTime,
            VkCommandBuffer commandBuffer,
            CullData cullData,
            LightingInfo lightingInfo)
        {
            TargetCamera = mainCamera;
            DeltaTime = deltaTime;

            CommandBuffer = commandBuffer;
            CullData = cullData;
            LightingInfo = lightingInfo;

        }

        public RendererFrameInfo(RendererFrameInfo frameInfo, VkRect2D outputRect)
        {
            TargetCamera = frameInfo.TargetCamera;
            DeltaTime = frameInfo.DeltaTime;

            CommandBuffer = frameInfo.CommandBuffer;
            CullData = frameInfo.CullData;
            LightingInfo = frameInfo.LightingInfo;
            OutputRect = outputRect;
            DepthPyramidSize = new(DepthReduction.PreviousPow2(outputRect.extent.width), DepthReduction.PreviousPow2(outputRect.extent.height));
            var tex = EngineTextures.TryGetTexture(ShaderProperties.DepthPyramidId).First;
            Vector2 scale = (Vector2)DepthPyramidSize / new Vector2(tex.Width, tex.Height);
            CullData.Pyramid = new Vector4(DepthPyramidSize.X, DepthPyramidSize.Y, scale.X, scale.Y);


        }

        public RendererFrameInfo(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer)
        {
            TargetCamera = frameInfo.TargetCamera;
            DeltaTime = frameInfo.DeltaTime;

            CommandBuffer = commandBuffer;
            CullData = frameInfo.CullData;
            LightingInfo = frameInfo.LightingInfo;
            OutputRect = frameInfo.OutputRect;
            DepthPyramidSize = frameInfo.DepthPyramidSize;
        }

        public static bool operator ==(RendererFrameInfo left, RendererFrameInfo right)
        {
            return left.DeltaTime == right.DeltaTime;
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
            return HashCode.Combine(DeltaTime);
        }
    }
}
