using System;
using System.Collections.Generic;
using System.Numerics;
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

        public ShadowInternal(FustrumCull cull) : base(cull)
        {
            GraphicsPipelineConfigInfo shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            shadowConfig.renderPass = Presenter.Instance.ShadowRenderPass;
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;

            _shadowOffscreen = Material.Create("ShadowOffscreen","shadow_offscreen.vert", "shadow_offscreen.frag", shadowConfig);
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
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


                if(_preSortedDrawCmds.TryGetValue(renderMesh.Mesh.DirectMesh,out var cmds))
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
            RenderShadows(frameInfo, drawCount);
        }

        private unsafe void RenderShadows(RendererFrameInfo frameInfo, int drawCount)
        {
            _shadowOffscreen.SetMatDescriptorHandleStorageRegions(0, 0, (uint)drawCount);
            ShadowImage.SetViewPort(frameInfo);

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

            for (int i = 0; i < 6; i++)
            {
                var viewMatrix = ShadowImage.GetViewMatrixForFace(i);

                cullData.viewMatrix = viewMatrix * model;

                VkBufferMemoryBarrier memoryBarrier = _cullCompute.Cull(frameInfo, cullData, (uint)drawCount, _indirectCmdBuffer, _modelBoundsBuffer);
                if (!_cullCompute.CPUCulling)
                {
                    Vulkan.vkCmdPipelineBarrier(frameInfo.CommandBuffer,
                            VkPipelineStageFlags.ComputeShader,
                            VkPipelineStageFlags.DrawIndirect,
                            0, 0, null, 1, &memoryBarrier, 0, null);
                }

                Presenter.Instance.ShadowImage.UpdateCubeFace(i, frameInfo.CommandBuffer);
                _shadowOffscreen.SetPushConstantMatrix4x4("viewCube", viewMatrix);
                _shadowOffscreen.ExecuteDrawCommandKeepCommands(frameInfo, _indirectCmdBuffer);
                Presenter.EndRenderPass(frameInfo.CommandBuffer);
            }

            _shadowOffscreen._drawCommands.Clear();
        }
    }
}
