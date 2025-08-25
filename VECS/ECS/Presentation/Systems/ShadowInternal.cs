using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using VECS.ECS.Transforms;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ShadowInternal : RenderSystemInternal
    {
        private readonly Material _shadowOffscreen;

        private Dictionary<int, List<EarlyDrawCommand>> _preSortedDrawCmds = [];

        private VkCommandBuffer[][] _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];

        public unsafe ShadowInternal(FustrumCull cull) : base(cull)
        {
            GraphicsPipelineConfigInfo shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            Cubemap shadowCube = AssetDataBase<Cubemap>.GetNamed("ShadowCubeMap");
            Texture2D shadowDepthStencil = AssetDataBase<Texture2D>.GetNamed("ShadowFBAttachment");
            var colour = shadowCube.Format;

            shadowConfig.renderPass = VkRenderPass.Null;
            shadowConfig.colourBlendAttachment = new()
            {
                colorWriteMask = VkColorComponentFlags.All,
                blendEnable = false,
            };
            shadowConfig.colourBlendInfo = new(shadowConfig.colourBlendAttachment);
            shadowConfig.dynamicStateEnables = [VkDynamicState.Viewport, VkDynamicState.Scissor];
            shadowConfig.pipelineRenderingCreateInfo = new()
            {
                colorAttachmentCount = 1,
                pColorAttachmentFormats = &colour,
                depthAttachmentFormat = shadowDepthStencil.Format,
                stencilAttachmentFormat = shadowDepthStencil.Format
            };
            shadowConfig.dynamicRendering = true;
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;
            _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
            for (int i = 0; i < _freeBuffers.Length; i++)
            {
                _freeBuffers[i] = new VkCommandBuffer[6];
            }
            _shadowOffscreen = Material.Create("ShadowOffscreen", "shadow_offscreen.vert", "shadow_offscreen.frag", shadowConfig);
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
        {
            int drawCount = GenDrawInternal(entityManager, entities);
            RenderShadows(frameInfo, drawCount);
        }

        private int GenDrawInternal(EntityManager entityManager, List<Entity> entities)
        {
            ResetEarlyDrawCommands(entities.Count);

            int drawCount = _earlyDrawCommands.Length;
            int materialIndex = Material.GetIndexOfMaterial(_shadowOffscreen);

            foreach (var key in _preSortedDrawCmds.Keys)
            {
                _preSortedDrawCmds[key].Clear();
            }
            for (int i = 0; i < drawCount; i++)
            {
                Entity entity = entities[i];
                var localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                var renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                var worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);

                DrawCommand drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds);
                _earlyDrawCommands[i] = new(entity, drawCommand, renderMesh);
                if (!_directMeshCmdRegions.TryAdd(renderMesh.Mesh.DirectMesh, new(1)))
                {
                    _directMeshCmdRegions[renderMesh.Mesh.DirectMesh] = new(_directMeshCmdRegions[renderMesh.Mesh.DirectMesh].Count + 1);
                }


                if (_preSortedDrawCmds.TryGetValue(renderMesh.Mesh.DirectMesh, out var cmds))
                {
                    cmds.Add(_earlyDrawCommands[i]);
                }
                else
                {
                    _preSortedDrawCmds[renderMesh.Mesh.DirectMesh] = [_earlyDrawCommands[i]];
                }
            }
            int iterator = 0;
            foreach (var key in _preSortedDrawCmds.Keys)
            {
                _preSortedDrawCmds[key].ForEach(cmd =>
                {
                    _earlyDrawCommands[iterator] = cmd;
                    iterator++;
                });
            }

            //Array.Sort(_earlyDrawCommands, (x, y) => { return x.DirectMesh.CompareTo(y.DirectMesh); });

            Span<ModelMatrices> matrices = _shadowOffscreen.GetStorageBuffer<ModelMatrices>("matricesBuffer");
            Span<ModelBounds> bounds = _modelBoundsBuffer.HostBuffer;
            Span<VkDrawIndexedIndirectCommand> shadowDraws = _indirectCmdBuffer.HostBuffer;

            int lastCount = 0;
            foreach (var key in _directMeshCmdRegions.Keys)
            {
                var region = _directMeshCmdRegions[key];
                region.StartIndex = lastCount;
                lastCount += region.Count;
                if (region.Count > 0)
                {
                    _shadowOffscreen.EnqueueDrawCmd(new(
                        materialIndex, 0,
                        new(drawCount), 0,
                        key,
                        region,
                        false));
                }
            }

            for (int i = 0; i < drawCount; i++)
            {
                var drawCommand = _earlyDrawCommands[i].DrawCommand;
                drawCommand.VkDraw.instanceCount = 1;
                drawCommand.VkDraw.firstInstance = (uint)i;
                matrices[i] = drawCommand.Matrices;
                bounds[i] = drawCommand.Bounds;
                shadowDraws[i] = drawCommand.VkDraw;
            }

            return drawCount;
        }

        private unsafe void RenderShadows(RendererFrameInfo frameInfo, int drawCount)
        {
            _shadowOffscreen.SetMatDescriptorHandleStorageRegions(0, 0, (uint)drawCount);

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
            
            for (int i = 0; i < 6; i++)
            {
                if (parallelCmdBuffers[i].IsNull)
                {
                    Vulkan.CheckResult(Vulkan.vkAllocateCommandBuffer(GraphicsDevice.Device, GraphicsDevice.SecondaryMainPipeCommandBuffers[i], VkCommandBufferLevel.Secondary, out parallelCmdBuffers[i]), "Failed to allocate command buffer!");
                }
            }

            if (_cullCompute.Shader.LastFrameIndex != frameInfo.FrameIndex)
            {
                _cullCompute.Shader.NextFrame(frameInfo.FrameIndex);
            }
            _cullCompute.Shader.SetStorageBuffer("boundsBuffer", _modelBoundsBuffer);
            _cullCompute.Shader.SetStorageBuffer("drawBuffer", _indirectCmdBuffer);
            _shadowOffscreen.PushConstants.EnsureCapacity(6);
            _shadowOffscreen.Update(frameInfo);
            Presenter.Instance.ShadowImage.SetImageLayoutWrite(frameInfo.CommandBuffer);
            //Stopwatch stopwatch = new();
            //stopwatch.Start();
            //for (int i = 0; i < 6; i++)
            //{
            //    RenderShadow(frameInfo, drawCount, i, model, cullData, parallelCmdBuffers);
            //}

            //Parallel.For(0, 6, (i) =>
            //{
            //    RenderShadow(frameInfo, drawCount, i, model, cullData, parallelCmdBuffers);
            //});

            Application.ParallelFor(6, (i) =>
            {
                RenderShadow(frameInfo, drawCount, i, model, cullData, parallelCmdBuffers);
            });
            

            //stopwatch.Stop();
            //Console.WriteLine("Shadow cube face recording time: {0} ticks", stopwatch.ElapsedTicks);

            _cullCompute.Shader.Increment(6);
            fixed (VkCommandBuffer* pCmdBuffers = &parallelCmdBuffers[0])
            {
                Vulkan.vkCmdExecuteCommands(frameInfo.CommandBuffer, 6, pCmdBuffers);
            }
            Presenter.Instance.ShadowImage.SetImageLayoutRead(frameInfo.CommandBuffer);

            _shadowOffscreen._drawCommands.Clear();
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
            VkBufferMemoryBarrier memoryBarrier = _cullCompute.Cull(internalBuffer, frameInfo.FrameIndex, cullData, (uint)drawCount, _indirectCmdBuffer, _modelBoundsBuffer, i + 1);
            if (!_cullCompute.CPUCulling)
            {
                Vulkan.vkCmdPipelineBarrier(internalBuffer,
                        VkPipelineStageFlags.ComputeShader,
                        VkPipelineStageFlags.DrawIndirect,
                        0, 0, null, 1, &memoryBarrier, 0, null);
            }
            Presenter.Instance.ShadowImage.UpdateCubeFace(i, internalBuffer);
            _shadowOffscreen.SetPushConstantMatrix4x4("viewCube", i, viewMatrix);
            _shadowOffscreen.ExecuteDrawCommandKeepCommands(internalBuffer, frameInfo.FrameIndex, _indirectCmdBuffer, i);
            Presenter.Instance.ShadowImage.EndShadowPass(internalBuffer);
            Vulkan.vkEndCommandBuffer(internalBuffer);
        }
    }
}
