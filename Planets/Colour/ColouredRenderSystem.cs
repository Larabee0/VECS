using System.Collections.Generic;
using System.Numerics;
using Vortice.Vulkan;
using VECS;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;

namespace Planets.Colour
{
    public class ColouredRenderSystem : PresentationSystemBase
    {
        public const ulong MAX_INDIRECT_COMMANDS = 1000;
        private EntityQuery _planetRenderQuery;
        private GPUBuffer<PlanetTileShaderParmeters>[] _shaderParamBuffers;
        private GPUBuffer<ObjectData>[] _objectDataBuffers;
        private GPUBuffer<VkDrawIndexedIndirectCommand>[] _indirectCmdBuffers;

        /// <summary>
        /// query setup, also creates the shader params buffer.
        /// </summary>
        /// <param name="entityManager"></param>
        public unsafe override void OnCreate(EntityManager entityManager)
        {
            _shaderParamBuffers = new GPUBuffer<PlanetTileShaderParmeters>[SwapChain.MAX_FRAMES_IN_FLIGHT];
            CreateIndirectCmdBuffers();
            for (int i = 0; i < _shaderParamBuffers.Length; i++)
            {
                _shaderParamBuffers[i] = new((uint)sizeof(PlanetTileShaderParmeters), 1, VkBufferUsageFlags.UniformBuffer, true);
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
                //List<PlanetTileDrawCall> drawCalls = [];
                int drawIndex = 0;
                _planetRenderQuery.GetEntities().ForEach(e =>
                {
                    var material = entityManager.GetComponent<MaterialIndex>(e);
                    int originalDrawIndex = drawIndex;
                    CreatePlanetDrawCalls(ref drawIndex, frameInfo, entityManager, e, camLTW);

                    if (drawIndex == originalDrawIndex) return;

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
                    var buffer = _indirectCmdBuffers[frameInfo.FrameIndex];
                    buffer.WriteFromHostBuffer();
                    _objectDataBuffers[frameInfo.FrameIndex].WriteFromHostBuffer();
                    Vulkan.vkCmdDrawIndexedIndirect(frameInfo.CommandBuffer,
                        buffer.VkBuffer,
                        0,
                        buffer.UInstanceCount32,
                        (uint)sizeof(VkDrawIndexedIndirectCommand));
                });
            }
        }

        private void CreatePlanetDrawCalls(ref int indirectWriteIndex,RendererFrameInfo frameInfo, EntityManager entityManager, Entity planetRoot, Matrix4x4 camLTW)
        {
            var children = entityManager.GetComponent<Children>(planetRoot);

            var drawCmds = _indirectCmdBuffers[frameInfo.FrameIndex].HostBuffer;
            var objData = _objectDataBuffers[frameInfo.FrameIndex].HostBuffer;

            for (int i = 0; i < children.Value.Length; i++)
            {
                if (!entityManager.HasComponent<TileNormalVector>(children.Value[i], out int signature)) {  continue; }
                LocalToWorld ltw = entityManager.GetComponent<LocalToWorld>(children.Value[i]);
                Vector3 toCamera = Vector3.Normalize(camLTW.Translation - ltw.Value.Translation);

                Vector3 forward = -entityManager.GetComponent<TileNormalVector>(signature).Value;
                forward = Vector3.TransformNormal(forward, ltw.Value);

                var subMesh = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(children.Value[i]));
                drawCmds[indirectWriteIndex] = subMesh.IndirectCommand;
                subMesh.DirectMeshBuffer.BindBuffers(frameInfo.CommandBuffer);
                objData[indirectWriteIndex] = new(ltw.Value, new(subMesh.Bounds.Bounds.center, subMesh.Bounds.Radius), new(subMesh.Bounds.Bounds.extents, subMesh.Bounds.Valid ? 1 : 0));

                indirectWriteIndex++;
                if (NumericsExtensions.Angle(forward, toCamera) > 100)
                {
                    
                }
            }

        }

        /// <summary>
        /// Writes to the descriptor set with the given textures and shader parameters
        /// </summary>
        /// <param name="frameInfo"></param>
        /// <param name="mat"></param>
        /// <param name="textures"></param>
        /// <param name="descriptorSet"></param>
        private unsafe void WriteDescriptorSet(RendererFrameInfo frameInfo, Material mat, GPUBuffer<PlanetTileShaderParmeters> shaderParams, PlanetPropeties textures, ref VkDescriptorSet descriptorSet)
        {
            fixed (VkDescriptorSet* pSet = &descriptorSet)
            {
                new DescriptorWriter(mat.MaterialDescriptorLayout, frameInfo.FrameDescriptorPool)
                .WriteBuffer(0, shaderParams.DescriptorInfo())
                .WriteImage(1, Texture2d.GetTextureImageInfoAtIndex(textures.ColourTexture))
                .WriteImage(2, Texture2d.GetTextureImageInfoAtIndex(textures.SteepTexture))
                .WriteImage(3, Texture2d.GetTextureImageInfoAtIndex(textures.TextureArrayIndex))
                .WriteImage(4, Texture2d.GetTextureImageInfoAtIndex(textures.WaveA))
                .WriteImage(5, Texture2d.GetTextureImageInfoAtIndex(textures.WaveB))
                .WriteImage(6, Texture2d.GetTextureImageInfoAtIndex(textures.WaveC))
                .WriteBuffer(7, _objectDataBuffers[frameInfo.FrameIndex].DescriptorInfo())
                .Build(pSet);

            }
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            //_planetRenderQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            base.OnDestroy(entityManager);

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _shaderParamBuffers[i]?.Dispose();
                _indirectCmdBuffers[i].Dispose();
                _objectDataBuffers[i].Dispose();
            }

        }


        private void CreateIndirectCmdBuffers()
        {
            _indirectCmdBuffers = new GPUBuffer<VkDrawIndexedIndirectCommand>[SwapChain.MAX_FRAMES_IN_FLIGHT];
            _objectDataBuffers = new GPUBuffer<ObjectData>[SwapChain.MAX_FRAMES_IN_FLIGHT];

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i] = new(MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
                _objectDataBuffers[i] = new(MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            }

            VkCommandBuffer commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i].FillBuffer(commandBuffer, 0);
                _objectDataBuffers[i].FillBuffer(commandBuffer, 0);
            }

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }
    }
}
