using SDL_Vulkan_CS.VulkanBackend;
using SDL3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.ECS.Presentation.Systems
{
    public class SimpleRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderQuery;

        public SimpleRenderSystem() : base() { }
        public SimpleRenderSystem(GraphicsDevice device, VkRenderPass renderPass, VkDescriptorSetLayout globalSetLayout) : base(device, renderPass, globalSetLayout) { }

        public override void OnCreate(EntityManager entityManager)
        {
            _renderQuery = new EntityQuery(entityManager).WithAll(typeof(MeshIndex), typeof(TextureIndex), typeof(MaterialIndex), typeof(LocalToWorld)).Build();   
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
                    var curMat = Material.GetMaterialAtIndex (drawCall.MaterialIndex);
                    if(mat == null || mat != curMat)
                    {
                        mat = curMat;
                        mat?.BindDescriptorSets(frameInfo);
                    }
                    mat?.BindMaterial(frameInfo, drawCall.MeshIndex, new SimplePushConstantData(drawCall.Ltw), drawCall.TextureIndex);
                }
            }
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _renderQuery.MarkStale();
        }

        public struct DrawCall : IComparer<DrawCall>
        {
            public int MeshIndex;
            public int TextureIndex;
            public int MaterialIndex;
            public Matrix4x4 Ltw;

            public int Compare(DrawCall x, DrawCall y)
            {
                if(x.MaterialIndex.CompareTo(y.MaterialIndex) != 0)
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
