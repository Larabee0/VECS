using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS.Transforms;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation.Systems
{
    public class DrawBoundsRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderBoundsQuery;
        private GPUBuffer<Vector3> _lineBuffer;
        private Material _lineMaterial;
        public override unsafe void OnCreate(EntityManager entityManager)
        {
            _renderBoundsQuery = new EntityQuery(entityManager)
                .WithAll(typeof(DirectSubMeshIndex),typeof(LocalToWorld))
                .WithNone(typeof(Prefab),typeof(DoNotRender))
                .Build();
            _lineBuffer = new(32, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true);

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
                    var renderBounds = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(entity)).Bounds;
                    var ltw = entityManager.GetComponent<LocalToWorld>(entity);

                    Matrix4x4.Decompose(ltw.Value, out Vector3 scale, out Quaternion rotation, out Vector3 translation);
                    var radius = new Vector3(renderBounds.Radius) * scale;
                    var center = Vector3.Transform(renderBounds.Bounds.center, ltw.Value);
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
