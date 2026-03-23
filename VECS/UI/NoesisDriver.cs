using Assimp.Unmanaged;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using Noesis;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;
using static Noesis.Shader.Vertex.Format;

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

        private readonly ShaderModule[] _vertexShaders = new ShaderModule[(int)Shader.Vertex.Enum.Count];
        private readonly ShaderModule[] _pixelShaders = new ShaderModule[(int)Shader.Enum.Count];

        private readonly Dictionary<int, GraphicsPipeline> Pipelines = [];

        private readonly SwapChainBuffer _indexBuffer;
        private readonly SwapChainBuffer _vertexBuffer;

        private VkCommandBuffer _currentCommandBuffer;
        private bool mCachedDepthTestEnable;
        private bool mCachedStencilTestEnable;
        private uint mCachedStencilOp;
        private uint mCachedStencilRef;
        private bool mStereoSupport;

        public NoesisDriver()
        {
            _indexBuffer = new SwapChainBuffer(sizeof(ushort), 100, VkBufferUsageFlags.IndexBuffer, true);
            _vertexBuffer = new SwapChainBuffer(sizeof(byte), 100, VkBufferUsageFlags.VertexBuffer, true);


            LoadShaderModules();
            CreateSamplers();
        }

        private unsafe void LoadShaderModules()
        {
            uint size = 0;

            fixed(byte* pSize = NoesisShaders.Shaders)
            {
                size = *(uint*)(pSize + 4);
            }

            byte[] decompressedShaders = new byte[size];

            DotFastLZ.Compression.FastLZ.Decompress(NoesisShaders.Shaders, NoesisShaders.Shaders.Length, decompressedShaders, size);


            Application.ParallelFor((int)Shader.Vertex.Enum.Count, (i) =>
            {
                ShaderVS vShader = NoesisShaders.ShadersVS((Shader.Vertex.Enum)i, Caps.LinearRendering, mStereoSupport);
                byte[] shaderCode = new byte[vShader.Size];
                Array.Copy(decompressedShaders, vShader.Start, shaderCode, 0, vShader.Size);
                _vertexShaders[i] = new ShaderModule(vShader.Label, shaderCode);
            });

            var pixelCreateTask = Task.Run(()=> Application.ParallelFor((int)Shader.Enum.Count, (i) =>
            {
                ShaderPS pShader = NoesisShaders.ShadersPS((Shader.Enum)i);
                byte[] shaderCode = new byte[pShader.Size];
                Array.Copy(decompressedShaders,pShader.Start,shaderCode,0, pShader.Size);
                _pixelShaders[i] = new ShaderModule(pShader.Label, shaderCode);
            }));

            for (int i = 0; i < _vertexShaders.Length; i++)
            {
                AssetDataBase<ShaderModule>.Add(_vertexShaders[i]);
            }

            pixelCreateTask.Wait();

            for (int i = 0; i < _pixelShaders.Length; i++)
            {
                AssetDataBase<ShaderModule>.Add(_pixelShaders[i]);
            }
        }

        private unsafe void CreateSamplers()
        {
            TextureSampler[] mSamplers = new TextureSampler[64];


            VkSamplerCreateInfo samplerInfo = new()
            {
                mipLodBias = -0.75f
            };

            string[] MinMagStr = ["Nearest", "Linear"];
            string[] MipStr = ["Disabled", "Nearest", "Linear"];
            string[] WrapStr = ["ClampToEdge", "ClampToZero", "Repeat", "MirrorU", "MirrorV", "Mirror"];
            int samplerIndex = 0;
            for(MinMagFilter minmag = MinMagFilter.Nearest; minmag <= MinMagFilter.Linear; minmag++)
            {
                for (MipFilter mip = MipFilter.Disabled; mip <= MipFilter.Linear; mip++)
                {
                    SetMinMagFilter(minmag, &samplerInfo);
                    SetMipFilter(mip, &samplerInfo);

                    for (WrapMode uv = WrapMode.ClampToEdge; uv <= WrapMode.Mirror; uv++,samplerIndex++)
                    {
                        SetAddress(uv, &samplerInfo);
                        mSamplers[samplerIndex] = new("NOESIS",samplerInfo);
                    }
                }
            }
        }

        private unsafe void SetAddress(WrapMode mode, VkSamplerCreateInfo* samplerInfo)
        {
            switch (mode)
            {
                case WrapMode.ClampToEdge:
                    {
                        samplerInfo->addressModeU = VkSamplerAddressMode.ClampToEdge;
                        samplerInfo->addressModeV = VkSamplerAddressMode.ClampToEdge;
                        break;
                    }
                case WrapMode.ClampToZero:
                    {
                        samplerInfo->addressModeU = VkSamplerAddressMode.ClampToBorder;
                        samplerInfo->addressModeV = VkSamplerAddressMode.ClampToBorder;
                        break;
                    }
                case WrapMode.Repeat:
                    {
                        samplerInfo->addressModeU = VkSamplerAddressMode.Repeat;
                        samplerInfo->addressModeV = VkSamplerAddressMode.Repeat;
                        break;
                    }
                case WrapMode.MirrorU:
                    {
                        samplerInfo->addressModeU = VkSamplerAddressMode.MirroredRepeat;
                        samplerInfo->addressModeV = VkSamplerAddressMode.Repeat;
                        break;
                    }
                case WrapMode.MirrorV:
                    {
                        samplerInfo->addressModeU = VkSamplerAddressMode.Repeat;
                        samplerInfo->addressModeV = VkSamplerAddressMode.MirroredRepeat;
                        break;
                    }
                case WrapMode.Mirror:
                    {
                        samplerInfo->addressModeU = VkSamplerAddressMode.MirroredRepeat;
                        samplerInfo->addressModeV = VkSamplerAddressMode.MirroredRepeat;
                        break;
                    }
                default:
                    throw new InvalidOperationException(string.Format("WrapMode {0} not supported", mode.ToString()));
            }
        }

        private unsafe void SetMinMagFilter(MinMagFilter minmag, VkSamplerCreateInfo* samplerInfo)
        {
            switch (minmag)
            {
                case MinMagFilter.Nearest:                    
                    samplerInfo->minFilter = VkFilter.Nearest;
                    samplerInfo->magFilter = VkFilter.Nearest;
                    break;                    
                case MinMagFilter.Linear:                    
                    samplerInfo->minFilter = VkFilter.Linear;
                    samplerInfo->magFilter = VkFilter.Linear;
                    break;                    
                default:
                    throw new InvalidOperationException(string.Format("MinMagFilter {0} not supported",minmag.ToString()));
            }
        }

        private unsafe void SetMipFilter(MipFilter mip, VkSamplerCreateInfo* samplerInfo)
        {
            switch (mip)
            {
                case MipFilter.Disabled:
                    samplerInfo->mipmapMode = VkSamplerMipmapMode.Nearest;
                    samplerInfo->maxLod = 0.0f;
                    break;
                case MipFilter.Nearest:
                    samplerInfo->mipmapMode = VkSamplerMipmapMode.Nearest;
                    samplerInfo->maxLod = float.MaxValue;
                    break;
                case MipFilter.Linear:
                    samplerInfo->mipmapMode = VkSamplerMipmapMode.Linear;
                    samplerInfo->maxLod = float.MaxValue;
                    break;
                default:
                    throw new InvalidOperationException(string.Format("MipFilter {0} not supported", mip.ToString()));
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

        private static uint Index(VkSampleCountFlags samples) => samples switch
        {
            VkSampleCountFlags.Count1 => 0,
            VkSampleCountFlags.Count2 => 1,
            VkSampleCountFlags.Count4 => 2,
            VkSampleCountFlags.Count8 => 3,
            VkSampleCountFlags.Count16 => 4,
            VkSampleCountFlags.Count32 => 5,
            VkSampleCountFlags.Count64 => 6,
            _ => throw new NotImplementedException(),
        };

        private static VkFormat Format(Shader.Vertex.Format.Attr.Type.Enum type) => type switch
        {
            Shader.Vertex.Format.Attr.Type.Enum.Float => VkFormat.R32Sfloat,
            Shader.Vertex.Format.Attr.Type.Enum.Float2 => VkFormat.R32G32Sfloat,
            Shader.Vertex.Format.Attr.Type.Enum.Float4 => VkFormat.R32G32B32A32Sfloat,
            Shader.Vertex.Format.Attr.Type.Enum.UByte4Norm => VkFormat.R8G8B8A8Unorm,
            Shader.Vertex.Format.Attr.Type.Enum.UShort4Norm => VkFormat.R16G16B16A16Unorm,
            _ => throw new NotImplementedException(string.Format("Format {0} not implemented", type.ToString())),
        };

        private static void FillVertexAttributes(Shader.Vertex.Format.Enum format, VkVertexInputAttributeDescription[] v)
        {
            var attributes = Shader.AttributesForFormat(format);
            uint offset = 0;

            for (Shader.Vertex.Format.Attr.Enum i = 0; i < Shader.Vertex.Format.Attr.Enum.Count; i++)
            {
                if ((attributes & (1 << (int)i)) == attributes)
                {
                    VkVertexInputAttributeDescription attr = new()
                    {
                        binding = 0,
                        location = (uint)i,
                        format = Format(Shader.TypeForAttr(i)),
                        offset = offset
                    };
                    v[(int)i] = attr;
                }
            }
        }

        public void CreatePipeline(NoesisRenderTarget renderTarget,VkSampleCountFlags sampleCountFlags)
        {

            for (Shader.Enum i = 0; i < Shader.Enum.Count; i++)
            {
                ShaderPS pShader = NoesisShaders.ShadersPS(i);

                if (!string.IsNullOrEmpty( pShader.Label))
                {
                    CreatePipelines(pShader.Label,  renderTarget, i, _pixelShaders[(int)i], sampleCountFlags, 0);
                }
            }

        }

        private void CreatePipelines(string label, NoesisRenderTarget renderTarget, Shader.Enum shader, ShaderModule psModule, VkSampleCountFlags sampleCount, uint custom)
        {
            var vsIndex = Shader.VertexForShader(shader);
            var format = Shader.FormatForVertex(vsIndex);
            var vertexShaderModule = _vertexShaders[(int)vsIndex];
            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            if (renderTarget.ColourAA != null)
            {
                configInfo.colourFormats = [renderTarget.ColourAA.Texture.Format];
            }
            else
            {
                configInfo.colourFormats = [renderTarget.Colour.Texture.Format];
            }

            if(renderTarget.Stencil != null)
            {
                configInfo.stencilFormat = renderTarget.Stencil.Texture.Format;
            }

            configInfo.depthFormat = VkFormat.Undefined;

            // Vertex Input State
            VkVertexInputAttributeDescription[] attrs = new VkVertexInputAttributeDescription[(int)Shader.Vertex.Format.Attr.Enum.Count];

            FillVertexAttributes(format, attrs);

            configInfo.AttributeDescriptions = attrs;

            VkVertexInputBindingDescription bindingDescription = new()
            {
                binding = 0,
                stride = (uint)Shader.SizeForFormat(format),
                inputRate = VkVertexInputRate.Vertex
            };

            configInfo.BindingDescriptions = [bindingDescription];

            // Multisample State
            configInfo.multisampleInfo = new()
            {
                sampleShadingEnable = false,
                rasterizationSamples = sampleCount,
                minSampleShading = 1.0f,
                alphaToCoverageEnable = false,
                alphaToOneEnable = false
            };

            // Dynamic State
            configInfo.dynamicStateEnables = [
                VkDynamicState.StencilReference,
                VkDynamicState.Scissor,
                VkDynamicState.Viewport,
                VkDynamicState.DepthTestEnable,
                VkDynamicState.StencilTestEnable,
                VkDynamicState.StencilOp,
                VkDynamicState.StencilCompareMask,
                VkDynamicState.StencilWriteMask,
                //VkDynamicState.PolygonModeEXT,
            ];

            configInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.TriangleList;
            configInfo.inputAssemblyInfo.primitiveRestartEnable = false;

          
            CreatePipelines(label, shader, vertexShaderModule.AssetName, psModule.AssetName, configInfo, custom);
        }
        private unsafe static void RasterizerInfo(VkPipelineRasterizationStateCreateInfo* info, RenderState state, VkPhysicalDeviceFeatures features, string label)
        {

            info->depthClampEnable = false;
            info->rasterizerDiscardEnable = false;
            info->lineWidth = 1.0f;
            info->cullMode = VkCullModeFlags.None;
            info->depthBiasEnable = false;
            info->depthBiasConstantFactor = 0.0f;
            info->depthBiasClamp = 0.0f;
            info->depthBiasSlopeFactor = 0.0f;

            if (state.Wireframe && features.fillModeNonSolid)
            {
                label += "_Wire";
                info->polygonMode = VkPolygonMode.Line;
            }
            else
            {
                info->polygonMode = VkPolygonMode.Fill;
            }
        }
        
        private static unsafe void BlendInfo(VkPipelineColorBlendAttachmentState* info, RenderState state, string label)
        {
            if (state.ColorEnable)
            {
                info->colorWriteMask = VkColorComponentFlags.All;

                info->colorBlendOp = VkBlendOp.Add;
                info->alphaBlendOp = VkBlendOp.Add;

                switch (state.BlendMode)
                {
                    case BlendMode.Src:
                        info->blendEnable = false;
                        break;
                    case BlendMode.SrcOver:
                        label += "_SrcOver";
                        info->blendEnable = true;
                        info->srcColorBlendFactor = VkBlendFactor.One;
                        info->dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        info->srcAlphaBlendFactor = VkBlendFactor.One;
                        info->dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    case BlendMode.SrcOver_Multiply:
                        label += "_SrcOver_Multiply";
                        info->blendEnable = true;
                        info->srcColorBlendFactor = VkBlendFactor.DstColor;
                        info->dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        info->srcAlphaBlendFactor = VkBlendFactor.One;
                        info->dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    case BlendMode.SrcOver_Screen:
                        label += "_SrcOver_Screen";
                        info->blendEnable = true;
                        info->srcColorBlendFactor = VkBlendFactor.One;
                        info->dstColorBlendFactor = VkBlendFactor.OneMinusSrcColor;
                        info->srcAlphaBlendFactor = VkBlendFactor.One;
                        info->dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    case BlendMode.SrcOver_Additive:
                        label += "_SrcOver_Additive";
                        info->blendEnable = true;
                        info->srcColorBlendFactor = VkBlendFactor.One;
                        info->dstColorBlendFactor = VkBlendFactor.One;
                        info->srcAlphaBlendFactor = VkBlendFactor.One;
                        info->dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    default:
                        throw new NotImplementedException(string.Format("BlendMode {0} unsupported", state.BlendMode.ToString()));
                }
            }
        }
        
        private static unsafe void DepthStencilInfo(VkPipelineDepthStencilStateCreateInfo* info, RenderState state, string label)
        {
            info->depthWriteEnable = false;
            info->depthBoundsTestEnable = false;
            info->depthCompareOp = VkCompareOp.GreaterOrEqual;

            info->front.failOp = VkStencilOp.Keep;
            info->front.depthFailOp = VkStencilOp.Keep;
            info->front.writeMask = 0xFF;
            info->front.compareMask = 0XFF;

            info->back.failOp = VkStencilOp.Keep;
            info->back.depthFailOp = VkStencilOp.Keep;
            info->back.writeMask = 0xFF;
            info->back.compareMask = 0XFF;

            switch (state.StencilMode)
            {
                case StencilMode.Disabled:
                    info->depthTestEnable = false;
                    info->stencilTestEnable = false;
                    info->front.compareOp =  VkCompareOp.Equal;
                    info->back.compareOp =  VkCompareOp.Equal;
                    info->front.passOp = VkStencilOp.Keep;
                    info->back.passOp = VkStencilOp.Keep;
                    break;
                case StencilMode.Equal_Keep:
                    label += "_Equal_Keep";
                    info->depthTestEnable = false;
                    info->stencilTestEnable = true;
                    info->front.compareOp = VkCompareOp.Equal;
                    info->back.compareOp = VkCompareOp.Equal;
                    info->front.passOp = VkStencilOp.Keep;
                    info->back.passOp = VkStencilOp.Keep;
                    break;
                case StencilMode.Equal_Incr:
                    label += "_Equal_Incr";
                    info->depthTestEnable = false;
                    info->stencilTestEnable = true;
                    info->front.compareOp = VkCompareOp.Equal;
                    info->back.compareOp = VkCompareOp.Equal;
                    info->front.passOp = VkStencilOp.IncrementAndWrap;
                    info->back.passOp = VkStencilOp.IncrementAndWrap;
                    break;
                case StencilMode.Equal_Decr:
                    label += "_Equal_Decr";
                    info->depthTestEnable = false;
                    info->stencilTestEnable = true;
                    info->front.compareOp = VkCompareOp.Equal;
                    info->back.compareOp = VkCompareOp.Equal;
                    info->front.passOp = VkStencilOp.DecrementAndWrap;
                    info->back.passOp = VkStencilOp.DecrementAndWrap;
                    break;
                case StencilMode.Clear:
                    label += "_Clear";
                    info->depthTestEnable = false;
                    info->stencilTestEnable = true;
                    info->front.compareOp = VkCompareOp.Always;
                    info->back.compareOp = VkCompareOp.Always;
                    info->front.passOp = VkStencilOp.Zero;
                    info->back.passOp = VkStencilOp.Zero;
                    break;
                case StencilMode.Disabled_ZTest:
                    label += "_ZTest";
                    info->depthTestEnable = true;
                    info->stencilTestEnable = false;
                    info->front.compareOp = VkCompareOp.Equal;
                    info->back.compareOp = VkCompareOp.Equal;
                    info->front.passOp = VkStencilOp.Keep;
                    info->back.passOp = VkStencilOp.Keep;
                    break;
                case StencilMode.Equal_Keep_ZTest:
                    label += "_Equal_Keep_ZTest";
                    info->depthTestEnable = true;
                    info->stencilTestEnable = true;
                    info->front.compareOp = VkCompareOp.Equal;
                    info->back.compareOp = VkCompareOp.Equal;
                    info->front.passOp = VkStencilOp.Keep;
                    info->back.passOp = VkStencilOp.Keep;
                    break;
                default:
                    throw new NotImplementedException(string.Format("StencilMode {0} not implemented", state.StencilMode.ToString()));
            }
        }


        private unsafe void CreatePipelines(string label, Shader.Enum shader_, string vertexShader, string pixelShader, GraphicsPipelineConfigInfo configInfo, uint custom)
        {
            GraphicsDevice.InstanceAPI.vkGetPhysicalDeviceFeatures(GraphicsDevice.PhysicalDevice,out var features);

            byte shaderEnum = (byte)shader_;
            for (byte i = 0; i <= byte.MaxValue; i++)
            {
                Shader shader = new();
                Buffer.MemoryCopy(&shaderEnum, &shader, 1, 1);

                RenderState state = new();
                Buffer.MemoryCopy(&i, &shader, 1, 1);

                // if (!IsValidBlendMode(shader, (BlendMode.Enum)state.BlendMode)) continue;
                // if (!IsValidColorEnable(shader, state.ColorEnable > 0)) continue;
                // if (!IsValidWireframe(shader, state.BlendMode > 0)) continue;
                // 
                // pipelineInfo.basePipelineHandle = parent;
                // pipelineInfo.flags = (parent != VK_NULL_HANDLE) ? VK_PIPELINE_CREATE_DERIVATIVE_BIT :
                //     VK_PIPELINE_CREATE_ALLOW_DERIVATIVES_BIT;

                VkPipelineRasterizationStateCreateInfo rasterizer = new();
                RasterizerInfo(&rasterizer, state, features, label);
                configInfo.rasterizationInfo = rasterizer;

                VkPipelineDepthStencilStateCreateInfo depthStencil = new();
                DepthStencilInfo(&depthStencil, state, label);
                configInfo.depthStencilInfo = depthStencil;

                VkPipelineColorBlendAttachmentState colorBlendAttachment = new();
                BlendInfo(&colorBlendAttachment, state, label);
                configInfo.colourBlendAttachment = colorBlendAttachment;

                VkPipelineColorBlendStateCreateInfo colorBlending = new()
                {
                    attachmentCount = 1,
                    pAttachments = &colorBlendAttachment
                };

                // pipelineInfo.pRasterizationState = &rasterizer;
                // pipelineInfo.pDepthStencilState = &depthStencil;
                // pipelineInfo.pColorBlendState = &colorBlending;

                var pipeline = new GraphicsPipeline(label, vertexShader, pixelShader, configInfo);
                Pipelines.Add(pipeline.Hash, pipeline);

                // VkPipeline pipeline;
                // V(vkCreateGraphicsPipelines(mDevice, mPipelineCache, 1, &pipelineInfo, 0, &pipeline));
                // VK_NAME(pipeline, PIPELINE, "Noesis_%s", label.Str());
                // 
                // parent = (parent == VK_NULL_HANDLE) ? pipeline : parent;
                // 
                // uint32_t hash = HashPipeline(renderPass, shader_, (uint8_t)i, custom);
                // mPipelineMap.Insert(hash, mPipelines.Size());
                // mPipelines.PushBack(pipeline);
            }
        }

    }
}
