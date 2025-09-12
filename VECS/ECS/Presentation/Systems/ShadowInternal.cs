using SDL3;
using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ShadowInternal : RenderSystemInternal
    {
        private readonly Material _shadowOffscreen;
        private readonly ShadowRenderBlob _shadowRenderBlob;

        private readonly VkCommandBuffer[][] _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];

        public unsafe ShadowInternal(FustrumCull cull) : base(cull)
        {
            var shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            Cubemap shadowCube = AssetDataBase<Cubemap>.GetNamed("ShadowCubeMap");
            Texture2D shadowDepthStencil = AssetDataBase<Texture2D>.GetNamed("ShadowDepthImage");

            shadowConfig.colourFormats = [shadowCube.Format];
            shadowConfig.depthFormat = shadowDepthStencil.Format;
            shadowConfig.stencilFormat = shadowDepthStencil.Format;
            shadowConfig.depthStencilInfo.depthWriteEnable = true;

            _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
            for (int i = 0; i < _freeBuffers.Length; i++)
            {
                _freeBuffers[i] = new VkCommandBuffer[7];
            }
            _shadowOffscreen = Material.Create("ShadowOffscreen", "shadow_offscreen.vert", "shadow_offscreen.frag", shadowConfig);
            _shadowRenderBlob = new(_shadowOffscreen, GenericRenderSystem.MAX_DRAWS);
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
        {
            if (_cullCompute.Shader.LastFrameIndex != frameInfo.FrameIndex)
            {
                _cullCompute.Shader.NextFrame(frameInfo.FrameIndex);
            }
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
            _shadowOffscreen.SetMatDescriptorHandleStorageRegions(0, 0, (uint)drawCount);

            _cullCompute.Shader.SetStorageBuffer("boundsBuffer", _shadowRenderBlob.ModelBoundsBuffer);
            _cullCompute.Shader.SetStorageBuffer("drawBuffer", _shadowRenderBlob.IndirectCmdBuffer);
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, ShadowImage.SHADOW_IMAGE_SIZE);
            Matrix4x4 model = Matrix4x4.CreateTranslation(frameInfo.Ubo.PointLights[0].Position.AsVector3());
            _shadowOffscreen.SetMatrix4x4("cubeConstant.cubeProj", projection);
            _shadowOffscreen.SetMatrix4x4("cubeConstant.cubeModel", model);

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
                    Vulkan.CheckResult(Vulkan.vkAllocateCommandBuffer(GraphicsDevice.Device, GraphicsDevice.SecondaryMainPipeCommandBuffers[i], VkCommandBufferLevel.Secondary, out parallelCmdBuffers[i]), "Failed to allocate command buffer!");
                }
            }

            _shadowOffscreen.PushConstants.EnsureCapacity(6);
            _shadowOffscreen.Update(frameInfo);
            Presenter.Instance.ShadowImage.SetImageLayoutWrite(frameInfo.CommandBuffer);

            Application.ParallelFor(6, (i) =>
            {
                
                    RenderShadow(frameInfo, drawCount, i, model, cullData, parallelCmdBuffers);
            });

            _cullCompute.Shader.Increment(6);
            fixed (VkCommandBuffer* pCmdBuffers = &parallelCmdBuffers[0])
            {
                Vulkan.vkCmdExecuteCommands(frameInfo.CommandBuffer, 6, pCmdBuffers);
            }
            Presenter.Instance.ShadowImage.SetImageLayoutRead(frameInfo.CommandBuffer);
        }

        private unsafe void RenderShadow(RendererFrameInfo frameInfo, int drawCount, int i, Matrix4x4 model, CullData cullData, VkCommandBuffer[] parallelCmdBuffers)
        {
            VkCommandBufferInheritanceInfo inheritanceInfo = new() { };
            VkCommandBufferBeginInfo bufferBeginInfo = new() { pInheritanceInfo = &inheritanceInfo };
            VkCommandBuffer internalBuffer = parallelCmdBuffers[i];
            Vulkan.vkBeginCommandBuffer(internalBuffer, &bufferBeginInfo);
            CullData cullDataInternal = cullData;
            var viewMatrix = ShadowImage.GetViewMatrixForFace(i);
            cullDataInternal.viewMatrix = viewMatrix * model;
            VkBufferMemoryBarrier memoryBarrier = _cullCompute.Cull(internalBuffer, frameInfo.FrameIndex, cullData, (uint)drawCount, _shadowRenderBlob.IndirectCmdBuffer, _shadowRenderBlob.ModelBoundsBuffer, i + 1);
            
            if (!_cullCompute.CPUCulling)
            {
                Vulkan.vkCmdPipelineBarrier(internalBuffer,
                        VkPipelineStageFlags.ComputeShader,
                        VkPipelineStageFlags.DrawIndirect,
                        0, 0, null, 1, &memoryBarrier, 0, null);
            }
            Presenter.Instance.ShadowImage.UpdateCubeFace(i, internalBuffer);
            _shadowOffscreen.SetPushConstantMatrix4x4("viewCube", i, viewMatrix);

            _shadowRenderBlob.Draw(internalBuffer, frameInfo.FrameIndex, i);

            Presenter.Instance.ShadowImage.EndShadowPass(internalBuffer);
            Vulkan.vkEndCommandBuffer(internalBuffer);
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            _shadowRenderBlob.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
