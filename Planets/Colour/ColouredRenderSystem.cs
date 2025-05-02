using System.Collections.Generic;
using System.Numerics;
using Vortice.Vulkan;
using VECS;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using System;

namespace Planets.Colour
{
    public class ColouredRenderSystem : PresentationSystemBase
    {
        public const ulong MAX_INDIRECT_COMMANDS = 1000;
        private EntityQuery _planetRenderQuery;
        private SwapChainBuffer<PlanetTileShaderParmeters> _shaderParamBuffers;
        private SwapChainBuffer<ObjectData> _objectDataBuffers;

        /// <summary>
        /// query setup, also creates the shader params buffer.
        /// </summary>
        /// <param name="entityManager"></param>
        public unsafe override void OnCreate(EntityManager entityManager)
        {
            _shaderParamBuffers = new((uint)sizeof(PlanetTileShaderParmeters), 1, VkBufferUsageFlags.UniformBuffer, true);
            CreateIndirectCmdBuffers();

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
                int drawIndex = 0;
                _planetRenderQuery.GetEntities().ForEach(e =>
                {
                    var material = entityManager.GetComponent<MaterialIndex>(e);
                    int originalDrawIndex = drawIndex;
                    var directMesh = CreatePlanetDrawCalls(ref drawIndex, frameInfo, entityManager, e, camLTW);

                    if (drawIndex == originalDrawIndex) return;

                    var curMat = Material.GetMaterialAtIndex(material.Value);
                    if (curMat == null) return;
                    
                    if (mat == null || mat != curMat)
                    {
                        mat = curMat;
                        curMat.BindGlobalDescriptorSet(frameInfo);
                    }
                    var planetProperties = entityManager.GetComponent<PlanetPropeties>(e);
                    planetProperties.WriteShaderParamters(_shaderParamBuffers.ActiveGPUBuffer);

                    VkDescriptorSet descriptorSet = new();
                    WriteDescriptorSet(frameInfo, curMat, planetProperties, ref descriptorSet);

                    Vulkan.vkCmdBindDescriptorSets(
                        frameInfo.CommandBuffer,
                        VkPipelineBindPoint.Graphics,
                        curMat.PipeLineLayout,
                        1,  // starting set (0 is the globalDescriptorSet, 1 is the set specific to this system)
                        descriptorSet);
                    _objectDataBuffers.WriteFromHostToActiveBuffer();
                    directMesh.BindBuffers(frameInfo.CommandBuffer);
                    //directMesh.DrawIndirect(frameInfo.CommandBuffer);
                });
            }
        }

        private DirectMesh CreatePlanetDrawCalls(ref int indirectWriteIndex,RendererFrameInfo frameInfo, EntityManager entityManager, Entity planetRoot, Matrix4x4 camLTW)
        {
            var children = entityManager.GetComponent<Children>(planetRoot);
            if (children.Value.Length > 0)
            {
                var mesh = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(children.Value[0])).DirectMeshBuffer;
                Span<VkDrawIndexedIndirectCommand> drawCmds = Span<VkDrawIndexedIndirectCommand>.Empty;// mesh.IndirectDrawBuffer.HostBuffer;
                var objData = _objectDataBuffers.HostBuffer;

                for (int i = 0; i < children.Value.Length; i++)
                {
                    if (!entityManager.HasComponent<TileNormalVector>(children.Value[i], out int signature)) { continue; }
                    LocalToWorld ltw = entityManager.GetComponent<LocalToWorld>(children.Value[i]);
                    Vector3 toCamera = Vector3.Normalize(camLTW.Translation - ltw.Value.Translation);

                    Vector3 forward = -entityManager.GetComponent<TileNormalVector>(signature).Value;
                    forward = Vector3.TransformNormal(forward, ltw.Value);

                    var subMesh = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(children.Value[i]));
                    drawCmds[indirectWriteIndex] = subMesh.IndirectCommand;
                    objData[indirectWriteIndex] = new(ltw.Value, new(subMesh.Bounds.Bounds.center, subMesh.Bounds.Radius), new(subMesh.Bounds.Bounds.extents, subMesh.Bounds.Valid ? 1 : 0));

                    indirectWriteIndex++;
                    if (NumericsExtensions.Angle(forward, toCamera) > 100)
                    {

                    }
                }
                return mesh;
            }
            return null;
        }

        /// <summary>
        /// Writes to the descriptor set with the given textures and shader parameters
        /// </summary>
        /// <param name="frameInfo"></param>
        /// <param name="mat"></param>
        /// <param name="textures"></param>
        /// <param name="descriptorSet"></param>
        private unsafe void WriteDescriptorSet(RendererFrameInfo frameInfo, Material mat, PlanetPropeties textures, ref VkDescriptorSet descriptorSet)
        {
            fixed (VkDescriptorSet* pSet = &descriptorSet)
            {
                new DescriptorWriter(mat.MaterialDescriptorLayout, frameInfo.EntityDescriptorPool)
                .WriteBuffer(0, _shaderParamBuffers.ActiveDescriptorInfo())
                .WriteImage(1, Texture2d.GetTextureImageInfoAtIndex(textures.ColourTexture))
                .WriteImage(2, Texture2d.GetTextureImageInfoAtIndex(textures.SteepTexture))
                .WriteImage(3, Texture2d.GetTextureImageInfoAtIndex(textures.TextureArrayIndex))
                .WriteImage(4, Texture2d.GetTextureImageInfoAtIndex(textures.WaveA))
                .WriteImage(5, Texture2d.GetTextureImageInfoAtIndex(textures.WaveB))
                .WriteImage(6, Texture2d.GetTextureImageInfoAtIndex(textures.WaveC))
                .WriteBuffer(7, _objectDataBuffers.ActiveDescriptorInfo())
                .Build(pSet);
            }
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            base.OnDestroy(entityManager);

            _objectDataBuffers?.Dispose();
            _shaderParamBuffers?.Dispose();
        }


        private void CreateIndirectCmdBuffers()
        {
            _objectDataBuffers = new(MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.StorageBuffer,
                    true);

            VkCommandBuffer commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            _objectDataBuffers.FillAllBuffers(commandBuffer, 0);

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }
    }
}
