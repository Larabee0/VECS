using SDL_Vulkan_CS.ECS;
using SDL_Vulkan_CS.ECS.Presentation;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Colour
{
    public class ColouredRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderQuery;
        private EntityQuery _shaderPropertyQuery;
        private CsharpVulkanBuffer _shaderParams;

        /// <summary>
        /// query setup, also creates the shader params buffer.
        /// </summary>
        /// <param name="entityManager"></param>
        public unsafe override void OnCreate(EntityManager entityManager)
        {
            _shaderParams = new(GraphicsDevice.Instance, (uint)sizeof(PlanetTileShaderParmeters), 1, VkBufferUsageFlags.UniformBuffer, true);

            _renderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(MeshIndex), typeof(MaterialIndex), typeof(LocalToWorld), typeof(ElevationMinMax))
                .WithNone(typeof(DoNotRender))
                .Build();

            _shaderPropertyQuery = new EntityQuery(entityManager)
                .WithAll(typeof(TerrainShaderTextures))
                .WithNone(typeof(DoNotRender))
                .Build();
        }

        /// <summary>
        ///  This is all a little bit hard coded for 1 planet as TerrainShaderTextures is expected as a singleton component.
        ///  
        ///  ### Improvements ###
        ///  - Create a transform hierarchy
        ///  - Each planet root entity would has shader paramters and other parameters (scale and ocean brightness)
        ///  - Draw calls created by querying parent entities and drawing all their children in one batch
        ///  - Tile culling based on tile local up vector vs camera forward vector difference threshold
        /// </summary>
        /// <param name="entityManager"></param>
        /// <param name="frameInfo"></param>
        public unsafe override void OnPresent(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (_renderQuery.HasEntities && _shaderPropertyQuery.HasEntities)
            {

                entityManager.SingletonComponent<TerrainShaderTextures>(out var properties);

                List<PlanetTileDrawCall> drawCalls = CreateDrawCalls(entityManager, properties);

                PlanetTileShaderParmeters shaderParameters = new()
                {
                    ElevationMin = float.MaxValue,
                    ElevationMax = float.MinValue,
                    SineTime = MathF.Sin(Application.TimeSinceStart),
                    CosineTime = MathF.Cos(Application.TimeSinceStart),
                    TextureCount = Texture2d.GetTextureAtIndex(properties.TextureArrayIndex).ImageExtent.depth,
                    TerrainScale = 3f,
                    OceanBrightness = 5f
                };

                Material mat = null;
                VkDescriptorSet descriptorSet = default;

                drawCalls.ForEach(drawCall =>
                {
                    var curMat = Material.GetMaterialAtIndex(drawCall.MaterialIndex);

                    // if mat is null or different from the last mat, it needs descriptor sets must be bound.
                    if (mat == null || mat != curMat)
                    {
                        mat = curMat;
                        mat?.BindGlobalDescriptorSet(frameInfo);
                        descriptorSet = new();
                        WriteDescriptorSet(frameInfo, mat, drawCall, ref descriptorSet);
                    }

                    mat = DrawTile(frameInfo, ref shaderParameters, mat, drawCall, ref descriptorSet);
                });
            }
        }

        private List<PlanetTileDrawCall> CreateDrawCalls(EntityManager entityManager,TerrainShaderTextures properties)
        {
            List<Entity> entities = _renderQuery.GetEntities();
            List<PlanetTileDrawCall> drawCalls = new(entities.Count);
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

            return drawCalls;
        }

        /// <summary>
        /// Writes to the descriptor set with the given textures and shader parameters
        /// </summary>
        /// <param name="frameInfo"></param>
        /// <param name="mat"></param>
        /// <param name="drawCall"></param>
        /// <param name="descriptorSet"></param>
        private unsafe void WriteDescriptorSet(RendererFrameInfo frameInfo, Material mat, PlanetTileDrawCall drawCall, ref VkDescriptorSet descriptorSet)
        {
            fixed (VkDescriptorSet* pSet = &descriptorSet)
            {
                new DescriptorWriter(mat.MaterialDescriptorLayout, frameInfo.FrameDescriptorPool)
                .WriteBufferCached(0, _shaderParams.DescriptorInfo())
                .WriteImageCached(1, Texture2d.GetTextureImageInfoAtIndex(drawCall.ColourTexture))
                .WriteImageCached(2, Texture2d.GetTextureImageInfoAtIndex(drawCall.SteepTexture))
                .WriteImageCached(3, Texture2d.GetTextureImageInfoAtIndex(drawCall.TextureArrayIndex))
                .WriteImageCached(4, Texture2d.GetTextureImageInfoAtIndex(drawCall.WaveA))
                .WriteImageCached(5, Texture2d.GetTextureImageInfoAtIndex(drawCall.WaveB))
                .WriteImageCached(6, Texture2d.GetTextureImageInfoAtIndex(drawCall.WaveC)).Build(pSet);
            }
        }

        /// <summary>
        ///  draw tile mesh, overwriting elevation minmax if required
        /// </summary>
        /// <param name="frameInfo"></param>
        /// <param name="shaderParameters"></param>
        /// <param name="mat"></param>
        /// <param name="drawCall"></param>
        /// <param name="descriptorSet"></param>
        /// <returns></returns>
        private unsafe Material DrawTile(RendererFrameInfo frameInfo, ref PlanetTileShaderParmeters shaderParameters, Material mat, PlanetTileDrawCall drawCall, ref VkDescriptorSet descriptorSet)
        {
            // overwrite MinMax if different.
            if (shaderParameters.ElevationMinMax != drawCall.elevationMinMax)
            {
                shaderParameters.UpdateMinMax(drawCall.elevationMinMax);
                fixed (PlanetTileShaderParmeters* pShaderParameters = &shaderParameters)
                {
                    _shaderParams.WriteToBuffer(pShaderParameters);
                }
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
            _shaderParams?.Dispose();
        }

        /// <summary>
        /// This is mostly hardcoded for one planet.
        /// Contains the data needed to draw a planet tile.
        /// </summary>
        public struct PlanetTileDrawCall : IComparer<PlanetTileDrawCall>
        {
            public int MeshIndex;
            public int MaterialIndex;
            public Vector2 elevationMinMax;
            public Matrix4x4 Ltw;
            public TerrainShaderTextures ShaderProperties;

            public readonly int ColourTexture => ShaderProperties.ColourTexture;
            public readonly int SteepTexture => ShaderProperties.SteepTexture;
            public readonly int WaveA => ShaderProperties.WaveA;
            public readonly int WaveB => ShaderProperties.WaveB;
            public readonly int WaveC => ShaderProperties.WaveC;
            public readonly int TextureArrayIndex => ShaderProperties.TextureArrayIndex;

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

        /// <summary>
        /// Contains the uniform paramters for the planet frag shader.
        /// </summary>
        private struct PlanetTileShaderParmeters
        {
            public float ElevationMin;
            public float ElevationMax;
            public float SineTime;
            public float CosineTime;
            public float TextureCount;
            public float TerrainScale;
            public float OceanBrightness;

            public readonly Vector2 ElevationMinMax => new(ElevationMin, ElevationMax);

            public void UpdateMinMax(Vector2 minMax)
            {
                ElevationMin = minMax.X;
                ElevationMax = minMax.Y;
            }
        }
    }
}
