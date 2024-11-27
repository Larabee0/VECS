using SDL_Vulkan_CS.VulkanBackend;
using System.Collections.Generic;
using System.Numerics;

namespace SDL_Vulkan_CS.ECS.Presentation
{
    /// <summary>
    /// Relatively generic render system that will operate on all materials
    /// 
    /// This expects all materials will have one texture and accept a push constant of <see cref="SimplePushConstantData"/>
    /// for the model local to world matrix.
    /// </summary>
    public class TexturedRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderQuery;

        public TexturedRenderSystem() : base() { }

        public override void OnCreate(EntityManager entityManager)
        {
            _renderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(MeshIndex), typeof(TextureIndex), typeof(MaterialIndex), typeof(LocalToWorld))
                .Build();
        }

        /// <summary>
        /// Called by <see cref="Presenter"/> to draw all the entities
        /// </summary>
        /// <param name="entityManager"></param>
        /// <param name="frameInfo"></param>
        public unsafe override void OnPresent(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (_renderQuery.HasEntities)
            {
                // get all data from the entities
                List<Entity> entities = _renderQuery.GetEntities();
                List<TexturedDrawCall> drawCalls = new(entities.Count);
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


                // to mimise material binding, the entities are sorted by material
                // this allows all entities of the same material to share a BindDescriptorSets operation
                drawCalls.Sort(new TexturedDrawCall());

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
                        mat?.BindDescriptorSets(frameInfo);
                    }
                    mat?.BindAndDraw(frameInfo, drawCall.MeshIndex, new SimplePushConstantData(drawCall.Ltw), drawCall.TextureIndex);
                }
            }
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _renderQuery.MarkStale();
        }

        /// <summary>
        /// contains the information needed to draw a mesh with a given texture, material and matrix
        /// </summary>
        public struct TexturedDrawCall : IComparer<TexturedDrawCall>
        {
            public int MeshIndex;
            public int TextureIndex;
            public int MaterialIndex;
            public Matrix4x4 Ltw;

            public readonly int Compare(TexturedDrawCall x, TexturedDrawCall y)
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
