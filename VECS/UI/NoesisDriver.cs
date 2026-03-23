using Assimp.Unmanaged;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using Noesis;
using System;
using System.Reflection.PortableExecutable;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{
    public class NoesisTexture : Noesis.Texture
    {
        public Texture2D Texture;
        public bool TextureInverted;
        public bool AlphaComponent;

        public override uint Width => (uint)Texture.Width;

        public override uint Height => (uint)Texture.Height;

        public override bool HasMipMaps => Texture.MipMapCount > 1;

        public override bool IsInverted => TextureInverted;

        public override bool HasAlpha => AlphaComponent;

        public NoesisTexture(Texture2D texture, bool inverted, bool alphaComponent)
        {
            Texture = texture;
            TextureInverted = inverted;
            AlphaComponent = alphaComponent;
        }
    }

    public class NoesisRenderTarget : Noesis.RenderTarget
    {
        public NoesisTexture Colour;
        public NoesisTexture ColourAA;
        public NoesisTexture Stencil;
                public uint ColourAttachmentCount => Stencil == null ? 1u : 2u;

        public VkSampleCountFlags samples = VkSampleCountFlags.Count1;

        public override Noesis.Texture Texture => Colour;
    }

    public class NoesisDriver : RenderDevice
    {
        public override DeviceCaps Caps => new()
        {
            LinearRendering = false,
            DepthRangeZeroToOne = true,
            ClipSpaceYInverted = true,
            SubpixelRendering = true,
            CenterPixelOffset = 0
        };

        private Shader

        private readonly SwapChainBuffer _indexBuffer;
        private readonly SwapChainBuffer _vertexBuffer;

        private VkCommandBuffer _currentCommandBuffer;
        private bool mCachedDepthTestEnable;
        private bool mCachedStencilTestEnable;
        private uint mCachedStencilOp;
        private uint mCachedStencilRef;



        public NoesisDriver()
        {
            _indexBuffer = new SwapChainBuffer(sizeof(ushort), 100, VkBufferUsageFlags.IndexBuffer, true);
            _vertexBuffer = new SwapChainBuffer(sizeof(byte), 100, VkBufferUsageFlags.VertexBuffer, true);
        }
        private void CreateLayouts()
        {

            for (Shader.Enum i = 0; i < Shader.Enum.Count; i++)
            {
                ShaderPS pShader = NoesisShaders.ShadersPS(i);
                CreateLayout(pShader.Signature, mLayouts[i]);
            } 
        }

        private void CreateLayouts(uint signature)
        {

            uint buffersVS = 0;
            uint buffersPS = 0;
            uint textures = 0;

            if ((signature & NoesisShaders.VS_CB0 )== NoesisShaders.VS_CB0) buffersVS++;
            if ((signature & NoesisShaders.VS_CB1 )== NoesisShaders.VS_CB1) buffersVS++;
            if ((signature & NoesisShaders.PS_CB0 )== NoesisShaders.PS_CB0) buffersPS++;
            if ((signature & NoesisShaders.PS_CB1 )== NoesisShaders.PS_CB1) buffersPS++;
            if ((signature & NoesisShaders.PS_T0  )== NoesisShaders.PS_T0 ) textures++;
            if ((signature & NoesisShaders.PS_T1  )== NoesisShaders.PS_T1 ) textures++;
            if ((signature & NoesisShaders.PS_T2  )== NoesisShaders.PS_T2 ) textures++;
            if ((signature & NoesisShaders.PS_T3  )== NoesisShaders.PS_T3 ) textures++;
            if ((signature & NoesisShaders.PS_T4 ) == NoesisShaders.PS_T4) textures++;

            uint hash = buffersVS | (buffersPS << 8) | (textures << 16);

        }

        private unsafe void CreateShaders()
        {
            //DotFastLZ.Compression.FastLZ.Buffer(NoesisShaders.Shaders, NoesisShaders.Shaders.Length,)
            uint size = 0;
            fixed(byte* pSize = NoesisShaders.Shaders)
            {
                size = *(uint*)(pSize + 4);
            }

            byte[] decomp = new byte[size];

            DotFastLZ.Compression.FastLZ.Decompress(NoesisShaders.Shaders, NoesisShaders.Shaders.Length, decomp, size);


            for (uint32_t i = 0; i < Shader::Vertex::Count; i++)
            {
                const ShaderVS&vShader = ShadersVS(i, mCaps.linearRendering, mStereoSupport);

                VkShaderModuleCreateInfo createInfo{ }
                ;
                createInfo.sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO;
                createInfo.codeSize = vShader.size;
                createInfo.pCode = (uint32_t*)(shaders + vShader.start);

                V(vkCreateShaderModule(mDevice, &createInfo, nullptr, &mVertexShaders[i]));
                VK_NAME(mVertexShaders[i], SHADER_MODULE, "Noesis_%s", vShader.label);
            }

        }

        public void CleanUpMeshData()
        {
            _indexBuffer.Dispose();
            _vertexBuffer.Dispose();
        }

        public unsafe override void BeginTile(Noesis.RenderTarget surface, Tile tile)
        {
            if(surface is  NoesisRenderTarget renderTarget)
            {

                VkRect2D renderArea;
                renderArea.offset.x = (int)tile.X;
                renderArea.offset.y = (int)renderTarget.Colour.Height - ((int)tile.Y + (int)tile.Height);
                renderArea.extent.width = tile.Width;
                renderArea.extent.height = tile.Height;
                VkRenderingAttachmentInfo stencil = default;

                VkRenderingAttachmentInfo colour = default;

                if (renderTarget.ColourAA != null)
                {
                    colour = new()
                    {
                        imageLayout = renderTarget.ColourAA.Texture.ImageLayout,
                        loadOp = VkAttachmentLoadOp.DontCare,
                        storeOp = VkAttachmentStoreOp.Store,
                        imageView = renderTarget.ColourAA.Texture._imageView,
                        resolveImageLayout = renderTarget.Colour.Texture._imageLayout,
                        resolveImageView = renderTarget.Colour.Texture._imageView,
                        resolveMode = VkResolveModeFlags.None
                    };

                    renderTarget.ColourAA.Texture.SetImageLayout(_currentCommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
                else
                {
                    colour = new()
                    {
                        imageLayout = renderTarget.Colour.Texture.ImageLayout,
                        loadOp = VkAttachmentLoadOp.DontCare,
                        storeOp = VkAttachmentStoreOp.Store,
                        imageView = renderTarget.Colour.Texture._imageView
                    };

                    renderTarget.ColourAA.Texture.SetImageLayout(_currentCommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.ColorAttachmentOutput);
                }

                if (renderTarget.Stencil != null)
                {
                    stencil = new()
                    {
                        loadOp = VkAttachmentLoadOp.DontCare,
                        storeOp = VkAttachmentStoreOp.DontCare,
                        imageLayout = renderTarget.Stencil.Texture.ImageLayout,
                        imageView = renderTarget.Stencil.Texture._imageView
                    };

                    renderTarget.Stencil.Texture.SetImageLayout(_currentCommandBuffer, VkImageLayout.StencilAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
                }

                VkRenderingInfo renderingInfo = new()
                {
                    colorAttachmentCount = 1,
                    pColorAttachments = &colour,
                    pStencilAttachment = renderTarget.Stencil != null  ? &stencil : null,
                    renderArea = renderArea,
                    layerCount = 1,
                    flags = VkRenderingFlags.ContentsInlineKHR
                };

                GraphicsDevice.DeviceAPI.vkCmdBeginRendering(_currentCommandBuffer,&renderingInfo);

                GraphicsDevice.DeviceAPI.vkCmdSetScissor(_currentCommandBuffer, 0, renderArea);
            }
        }

        public override void EndTile(Noesis.RenderTarget surface)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(_currentCommandBuffer);
        }

        public override Noesis.RenderTarget CreateRenderTarget(string label, uint width, uint height, uint sampleCount, bool needsStencil)
        {
            NoesisRenderTarget renderTarget = new()
            {
                samples = GetSampleCount(sampleCount, GraphicsDevice.PropertiesVK10.limits)
            };
            if (needsStencil)
            {
                renderTarget.Stencil = CreateTexture(string.Format("NOESIS_{0}_STENCIL", label),
                    width,
                    height,
                    PreferredFormats.STENCIL_ONLY,
                    VkSampleCountFlags.Count1,
                    VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransientAttachment,
                    VkImageAspectFlags.Stencil);

                renderTarget.Stencil.Texture.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            }

            if(renderTarget.samples > VkSampleCountFlags.Count1)
            {
                renderTarget.ColourAA = CreateTexture(string.Format("NOESIS_{0}_COLOUR_AA", label),
                    width,
                    height,
                    VkFormat.R8G8B8A8Unorm,
                    renderTarget.samples,
                    VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc,
                    VkImageAspectFlags.Color);

                renderTarget.Colour = CreateTexture(string.Format("NOESIS_{0}_COLOUR", label),
                    width,
                    height,
                    VkFormat.R8G8B8A8Unorm,
                    VkSampleCountFlags.Count1,
                    VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
                    VkImageAspectFlags.Color);

                renderTarget.Colour.Texture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);

                renderTarget.ColourAA.Texture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);
            }
            else
            {
                renderTarget.Colour = CreateTexture(string.Format("NOESIS_{0}_COLOUR", label),
                    width,
                    height,
                    VkFormat.R8G8B8A8Unorm,
                    VkSampleCountFlags.Count1,
                    VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc,
                    VkImageAspectFlags.Color);

                renderTarget.Colour.Texture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);
            }

            return renderTarget;
        }

        public override Noesis.RenderTarget CloneRenderTarget(string label, Noesis.RenderTarget surface)
        {
            if(surface is NoesisRenderTarget renderTarget)
            {
                var clonedTarget = new NoesisRenderTarget
                {
                    Stencil = renderTarget.Stencil,
                    ColourAA = renderTarget.ColourAA,
                    samples = renderTarget.samples,
                };

                uint width = renderTarget.Colour.Width;
                uint height = renderTarget.Colour.Height;

                if (clonedTarget.samples > VkSampleCountFlags.Count1)
                {
                    clonedTarget.Colour = CreateTexture(string.Format("NOESIS_{0}_COLOUR_AA", label),
                        width,
                        height,
                        VkFormat.R8G8B8A8Unorm,
                        VkSampleCountFlags.Count1,
                        VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
                        VkImageAspectFlags.Color);

                    clonedTarget.Colour.Texture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);
                }
                else
                {
                    clonedTarget.Colour = CreateTexture(string.Format("NOESIS_{0}_COLOUR", label),
                        width,
                        height,
                        VkFormat.R8G8B8A8Unorm,
                        VkSampleCountFlags.Count1,
                        VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc,
                        VkImageAspectFlags.Color);

                    clonedTarget.Colour.Texture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);
                }

                return clonedTarget;
            }

            return null;
        }

        public override void SetRenderTarget(Noesis.RenderTarget surface)
        {
            if (surface is NoesisRenderTarget renderTarget)
            {
                VkViewport viewport = new(renderTarget.Colour.Width, renderTarget.Colour.Height);
                GraphicsDevice.DeviceAPI.vkCmdSetViewport(_currentCommandBuffer, 0, viewport);
            }
        }

        public unsafe override void ResolveRenderTarget(Noesis.RenderTarget surface, Tile[] tiles)
        {
            if (surface is NoesisRenderTarget renderTarget)
            {

                if (renderTarget.samples > VkSampleCountFlags.Count1)
                {
                    VkImageResolve* regions = stackalloc VkImageResolve[tiles.Length];

                    var src = renderTarget.ColourAA;
                    var dst = renderTarget.Colour;

                    VkImageResolve region;

                    for (uint i = 0; i < tiles.Length; i++)
                    {
                        region = default;
                        region.srcSubresource.aspectMask = VkImageAspectFlags.Color;
                        region.srcSubresource.mipLevel = 0;
                        region.srcSubresource.baseArrayLayer = 0;
                        region.srcSubresource.layerCount = 1;

                        region.srcOffset.x = (int)tiles[i].X;
                        region.srcOffset.y = (int)dst.Height - (int)tiles[i].Y - (int)tiles[i].Height;
                        region.srcOffset.z = 0; ;

                        region.dstSubresource = region.srcSubresource;
                        region.dstOffset = region.srcOffset;

                        region.extent.width = tiles[i].Width;
                        region.extent.height = tiles[i].Height;
                        region.extent.depth = 1;

                        regions[i] = region;
                    }

                    dst.Texture.SetImageLayout(_currentCommandBuffer,
                        VkImageLayout.TransferDstOptimal,
                        VkPipelineStageFlags2.ColorAttachmentOutput,
                        VkPipelineStageFlags2.Transfer);

                    GraphicsDevice.DeviceAPI.vkCmdResolveImage(_currentCommandBuffer,
                        src.Texture._vkImage,
                        VkImageLayout.TransferSrcOptimal,
                        dst.Texture._vkImage,
                        VkImageLayout.TransferDstOptimal,
                        (uint)tiles.Length,
                        regions);

                    dst.Texture.SetImageLayout(_currentCommandBuffer,
                        VkImageLayout.ShaderReadOnlyOptimal,
                        VkPipelineStageFlags2.Transfer,
                        VkPipelineStageFlags2.FragmentShader);
                }
            }
        }

        public unsafe override nint MapIndices(uint bytes)
        {
            if (_indexBuffer.HostBufferSize32 < bytes)
            {
                _indexBuffer.Realloc(bytes);
            }
            GPUBufferExtensions.WriteFromHostDelayed(_indexBuffer, Presenter.Instance.FrameIndex);
            return (nint)_indexBuffer.HostPtr;
        }

        public unsafe override nint MapVertices(uint bytes)
        {
            if (_vertexBuffer.HostBufferSize32 < bytes)
            {
                _vertexBuffer.Realloc(bytes * 2);
            }
            GPUBufferExtensions.WriteFromHostDelayed(_vertexBuffer, Presenter.Instance.FrameIndex);
            return (nint)_vertexBuffer.HostPtr;
        }

        public override void UnmapIndices()
        {

        }

        public override void UnmapVertices()
        {

        }

        public static NoesisTexture CreateTexture(string label, uint width, uint height, VkFormat format, VkSampleCountFlags sampleCountFlags, VkImageUsageFlags usage, VkImageAspectFlags aspectFlags)
        {
            var vkTexture = new Texture2D(label, (int)width, (int)height, format, sampleCountFlags, usage, false);


            vkTexture.CreateImage(vkTexture.GetImageCreateInfo());

            vkTexture.SetImageLayoutAndAspectFromUsage();

            vkTexture.CreateImageView(vkTexture.GetImageViewCreateInfo());

            if (vkTexture._useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                vkTexture.CreateSampler();
            }

            vkTexture.UpdateDescriptor();

            return new(vkTexture, false, format == VkFormat.R8G8B8A8Unorm);
        }

        public unsafe override Noesis.Texture CreateTexture(string label, uint width, uint height, uint numLevels, TextureFormat format, nint data)
        {
            VkFormat vkFormat = format switch
            {
                TextureFormat.RGBA8 => VkFormat.R8G8B8A8Unorm,
                TextureFormat.RGBX8 => VkFormat.R8G8B8Unorm,
                TextureFormat.R8 => VkFormat.R8Unorm,
                _ => throw new NotImplementedException(string.Format("Noesis TextureFormat: {0} not implemented", format.ToString()))
            };
            Texture2D texture = new(label, (int)width, (int)height, vkFormat, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, numLevels > 1);

            if ((void*)data != null)
            {
                ulong blockSize = (uint)vkFormat.BlockSize();
                ulong totalSize = 0;
                ulong[] offsets = new ulong[numLevels];
                VkExtent3D[] extents = new VkExtent3D[numLevels];
                for (uint i = 0; i < numLevels; i++)
                {
                    TextureLoader.CalculateMipLevelSize(width, height, i, out var mipWidth, out var mipHeight);
                    extents[i] = new(mipWidth, mipHeight, 1);
                    offsets[i] = totalSize;
                    totalSize += (uint)mipWidth * (uint)mipHeight * blockSize;
                }
                GPUBuffer textureData = new(blockSize, totalSize, VkBufferUsageFlags.TransferSrc, true, true, false);
                textureData.WriteToBuffer((void*)data);
                texture.CopyFromBuffer(textureData, offsets, extents, true);
            }
            var noeTex = new NoesisTexture(texture, false, format == TextureFormat.RGBA8);

            return noeTex;
        }

        public unsafe override void UpdateTexture(Noesis.Texture texture, uint level, uint x, uint y, uint width, uint height, nint data)
        {
            if (texture is NoesisTexture noeTex)
            {
                if ((void*)data != null)
                {
                    VkBufferImageCopy bufferCopyRegion = new()
                    {
                        bufferOffset = 0,
                        bufferRowLength = 0,
                        bufferImageHeight = 0,
                        imageSubresource = new()
                        {
                            aspectMask = noeTex.Texture.GetSubresourceRange().aspectMask,
                            mipLevel = level,
                            baseArrayLayer = 0,
                            layerCount = 1
                        },
                        imageOffset = new((int)x, (int)y, 0),
                        imageExtent = new(width, height, 1)
                    };

                    GPUBuffer textureData = new((ulong)(uint)noeTex.Texture.Format.BlockSize(), width * height, VkBufferUsageFlags.TransferSrc, true, true, false);
                    textureData.WriteToBuffer((void*)data);
                    noeTex.Texture.CopyFromBuffer(textureData, bufferCopyRegion, true);
                }
            }
        }

        public override void DrawBatch(ref Batch batch)
        {
            var state = batch.RenderState;
            var stateV = state.GetHashCode();
            var shaderV = batch.Shader.Index;
            var pixelShader = (int)batch.PixelShader;

            var shaderHash = HashCode.Combine(stateV,shaderV, pixelShader);

            SetStencilMode(state.StencilMode);
            SetStencilRef(batch.StencilRef);

            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(_currentCommandBuffer, 0, _vertexBuffer.ActiveVkBuffer, batch.VertexOffset);
            GraphicsDevice.DeviceAPI.vkCmdBindIndexBuffer(_currentCommandBuffer, _indexBuffer.ActiveVkBuffer, 0, VkIndexType.Uint16);

            uint firstIndex = batch.StartIndex;
            if (batch.SinglePassStereo)
            {
                GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(_currentCommandBuffer, batch.NumIndices, 2, firstIndex, 0, 0);
            }
            else
            {
                GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(_currentCommandBuffer, batch.NumIndices, 1, firstIndex, 0, 0);
            }
        }
        private void SetStencilRef(uint stencilRef)
        {
            if (mCachedStencilRef != stencilRef)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetStencilReference(_currentCommandBuffer, VkStencilFaceFlags.FrontAndBack, stencilRef);
                mCachedStencilRef = stencilRef;
            }
        }
        private void SetStencilMode(StencilMode mode)
        {
            switch (mode)
            {
                case StencilMode.Disabled:
                    SetDepthTestEnable(false);
                    SetStencilTestEnable(false);
                    break;
                case StencilMode.Equal_Keep:
                    SetDepthTestEnable(false);
                    SetStencilTestEnable(true);
                    SetStencilOp(VkStencilOp.Keep, VkCompareOp.Equal);
                    break;
                case StencilMode.Equal_Incr:

                    SetDepthTestEnable(false);
                    SetStencilTestEnable(true);
                    SetStencilOp(VkStencilOp.IncrementAndWrap, VkCompareOp.Equal);
                    break;
                case StencilMode.Equal_Decr:
                    SetDepthTestEnable(false);
                    SetStencilTestEnable(true);
                    SetStencilOp(VkStencilOp.DecrementAndWrap, VkCompareOp.Equal);
                    break;
                case StencilMode.Clear:
                    SetDepthTestEnable(false);
                    SetStencilTestEnable(true);
                    SetStencilOp(VkStencilOp.Zero, VkCompareOp.Always);
                    break;
                case StencilMode.Disabled_ZTest:
                    SetDepthTestEnable(true);
                    SetStencilTestEnable(false);
                    break;
                case StencilMode.Equal_Keep_ZTest:
                    SetDepthTestEnable(true);
                    SetStencilTestEnable(true);
                    SetStencilOp(VkStencilOp.Keep, VkCompareOp.Equal);
                    break;
                default:
                    throw new NotImplementedException(string.Format("Stencil Mode {0} not supported!", mode.ToString()));
            }
        }

        private void SetDepthTestEnable(bool depthTestEnable)
        {
            if (mCachedDepthTestEnable != depthTestEnable)
            {
               GraphicsDevice.DeviceAPI.vkCmdSetDepthTestEnableEXT(_currentCommandBuffer, depthTestEnable);
                mCachedDepthTestEnable = depthTestEnable;
            }
        }


        private void SetStencilTestEnable(bool stencilTestEnable)
        {
            if (mCachedStencilTestEnable != stencilTestEnable)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetStencilTestEnableEXT(_currentCommandBuffer, stencilTestEnable);
                mCachedStencilTestEnable = stencilTestEnable;
            }
        }

        private void SetStencilOp(VkStencilOp passOp, VkCompareOp compareOp)
        {
            uint stencilOp = ((uint)passOp << 8) | (uint)(compareOp);

            if (mCachedStencilOp != stencilOp)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetStencilOpEXT(_currentCommandBuffer,VkStencilFaceFlags.FrontAndBack,VkStencilOp.Keep,
                    passOp, VkStencilOp.Keep, compareOp);
                mCachedStencilOp = stencilOp;
            }
        }

        public override void BeginOffscreenRender()
        {
            //throw new NotImplementedException();
        }

        public override void BeginOnscreenRender()
        {
            //throw new NotImplementedException();
        }

        public override void EndOffscreenRender()
        {
            //throw new NotImplementedException();
        }

        public override void EndOnscreenRender()
        {
            //throw new NotImplementedException();
        }
        public static VkSampleCountFlags GetSampleCount(uint samples, VkPhysicalDeviceLimits limits)
        {
            var sampleCounts = limits.framebufferColorSampleCounts & limits.framebufferStencilSampleCounts;

            // for (VkSampleCountFlags bits = VkSampleCountFlags.Count64; bits > VkSampleCountFlags.Count1; bits++)
            // {
            //     if (samples >= bits && (sampleCounts & bits) > 0)
            //     {
            //         return bits;
            //     }
            // }

            return VkSampleCountFlags.Count1;
        }
    }
}
