using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class MemoryBarrierHelper
    {
        /// <summary>
        /// https://vulkan.lunarg.com/doc/view/1.4.328.1/windows/antora/spec/latest/chapters/synchronization.html#synchronization-access-types-supported
        /// </summary>
        /// <param name="access"></param>
        /// <param name="stage"></param>
        /// <returns></returns>
        public static bool ValidateStages(VkAccessFlags2 access, VkPipelineStageFlags2 stage)
        {
            switch (access)
            {
                case VkAccessFlags2.IndirectCommandRead:
                    return stage == VkPipelineStageFlags2.DrawIndirect || stage == VkPipelineStageFlags2.AccelerationStructureBuildKHR;
                case VkAccessFlags2.IndexRead:
                    return stage == VkPipelineStageFlags2.VertexInput || stage == VkPipelineStageFlags2.IndexInput;
                case VkAccessFlags2.VertexAttributeRead:
                    return stage == VkPipelineStageFlags2.VertexInput || stage == VkPipelineStageFlags2.VertexAttributeInput;
                case VkAccessFlags2.UniformRead:
                    return stage == VkPipelineStageFlags2.VertexShader
                        || stage == VkPipelineStageFlags2.TessellationControlShader
                        || stage == VkPipelineStageFlags2.TessellationEvaluationShader
                        || stage == VkPipelineStageFlags2.GeometryShader
                        || stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.ComputeShader
                        || stage == VkPipelineStageFlags2.RayTracingShaderKHR
                        || stage == VkPipelineStageFlags2.TaskShaderEXT
                        || stage == VkPipelineStageFlags2.MeshShaderEXT
                        || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI
                        || stage == VkPipelineStageFlags2.ClusterCullingShaderHUAWEI;
                case VkAccessFlags2.InputAttachmentRead:
                    return stage == VkPipelineStageFlags2.FragmentShader || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI;
                case VkAccessFlags2.ShaderRead:
                    return stage == VkPipelineStageFlags2.AccelerationStructureBuildKHR
                        || stage == VkPipelineStageFlags2.MicromapBuildEXT
                        || stage == VkPipelineStageFlags2.VertexShader
                        || stage == VkPipelineStageFlags2.TessellationControlShader
                        || stage == VkPipelineStageFlags2.TessellationEvaluationShader
                        || stage == VkPipelineStageFlags2.GeometryShader
                        || stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.ComputeShader
                        || stage == VkPipelineStageFlags2.RayTracingShaderKHR
                        || stage == VkPipelineStageFlags2.TaskShaderEXT
                        || stage == VkPipelineStageFlags2.MeshShaderEXT
                        || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI
                        || stage == VkPipelineStageFlags2.ClusterCullingShaderHUAWEI;
                case VkAccessFlags2.ShaderWrite:
                    return stage == VkPipelineStageFlags2.VertexShader
                        || stage == VkPipelineStageFlags2.TessellationControlShader
                        || stage == VkPipelineStageFlags2.TessellationEvaluationShader
                        || stage == VkPipelineStageFlags2.GeometryShader
                        || stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.ComputeShader
                        || stage == VkPipelineStageFlags2.RayTracingShaderKHR
                        || stage == VkPipelineStageFlags2.TaskShaderEXT
                        || stage == VkPipelineStageFlags2.MeshShaderEXT
                        || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI
                        || stage == VkPipelineStageFlags2.ClusterCullingShaderHUAWEI;
                case VkAccessFlags2.ColorAttachmentRead:
                    return stage == VkPipelineStageFlags2.FragmentShader || stage == VkPipelineStageFlags2.ColorAttachmentOutput;
                case VkAccessFlags2.ColorAttachmentWrite:
                    return stage == VkPipelineStageFlags2.ColorAttachmentOutput;
                case VkAccessFlags2.DepthStencilAttachmentRead:
                    return stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.EarlyFragmentTests
                        || stage == VkPipelineStageFlags2.LateFragmentTests;
                case VkAccessFlags2.DepthStencilAttachmentWrite:
                    return stage == VkPipelineStageFlags2.EarlyFragmentTests
                        || stage == VkPipelineStageFlags2.LateFragmentTests;
                case VkAccessFlags2.TransferRead:
                    return stage == VkPipelineStageFlags2.AllTransfer
                        || stage == VkPipelineStageFlags2.Copy
                        || stage == VkPipelineStageFlags2.Resolve
                        || stage == VkPipelineStageFlags2.Blit
                        || stage == VkPipelineStageFlags2.AccelerationStructureBuildKHR
                        || stage == VkPipelineStageFlags2.AccelerationStructureCopyKHR
                        || stage == VkPipelineStageFlags2.MicromapBuildEXT
                        || stage == VkPipelineStageFlags2.ConvertCooperativeVectorMatrixNV;
                case VkAccessFlags2.TransferWrite:
                    return stage == VkPipelineStageFlags2.AllTransfer
                        || stage == VkPipelineStageFlags2.Copy
                        || stage == VkPipelineStageFlags2.Resolve
                        || stage == VkPipelineStageFlags2.Blit
                        || stage == VkPipelineStageFlags2.Clear
                        || stage == VkPipelineStageFlags2.AccelerationStructureBuildKHR
                        || stage == VkPipelineStageFlags2.AccelerationStructureCopyKHR
                        || stage == VkPipelineStageFlags2.MicromapBuildEXT
                        || stage == VkPipelineStageFlags2.ConvertCooperativeVectorMatrixNV;
                case VkAccessFlags2.HostRead:
                    return stage == VkPipelineStageFlags2.Host;
                case VkAccessFlags2.HostWrite:
                    return stage == VkPipelineStageFlags2.Host;
                case VkAccessFlags2.ShaderSampledRead:
                    return stage == VkPipelineStageFlags2.VertexShader
                        || stage == VkPipelineStageFlags2.TessellationControlShader
                        || stage == VkPipelineStageFlags2.TessellationEvaluationShader
                        || stage == VkPipelineStageFlags2.GeometryShader
                        || stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.ComputeShader
                        || stage == VkPipelineStageFlags2.RayTracingShaderKHR
                        || stage == VkPipelineStageFlags2.TaskShaderEXT
                        || stage == VkPipelineStageFlags2.MeshShaderEXT
                        || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI
                        || stage == VkPipelineStageFlags2.ClusterCullingShaderHUAWEI;
                case VkAccessFlags2.ShaderStorageRead:
                    return stage == VkPipelineStageFlags2.VertexShader
                        || stage == VkPipelineStageFlags2.TessellationControlShader
                        || stage == VkPipelineStageFlags2.TessellationEvaluationShader
                        || stage == VkPipelineStageFlags2.GeometryShader
                        || stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.ComputeShader
                        || stage == VkPipelineStageFlags2.RayTracingShaderKHR
                        || stage == VkPipelineStageFlags2.TaskShaderEXT
                        || stage == VkPipelineStageFlags2.MeshShaderEXT
                        || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI
                        || stage == VkPipelineStageFlags2.ClusterCullingShaderHUAWEI;
                case VkAccessFlags2.ShaderStorageWrite:
                    return stage == VkPipelineStageFlags2.VertexShader
                        || stage == VkPipelineStageFlags2.TessellationControlShader
                        || stage == VkPipelineStageFlags2.TessellationEvaluationShader
                        || stage == VkPipelineStageFlags2.GeometryShader
                        || stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.ComputeShader
                        || stage == VkPipelineStageFlags2.RayTracingShaderKHR
                        || stage == VkPipelineStageFlags2.TaskShaderEXT
                        || stage == VkPipelineStageFlags2.MeshShaderEXT
                        || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI
                        || stage == VkPipelineStageFlags2.ClusterCullingShaderHUAWEI;
                case VkAccessFlags2.VideoDecodeReadKHR:
                    return stage == VkPipelineStageFlags2.VideoDecodeKHR;
                case VkAccessFlags2.VideoDecodeWriteKHR:
                    return stage == VkPipelineStageFlags2.VideoDecodeKHR;
                case VkAccessFlags2.VideoEncodeReadKHR:
                    return stage == VkPipelineStageFlags2.VideoEncodeKHR;
                case VkAccessFlags2.VideoEncodeWriteKHR:
                    return stage == VkPipelineStageFlags2.VideoEncodeKHR;
                case VkAccessFlags2.ShaderTileAttachmentReadQCOM:
                    return stage == VkPipelineStageFlags2.FragmentShader || stage == VkPipelineStageFlags2.ComputeShader;
                case VkAccessFlags2.ShaderTileAttachmentWriteQCOM:
                    return stage == VkPipelineStageFlags2.FragmentShader || stage == VkPipelineStageFlags2.ComputeShader;
                case VkAccessFlags2.TransformFeedbackWriteEXT:
                    return stage == VkPipelineStageFlags2.TransformFeedbackEXT;
                case VkAccessFlags2.TransformFeedbackCounterReadEXT:
                    return stage == VkPipelineStageFlags2.DrawIndirect || stage == VkPipelineStageFlags2.TransformFeedbackEXT;
                case VkAccessFlags2.TransformFeedbackCounterWriteEXT:
                    return stage == VkPipelineStageFlags2.TransformFeedbackEXT;
                case VkAccessFlags2.ConditionalRenderingReadEXT:
                    return stage == VkPipelineStageFlags2.ConditionalRenderingEXT;
                case VkAccessFlags2.CommandPreprocessReadNV:
                    return stage == VkPipelineStageFlags2.CommandPreprocessNV || stage == VkPipelineStageFlags2.CommandPreprocessEXT;
                case VkAccessFlags2.CommandPreprocessWriteNV:
                    return stage == VkPipelineStageFlags2.CommandPreprocessNV || stage == VkPipelineStageFlags2.CommandPreprocessEXT;
                case VkAccessFlags2.FragmentShadingRateAttachmentReadKHR:
                    return stage == VkPipelineStageFlags2.FragmentShadingRateAttachmentKHR;
                case VkAccessFlags2.AccelerationStructureReadKHR:
                    return stage == VkPipelineStageFlags2.VertexShader
                        || stage == VkPipelineStageFlags2.TessellationControlShader
                        || stage == VkPipelineStageFlags2.TessellationEvaluationShader
                        || stage == VkPipelineStageFlags2.GeometryShader
                        || stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.ComputeShader
                        || stage == VkPipelineStageFlags2.RayTracingShaderKHR
                        || stage == VkPipelineStageFlags2.TaskShaderEXT
                        || stage == VkPipelineStageFlags2.MeshShaderEXT
                        || stage == VkPipelineStageFlags2.ClusterCullingShaderHUAWEI
                        || stage == VkPipelineStageFlags2.AccelerationStructureBuildKHR
                        || stage == VkPipelineStageFlags2.AccelerationStructureCopyKHR
                        || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI;
                case VkAccessFlags2.AccelerationStructureWriteKHR:
                    return stage == VkPipelineStageFlags2.AccelerationStructureBuildKHR || stage == VkPipelineStageFlags2.AccelerationStructureCopyKHR;
                case VkAccessFlags2.FragmentDensityMapReadEXT:
                    return stage == VkPipelineStageFlags2.FragmentDensityProcessEXT;
                case VkAccessFlags2.ColorAttachmentReadNoncoherentEXT:
                    return stage == VkPipelineStageFlags2.ColorAttachmentOutput;
                case VkAccessFlags2.DescriptorBufferReadEXT:
                    return stage == VkPipelineStageFlags2.VertexShader
                        || stage == VkPipelineStageFlags2.TessellationControlShader
                        || stage == VkPipelineStageFlags2.TessellationEvaluationShader
                        || stage == VkPipelineStageFlags2.GeometryShader
                        || stage == VkPipelineStageFlags2.FragmentShader
                        || stage == VkPipelineStageFlags2.ComputeShader
                        || stage == VkPipelineStageFlags2.RayTracingShaderKHR
                        || stage == VkPipelineStageFlags2.TaskShaderEXT
                        || stage == VkPipelineStageFlags2.MeshShaderEXT
                        || stage == VkPipelineStageFlags2.SubpassShaderHUAWEI
                        || stage == VkPipelineStageFlags2.ClusterCullingShaderHUAWEI;
                case VkAccessFlags2.InvocationMaskReadHUAWEI:
                    return stage == VkPipelineStageFlags2.InvocationMaskHUAWEI;
                case VkAccessFlags2.MicromapReadEXT:
                    return stage == VkPipelineStageFlags2.MicromapBuildEXT || stage == VkPipelineStageFlags2.AccelerationStructureBuildKHR;
                case VkAccessFlags2.MicromapWriteEXT:
                    return stage == VkPipelineStageFlags2.MicromapBuildEXT;
                case VkAccessFlags2.OpticalFlowReadNV:
                    return stage == VkPipelineStageFlags2.OpticalFlowNV;
                case VkAccessFlags2.OpticalFlowWriteNV:
                    return stage == VkPipelineStageFlags2.OpticalFlowNV;
                case VkAccessFlags2.DataGraphReadARM:
                    return stage == VkPipelineStageFlags2.DataGraphARM;
                case VkAccessFlags2.DataGraphWriteARM:
                    return stage == VkPipelineStageFlags2.DataGraphARM;
                case VkAccessFlags2.None:
                    return true;
                case VkAccessFlags2.MemoryRead:
                    return true;
                case VkAccessFlags2.MemoryWrite:
                    return true;
                case VkAccessFlags2.ShaderBindingTableReadKHR:
                    return stage.HasFlag(VkPipelineStageFlags2.AllCommands) || stage.HasFlag(VkPipelineStageFlags2.RayTracingShaderKHR);
                default:
#if DEBUG
                    Console.WriteLine("Unsupported access/stage combination: {0} {1}",access.ToString(),stage.ToString());
#endif
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetImageLayout(VkCommandBuffer cmdbuffer,
            VkImage image,
            VkImageAspectFlags aspectMask,
            VkImageLayout oldImageLayout,
            VkImageLayout newImageLayout,
            VkPipelineStageFlags2 srcStageMask,
            VkPipelineStageFlags2 dstStageMask)
        {
            VkImageSubresourceRange subresourceRange = new(aspectMask, 0, 1, 0, 1);
            SetImageLayout(cmdbuffer, image, oldImageLayout, newImageLayout, subresourceRange, srcStageMask, dstStageMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetImageLayout(
            VkCommandBuffer cmdBuffer,
            VkImage image,
            VkImageLayout oldLayout,
            VkImageLayout newLayout,
            VkImageSubresourceRange subresourceRange,
            VkPipelineStageFlags2 srcStage,
            VkPipelineStageFlags2 dstStage)
        {
            VkAccessFlags2 dstAccessMask;
            var srcAccessMask = oldLayout switch
            {
                VkImageLayout.Undefined => VkAccessFlags2.None,
                VkImageLayout.Preinitialized => VkAccessFlags2.HostWrite,
                VkImageLayout.ColorAttachmentOptimal => VkAccessFlags2.ColorAttachmentWrite,
                VkImageLayout.DepthStencilAttachmentOptimal => VkAccessFlags2.DepthStencilAttachmentWrite,
                VkImageLayout.DepthAttachmentStencilReadOnlyOptimal => VkAccessFlags2.DepthStencilAttachmentRead,
                VkImageLayout.TransferSrcOptimal => VkAccessFlags2.TransferRead,
                VkImageLayout.TransferDstOptimal => VkAccessFlags2.TransferWrite,
                VkImageLayout.ShaderReadOnlyOptimal => VkAccessFlags2.ShaderRead,
                VkImageLayout.PresentSrcKHR => VkAccessFlags2.TransferRead,
                _ => throw new InvalidOperationException(string.Format("Unhandled Image transition from image layout {0}", oldLayout.ToString())),// Other source layouts aren't handled (yet)
            };
            switch (newLayout)
            {
                case VkImageLayout.TransferDstOptimal:
                    dstAccessMask = VkAccessFlags2.TransferWrite;
                    break;
                case VkImageLayout.TransferSrcOptimal:
                    dstAccessMask = VkAccessFlags2.TransferRead;
                    break;
                case VkImageLayout.ColorAttachmentOptimal:
                    dstAccessMask = VkAccessFlags2.ColorAttachmentWrite;
                    break;
                case VkImageLayout.DepthAttachmentOptimal:
                    dstAccessMask = VkAccessFlags2.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.DepthStencilAttachmentOptimal:
                    dstAccessMask = VkAccessFlags2.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.DepthAttachmentStencilReadOnlyOptimal:
                    dstAccessMask = VkAccessFlags2.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.General:
                    dstAccessMask = VkAccessFlags2.DepthStencilAttachmentRead | VkAccessFlags2.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.ShaderReadOnlyOptimal:
                    if(srcAccessMask == VkAccessFlags2.None)
                    {
                        srcAccessMask = VkAccessFlags2.HostRead | VkAccessFlags2.HostWrite;
                        srcStage = VkPipelineStageFlags2.Host;
                    }
                    dstAccessMask = VkAccessFlags2.ShaderRead;
                    break;
                case VkImageLayout.PresentSrcKHR:
                    dstAccessMask = VkAccessFlags2.None;
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unhandled Image transition to image layout {0}", newLayout.ToString()));
            }

            uint queueFamily = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily;
            ImageMemoryBarrier(cmdBuffer, image,
                subresourceRange,
                srcStage, srcAccessMask,
                dstStage, dstAccessMask,
                oldLayout, newLayout,
                queueFamily, queueFamily);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ImageMemoryBarrier(
            VkCommandBuffer cmdBuffer,
            VkImage image,
            VkImageSubresourceRange subresourceRange,
            VkPipelineStageFlags2 srcStage, VkAccessFlags2 srcAccess,
            VkPipelineStageFlags2 dstStage, VkAccessFlags2 dstAccess,
            VkImageLayout oldLayout, VkImageLayout newLayout,
            uint srcQueue,
            uint dstQueue
            )
        {
            VkImageMemoryBarrier2 imageMemoryBarrier2 = new(
                image,
                subresourceRange,
                srcStage, srcAccess,
                dstStage, dstAccess,
                oldLayout, newLayout,
                srcQueue, dstQueue
            );

            VkDependencyInfo info = new()
            {
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = &imageMemoryBarrier2
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(cmdBuffer, &info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BufferMemoryBarrier(VkCommandBuffer cmdBuffer,uint barrierCount,VkBufferMemoryBarrier2* barriers)
        {
            VkDependencyInfo info = new()
            {
                bufferMemoryBarrierCount = barrierCount,
                pBufferMemoryBarriers = barriers
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(cmdBuffer, &info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BufferMemoryBarrier(VkCommandBuffer cmdBuffer, VkBufferMemoryBarrier2 barrier, VkPipelineStageFlags2 srcStage, VkPipelineStageFlags2 dstStage)
        {
            barrier.srcStageMask = srcStage;
            barrier.dstStageMask = dstStage;
            BufferMemoryBarrier(cmdBuffer, barrier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BufferMemoryBarrier(VkCommandBuffer cmdBuffer, VkBufferMemoryBarrier2 barrier)
        {
            VkDependencyInfo info = new()
            {
                bufferMemoryBarrierCount = 1,
                pBufferMemoryBarriers = &barrier
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(cmdBuffer, &info);
        }
    }
}
