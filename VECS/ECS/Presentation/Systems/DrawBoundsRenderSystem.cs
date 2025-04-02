using System;
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

        private GPUBuffer<Vector3> _lineBuffer;
        private GPUBuffer<Vector3> _frustrumBuffer;
        private Material _lineMaterial;
        public override unsafe void OnCreate(EntityManager entityManager)
        {
            _renderBoundsQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectSubMeshIndex),typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab),typeof(DoNotRender))
                .Build();
            _cameraQuery = new EntityQuery(entityManager)
                .WithAll(typeof(CameraPerspective), typeof(Camera), typeof(LocalToWorld))
                .WithNone(typeof(Prefab))
                .Build();
            _lineBuffer = new(32, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);
            _frustrumBuffer = new(14, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);

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

            pipelineConfigInfo.rasterizationInfo.polygonMode = VkPolygonMode.Line;
            pipelineConfigInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.LineStrip;
            pipelineConfigInfo.rasterizationInfo.lineWidth = 1;

            _lineMaterial = new("line_shader.vert", "line_shader.frag", typeof(LTW), [new VkVertexInputBindingDescription(sizeof(Vector3))], [new VkVertexInputAttributeDescription(0, VkFormat.R32G32B32Sfloat, 0)], pipelineConfigInfo);
        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (_renderBoundsQuery.HasEntities)
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

            if (_cameraQuery.HasEntities && SwapChain.Instance != null)
            {
                if (entityManager.SingletonComponent(out FrameInfo frameInfo)) {
                    Vector2 screenWidthHeight = new(SwapChain.Instance.SwapChainExtent.width, SwapChain.Instance.SwapChainExtent.height);
                    var cameras = _cameraQuery.GetEntities();
                    _lineMaterial.BindGlobalDescriptorSet(rendererFrameInfo);
                    for (int i = 0; i < cameras.Count; i++)
                    {
                        var cam = cameras[i];
                        var fustrum = entityManager.GetComponent<CameraPerspective>(cam);
                        var ltw = entityManager.GetComponent<LocalToWorld>(cam).Value;

                        ltw = TransformExtensions.TRS(Vector3.Zero, Quaternion.Identity, Vector3.One);

                        var minNear = new Vector3(-screenWidthHeight.X * 0.5f, -screenWidthHeight.Y * 0.5f, fustrum.ClipNear);
                        var maxNear = new Vector3(screenWidthHeight.X * 0.5f, screenWidthHeight.Y * 0.5f, fustrum.ClipNear);

                        _frustrumBuffer.HostBuffer[0] = minNear;
                        _frustrumBuffer.HostBuffer[1] = new(minNear.X, maxNear.Y, maxNear.Z);
                        _frustrumBuffer.HostBuffer[2] = maxNear;
                        _frustrumBuffer.HostBuffer[3] = new(maxNear.X, minNear.Y, maxNear.Z);
                        _frustrumBuffer.WriteFromHostBuffer();

                        Vulkan.vkCmdBindVertexBuffer(rendererFrameInfo.CommandBuffer, 0, _frustrumBuffer.VkBuffer);
                        _lineMaterial.PushConstants(rendererFrameInfo.CommandBuffer, new LTW() { ltw = Matrix4x4.Identity });
                        Vulkan.vkCmdDraw(rendererFrameInfo.CommandBuffer, 4, 1, 0, 0);
                    } 
                }
            }
        }

        public override void OnDestroy(EntityManager entityManager)
        {

            _lineBuffer?.Dispose();
            _lineMaterial?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential,Size = 64)]
        private struct LTW
        {
            public Matrix4x4 ltw;
        }

    }
}
