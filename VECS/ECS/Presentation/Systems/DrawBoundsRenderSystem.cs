using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation.Systems
{
    public class DrawBoundsRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderBoundsQuery;
        private EntityQuery _cameraQuery;
        private bool _drawBounds = false;
        private readonly Vector2 _min = new(-1, -1);
        private readonly Vector2 _max = new(1, 1);
        private readonly Vector4[] _fustrumVerts = new Vector4[16];
        private GPUBuffer<Vector3> _lineBuffer;
        private GPUBuffer<Vector3> _frustrumBuffer;
        private GPUBuffer<Vector3> _cubeBuffer;
        private Material _lineMaterial;

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
            _lineBuffer = new(32, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);
            _frustrumBuffer = new(16, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);
            _cubeBuffer = new(16, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);
            CreateWireCube();

            var vertices = _lineBuffer.HostBuffer;
            float radians = 0;
            float radPerStep = float.DegreesToRadians(360) / 31;

            for (int i = 0; i < vertices.Length-1; i++)
            {
                Vector3 dir = new(MathF.Sin(radians), -MathF.Cos(radians), 0);

                vertices[i] = Vector3.Zero + dir * 1f;

                radians += radPerStep;
            }
            vertices[^1] = (Vector3.Zero + new Vector3(MathF.Sin(0), -MathF.Cos(0), 0)) * 1f;
            _lineBuffer.WriteFromHostBuffer();
            var pipelineConfigInfo = GraphicsPipelines.GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo(Presenter.Instance.RenderPass,VkPipelineLayout.Null);

            pipelineConfigInfo.rasterizationInfo.cullMode = VkCullModeFlags.FrontAndBack;
            pipelineConfigInfo.rasterizationInfo.polygonMode = VkPolygonMode.Line;
            pipelineConfigInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.LineStrip;
            pipelineConfigInfo.rasterizationInfo.lineWidth = 1;

            _lineMaterial = new("line_shader.vert", "line_shader.frag", typeof(LTW), [new VkVertexInputBindingDescription(sizeof(Vector3))], [new VkVertexInputAttributeDescription(0, VkFormat.R32G32B32Sfloat, 0)], pipelineConfigInfo);
        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (_drawBounds && _renderBoundsQuery.HasEntities)
            {
                var entities = _renderBoundsQuery.GetEntities();
                _lineMaterial.BindGlobalDescriptorSet(rendererFrameInfo);
                Vulkan.vkCmdBindVertexBuffer(rendererFrameInfo.CommandBuffer, 0, _lineBuffer.VkBuffer);
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    var bounds = entityManager.GetComponent<WorldRenderBounds>(entity);
                    var center = bounds.Bounds.center;
                    var radius = bounds.Radius;
                    LTW a = new()
                    {
                        ltw = TransformExtensions.TRS(center, new Vector3(), radius)
                    };
                    LTW b = new()
                    {
                        ltw = TransformExtensions.TRS(center, new Vector3(float.DegreesToRadians(90),0,0), radius)
                    };
                    LTW c = new()
                    {
                        ltw = TransformExtensions.TRS(center, new Vector3(0, float.DegreesToRadians(90),  0), radius)
                    };
                    LTW d = new()
                    {
                        ltw = TransformExtensions.TRS(center, new Vector3(0, 0, float.DegreesToRadians(90)), radius)
                    };
                    _lineMaterial.PushConstants(rendererFrameInfo.CommandBuffer, a);
                    Vulkan.vkCmdDraw(rendererFrameInfo.CommandBuffer, 32, 1, 0, 0);
                    _lineMaterial.PushConstants(rendererFrameInfo.CommandBuffer, b);
                    Vulkan.vkCmdDraw(rendererFrameInfo.CommandBuffer, 32, 1, 0, 0);
                    _lineMaterial.PushConstants(rendererFrameInfo.CommandBuffer, c);
                    Vulkan.vkCmdDraw(rendererFrameInfo.CommandBuffer, 32, 1, 0, 0);
                    _lineMaterial.PushConstants(rendererFrameInfo.CommandBuffer, d);
                    Vulkan.vkCmdDraw(rendererFrameInfo.CommandBuffer, 32, 1, 0, 0);
                }
            }

            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F1))
            {
                _drawBounds = !_drawBounds;
            }

            if (_cameraQuery.HasEntities && SwapChain.Instance != null)
            {
                if (entityManager.SingletonComponent(out FrameInfo frameInfo)) {
                    var cameras = _cameraQuery.GetEntities();
                    _lineMaterial.BindGlobalDescriptorSet(rendererFrameInfo);
                    for (int i = 0; i < cameras.Count; i++)
                    {
                        var cam = cameras[i];
                        var ltw = entityManager.GetComponent<LocalToWorld>(cam).Value;
                        if (InputManager.Instance.GetKey(SDL3.SDL_Keycode.Space) && entityManager.SingletonEntity<MainCamera>(out Entity mainCamera))
                        {
                            ltw = entityManager.GetComponent<LocalToWorld>(mainCamera).Value;
                            entityManager.SetComponent(cam,new LocalToWorld() { Value =ltw});
                        }
                        var fustrum = entityManager.GetComponent<CameraPerspective>(cam);
                        Matrix4x4 projection = CameraSystem.GetPerspectiveProject(fustrum, frameInfo.screenAspect);
                        Matrix4x4 view = CameraSystem.GetViewMatrix(Matrix4x4.Identity);
                        Matrix4x4.Invert(view * projection, out projection);
                        float scale = 1;
                        _fustrumVerts[0] = Vector4.Transform(new Vector4(_min, scale,1),projection);
                        _fustrumVerts[1] = Vector4.Transform(new Vector4(_min.X, _max.Y, scale, 1),projection);
                        _fustrumVerts[2] = Vector4.Transform(new Vector4(_max, scale, 1),projection);
                        _fustrumVerts[3] = Vector4.Transform(new Vector4(_max.X, _min.Y, scale, 1), projection);
                        _fustrumVerts[4] = Vector4.Transform(new Vector4(_min, scale, 1),projection);
                        scale = -1;
                        _fustrumVerts[5] = Vector4.Transform(new Vector4(_min, scale, 1),projection);
                        _fustrumVerts[6] = Vector4.Transform(new Vector4(_min.X, _max.Y, scale, 1),projection);
                        _fustrumVerts[7] = Vector4.Transform(new Vector4(_max, scale, 1),projection);
                        _fustrumVerts[8] = Vector4.Transform(new Vector4(_max.X, _min.Y, scale, 1),projection);
                        _fustrumVerts[9] = Vector4.Transform(new Vector4(_min.X, _min.Y, scale, 1),projection);
                        _fustrumVerts[10] = Vector4.Transform(new Vector4(_min.X, _max.Y, scale, 1),projection);
                        scale = 1;
                        _fustrumVerts[11] = Vector4.Transform(new Vector4(_min.X, _max.Y, scale, 1),projection);
                        _fustrumVerts[12] = Vector4.Transform(new Vector4(_max, scale, 1),projection);
                        scale = -1;
                        _fustrumVerts[13] = Vector4.Transform(new Vector4(_max, scale, 1),projection);
                        _fustrumVerts[14] = Vector4.Transform(new Vector4(_max.X, _min.Y, scale, 1),projection);
                        scale = 1;
                        _fustrumVerts[15] = Vector4.Transform(new Vector4(_max.X, _min.Y, scale, 1),projection);

                        var buffer = _frustrumBuffer.HostBuffer;
                        for (int j = 0; j < _fustrumVerts.Length; j++)
                        {
                            _fustrumVerts[j].X /= _fustrumVerts[j].W;
                            _fustrumVerts[j].Y /= _fustrumVerts[j].W;
                            _fustrumVerts[j].Z /= _fustrumVerts[j].W;
                            _fustrumVerts[j].W = 1.0f;
                            buffer[j] = new Vector3(_fustrumVerts[j].X, _fustrumVerts[j].Y, _fustrumVerts[j].Z);
                        }

                        _frustrumBuffer.WriteFromHostBuffer();

                        Vulkan.vkCmdBindVertexBuffer(rendererFrameInfo.CommandBuffer, 0, _frustrumBuffer.VkBuffer);
                        _lineMaterial.PushConstants(rendererFrameInfo.CommandBuffer, new LTW() { ltw = ltw });
                        Vulkan.vkCmdDraw(rendererFrameInfo.CommandBuffer, 16, 1, 0, 0);
                    } 
                }
            }

            if (AABBQueue.Count > 0 && _cameraQuery.HasEntities)
            {
                var camera = _cameraQuery.GetEntities()[0];
                var m = entityManager.GetComponent<LocalToWorld>(camera).Value;
                _lineMaterial.BindGlobalDescriptorSet(rendererFrameInfo);

                Matrix4x4.Decompose(m, out var scale, out var rotation, out var center);

                while (AABBQueue.Count > 0)
                {
                    var aabb = AABBQueue.Dequeue();
                    Matrix4x4 trs = TransformExtensions.TRS(center + aabb.center, rotation, aabb.extents);
                    DrawWireCube(rendererFrameInfo.CommandBuffer, trs);
                }
            }
        }

        private void DrawWireCube(VkCommandBuffer commandBuffer, Matrix4x4 ltw)
        {
            Vulkan.vkCmdBindVertexBuffer(commandBuffer, 0, _cubeBuffer.VkBuffer);
            _lineMaterial.PushConstants(commandBuffer, new LTW() { ltw = ltw });
            Vulkan.vkCmdDraw(commandBuffer, 16, 1, 0, 0);
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

        public override void OnDestroy(EntityManager entityManager)
        {

            _lineBuffer?.Dispose();
            _frustrumBuffer?.Dispose();
            _cubeBuffer?.Dispose();
            _lineMaterial?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential,Size = 64)]
        private struct LTW
        {
            public Matrix4x4 ltw;
        }

    }
}
