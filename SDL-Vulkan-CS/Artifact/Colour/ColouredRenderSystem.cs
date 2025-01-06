using SDL_Vulkan_CS.ECS;
using SDL_Vulkan_CS.ECS.Presentation;
using SDL_Vulkan_CS.VulkanBackend;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Colour
{
    public class ColouredRenderSystem : PresentationSystemBase
    {
        private EntityQuery _planetRenderQuery;
        private CsharpVulkanBuffer<PlanetTileShaderParmeters>[] _shaderParamBuffers;

        /// <summary>
        /// query setup, also creates the shader params buffer.
        /// </summary>
        /// <param name="entityManager"></param>
        public unsafe override void OnCreate(EntityManager entityManager)
        {
            _shaderParamBuffers = new CsharpVulkanBuffer<PlanetTileShaderParmeters>[SwapChain.MAX_FRAMES_IN_FLIGHT];
            for (int i = 0; i < _shaderParamBuffers.Length; i++)
            {
                _shaderParamBuffers[i] = new(GraphicsDevice.Instance, (uint)sizeof(PlanetTileShaderParmeters), 1, VkBufferUsageFlags.UniformBuffer, true);
            }

            _planetRenderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(Children),typeof(PlanetPropeties),typeof(LocalToWorld),typeof(MaterialIndex))
                .WithNone(typeof(DoNotRender), typeof(Prefab))
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
        public unsafe override void OnFowardPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (_planetRenderQuery.HasEntities)
            {
                Material mat = null;
                Matrix4x4 camLTW = Matrix4x4.Identity;
                if(entityManager.SingletonEntity<Camera>(out var camEntity))
                {
                    camLTW = entityManager.GetComponent<LocalToWorld>(camEntity).Value;
                }
                var shaderParams = _shaderParamBuffers[frameInfo.FrameIndex];
                _planetRenderQuery.GetEntities().ForEach(e =>
                {
                    var material = entityManager.GetComponent<MaterialIndex>(e);

                    var drawCalls = CreatePlanetDrawCalls(entityManager, e,camLTW);


                    if (drawCalls == null || drawCalls.Count == 0) return;

                    var curMat = Material.GetMaterialAtIndex(material.Value);
                    if (curMat == null) return;
                    
                    if (mat == null || mat != curMat)
                    {
                        mat = curMat;
                        curMat.BindGlobalDescriptorSet(frameInfo);
                    }
                    var planetProperties = entityManager.GetComponent<PlanetPropeties>(e);
                    planetProperties.WriteShaderParamters(shaderParams);

                    VkDescriptorSet descriptorSet = new();
                    WriteDescriptorSet(frameInfo, curMat,shaderParams, planetProperties, ref descriptorSet);

                    Vulkan.vkCmdBindDescriptorSets(
                        frameInfo.CommandBuffer,
                        VkPipelineBindPoint.Graphics,
                        curMat.PipeLineLayout,
                        1,  // starting set (0 is the globalDescriptorSet, 1 is the set specific to this system)
                        descriptorSet);

                    drawCalls.ForEach(draw => curMat.BindAndDraw(frameInfo, draw.MeshIndex, new ModelPushConstantData(draw.Ltw)));
                });
            }
        }

        private static List<PlanetTileDrawCall> CreatePlanetDrawCalls(EntityManager entityManager, Entity planetRoot, Matrix4x4 camLTW)
        {
            var children = entityManager.GetComponent<Children>(planetRoot);
            if(children.Value == null) { return null; }

            List<PlanetTileDrawCall> drawCalls = new (children.Value.Length);

            for(int i = 0; i < children.Value.Length; i++)
            {
                if (!entityManager.HasComponent<TileNormalVector>(children.Value[i], out int signature)) {  continue; }
                LocalToWorld ltw = entityManager.GetComponent<LocalToWorld>(children.Value[i]);
                Vector3 toCamera = Vector3.Normalize(camLTW.Translation - ltw.Value.Translation);

                Vector3 forward = -entityManager.GetComponent<TileNormalVector>(signature).Value;
                forward = Vector3.TransformNormal(forward, ltw.Value);
                

                if (NumericsExtensions.Angle(forward, toCamera) > 100)
                {
                    drawCalls.Add(new(entityManager.GetComponent<MeshIndex>(children.Value[i]), ltw));
                }
            }

            return drawCalls;
        }

        /// <summary>
        /// Writes to the descriptor set with the given textures and shader parameters
        /// </summary>
        /// <param name="frameInfo"></param>
        /// <param name="mat"></param>
        /// <param name="textures"></param>
        /// <param name="descriptorSet"></param>
        private unsafe void WriteDescriptorSet(RendererFrameInfo frameInfo, Material mat, CsharpVulkanBuffer<PlanetTileShaderParmeters> shaderParams, PlanetPropeties textures, ref VkDescriptorSet descriptorSet)
        {
            fixed (VkDescriptorSet* pSet = &descriptorSet)
            {
                new DescriptorWriter(mat.MaterialDescriptorLayout, frameInfo.FrameDescriptorPool)
                .WriteBufferCached(0, shaderParams.DescriptorInfo())
                .WriteImageCached(1, Texture2d.GetTextureImageInfoAtIndex(textures.ColourTexture))
                .WriteImageCached(2, Texture2d.GetTextureImageInfoAtIndex(textures.SteepTexture))
                .WriteImageCached(3, Texture2d.GetTextureImageInfoAtIndex(textures.TextureArrayIndex))
                .WriteImageCached(4, Texture2d.GetTextureImageInfoAtIndex(textures.WaveA))
                .WriteImageCached(5, Texture2d.GetTextureImageInfoAtIndex(textures.WaveB))
                .WriteImageCached(6, Texture2d.GetTextureImageInfoAtIndex(textures.WaveC)).Build(pSet);
            }
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _planetRenderQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            base.OnDestroy(entityManager);

            for (int i = 0; i < _shaderParamBuffers.Length; i++)
            {
                _shaderParamBuffers[i]?.Dispose();
            }

        }

        /// <summary>
        /// Contains the data needed to draw a planet tile.
        /// </summary>
        private struct PlanetTileDrawCall
        {
            public int MeshIndex;
            public Matrix4x4 Ltw;

            public PlanetTileDrawCall(MeshIndex meshIndex, LocalToWorld ltw)
            {
                MeshIndex = meshIndex.Value;
                Ltw = ltw.Value;
            }
        }
    }
}
