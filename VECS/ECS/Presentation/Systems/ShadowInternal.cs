using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ShadowInternal : RenderSystemInternal
    {
        private readonly ShadowRenderBlob _shadowRenderBlob;

        private readonly VkCommandBuffer[][] _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];

        public unsafe ShadowInternal()
        {
            _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
            for (int i = 0; i < _freeBuffers.Length; i++)
            {
                _freeBuffers[i] = new VkCommandBuffer[7];
            }
            _shadowRenderBlob = new(MaterialV2.ShadowOffscreen, GenericRenderSystem.MAX_DRAWS);
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
        {
            if (_shadowRenderBlob.DrawCount != entities.Count)
            {
                _shadowRenderBlob.RebuildBlob(entityManager, entities);
            }
            else
            {
                _shadowRenderBlob.UpdateDrawCommands(entityManager);
            }


            RenderShadows(frameInfo, entities.Count);
        }

        private unsafe void RenderShadows(RendererFrameInfo frameInfo, int drawCount)
        {
            MaterialV2 shadowOffscreen = MaterialV2.ShadowOffscreen;

            shadowOffscreen.SetStorageBufferLength(RenderBlob.MatricesBufferId, 0, (uint)drawCount);

            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, ShadowImage.SHADOW_IMAGE_SIZE);
            Matrix4x4 model = Matrix4x4.CreateTranslation(frameInfo.Ubo.PointLights[0].Position.AsVector3());
            shadowOffscreen.SetMatrix4x4("cubeConstant.cubeProj".GetHashCode(),0, projection);
            shadowOffscreen.SetMatrix4x4("cubeConstant.cubeModel".GetHashCode(),0, model);

            Matrix4x4 projectionT = Matrix4x4.Transpose(projection);
            Vector4 frustrumX = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0)).NormalizePlane();
            Vector4 frustrumY = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1)).NormalizePlane();
            Vector4 frustum = new(frustrumX.X, frustrumX.Z, frustrumY.Y, frustrumY.Z);
            CullData cullData = frameInfo.cullData;
            cullData.P00 = projection[0, 0];
            cullData.P11 = projection[1, 1];
            cullData.znear = 0.1f;
            cullData.zfar = ShadowImage.SHADOW_IMAGE_SIZE;
            cullData.frustum = frustum;
            VkCommandBuffer[] parallelCmdBuffers = _freeBuffers[frameInfo.FrameIndex];

            for (int i = 0; i < parallelCmdBuffers.Length; i++)
            {
                if (parallelCmdBuffers[i].IsNull)
                {
                    GraphicsDevice.DeviceAPI.vkAllocateCommandBuffer(GraphicsDevice.Device, GraphicsDevice.SecondaryMainPipeCommandBuffers[i], VkCommandBufferLevel.Secondary, out parallelCmdBuffers[i]).CheckResult("Failed to allocate command buffer!");
                }
            }

            shadowOffscreen.PushConstants.EnsureCapacity(6);
            MaterialV2.Update(shadowOffscreen, frameInfo);
            Presenter.Instance.ShadowImage.SetImageLayoutWrite(frameInfo.CommandBuffer);

            //Application.ParallelFor(6, (i) =>
            //{
            //    RenderShadow(frameInfo, drawCount, i, model, cullData, parallelCmdBuffers);
            //});

            for (int i = 0; i < 6; i++)
            {
                RenderShadow(frameInfo, drawCount, i, model, cullData, parallelCmdBuffers);
            }

            fixed (VkCommandBuffer* pCmdBuffers = &parallelCmdBuffers[0])
            {
                GraphicsDevice.DeviceAPI.vkCmdExecuteCommands(frameInfo.CommandBuffer, 6, pCmdBuffers);
            }
            Presenter.Instance.ShadowImage.SetImageLayoutRead(frameInfo.CommandBuffer);
        }

        private unsafe void RenderShadow(RendererFrameInfo frameInfo, int drawCount, int i, Matrix4x4 model, CullData cullData, VkCommandBuffer[] parallelCmdBuffers)
        {
            VkCommandBufferInheritanceInfo inheritanceInfo = new() { };
            VkCommandBufferBeginInfo bufferBeginInfo = new() { pInheritanceInfo = &inheritanceInfo };
            VkCommandBuffer internalBuffer = parallelCmdBuffers[i];
            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(internalBuffer, &bufferBeginInfo);
            CullData cullDataInternal = cullData;
            var viewMatrix = ShadowImage.GetViewMatrixForFace(i);
            cullDataInternal.viewMatrix = viewMatrix * model;
            VkBufferMemoryBarrier2 memoryBarrier = FustrumCull.Cull(internalBuffer, frameInfo.FrameIndex, cullData, (uint)drawCount, _shadowRenderBlob.IndirectCmdBuffer, _shadowRenderBlob.ModelBoundsBuffer);
            
            if (!FustrumCull.CPUCulling)
            {
                MemoryBarrierHelper.BufferMemoryBarrier(internalBuffer, memoryBarrier, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.DrawIndirect);
            }
            Presenter.Instance.ShadowImage.UpdateCubeFace(i, internalBuffer);
            MaterialV2.ShadowOffscreen.PushConstants.SetPushConstantMatrix4x4("viewCube", i, viewMatrix);

            _shadowRenderBlob.Draw(frameInfo, internalBuffer, i);

            Presenter.Instance.ShadowImage.EndShadowPass(internalBuffer);
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(internalBuffer);
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            _shadowRenderBlob.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
