using VECS.ECS.Transforms;
using VECS.LowLevel;

namespace VECS.ECS.Presentation
{

    public class GenericRenderSystem : PresentationSystemBase
    {
        public const uint MAX_DRAWS = 2000;
        private EntityQuery _renderEntityQuery;
        private EntityQuery _renderBloomEntityQuery;

        // private ForwardInternal _forwardData;
        // private ShadowInternal _shadowData;
        // private DepthInternal _depthData;

        

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _renderBloomEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds), typeof(BloomTag))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            DrawBlob.AllInOneMats.Add(MaterialV2.DepthOnly.Hash);
            // _forwardData = new();
            // _shadowData = new();
            // _depthData = new(_forwardData._renderBlob);
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            // _forwardData?.Dispose();
            // _shadowData?.Dispose();
            // _depthData?.Dispose();
        }

        public override void OnPreCull(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            DrawBlob.RebuildOrUpdate(entityManager, entities);
        }

        public override void OnCull(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();

            // _forwardData.GenerateDrawCmds(rendererFrameInfo, entityManager, entities);
            // DrawBlob.RebuildStructure(entityManager, entities);
        }

        public unsafe override void OnPreForwardPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (!_renderEntityQuery.HasEntities)
            {
                // empty depth pass just to clear the depth texture.
                SwapChain.Instance.BeginForwardDepth(frameInfo.CommandBuffer);
                SwapChain.Instance.EndForwardDepthRendering(frameInfo.CommandBuffer);
                return;
            }

            DrawBlob.CullAllInOne(frameInfo, frameInfo.cullData);
            SwapChain.Instance.BeginForwardDepth(frameInfo.CommandBuffer);
            DrawBlob.ExecuteAllInOneDrawCmds(frameInfo, frameInfo.CommandBuffer, MaterialV2.DepthOnly.Hash);
            SwapChain.Instance.EndForwardDepthRendering(frameInfo.CommandBuffer);

            //_shadowData.GenerateDrawCmds(frameInfo, entityManager, entities);
            // _depthData.GenerateDrawCmds(frameInfo, entityManager, entities);
        }

        public override void OnBloomGlow(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            // _forwardData.ExecuteBloomDrawCmds(rendererFrameInfo);
        }

        public override unsafe void OnFowardPass(EntityManager entityManager, RendererFrameInfo frameInfo)
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

            DrawBlob.CullByMat(frameInfo,frameInfo.cullData);
            DrawBlob.ExecuteDrawCmds(frameInfo, null, null, 0, default, default);

            // 
            // _forwardData.ExecuteDrawCmds(frameInfo);
        }
    }
}
