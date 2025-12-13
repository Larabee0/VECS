using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ShadowInternal
    {
        private readonly VkCommandBuffer[][] _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];

        public unsafe ShadowInternal()
        {
            _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
            for (int i = 0; i < _freeBuffers.Length; i++)
            {
                _freeBuffers[i] = new VkCommandBuffer[7];
            }
            DrawBlob.AllInOneMats.Add(EngineMaterials.ShadowOffscreen.Hash);
        }

        public void RenderShadows(RendererFrameInfo frameInfo)
        {
            Material shadowOffscreen = EngineMaterials.ShadowOffscreen;

            Matrix4x4 projection = ShadowImage.CubeProjectionMatrix;
            Matrix4x4 model = Matrix4x4.CreateTranslation(frameInfo.Ubo.PointLights[0].Position.AsVector3());
            shadowOffscreen.SetMatrix4x4("cubeConstant.cubeProj".GetHashCode(),0, projection);
            shadowOffscreen.SetMatrix4x4("cubeConstant.cubeModel".GetHashCode(),0, model);

            shadowOffscreen.PushConstants.EnsureCapacity(6);
            Material.Update(shadowOffscreen, frameInfo);
            Presenter.Instance.ShadowImage.SetImageLayoutWrite(frameInfo.CommandBuffer);
            if (DrawBlob.MULTI_THREAD_RENDERING)
            {
                VkCommandBuffer[] parallelCmdBuffers = _freeBuffers[frameInfo.FrameIndex];

                for (int i = 0; i < parallelCmdBuffers.Length; i++)
                {
                    if (parallelCmdBuffers[i].IsNull)
                    {
                        unsafe
                        {
                            GraphicsDevice.DeviceAPI.vkAllocateCommandBuffer(GraphicsDevice.Device, GraphicsDevice.SecondaryMainPipeCommandBuffers[i], VkCommandBufferLevel.Secondary, out parallelCmdBuffers[i]).CheckResult("Failed to allocate command buffer!");
                        }
                    }
                }

                Application.ParallelFor(6, (i) =>
                {
                    RenderShadow(frameInfo, i, model, parallelCmdBuffers);
                });

                unsafe
                {
                    fixed (VkCommandBuffer* pCmdBuffers = &parallelCmdBuffers[0])
                    {
                        GraphicsDevice.DeviceAPI.vkCmdExecuteCommands(frameInfo.CommandBuffer, 6, pCmdBuffers);
                    }
                }
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    RenderShadow(frameInfo, i, model, null);
                }
            }

            Presenter.Instance.ShadowImage.SetImageLayoutRead(frameInfo.CommandBuffer);
        }

        private static void RenderShadow(RendererFrameInfo frameInfo, int i, Matrix4x4 model, VkCommandBuffer[] parallelCmdBuffers)
        {
            VkCommandBuffer internalBuffer = frameInfo.CommandBuffer;
            if (DrawBlob.MULTI_THREAD_RENDERING)
            {
                unsafe
                {
                    VkCommandBufferInheritanceInfo inheritanceInfo = new() { };
                    VkCommandBufferBeginInfo bufferBeginInfo = new() { pInheritanceInfo = &inheritanceInfo };
                    internalBuffer = parallelCmdBuffers[i];
                    GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(internalBuffer, &bufferBeginInfo);
                }
            }
            
            var viewMatrix = ShadowImage.GetViewMatrixForFace(i) * model;
            var proj = ShadowImage.CubeProjectionMatrix;

            CullData cullDataInternal = new(ShadowImage.SHADOW_CULLING, ShadowImage.SHADOW_DST_CULLING, ShadowImage.SHADOW_DEPTH_CULLING, viewMatrix * proj);


            EngineMaterials.ShadowOffscreen.PushConstants.SetPushConstantMatrix4x4("viewCube", i, viewMatrix);


            DrawBlob.CullAllInOne(frameInfo, internalBuffer, cullDataInternal);
            
            Presenter.Instance.ShadowImage.UpdateCubeFace(i, internalBuffer);

            DrawBlob.ExecuteAllInOneDrawCmds(frameInfo, internalBuffer, EngineMaterials.ShadowOffscreen.Hash, i);

            Presenter.Instance.ShadowImage.EndShadowPass(internalBuffer);

            DrawBlob.IndirectToComputeMemoryBarrierAllInOne(internalBuffer);

            if (DrawBlob.MULTI_THREAD_RENDERING)
            {
                GraphicsDevice.DeviceAPI.vkEndCommandBuffer(internalBuffer);
            }
        }
    }
}
