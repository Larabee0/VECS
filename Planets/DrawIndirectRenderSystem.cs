using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS;
using VECS.Compute;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace Planets
{
    public class DrawIndirectRenderSystem : PresentationSystemBase
    {
        public const ulong MAX_INDIRECT_COMMANDS = 1000;
        public const bool COMPUTE_CULL = false;
        private GPUBuffer<ObjectData>[] _objectDataBuffers;
        private GPUBuffer<VkDrawIndexedIndirectCommand>[] _indirectCmdBuffers;
        private GPUBuffer<float>[] _depthSamples;
        

        //float[] texCopy;
        private GPUBuffer<float> _CPUDepthSample;
        private GPUBuffer<float> _sampleOutput;
        private GPUBuffer<float> _sampleInput;

        private EntityQuery _planetRenderQuery;
        
        private GenericComputePipeline _cullCompute;
        private GenericComputePipeline _sampler;

        public override void OnCreate(EntityManager entityManager)
        {
            base.OnCreate(entityManager);
            CreateIndirectCmdBuffers();
            CreateCullComputePipeline();
            _planetRenderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectSubMeshIndex), typeof(LocalToWorld), typeof(MaterialIndex))
                .WithNone(typeof(DoNotRender), typeof(Prefab))
                .Build();
        }

        public unsafe override void OnCull(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_planetRenderQuery.HasEntities) return;
            var cmdBuffer = rendererFrameInfo.CommandBuffer;

            var indirectCmdBuffer = _indirectCmdBuffers[rendererFrameInfo.FrameIndex];
            var depthSampler = _depthSamples[rendererFrameInfo.FrameIndex];
            var objectDataBuffer = _objectDataBuffers[rendererFrameInfo.FrameIndex];
            var entities = _planetRenderQuery.GetEntities();

            CullParams cullParams = new()
            {
                ProjectionMatrix = rendererFrameInfo.Ubo.Projection,
                ViewMatrix = rendererFrameInfo.Ubo.View,
                FrustrumCulling = false,
                OcclusionCulling = false,
                DrawDist = 9999999
            };

            depthSampler.ReadToHostBuffer();
            Span<float> drawOld = depthSampler.HostBuffer;
            bool anySample = false;
            for (int i = 0; i < entities.Count; i++)
            {
                if(drawOld[i] != 0)
                {
                    anySample = true;
                    break;
                }
            }

            Vector3 center = default;
            float radius = float.MaxValue;
            Vector4 aabb = default;

            center.X = drawOld[0];
            center.Y = drawOld[1];
            center.Z = drawOld[2];
            
            radius = drawOld[3];

            aabb.X = drawOld[4];
            aabb.Y = drawOld[5];
            aabb.Z = drawOld[6];
            aabb.W = drawOld[7];

            Span<VkDrawIndexedIndirectCommand> drawCmds = indirectCmdBuffer.HostBuffer;
            Span<ObjectData> drawObjectData = objectDataBuffer.HostBuffer;

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];

                var mesh = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(entity));
                var subMesh = mesh.DirectSubMeshInfo;

                drawCmds[i] = new()
                {
                    instanceCount = 0,
                    firstIndex = subMesh.FirstIndex,
                    indexCount = subMesh.IndexCount,
                    vertexOffset = (int)subMesh.VertexOffset,
                    firstInstance = (uint)i
                };

                var renderBounds = mesh.Bounds;
                drawObjectData[i] = new(entityManager.GetComponent<LocalToWorld>(entity).Value, new(renderBounds.Bounds.center, renderBounds.Radius), new(renderBounds.Bounds.extents, renderBounds.Valid ? 1 : 0));
            }

            var drawCull = GenerateCullData(rendererFrameInfo,cullParams, drawCmds.Length);

            if (COMPUTE_CULL)
            {
                indirectCmdBuffer.WriteFromHostBuffer();
            }
            objectDataBuffer.WriteFromHostBuffer();
            if (!COMPUTE_CULL) {

                FrustumCull(drawCull,rendererFrameInfo, drawCmds, drawObjectData);
                bool drawAll = true;
                bool drawAny = false;
                bool drawNone = true;
                bool anyDontDraw = false;
                for (int i = 0; i < drawCmds.Length; i++)
                {
                    if (drawCmds[i].instanceCount == 0)
                    {
                        drawAll = false;
                        anyDontDraw = true;
                    }
                    if (drawCmds[i].instanceCount > 0)
                    {
                        drawNone = false;
                        drawAny = true;
                    }
                }

                if (drawAny)
                {
                    drawAny = true;
                }

                if (anyDontDraw)
                {
                    anyDontDraw = true;
                }

                if (drawAll)
                {
                    drawAll = true;
                }

                if (drawNone)
                {
                    drawNone = true;
                }

                fixed (VkDrawIndexedIndirectCommand* pDrawCmds = &drawCmds[0])
                {
                    indirectCmdBuffer.WriteToBuffer(pDrawCmds, (ulong)(sizeof(VkDrawIndexedIndirectCommand) * drawCmds.Length));
                }

            }
            else
            {
                _cullCompute.Prepare((uint)entities.Count, (uint)entities.Count);

                fixed (VkDescriptorSet* pSet = &_cullCompute.DescriptorSet)
                {
                    new DescriptorWriter(_cullCompute.DescriptorSetLayout, rendererFrameInfo.FrameDescriptorPool)
                        .WriteBuffer(0, rendererFrameInfo.UboBuffer.DescriptorInfo())
                        .WriteBuffer(1, objectDataBuffer.DescriptorInfo())
                        .WriteBuffer(2, indirectCmdBuffer.DescriptorInfo())
                        .WriteImage(3, rendererFrameInfo.DepthPyramid)
                        .WriteBuffer(4,depthSampler.DescriptorInfo())
                        .Build(pSet);
                }

                _cullCompute.Dispatch(cmdBuffer, drawCull, ((uint)entities.Count / 256) + 1, 1, 1);
                VkBufferMemoryBarrier barrier = new()
                {
                    buffer = indirectCmdBuffer.VkBuffer,
                    size = Vulkan.VK_WHOLE_SIZE,
                    srcQueueFamilyIndex = (uint)GraphicsDevice.Instance.PhysicalQueueFamilies.graphicsFamily,
                    dstQueueFamilyIndex = (uint)GraphicsDevice.Instance.PhysicalQueueFamilies.graphicsFamily,
                    srcAccessMask = VkAccessFlags.ShaderWrite,
                    dstAccessMask = VkAccessFlags.IndirectCommandRead
                };
                rendererFrameInfo.PostCullBarriers.Add(barrier);
            }
        }

        public unsafe override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {

            if (!_planetRenderQuery.HasEntities) return;

            var cmdBuffer = rendererFrameInfo.CommandBuffer;

            var indirectCmdBuffer = _indirectCmdBuffers[rendererFrameInfo.FrameIndex];
            var modelMatricesBuffer = _objectDataBuffers[rendererFrameInfo.FrameIndex];

            var entities = _planetRenderQuery.GetEntities();

            DirectMeshBuffer meshSet = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(entities[0])).DirectMeshBuffer;

            Material material = Material.Materials[entityManager.GetComponent<MaterialIndex>(entities[0]).Value];

            material.BindGlobalDescriptorSet(rendererFrameInfo);

            DescriptorWriter writer = new(material.MaterialDescriptorLayout, rendererFrameInfo.FrameDescriptorPool);
            writer.WriteBuffer(0, modelMatricesBuffer.DescriptorInfo());
            material.BindDescriptorSet(rendererFrameInfo, writer);
            meshSet.BindBuffers(cmdBuffer);
            Vulkan.vkCmdDrawIndexedIndirect(cmdBuffer,
                indirectCmdBuffer.VkBuffer,
                0,
                (uint)indirectCmdBuffer.InstanceCount,
                (uint)sizeof(VkDrawIndexedIndirectCommand));
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            //_planetRenderQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            _sampleInput?.Dispose();
            _sampleOutput?.Dispose();
            _CPUDepthSample?.Dispose();
            _sampler?.Dispose();
            _cullCompute?.Dispose();
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i].Dispose();
                _objectDataBuffers[i].Dispose();
                _depthSamples[i].Dispose();
            }
        }

        private void CreateIndirectCmdBuffers()
        {
            _indirectCmdBuffers = new GPUBuffer<VkDrawIndexedIndirectCommand>[SwapChain.MAX_FRAMES_IN_FLIGHT];
            _objectDataBuffers = new GPUBuffer<ObjectData>[SwapChain.MAX_FRAMES_IN_FLIGHT];
            _depthSamples = new GPUBuffer<float>[SwapChain.MAX_FRAMES_IN_FLIGHT];

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i] = new(MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
                _objectDataBuffers[i] = new(MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
                _depthSamples[i] = new(MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            }

            VkCommandBuffer commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i].FillBuffer(commandBuffer, 0);
                _objectDataBuffers[i].FillBuffer(commandBuffer, 0);
                _depthSamples[i].FillBuffer(commandBuffer, 0);
            }

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }

        private void CreateCullComputePipeline()
        {
            var value = SwapChain.Instance.DepthPyramidHeight * SwapChain.Instance.DepthPyramidWidth;
            _CPUDepthSample = new(value, VkBufferUsageFlags.TransferDst, true);
            _sampleOutput = new(1, VkBufferUsageFlags.StorageBuffer, true);
            _sampleInput = new(3, VkBufferUsageFlags.UniformBuffer, true);
            _cullCompute = new("indirect_cull.comp", typeof(DrawCullData),
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer,VkShaderStageFlags.Compute));

            _sampler = new("sample_texture_mip.comp",
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute));

        }

        public void FrustumCull(DrawCullData cullData, RendererFrameInfo frameInfo, Span<VkDrawIndexedIndirectCommand> drawCmds, Span<ObjectData> objectData)
        {
            for (int i = 0; i < cullData.DrawCount; i++)
            {
                bool visible;

                if (cullData.AABBcheck == 0)
                {
                    visible = IsVisible(i, frameInfo, cullData, objectData);
                }
                else
                {
                    visible = IsVisibleAABB(i, cullData, objectData);
                }

                drawCmds[i].instanceCount = visible ? 1u : 0u;
            }
        }

        static bool ProjectSphere(Vector3 C, float r, float znear, float P00, float P11, out Vector4 aabb)
        {
            aabb = Vector4.Zero;
            if (-C.Z < r + znear)
            {
                return false;
            }

            Vector2 cx = new(C.X, C.Z);
            Vector2 vx = new(MathF.Sqrt(Vector2.Dot(cx, cx) - r * r), r);
            Vector2 minx = new Mat2(vx.X, vx.Y, -vx.Y, vx.X) * cx;
            Vector2 maxx = new Mat2(vx.X, -vx.Y, vx.Y, vx.X) * cx;

            Vector2 cy = new(C.Y, C.Z);
            Vector2 vy = new(MathF.Sqrt(Vector2.Dot(cy, cy) - r * r), r);
            Vector2 miny = new Mat2(vy.X, vy.Y, -vy.Y, vy.X) * cy;
            Vector2 maxy = new Mat2(vy.X, -vy.Y, vy.Y, vy.X) * cy;

            aabb = new Vector4(minx.X / minx.Y * P00, miny.X / miny.Y * P11, maxx.X / maxx.Y * P00, maxy.X / maxy.Y * P11);
            aabb = new Vector4( aabb.X, aabb.W, aabb.Z, aabb.Y) * new Vector4(0.5f, -0.5f, 0.5f, -0.5f) + new Vector4(0.5f); // clip space -> uv space

            return true;
        }
        private unsafe bool IsVisible(int i,RendererFrameInfo frameInfo, DrawCullData drawCullData, Span<ObjectData> objectData)
        {
            Vector4 sphereBounds = objectData[i].SphereBounds;

            Vector4 centerV4 = sphereBounds;
            centerV4.W = 1;

            Vector3 center = new(centerV4.X, centerV4.Y, centerV4.Z);
            centerV4 = Vector4.Transform(centerV4, objectData[i].ModelMatrix);
            centerV4.W = 1;
            centerV4 = Vector4.Transform(centerV4, frameInfo.Ubo.View);
            center = new(centerV4.X, centerV4.Y, centerV4.Z);

            float radius = sphereBounds.W;
            bool visible = true;
            float fusX = center.Z * drawCullData.Frustum[1] - MathF.Abs(center.X) * drawCullData.Frustum[0];
            float fusY = center.Z * drawCullData.Frustum[3] - MathF.Abs(center.Y) * drawCullData.Frustum[2];
            visible = visible && fusX > -radius;
            visible = visible && fusY > -radius;
            if (!visible)
            {
                visible = false;
            }
            if (drawCullData.DistanceCheck != 0)
            {// the near/far plane culling uses camera space Z directly
                visible = visible && center.Z + radius > drawCullData.Znear && center.Z - radius < drawCullData.Zfar;
            }

            visible = visible || drawCullData.CullingEnabled == 0;

            center.Y *= -1;
            //Dictionary<float, int> interest = [];
            //for (int t = 0; t < texCopy.Length; t++)
            //{
            //    if (texCopy[t] != 0 && !interest.TryAdd(texCopy[t], 1))
            //    {
            //        interest[texCopy[t]]++;
            //    }
            //}
            //if(interest.Count != 0)
            //{
            //    var count = interest.Count;
            //}
            //Console.WriteLine(interest.Count);

            if (visible && drawCullData.OcclusionEnabled != 0)
            {
                if (ProjectSphere(center, radius, drawCullData.Znear, drawCullData.P00, drawCullData.P11, out Vector4 aabb))
                {
                    float width = MathF.Abs((aabb.Z - aabb.X) * drawCullData.PyramidWidth);
                    float height = MathF.Abs((aabb.W - aabb.Y) * drawCullData.PyramidHeight);

                    float level = MathF.Floor(MathF.Log2(Math.Max(width, height)));

                    // Sampler is set up to do min reduction, so this computes the minimum depth of a 2x2 texel quad
                    Vector2 uv = (new Vector2(aabb.X,aabb.Y) + new Vector2(aabb.Z,aabb.W)) * 0.5f;
                    uv.X = 1 - uv.X;
                    //uv.Y = 1 - uv.Y;
                    //float depth = textureLod(depthPyramid, uv, level).x;
                    //ReadDepthPyramidAt(frameInfo, level);
                    float depth = SampleDepthPyramid(frameInfo,uv, level);
                    float depthSphere =Math.Abs((drawCullData.Znear / (center.Z - radius)));
                    float sum = depth + depthSphere;
                    if (depth != 0 && 1 - depthSphere <= depth)
                    {
                        visible = visible;
                    }
                    if(depth != 0)
                    {
                        visible = visible ;
                    }
                    visible = visible && depthSphere >= depth;
                    if (!visible)
                    {
                        visible = false;
                    }
                }
            }
            return visible;
        }

        private unsafe float SampleDepthPyramid(RendererFrameInfo frameInfo,Vector2 uv, float mipmapLevel)
        {
            _sampleInput.WriteToBuffer(&uv, 8);
            _sampleInput.WriteToBuffer(&mipmapLevel, 4, 8);

            float output =  0;
            _sampleOutput.WriteToBuffer(&output);
            fixed (VkDescriptorSet* pSet = &_sampler.DescriptorSet)
            {
                new DescriptorWriter(_sampler.DescriptorSetLayout, frameInfo.FrameDescriptorPool)
                    .WriteBuffer(0, _sampleInput.DescriptorInfo())
                    .WriteImage(1, frameInfo.DepthPyramid)
                    .WriteBuffer(2, _sampleOutput.DescriptorInfo())
                    .Build(pSet);
            }

            _sampler.Prepare(1, 1);
            VkCommandBuffer cmd = GraphicsDevice.Instance.BeginSingleTimeCommands();

            _sampler.Dispatch(cmd, 1, 1, 1);

            GraphicsDevice.Instance.EndSingleTimeCommands(cmd);

            _sampleOutput.ReadFromBuffer(&output);

            return output;
        }

        private unsafe void ReadDepthPyramidAt(RendererFrameInfo frameInfo,float mipmapLevel)
        {
            var pyramid = frameInfo.DepthPyramid;

            VkBufferImageCopy copy = new()
            {
                bufferOffset = 0,
                bufferRowLength = SwapChain.Instance.DepthPyramidImage.ImageExtent.width,
                bufferImageHeight = SwapChain.Instance.DepthPyramidImage.ImageExtent.height,
                imageOffset = new()
                { 
                    x = 0,
                    y = 0,
                    z = 0
                },
                imageExtent = SwapChain.Instance.DepthPyramidImage.ImageExtent,
                imageSubresource = new()
                {
                    aspectMask = VkImageAspectFlags.Color,
                    baseArrayLayer = 0,
                    layerCount = 1,
                    mipLevel = 0,
                }
            };

            VkCommandBuffer cmd = GraphicsDevice.Instance.BeginSingleTimeCommands();
            _CPUDepthSample.FillBuffer(cmd, 0);
            SwapChain.Instance.DepthPyramidImage.TextureImage.CopyToBuffer(cmd, pyramid.imageLayout, 1, &copy, _CPUDepthSample);
            GraphicsDevice.Instance.EndSingleTimeCommands(cmd);
            float[] texCopy = new float[_CPUDepthSample.InstanceCount32];
            fixed (float* pTexCopy = &texCopy[0])
            {
                _CPUDepthSample.ReadFromBuffer(pTexCopy);
            }
        }

        private static bool IsVisibleAABB(int i, DrawCullData drawCullData, Span<ObjectData> objectData)
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

        public static DrawCullData GenerateCullData(RendererFrameInfo frameInfo,CullParams cullParams,int drawCount)
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

            drawCullData.PyramidWidth = frameInfo.DepthPyramidWidth;
            drawCullData.PyramidHeight = frameInfo.DepthPyramidHeight;

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


    [StructLayout(LayoutKind.Sequential, Size = 160)]
    public struct ObjectData
    {
        public Matrix4x4 ModelMatrix;
        public Matrix4x4 NormalMatrix;
        public Vector4 SphereBounds;
        public Vector4 Extents;

        public ObjectData(Matrix4x4 modelMatrix, Vector4 sphereBounds, Vector4 extents)
        {
            SphereBounds = sphereBounds;
            Extents = extents;
            ModelMatrix = modelMatrix;
            if (Matrix4x4.Invert(modelMatrix, out NormalMatrix))
            {
                NormalMatrix = Matrix4x4.Transpose(NormalMatrix);
            }
        }
    }

    public struct Mat2
    {
        public Vector2 c0;
        public Vector2 c1;

        public Mat2(float m00, float m01,float m10, float m11)
        {
            c0 = new Vector2(m00, m10);
            c1 = new Vector2(m01, m11);
        }

        public static Vector2 operator *(Mat2 a,Vector2 b)
        {
            return a.c0 * b.X + a.c1 * b.Y;
        }
    }

    
}
