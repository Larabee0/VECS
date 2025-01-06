using SDL_Vulkan_CS.VulkanBackend;
using System.Collections.Generic;
using System.Numerics;

namespace SDL_Vulkan_CS.ECS.Presentation
{
    public class TexturelessRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderQuery;
        public TexturelessRenderSystem() : base() { }

        public override void OnCreate(EntityManager entityManager)
        {
            _renderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(MeshIndex), typeof(MaterialIndex), typeof(LocalToWorld))
                .Build();
        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (_renderQuery.HasEntities)
            {
                // get all data from the entities
                List<Entity> entities = _renderQuery.GetEntities();
                List<DrawCall> drawCalls = new(entities.Count);
                entities.ForEach(e =>
                {
                    drawCalls.Add(new()
                    {
                        MeshIndex = entityManager.GetComponent<MeshIndex>(e).Value,
                        MaterialIndex = entityManager.GetComponent<MaterialIndex>(e).Value,
                        Ltw = entityManager.GetComponent<LocalToWorld>(e).Value
                    });
                });


                // to mimise material binding, the entities are sorted by material
                // this allows all entities of the same material to share a BindDescriptorSets operation
                drawCalls.Sort(new DrawCall());

                // draw each entity in material order.
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
                    mat?.BindAndDraw(frameInfo, drawCall.MeshIndex, new ModelPushConstantData(drawCall.Ltw));
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
            public int MaterialIndex;
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
