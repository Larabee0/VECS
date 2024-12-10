using SDL_Vulkan_CS.ECS;
using SDL_Vulkan_CS.ECS.Presentation;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Colour
{
    public class ColouredRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderQuery;
        CsharpVulkanBuffer minMaxBuffer;
        public unsafe override void OnCreate(EntityManager entityManager)
        {
             minMaxBuffer = new(GraphicsDevice.Instance, (uint)sizeof(Vector2), 1, Vortice.Vulkan.VkBufferUsageFlags.UniformBuffer, true);
            _renderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(MeshIndex), typeof(TextureIndex), typeof(MaterialIndex), typeof(LocalToWorld),typeof(ElevationMinMax))
                .Build();
        }

        public unsafe override void OnPresent(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (_renderQuery.HasEntities)
            {
                List<Entity> entities = _renderQuery.GetEntities();
                List<DrawCall> drawCalls = new(entities.Count);
                entities.ForEach(e =>
                {
                    drawCalls.Add(new()
                    {
                        MeshIndex = entityManager.GetComponent<MeshIndex>(e).Value,
                        TextureIndex = entityManager.GetComponent<TextureIndex>(e).Value,
                        MaterialIndex = entityManager.GetComponent<MaterialIndex>(e).Value,
                        Ltw = entityManager.GetComponent<LocalToWorld>(e).Value
                    });
                });


                drawCalls.Sort(new DrawCall());


                Material mat = null;
                for (int i = 0; i < drawCalls.Count; i++)
                {
                    var drawCall = drawCalls[i];
                    var curMat = Material.GetMaterialAtIndex(drawCall.MaterialIndex);

                    // if mat is null or different from the last mat, it needs its descriptor sets bound
                    if (mat == null || mat != curMat)
                    {
                        mat = curMat;
                        mat?.BindGlobalDescriptorSet(frameInfo);
                        
                    }

                    minMaxBuffer.WriteToBuffer(&drawCall.elevationMinMax);

                    var descriptorWriter = new DescriptorWriter(mat.MaterialDescriptorLayout, frameInfo.FrameDescriptorPool)

                    .WriteBuffer(0, minMaxBuffer.DescriptorInfo())
                    .WriteImage(1, Texture2d.GetTextureImageInfoAtIndex(drawCall.TextureIndex));


                    VkDescriptorSet descriptorSet = new();
                    if (!descriptorWriter.Build(&descriptorSet))
                    {
                        throw new Exception("Failed to build descriptor set");
                    }

                    Vulkan.vkCmdBindDescriptorSets(
                                    frameInfo.CommandBuffer,
                                    VkPipelineBindPoint.Graphics,
                                    mat.PipeLineLayout,
                                    1,  // starting set (0 is the globalDescriptorSet, 1 is the set specific to this system)
                                    descriptorSet);

                    mat?.BindAndDraw(frameInfo, drawCall.MeshIndex, new SimplePushConstantData(drawCall.Ltw));
                }
            }
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _renderQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            base.OnDestroy(entityManager);
            minMaxBuffer.Dispose();
        }

        public struct DrawCall : IComparer<DrawCall>
        {
            public int MeshIndex;
            public int TextureIndex;
            public int MaterialIndex;
            public Vector2 elevationMinMax;
            public Matrix4x4 Ltw;

            public readonly int Compare(DrawCall x, DrawCall y)
            {
                if (x.MaterialIndex.CompareTo(y.MaterialIndex) != 0)
                {
                    return x.MaterialIndex.CompareTo(y.MaterialIndex);
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}
