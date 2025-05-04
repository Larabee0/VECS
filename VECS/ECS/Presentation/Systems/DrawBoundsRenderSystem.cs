using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation.Systems
{
    public class DrawBoundsRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderBoundsQuery;
        private EntityQuery _cameraQuery;
        private bool _drawBounds = true;
        private readonly Vector2 _min = new(-1, -1);
        private readonly Vector2 _max = new(1, 1);
        private readonly Vector4[] _fustrumVerts = new Vector4[16];
        private GPUBuffer<Vector3> _circleBuffer;
        private GPUBuffer<Vector3> _frustrumBuffer;
        private GPUBuffer<Vector3> _cubeBuffer;
        private Material _lineMaterial;

        private SwapChainBuffer<VkDrawIndirectCommand> _drawBuffer;

        public Queue<Bounds> AABBQueue = new();

        public override unsafe void OnCreate(EntityManager entityManager)
        {
            _renderBoundsQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectSubMeshIndex),typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab),typeof(DoNotRender))
                .Build();
            _cameraQuery = new EntityQuery(entityManager)
                .WithAll(typeof(CameraPerspective), typeof(Camera), typeof(LocalToWorld))
                .WithNone(typeof(Prefab),typeof(MainCamera))
                .Build();
            _circleBuffer = new(32, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);
            _frustrumBuffer = new(16*1000, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);
            _cubeBuffer = new(16, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);
            CreateDrawBuffers();
            CreateWireCube();

            var vertices = _circleBuffer.HostBuffer;
            float radians = 0;
            float radPerStep = float.DegreesToRadians(360) / 31;

            for (int i = 0; i < vertices.Length-1; i++)
            {
                Vector3 dir = new(MathF.Sin(radians), -MathF.Cos(radians), 0);

                vertices[i] = Vector3.Zero + dir * 1f;

                radians += radPerStep;
            }
            vertices[^1] = (Vector3.Zero + new Vector3(MathF.Sin(0), -MathF.Cos(0), 0)) * 1f;
            _circleBuffer.WriteFromHostBuffer();
            var pipelineConfigInfo = GraphicsPipelines.GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo(Presenter.Instance.RenderPass,VkPipelineLayout.Null);

            pipelineConfigInfo.rasterizationInfo.cullMode = VkCullModeFlags.FrontAndBack;
            pipelineConfigInfo.rasterizationInfo.polygonMode = VkPolygonMode.Line;
            pipelineConfigInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.LineStrip;
            pipelineConfigInfo.rasterizationInfo.lineWidth = 1;

            _lineMaterial = Material.Create("line_shader.vert", "line_shader.frag", pipelineConfigInfo);
        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F1))
            {
                _drawBounds = !_drawBounds;
            }

            int drawIndex = 0;
            var matrices = Span<ModelMatrices>.Empty;
            var colours = Span<Vector4>.Empty;
            var draws = Span<VkDrawIndirectCommand>.Empty;

            if (AABBQueue.Count > 0 && _cameraQuery.HasEntities
                || _cameraQuery.HasEntities && SwapChain.Instance != null
                || _drawBounds && _renderBoundsQuery.HasEntities)
            {
                _lineMaterial.BindAll(frameInfo);
                matrices = _lineMaterial.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                colours = _lineMaterial.GetStorageBuffer<Vector4>("colourBuffer");
                draws = _drawBuffer.HostBuffer;
            }

            if (AABBQueue.Count > 0 && _cameraQuery.HasEntities)
            {
                var camera = _cameraQuery.GetEntities()[0];
                var m = entityManager.GetComponent<LocalToWorld>(camera).Value;

                Matrix4x4.Decompose(m, out _, out var rotation, out var center);

                draws[0] = new()
                {
                    firstVertex = 0,
                    firstInstance = 0,
                    vertexCount = 16,
                    instanceCount = (uint)AABBQueue.Count
                };

                while (AABBQueue.Count > 0)
                {
                    var aabb = AABBQueue.Dequeue();
                    matrices[drawIndex] = TransformExtensions.TRS(center + aabb.center, rotation, aabb.extents);
                    colours[drawIndex] = Vector4.One;
                    drawIndex++;
                }
                Vulkan.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _cubeBuffer.VkBuffer);
                DrawIndirect(frameInfo, 0, 1);
            }

            if (_cameraQuery.HasEntities && SwapChain.Instance != null)
            {
                if (entityManager.SingletonComponent(out FrameInfo screenAspect))
                {
                    var cameras = _cameraQuery.GetEntities();
                    int offset = drawIndex != 0 ? 1 : 0;
                    int vertexOffset = 0;
                    for (int i = 0; i < cameras.Count; i++, vertexOffset += 16)
                    {
                        var cam = cameras[i];
                        var ltw = entityManager.GetComponent<LocalToWorld>(cam).Value;
                        if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.Space) && entityManager.SingletonEntity<MainCamera>(out Entity mainCamera))
                        {
                            ltw = entityManager.GetComponent<LocalToWorld>(mainCamera).Value;
                            entityManager.SetComponent(cam, new LocalToWorld() { Value = ltw });
                        }
                        var fustrum = entityManager.GetComponent<CameraPerspective>(cam);
                        Matrix4x4 projection = CameraSystem.GetPerspectiveProject(fustrum, screenAspect.screenAspect);
                        Matrix4x4 view = CameraSystem.GetViewMatrix(Matrix4x4.Identity);
                        Matrix4x4.Invert(view * projection, out projection);
                        float scale = 1;
                        _fustrumVerts[vertexOffset + 0] = Vector4.Transform(new Vector4(_min, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 1] = Vector4.Transform(new Vector4(_min.X, _max.Y, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 2] = Vector4.Transform(new Vector4(_max, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 3] = Vector4.Transform(new Vector4(_max.X, _min.Y, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 4] = Vector4.Transform(new Vector4(_min, scale, 1), projection);
                        scale = -1;
                        _fustrumVerts[vertexOffset + 5] = Vector4.Transform(new Vector4(_min, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 6] = Vector4.Transform(new Vector4(_min.X, _max.Y, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 7] = Vector4.Transform(new Vector4(_max, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 8] = Vector4.Transform(new Vector4(_max.X, _min.Y, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 9] = Vector4.Transform(new Vector4(_min.X, _min.Y, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 10] = Vector4.Transform(new Vector4(_min.X, _max.Y, scale, 1), projection);
                        scale = 1;
                        _fustrumVerts[vertexOffset + 11] = Vector4.Transform(new Vector4(_min.X, _max.Y, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 12] = Vector4.Transform(new Vector4(_max, scale, 1), projection);
                        scale = -1;
                        _fustrumVerts[vertexOffset + 13] = Vector4.Transform(new Vector4(_max, scale, 1), projection);
                        _fustrumVerts[vertexOffset + 14] = Vector4.Transform(new Vector4(_max.X, _min.Y, scale, 1), projection);
                        scale = 1;
                        _fustrumVerts[vertexOffset + 15] = Vector4.Transform(new Vector4(_max.X, _min.Y, scale, 1), projection);

                        var buffer = _frustrumBuffer.HostBuffer;
                        for (int j = 0; j < 16; j++)
                        {
                            Vector4 vertex = _fustrumVerts[vertexOffset + j];
                            vertex.X /= vertex.W;
                            vertex.Y /= vertex.W;
                            vertex.Z /= vertex.W;
                            vertex.W = 1.0f;
                            buffer[vertexOffset + j] = vertex.AsVector3();
                            _fustrumVerts[vertexOffset + j] = vertex;
                        }

                        matrices[drawIndex] = ltw;
                        colours[drawIndex] = Vector4.One;

                        draws[drawIndex] = new()
                        {
                            firstInstance = (uint)drawIndex,
                            firstVertex = (uint)vertexOffset,
                            instanceCount = 1,
                            vertexCount = 16
                        };
                        drawIndex++;
                    }
                    _frustrumBuffer.WriteFromHostBuffer();

                    Vulkan.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _frustrumBuffer.VkBuffer);
                    DrawIndirect(frameInfo, offset, cameras.Count);
                }
            }

            if (_drawBounds && _renderBoundsQuery.HasEntities)
            {
                var entities = _renderBoundsQuery.GetEntities();
                int offset = drawIndex;
                draws[drawIndex] = new()
                {
                    vertexCount = 32,
                    firstVertex = 0,
                    firstInstance = (uint)offset,
                    instanceCount = (uint)entities.Count * 4
                };

                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    var bounds = entityManager.GetComponent<WorldRenderBounds>(entity);
                    var center = bounds.Bounds.center;
                    var radius = (bounds.Radius == Vector3.Zero) ? Vector3.One : bounds.Radius;
                    var a = TransformExtensions.TRS(center, new Vector3(), radius);
                    var b = TransformExtensions.TRS(center, new Vector3(float.DegreesToRadians(90), 0, 0), radius);
                    var c = TransformExtensions.TRS(center, new Vector3(0, float.DegreesToRadians(90), 0), radius);
                    var d = TransformExtensions.TRS(center, new Vector3(0, 0, float.DegreesToRadians(90)), radius);

                    matrices[drawIndex] = a;
                    matrices[drawIndex + 1] = b;
                    matrices[drawIndex + 2] = c;
                    matrices[drawIndex + 3] = d;
                    colours[drawIndex] = Vector4.One;
                    colours[drawIndex + 1] = Vector4.One;
                    colours[drawIndex + 2] = Vector4.One;
                    colours[drawIndex + 3] = Vector4.One;

                    drawIndex += 4;
                }

                Vulkan.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _circleBuffer.VkBuffer);
                DrawIndirect(frameInfo, offset, 1);
            }

        }

        private unsafe void DrawIndirect(RendererFrameInfo frameInfo,int offset, int count)
        {
            Vulkan.vkCmdDrawIndirect(frameInfo.CommandBuffer, _drawBuffer.ActiveVkBuffer, (uint)offset * (uint)sizeof(VkDrawIndirectCommand), (uint)count, (uint)sizeof(VkDrawIndirectCommand));
        }

        private void CreateWireCube()
        {
            Vector3 min = new(-0.5f, -0.5f, -0.5f);
            Vector3 max = new(0.5f, 0.5f, 0.5f);
            var verts = _cubeBuffer.HostBuffer;
            verts[0] = min;
            verts[1] = new Vector3(min.X, max.Y, min.Z);
            verts[2] = new Vector3(max.X,max.Y, min.Z);
            verts[3] = new Vector3(max.X, min.Y, min.Z);
            verts[4] = min;

            verts[5] = new Vector3(min.X,min.Y, max.Z);
            verts[6] = new Vector3(min.X, max.Y, max.Z);
            verts[7] = max;
            verts[8] = new Vector3(max.X, min.Y, max.Z);
            verts[9] = new Vector3(min.X, min.Y, max.Z);
            verts[10] = new Vector3(min.X, max.Y, max.Z);
                                        
            verts[11] = new Vector3(min.X, max.Y, min.Z);
            verts[12] = new Vector3(max.X,max.Y, min.Z);
                                        
            verts[13] = max;
            verts[14] = new Vector3(max.X, min.Y, max.Z);

            verts[15] = new Vector3(max.X, min.Y, min.Z);

            _cubeBuffer.WriteFromHostBuffer();
        }

        private void CreateDrawBuffers()
        {
            _drawBuffer = new(GenericRenderSystem.MAX_DRAWS, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.IndirectBuffer, true);
        }

        public override void OnDestroy(EntityManager entityManager)
        {

            _circleBuffer?.Dispose();
            _frustrumBuffer?.Dispose();
            _cubeBuffer?.Dispose();
            _lineMaterial?.Dispose();

            _drawBuffer?.Dispose();
        }
    }
}
