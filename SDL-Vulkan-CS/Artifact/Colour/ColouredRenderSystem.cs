using SDL_Vulkan_CS.ECS;
using SDL_Vulkan_CS.ECS.Presentation;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private EntityQuery _shaderPropertyQuery;
        CsharpVulkanBuffer shareParams;
        public unsafe override void OnCreate(EntityManager entityManager)
        {
             shareParams = new(GraphicsDevice.Instance, (uint)sizeof(float)*7, 1, VkBufferUsageFlags.UniformBuffer, true);
            _renderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(MeshIndex), typeof(MaterialIndex), typeof(LocalToWorld),typeof(ElevationMinMax))
                .Build();

            _shaderPropertyQuery = new EntityQuery(entityManager)
                .WithAll(typeof(TerrainShaderProperties))
                .Build();
        }

        public unsafe override void OnPresent(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (_renderQuery.HasEntities && _shaderPropertyQuery.HasEntities)
            {
                List<Entity> propertyEntities = _shaderPropertyQuery.GetEntities();
                List<Entity> entities = _renderQuery.GetEntities();
                List<PlanetTileDrawCall> drawCalls = new(entities.Count);

                var properties = entityManager.GetComponent<TerrainShaderProperties>(propertyEntities[0]);
                entities.ForEach(e =>
                {
                    drawCalls.Add(new()
                    {
                        ShaderProperties = properties,
                        MeshIndex = entityManager.GetComponent<MeshIndex>(e).Value,
                        MaterialIndex = entityManager.GetComponent<MaterialIndex>(e).Value,
                        Ltw = entityManager.GetComponent<LocalToWorld>(e).Value,
                        elevationMinMax = entityManager.GetComponent<ElevationMinMax>(e).Value
                    });
                });


                drawCalls.Sort(new PlanetTileDrawCall());


                float* pParams = stackalloc float[]
                {
                    float.MaxValue,
                    float.MinValue,
                    MathF.Sin(Application.TimeSinceStart),
                    MathF.Cos(Application.TimeSinceStart),
                    Texture2d.GetTextureAtIndex(properties.TextureArrayIndex).ImageExtent.depth,
                    3f,
                    5f
                };
                Material mat = null;
                VkDescriptorSet descriptorSet = default;
                for (int i = 0; i < drawCalls.Count; i++)
                {
                    var drawCall = drawCalls[i];
                    var curMat = Material.GetMaterialAtIndex(drawCall.MaterialIndex);

                    // if mat is null or different from the last mat, it needs its descriptor sets bound
                    if (mat == null || mat != curMat)
                    {
                        mat = curMat;
                        mat?.BindGlobalDescriptorSet(frameInfo);
                        descriptorSet = new();
                        CreateDescriptorSet(frameInfo, mat, drawCall, ref descriptorSet);
                    }

                    // if (!descriptorWriter.Build(&descriptorSet))
                    // {
                    //     throw new Exception("Failed to build descriptor set");
                    // }

                    mat = Draw(frameInfo, pParams, mat, drawCall, ref descriptorSet);
                }
            }
        }

        private unsafe void CreateDescriptorSet(RendererFrameInfo frameInfo, Material mat, PlanetTileDrawCall drawCall, ref VkDescriptorSet descriptorSet)
        {
            fixed (VkDescriptorSet* pSet = &descriptorSet)
            {
                new DescriptorWriter(mat.MaterialDescriptorLayout, frameInfo.FrameDescriptorPool)
                .WriteBufferCached(0, shareParams.DescriptorInfo())
                .WriteImageCached(1, Texture2d.GetTextureImageInfoAtIndex(drawCall.ColourTexture))
                .WriteImageCached(2, Texture2d.GetTextureImageInfoAtIndex(drawCall.SteepTexture))
                .WriteImageCached(3, Texture2d.GetTextureImageInfoAtIndex(drawCall.TextureArrayIndex))
                .WriteImageCached(4, Texture2d.GetTextureImageInfoAtIndex(drawCall.WaveA))
                .WriteImageCached(5, Texture2d.GetTextureImageInfoAtIndex(drawCall.WaveB))
                .WriteImageCached(6, Texture2d.GetTextureImageInfoAtIndex(drawCall.WaveC)).Build(pSet);
            }
        }

        private unsafe Material Draw(RendererFrameInfo frameInfo, float* pParams, Material mat, PlanetTileDrawCall drawCall, ref VkDescriptorSet descriptorSet)
        {
            
            if (pParams[0] != drawCall.elevationMinMax.X|| pParams[1] != drawCall.elevationMinMax.Y)
            {
                pParams[0] = drawCall.elevationMinMax.X;
                pParams[1] = drawCall.elevationMinMax.Y;
                shareParams.WriteToBuffer(pParams);
            }

            


            Vulkan.vkCmdBindDescriptorSets(
                frameInfo.CommandBuffer,
                VkPipelineBindPoint.Graphics,
                mat.PipeLineLayout,
                1,  // starting set (0 is the globalDescriptorSet, 1 is the set specific to this system)
                descriptorSet);

            mat?.BindAndDraw(frameInfo, drawCall.MeshIndex, new SimplePushConstantData(drawCall.Ltw));
            return mat;
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _renderQuery.MarkStale();
            _shaderPropertyQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            base.OnDestroy(entityManager);
            shareParams.Dispose();
        }

        public struct PlanetTileDrawCall : IComparer<PlanetTileDrawCall>
        {
            public int MeshIndex;
            public TerrainShaderProperties ShaderProperties;
            public int ColourTexture=> ShaderProperties.ColourTexture;
            public int SteepTexture=> ShaderProperties.SteepTexture;
            public int WaveA=>ShaderProperties.WaveA;
            public int WaveB=>ShaderProperties.WaveB;
            public int WaveC=> ShaderProperties.WaveC;
            public int TextureArrayIndex=> ShaderProperties.TextureArrayIndex;
            public int MaterialIndex;
            public Vector2 elevationMinMax;
            public Matrix4x4 Ltw;

            public readonly int Compare(PlanetTileDrawCall x, PlanetTileDrawCall y)
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
