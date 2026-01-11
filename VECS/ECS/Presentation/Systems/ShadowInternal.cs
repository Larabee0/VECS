using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ShadowInternal
    {
        private readonly VkCommandBuffer[][] _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
        private readonly Matrix4x4[] shadowMats = new Matrix4x4[6];
        private readonly int matsPropertyId = "shadowMats.value".GetShaderPropertyId();
        public unsafe ShadowInternal()
        {
            _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
            for (int i = 0; i < _freeBuffers.Length; i++)
            {
                _freeBuffers[i] = new VkCommandBuffer[7];
            }
            DrawBlob.AllInOneMats.Add(EngineMaterials.ShadowOffscreen.Hash);
        }

        public void RenderShadowsSinglePass(RendererFrameInfo frameInfo)
        {
            var mat = AssetDataBase<Material>.GetNamed("PointLightShadowCaster");
            ShadowImage.FillViewMatrices(frameInfo.PointLights[0].Position.AsVector3(), shadowMats);
            mat.SetMatrix4x4Array(matsPropertyId, 0, shadowMats);
            mat.PushConstants.SetPushConstantVector4("lightPos", frameInfo.PointLights[0].Position);
            mat.PushConstants.SetPushConstantFloat("far_plane", 25f);
            Material.Update(mat, frameInfo);
            Presenter.Instance.ShadowImage.SetImageLayoutWrite(frameInfo.CommandBuffer);
            Matrix4x4 model = Matrix4x4.CreateTranslation(frameInfo.PointLights[0].Position.AsVector3());
            var viewMatrix = ShadowImage.GetViewMatrixForFace(0) * model;
            var projectionMatrix = ShadowImage.CubeProjectionMatrix;

            CullData cullDataInternal = new(
                ShadowImage.SHADOW_INCLUDE_MASK,
                ShadowImage.SHADOW_EXCLUDE_MASK,
                ShadowImage.SHADOW_CULLING,
                ShadowImage.SHADOW_DST_CULLING,
                ShadowImage.SHADOW_DEPTH_CULLING,
                frameInfo.CullData.zNear,
                projectionMatrix,
                viewMatrix
            );
            DrawBlob.CullAllInOne(frameInfo, frameInfo.CommandBuffer, cullDataInternal);

            Presenter.Instance.ShadowImage.SetImageLayoutWrite(frameInfo.CommandBuffer);

            Presenter.Instance.ShadowImage.UpdateCube(frameInfo.CommandBuffer);
            DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, frameInfo.CommandBuffer, mat.Hash, 0);
            Presenter.Instance.ShadowImage.EndShadowPass(frameInfo.CommandBuffer);

            Presenter.Instance.ShadowImage.SetImageLayoutRead(frameInfo.CommandBuffer);
        }

        public void RenderShadows(RendererFrameInfo frameInfo)
        {
            Material shadowOffscreen = EngineMaterials.ShadowOffscreen;

            Matrix4x4 projection = ShadowImage.CubeProjectionMatrix;
            Matrix4x4 model = Matrix4x4.CreateTranslation(frameInfo.PointLights[0].Position.AsVector3());
            shadowOffscreen.SetMatrix4x4("cubeConstant.cubeProj".GetShaderPropertyId(),0, projection);
            shadowOffscreen.SetMatrix4x4("cubeConstant.cubeModel".GetShaderPropertyId(),0, model);

            shadowOffscreen.PushConstants.EnsureCapacity(6);
            Material.Update(shadowOffscreen, frameInfo);
            Presenter.Instance.ShadowImage.SetImageLayoutWrite(frameInfo.CommandBuffer);
#pragma warning disable CS0162
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
#pragma warning restore CS0162
            Presenter.Instance.ShadowImage.SetImageLayoutRead(frameInfo.CommandBuffer);
        }

        private static void RenderShadow(RendererFrameInfo frameInfo, int i, Matrix4x4 model, VkCommandBuffer[] parallelCmdBuffers)
        {
            VkCommandBuffer internalBuffer = frameInfo.CommandBuffer;
            if (DrawBlob.MULTI_THREAD_RENDERING)
            {
#pragma warning disable CS0162
                unsafe
                {
                    VkCommandBufferInheritanceInfo inheritanceInfo = new() { };
                    VkCommandBufferBeginInfo bufferBeginInfo = new() { pInheritanceInfo = &inheritanceInfo };
                    internalBuffer = parallelCmdBuffers[i];
                    GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(internalBuffer, &bufferBeginInfo);
                }
#pragma warning restore CS0162
            }

            var viewMatrix = ShadowImage.GetViewMatrixForFace(i) * model;
            var projectionMatrix = ShadowImage.CubeProjectionMatrix;

            CullData cullDataInternal = new(
                ShadowImage.SHADOW_INCLUDE_MASK,
                ShadowImage.SHADOW_EXCLUDE_MASK,
                ShadowImage.SHADOW_CULLING,
                ShadowImage.SHADOW_DST_CULLING,
                ShadowImage.SHADOW_DEPTH_CULLING,
                frameInfo.CullData.zNear,
                projectionMatrix,
                viewMatrix
            );


            EngineMaterials.ShadowOffscreen.PushConstants.SetPushConstantMatrix4x4("viewCube", i, viewMatrix);

            DrawBlob.CullAllInOne(frameInfo, internalBuffer, cullDataInternal);
            
            Presenter.Instance.ShadowImage.UpdateCubeFace(i, internalBuffer);

            DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, internalBuffer, EngineMaterials.ShadowOffscreen.Hash, i);

            Presenter.Instance.ShadowImage.EndShadowPass(internalBuffer);

            DrawBlob.IndirectToComputeMemoryBarrierAllInOne(internalBuffer);

            if (DrawBlob.MULTI_THREAD_RENDERING)
            {
#pragma warning disable CS0162
                GraphicsDevice.DeviceAPI.vkEndCommandBuffer(internalBuffer);
#pragma warning restore CS0162
            }
        }
    }
}
