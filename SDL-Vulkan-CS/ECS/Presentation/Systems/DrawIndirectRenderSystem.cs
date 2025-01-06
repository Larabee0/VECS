using SDL_Vulkan_CS.Artifact.Colour;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.ECS.Presentation.Systems
{
    public class DrawIndirectRenderSystem : PresentationSystemBase
    {
        public const ulong MAX_INDIRECT_COMMANDS = 1000;
        private CsharpVulkanBuffer<VkDrawIndexedIndirectCommand>[] _indirectCmdBuffers;
        private CsharpVulkanBuffer<ModelPushConstantData>[] _modelMatricesBuffers;

        private EntityQuery _planetRenderQuery;

        public int depthPyramidWidth;
        public int depthPyramidHeight;

        public override void OnCreate(EntityManager entityManager)
        {
            base.OnCreate(entityManager);
            CreateIndirectCmdBuffers();

            _planetRenderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(InDirectMesh), typeof(LocalToWorld), typeof(MaterialIndex))
                .WithNone(typeof(DoNotRender), typeof(Prefab))
                .Build();
        }

        public unsafe override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {

            if (!_planetRenderQuery.HasEntities) return;

            var cmdBuffer = rendererFrameInfo.CommandBuffer;

            var indirectCmdBuffer = _indirectCmdBuffers[rendererFrameInfo.FrameIndex];
            var modelMatricesBuffer = _modelMatricesBuffers[rendererFrameInfo.FrameIndex];

            var entities = _planetRenderQuery.GetEntities();

            CullParams cullParams = new()
            {
                ProjectionMatrix = rendererFrameInfo.Ubo.Projection,
                ViewMatrix = rendererFrameInfo.Ubo.View,
                FrustrumCulling = false,

                DrawDist = 9999999
            };

            

            VkDrawIndexedIndirectCommand[] drawCmds = new VkDrawIndexedIndirectCommand[entities.Count];
            ModelPushConstantData[] modelMatrices = new ModelPushConstantData[entities.Count];
            ObjectData[] drawObjectData = new ObjectData[entities.Count];

            for (uint i = 0; i < entities.Count; i++)
            {
                var entity = entities[(int)i];

                var mesh = GPUMesh<Vertex>.Meshes[ entityManager.GetComponent<InDirectMesh>(entity).Value];
                var subMesh = mesh.SubMesh;

                drawCmds[i] = new()
                {
                    instanceCount = 1,
                    firstIndex = (uint)subMesh.IndexOffset,
                    indexCount = (uint)subMesh.IndexCount,
                    vertexOffset = (int)subMesh.VertexOffset,
                    firstInstance = i
                };

                modelMatrices[i] = new(entityManager.GetComponent<LocalToWorld>(entity).Value);
                var renderBounds = mesh.renderBounds;
                drawObjectData[i] = new()
                {
                    ModelMatrix = modelMatrices[i].ModelMatrix,
                    SphereBounds = new(renderBounds.Origin,renderBounds.Radius),
                    Extents = new(renderBounds.Extents,renderBounds.Valid ? 1 : 0)
                };
            }


            var drawCull = GenerateCullData(cullParams, drawCmds.Length);

            FrustumCull(drawCull, ref drawCmds, drawObjectData);

            fixed (VkDrawIndexedIndirectCommand* pDrawCmds = &drawCmds[0])
            {
                indirectCmdBuffer.WriteToBuffer(pDrawCmds, (ulong)(sizeof(VkDrawIndexedIndirectCommand) * drawCmds.Length));
            }

            fixed (ModelPushConstantData* pMatrices = &modelMatrices[0])
            {
                modelMatricesBuffer.WriteToBuffer(pMatrices,(ulong)(sizeof(ModelPushConstantData) * modelMatrices.Length));
            }

            MeshSet<Vertex> meshSet = GPUMesh<Vertex>.Meshes[entityManager.GetComponent<InDirectMesh>(entities[0]).Value].MeshSet;

            Material material = Material.Materials[entityManager.GetComponent<MaterialIndex>(entities[0]).Value];

            material.BindGlobalDescriptorSet(rendererFrameInfo);

            DescriptorWriter writer = new(material.MaterialDescriptorLayout, rendererFrameInfo.FrameDescriptorPool);
            writer.WriteBuffer(0, modelMatricesBuffer.DescriptorInfo());

            material.BindDescriptorSet(rendererFrameInfo, writer);

            Vulkan.vkCmdBindVertexBuffer(cmdBuffer, 0, meshSet._vertexBuffer.VkBuffer, 0);
            Vulkan.vkCmdBindIndexBuffer(cmdBuffer, meshSet._indexBuffer.VkBuffer, 0, VkIndexType.Uint32);


            //for (int i = 0; i < drawCmds.Length; i++)
            //{
            //    var drawCmd = drawCmds[i];
            //    Vulkan.vkCmdDrawIndexed(cmdBuffer, drawCmd.indexCount, 1, drawCmd.firstIndex,drawCmd.vertexOffset, drawCmd.firstInstance);
            //}
            

            Vulkan.vkCmdDrawIndexedIndirect(cmdBuffer,
                indirectCmdBuffer.VkBuffer,
                0,
                (uint)drawCmds.Length,
                (uint)sizeof(VkDrawIndexedIndirectCommand));
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _planetRenderQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i].Dispose();
                _modelMatricesBuffers[i].Dispose();
            }

        }

        private void CreateIndirectCmdBuffers()
        {
            _indirectCmdBuffers = new CsharpVulkanBuffer<VkDrawIndexedIndirectCommand>[SwapChain.MAX_FRAMES_IN_FLIGHT];
            _modelMatricesBuffers = new CsharpVulkanBuffer<ModelPushConstantData>[SwapChain.MAX_FRAMES_IN_FLIGHT];

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i] = new(GraphicsDevice.Instance,
                    MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
                _modelMatricesBuffers[i] = new(GraphicsDevice.Instance,
                    MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            }

            VkCommandBuffer commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                Vulkan.vkCmdFillBuffer(commandBuffer, _indirectCmdBuffers[i].VkBuffer, 0, _indirectCmdBuffers[i].BufferSize, 0);
                Vulkan.vkCmdFillBuffer(commandBuffer, _modelMatricesBuffers[i].VkBuffer, 0, _modelMatricesBuffers[i].BufferSize, 0);
            }

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }

        public static void FrustumCull(DrawCullData cullData, ref VkDrawIndexedIndirectCommand[] drawCmds, ObjectData[] objectData)
        {
            for (int i = 0; i < cullData.DrawCount; i++)
            {
                bool visible;

                if (cullData.AABBcheck == 0)
                {
                    visible = IsVisible(i, cullData, objectData);
                }
                else
                {
                    visible = IsVisibleAABB(i, cullData, objectData);
                }

                drawCmds[i].instanceCount = visible ? 1u : 0u;
            }
        }

        private static bool IsVisible(int i, DrawCullData drawCullData, ObjectData[] objectData)
        {
            Vector4 sphereBounds = objectData[i].SphereBounds;
            
            Vector4 centerV4 = sphereBounds;
            centerV4.W = 1;
            Vector3 center = new(centerV4.X, centerV4.Y, centerV4.Z);
            center = Vector3.Transform(center, objectData[i].ModelMatrix);
            centerV4 = new(center, 1);
            centerV4 = Vector4.Transform(centerV4, drawCullData.ViewMat);
            center = new(centerV4.X, centerV4.Y, centerV4.Z);
            float radius = sphereBounds.W;
            bool visible = true;

            visible = visible && center.Z * drawCullData.Frustum[1] - MathF.Abs(center.X) * drawCullData.Frustum[0] > -radius;
            visible = visible && center.Z * drawCullData.Frustum[3] - MathF.Abs(center.Y) * drawCullData.Frustum[2] > -radius;

            if (drawCullData.DistanceCheck != 0)
            {// the near/far plane culling uses camera space Z directly
                visible = visible && center.Z + radius > drawCullData.Znear && center.Z - radius < drawCullData.Zfar;
            }

            visible = visible || drawCullData.CullingEnabled == 0;

            center.Y *= -1;

            // if (visible && drawCullData.OcclusionEnabled != 0)
            // {
            //     if (projectSphere(center, radius, drawCullData.Znear, drawCullData.P00, drawCullData.P11, out Vector4 aabb))
            //     {
            //         float width = (aabb.Z - aabb.X) * drawCullData.PyramidWidth;
            //         float height = (aabb.W - aabb.Y) * drawCullData.PyramidHeight;
            // 
            //         float level = MathF.Floor(MathF.Log2(Math.Max(width, height)));
            // 
            //         // Sampler is set up to do min reduction, so this computes the minimum depth of a 2x2 texel quad
            // 
            //         float depth = textureLod(depthPyramid, (aabb.xy + aabb.zw) * 0.5, level).x;
            //         float depthSphere = drawCullData.Znear / (center.Z - radius);
            // 
            //         visible = visible && depthSphere >= depth;
            //     }
            // }

            return visible;
        }

        private static bool IsVisibleAABB(int i, DrawCullData drawCullData, ObjectData[] objectData)
        {
            Vector4 sphereBounds = objectData[i].SphereBounds;

            Vector3 center = new(sphereBounds.X,sphereBounds.Y,sphereBounds.Z);
            //center = (cullData.view * vec4(center,1.f)).xyz;
            float radius = sphereBounds.W;

            bool visible = true;

            Vector3 aabbmin = new Vector3(drawCullData.AabbMin_x, drawCullData.AabbMin_y, drawCullData.AabbMin_z) + new Vector3(radius);
            Vector3 aabbmax = new Vector3(drawCullData.AabbMax_x, drawCullData.AabbMax_y, drawCullData.AabbMax_z) - new Vector3(radius);

            visible = visible && (center.X > aabbmin.X) && (center.X < aabbmax.X);
            visible = visible && (center.Y > aabbmin.Y) && (center.Y < aabbmax.Y);
            visible = visible && (center.Z > aabbmin.Z) && (center.Z < aabbmax.Z);

            return visible;
        }

        public DrawCullData GenerateCullData(CullParams cullParams,int drawCount)
        {
            Matrix4x4 projection = cullParams.ProjectionMatrix;
            Matrix4x4 projectionT = Matrix4x4.Transpose(projection);
            
            Vector4 frustrumX = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0)).NormalizePlane();
            Vector4 frustrumY = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1)).NormalizePlane();
            Vector4 frustum = new(frustrumX.X, frustrumX.Z, frustrumY.Y, frustrumY.Z);
            DrawCullData drawCullData = default;
            drawCullData.P00 = projection[0,0];
            drawCullData.P11 = projection[1,1];
            drawCullData.Znear = 0.1f;
            drawCullData.Zfar = cullParams.DrawDist;
            drawCullData.Frustum = frustum;
            drawCullData.DrawCount = drawCount;
            drawCullData.CullingEnabled = cullParams.FrustrumCulling ? 1: 0;
            drawCullData.LodEnabled = false ? 1 : 0;
            drawCullData.OcclusionEnabled = cullParams.OcclusionCulling ? 1 : 0;
            drawCullData.LodBase = 10.0f;
            drawCullData.LodStep = 1.5f;

            drawCullData.PyramidWidth = depthPyramidWidth;
            drawCullData.PyramidHeight = depthPyramidHeight;
            drawCullData.ViewMat = cullParams.ViewMatrix;//get_view_matrix();

            drawCullData.AABBcheck = cullParams.Aabb ? 1 : 0;
            drawCullData.AabbMin_x = cullParams.AabbMin.X;
            drawCullData.AabbMin_y = cullParams.AabbMin.Y;
            drawCullData.AabbMin_z = cullParams.AabbMin.Z;

            drawCullData.AabbMax_x = cullParams.AabbMax.X;
            drawCullData.AabbMax_y = cullParams.AabbMax.Y;
            drawCullData.AabbMax_z = cullParams.AabbMax.Z;

            if (cullParams.DrawDist > 10000)
	        {
                drawCullData.DistanceCheck = false ? 1 : 0;
            }

            else
            {
                drawCullData.DistanceCheck = true ? 1 : 0;
            }

            return drawCullData;
        }

    }

    public struct ObjectData
    {
        public Matrix4x4 ModelMatrix;
        public Vector4 SphereBounds;
        public Vector4 Extents;
    }

    public struct RenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector3 Origin;
        public float Radius;
        public Vector3 Extents;
        public bool Valid;
    }

    public struct InDirectMesh : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;
    }
}
