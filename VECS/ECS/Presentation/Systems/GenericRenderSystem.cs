using VECS.ECS.Transforms;
using VECS.LowLevel;
using VECS.Presentation;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{

    public class GenericRenderSystem : PresentationSystemBase
    {
        public const uint MAX_DRAWS = 2000;
        private EntityQuery _renderEntityQuery;

         private ShadowInternal _shadowData;

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            DrawBlob.AllInOneMats.Add(EngineMaterials.DepthOnly.Hash);
            _shadowData = new();
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            DrawBlob.RebuildOrUpdate(entityManager, entities);
        }

        public override void OnShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (!_renderEntityQuery.HasEntities)
            {
                // empty shadow passes just to clear the cube map.
                for (int i = 0; i < 6; i++)
                {
                    Presenter.Instance.ShadowImage.UpdateCubeFace(i, frameInfo.CommandBuffer);
                    Presenter.Instance.ShadowImage.EndShadowPass(frameInfo.CommandBuffer);
                }
                return;
            }

            _shadowData.RenderShadows(frameInfo);
        }

        public unsafe override void OnPreOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            if (!_renderEntityQuery.HasEntities)
            {
                // empty depth pass just to clear the depth texture.
                Presenter.Instance.ForwardRenderer.BeginForwardDepthOnlyRendering(commandBuffer);
                Presenter.Instance.ForwardRenderer.EndForwardDepthOnlyRendering(commandBuffer);

                DepthReduction.ReduceDepth(frameInfo);

                return;
            }

            var depthBufferCullInfo = frameInfo.CullData;
            depthBufferCullInfo.depthCulling = 0;
            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

            Presenter.Instance.ForwardRenderer.BeginForwardDepthOnlyRendering(commandBuffer);

            DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, commandBuffer, EngineMaterials.DepthOnly.Hash);

            Presenter.Instance.ForwardRenderer.EndForwardDepthOnlyRendering(commandBuffer);

            DepthReduction.ReduceDepth(frameInfo);
            DrawBlob.CullByMat(frameInfo, frameInfo.CullData);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

        }

        public override unsafe void OnOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            //if (GraphicsDevice.MeshShading)
            //{
            //    MaterialV2 meshShader = AssetDataBase<MaterialV2>.GetNamed("MeshShader");
            //
            //    //DirectMesh cube = AssetDataBase<DirectMesh>.GetNamed("cube-UV");
            //    //cube.MeshShaderSet.Update(frameInfo);
            //    //var subMesh = cube.DirectSubMeshes[0];
            //    //var meshletInfo = subMesh.MeshletInfo;
            //    //meshShader.PushConstants.SetPushConstantUInt("meshletCount", (uint)meshletInfo.MeshletCount);
            //    //meshShader.Update(frameInfo);
            //    //meshShader.BindAll(frameInfo);
            //    //meshShader.BindMeshShaderData(frameInfo, cube);
            //    //meshShader.PushConstants.BindPushConstants(frameInfo, meshShader.PipeLineLayout);
            //    //Vulkan.vkCmdDrawMeshTasksEXT(frameInfo.CommandBuffer, 1, 1, 1);
            //
            //    DirectMesh vase = AssetDataBase<DirectMesh>.GetNamed("smooth_vase");
            //    var subMesh = vase.DirectSubMeshes[0];
            //    var meshletInfo = subMesh.MeshletInfo;
            //    meshShader.PushConstants.SetPushConstantUInt("meshletCount", (uint)meshletInfo.MeshletCount);
            //
            //    meshShader.BindAllMesh(frameInfo, 0, vase);
            //    GraphicsDevice.DeviceAPI.vkCmdDrawMeshTasksEXT(frameInfo.CommandBuffer, 1, 1, 1);
            //
            //    //var unlit = Presenter.Instance.Unlit;
            //    //var drawCmd = subMesh.DirectSubMeshInfo.IndirectDrawCmd;
            //    //unlit.GetStorageBuffer<ModelMatrices>("matricesBuffer")[0] = new(TransformExtensions.TRS(new(0, 0, 10), System.Numerics.Quaternion.Identity, new(10)));
            //    //unlit.Update(frameInfo);
            //    //unlit.BindAll(frameInfo);
            //    //cube.BindSpecificBuffers(frameInfo.CommandBuffer, unlit.VertexBindings, unlit.VertexAttributes);
            //    //Vulkan.vkCmdDrawIndexed(frameInfo.CommandBuffer, drawCmd.indexCount, 1, drawCmd.firstIndex, drawCmd.vertexOffset, 0);
            //}

            /*
            DirectMesh cube = AssetDataBase<DirectMesh>.GetNamed("cube-UV");

            bool descriptorBuffers = true;
            if (descriptorBuffers)
            {
                MaterialV2 descBufferTest = AssetDataBase<MaterialV2>.GetNamed("LitTexture");
                
                descBufferTest.BindAll(frameInfo);
            }
            else
            {
                var unlit = Presenter.Instance.Unlit;
                unlit.GetStorageBuffer<ModelMatrices>("matricesBuffer")[0] = new(TransformExtensions.TRS(new(0, 0, 0), System.Numerics.Quaternion.Identity, new(5)));
                //unlit.SetStorageBufferUsageSize("matricesBuffer", (uint)sizeof(ModelMatrices));
                unlit.Update(frameInfo);
                unlit.BindAll(frameInfo);
            }
            cube.DirectSubMeshes[0].SimpleBindAndDraw(frameInfo.CommandBuffer);
            */

            if (!_renderEntityQuery.HasEntities) { return; }

            DrawBlob.ExecuteOpaqueDrawCmds(frameInfo, null, null, 0, default, default);
        }

        public override void OnPreTransparentPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            
            if (!_renderEntityQuery.HasEntities)
            {
                return;
            }

            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;

            Presenter.Instance.ForwardRenderer.BeginForwardDepthOnlyRendering(commandBuffer,VkAttachmentLoadOp.Load);

            DrawBlob.ExecuteAllInOneTransparentDrawCmds(frameInfo, commandBuffer, EngineMaterials.DepthOnly.Hash);

            Presenter.Instance.ForwardRenderer.EndForwardDepthOnlyRendering(commandBuffer);
            var cullData = frameInfo.CullData;
            cullData.depthCulling = 0;

            DrawBlob.CullByMat(frameInfo, cullData);

            DrawBlob.IndirectToComputeMemoryBarrierAllInOne(commandBuffer);
        }

        public override unsafe void OnTransparentPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            DrawBlob.ExecuteTransparentDrawCmds(frameInfo, null, null, 0, default, default);
        }
    }
}
