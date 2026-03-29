using Noesis;
using SDL3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using VECS.ECS;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;
using Vector4 = System.Numerics.Vector4;

namespace VECS.UI
{

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

        private readonly HashSet<int> ShaderSets = [];
        private readonly Dictionary<int, GraphicsPipeline> Pipelines = [];
        private readonly Dictionary<int, Material> Materials = [];
        private readonly Dictionary<int, uint> PipelineVariantCounts = [];

        private readonly SwapChainBuffer _indexBuffer;
        private readonly SwapChainBuffer _vertexBuffer;

        private VkCommandBuffer _currentCommandBuffer => _currentFrameInfo.CommandBuffer;
        public RendererFrameInfo _currentFrameInfo;
        private bool mCachedDepthTestEnable;
        private bool mCachedStencilTestEnable;
        private uint mCachedStencilOp;
        private uint mCachedStencilRef;
        private bool mStereoSupport;
        private bool mFillModeNonSolid;



        private readonly int patternId = "pattern".GetShaderPropertyId();
        private readonly int rampsId = "ramps".GetShaderPropertyId();
        private readonly int imageId = "image".GetShaderPropertyId();
        private readonly int glyphsId = "glyphs".GetShaderPropertyId();
        private readonly int shadowId = "shadow".GetShaderPropertyId();

        public NoesisDriver()
        {

            _indexBuffer = new SwapChainBuffer(sizeof(ushort), 100, VkBufferUsageFlags.IndexBuffer, true);
            _vertexBuffer = new SwapChainBuffer(sizeof(byte), 100, VkBufferUsageFlags.VertexBuffer, true);

            GraphicsDevice.InstanceAPI.vkGetPhysicalDeviceFeatures(GraphicsDevice.PhysicalDevice, out var features);
            mFillModeNonSolid = features.fillModeNonSolid;

            LoadShaderModules();
            CreateSamplers();
        }

        public static void ErrorCallback(Exception exception)
        {
            throw exception;
        }

        public static void LoggerCallback(LogLevel level, string channel, string message)
        {
            Console.WriteLine("{0} {1} {2}",level.ToString(),channel,message);
        }

        private unsafe void LoadShaderModules()
        {
            for (Shader.Vertex.Enum i = 0; i < Shader.Vertex.Enum.Count; i++)
            {
                _vertexShaders[(int)i] = AssetDataBase<ShaderModule>.GetNamed(string.Format("{0}_VS", i.ToString()));
            }

            for (Shader.Enum i = 0; i < Shader.Enum.Count; i++)
            {
                _pixelShaders[(int)i] = AssetDataBase<ShaderModule>.GetNamed(string.Format("{0}_PS", i.ToString()));
            }
        }

        private unsafe void CreateSamplers()
        {
            TextureSampler[] mSamplers = new TextureSampler[64];


            VkSamplerCreateInfo samplerInfo = new()
            {
                mipLodBias = -0.75f
            };

            // string[] MinMagStr = ["Nearest", "Linear"];
            // string[] MipStr = ["Disabled", "Nearest", "Linear"];
            // string[] WrapStr = ["ClampToEdge", "ClampToZero", "Repeat", "MirrorU", "MirrorV", "Mirror"];
            int samplerIndex = 0;
            for (MinMagFilter minmag = MinMagFilter.Nearest; minmag <= MinMagFilter.Linear; minmag++)
            {
                for (MipFilter mip = MipFilter.Disabled; mip <= MipFilter.Linear; mip++)
                {
                    SetMinMagFilter(minmag, &samplerInfo);
                    SetMipFilter(mip, &samplerInfo);

                    for (WrapMode uv = WrapMode.ClampToEdge; uv <= WrapMode.Mirror; uv++, samplerIndex++)
                    {
                        SetAddress(uv, &samplerInfo);
                        mSamplers[samplerIndex] = new("NOESIS", samplerInfo);
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
                    throw new InvalidOperationException(string.Format("MinMagFilter {0} not supported", minmag.ToString()));
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
            if (surface is NoesisRenderTarget renderTarget)
            {

                VkRect2D renderArea;
                renderArea.offset.x = (int)tile.X;
                renderArea.offset.y = (int)renderTarget.Colour.Height - ((int)tile.Y + (int)tile.Height);
                renderArea.extent.width = tile.Width;
                renderArea.extent.height = tile.Height;
                VkRenderingAttachmentInfo stencil = default;

                VkRenderingAttachmentInfo colour;
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
                    pStencilAttachment = renderTarget.Stencil != null ? &stencil : null,
                    renderArea = renderArea,
                    layerCount = 1,
                    flags = VkRenderingFlags.ContentsInlineKHR
                };

                GraphicsDevice.DeviceAPI.vkCmdBeginRendering(_currentCommandBuffer, &renderingInfo);

                GraphicsDevice.DeviceAPI.vkCmdSetScissor(_currentCommandBuffer, 0, renderArea);
                GraphicsDevice.DeviceAPI.vkCmdSetRasterizationSamplesEXT(_currentCommandBuffer, renderTarget.samples);
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

            if (renderTarget.samples > VkSampleCountFlags.Count1)
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

            CreatePipeline(renderTarget);

            return renderTarget;
        }

        public override Noesis.RenderTarget CloneRenderTarget(string label, Noesis.RenderTarget surface)
        {
            if (surface is NoesisRenderTarget renderTarget)
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
            //texture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);
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

        public unsafe override void DrawBatch(ref Batch batch)
        {
            var state = batch.RenderState;
            var stateV = state.GetHashCode();
            var shaderV = batch.Shader.Index;
            var pixelShader = (uint)batch.PixelShader;

            var shaderHash = HashPipeline((byte)shaderV, stateV, pixelShader);
            if (!Pipelines.TryGetValue(shaderHash, out var pipeline))
            {
                Debugger.Break();
            }
            //var pipeline = Pipelines[shaderHash];

            var mat = GetMaterial(ref batch, pipeline);

            //mat.Pipeline._uniformBuffer.WriteFromHostToBuffer(_currentFrameInfo.FrameIndex);
            mat.Bind(_currentFrameInfo);
            SetDescriptors(ref batch, mat);

            SetStencilMode(state.StencilMode);
            SetStencilRef(batch.StencilRef);
            SetRasterizerInfo(_currentCommandBuffer, state.Wireframe);
            SetBlendInfo(_currentCommandBuffer, state.ColorEnable, state.BlendMode);

            var vertexOffset = (ulong)batch.VertexOffset;
            var buffer = _vertexBuffer.ActiveVkBuffer;
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffers(_currentCommandBuffer, 0, 1,&buffer, &vertexOffset);
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

        private Material GetMaterial(ref Batch batch, GraphicsPipeline pipeline)
        {
            NoesisTexture pattern = (NoesisTexture)batch.Pattern;
            NoesisTexture ramps = (NoesisTexture)batch.Ramps;
            NoesisTexture image = (NoesisTexture)batch.Image;
            NoesisTexture glyphs = (NoesisTexture)batch.Glyphs;
            NoesisTexture shadow = (NoesisTexture)batch.Shadow;

            int patternHash = pattern == null ? 0 : pattern.Texture.Hash;
            int rampsHash = ramps == null ? 0 : ramps.Texture.Hash;
            int imageHash = image == null ? 0 : image.Texture.Hash;
            int glyphsHash = glyphs == null ? 0 : glyphs.Texture.Hash;
            int shadowHash = shadow == null ? 0 : shadow.Texture.Hash;


            var textureHash = HashCode.Combine(patternHash, rampsHash, imageHash, glyphsHash, shadowHash);

            var u0 = batch.VertexUniform0;
            var u1 = batch.VertexUniform1;
            var u2 = batch.PixelUniform0;
            var u3 = batch.PixelUniform1;

            var uniformHash = HashCode.Combine(u0.Hash, u1.Hash, u2.Hash, u3.Hash);

            var combinedHashCode = HashCode.Combine(pipeline.Hash, textureHash, uniformHash);

            if (Materials.TryGetValue(combinedHashCode, out var material))
            {
                return material;
            }
            else
            {
                if (PipelineVariantCounts.TryGetValue(pipeline.Hash, out var currentMax))
                {
                    material = pipeline.GetOrCreateVariant(currentMax);
                    PipelineVariantCounts[pipeline.Hash]++;
                }
                else
                {
                    material = pipeline.Default();
                    PipelineVariantCounts[pipeline.Hash] = 1;
                }

                Materials.Add(combinedHashCode, material);
                return material;
            }
        }

        private unsafe void SetDescriptors(ref Batch batch, Material mat)
        {
            NoesisTexture pattern = (NoesisTexture)batch.Pattern;
            NoesisTexture ramps = (NoesisTexture)batch.Ramps;
            NoesisTexture image = (NoesisTexture)batch.Image;
            NoesisTexture glyphs = (NoesisTexture)batch.Glyphs;
            NoesisTexture shadow = (NoesisTexture)batch.Shadow;

            if (pattern != null)
            {
                mat.SetTexture(patternId, pattern.Texture);
            }
            if (ramps != null)
            {
                mat.SetTexture(rampsId, ramps.Texture);
            }
            if (image != null)
            {
                mat.SetTexture(imageId, image.Texture);
            }
            if (glyphs != null)
            {
                mat.SetTexture(glyphsId, glyphs.Texture);
            }
            if (shadow != null)
            {
                mat.SetTexture(shadowId, shadow.Texture);
            }

            var u0 = batch.VertexUniform0;
            var u1 = batch.VertexUniform1;
            var u2 = batch.PixelUniform0;
            var u3 = batch.PixelUniform1;


            #region u0
            if (u0.NumWords == 16)
            {
                Matrix4x4 matrix = default;
                Buffer.MemoryCopy(u0.Values.ToPointer(), &matrix, sizeof(Matrix4x4), sizeof(Matrix4x4));
                mat.SetMatrix4x4("buffer0.projectionMtx".GetShaderPropertyId(), matrix);
            }
            else if (u0.NumWords == 32)
            {
                Matrix4x4* matrices = stackalloc Matrix4x4[2];
                Buffer.MemoryCopy(u0.Values.ToPointer(), matrices, sizeof(Matrix4x4) * 2, sizeof(Matrix4x4) * 2);

                mat.SetMatrix4x4Array("buffer0.projectionMtx".GetShaderPropertyId(), new Span<Matrix4x4>(matrices, 2));
            }
            else if (u0.NumWords != 0)
            {
                throw new NotSupportedException(string.Format("{0} words not supported!", u0.NumWords));
            }
            #endregion

            #region u1
            if (u1.NumWords == 2)
            {
                Vector2 textureDimensions = default;
                Buffer.MemoryCopy(u1.Values.ToPointer(), &textureDimensions, sizeof(Vector2), sizeof(Vector2));
                mat.SetVector2("buffer1.textureDimensions".GetShaderPropertyId(), textureDimensions);
            }
            else if (u1.NumWords != 0)
            {
                throw new NotSupportedException(string.Format("{0} words not supported!", u0.NumWords));
            }
            #endregion

            #region u2
            if (u2.NumWords == 4)
            {
                Vector4 rgba = default;
                Buffer.MemoryCopy(u2.Values.ToPointer(), &rgba, sizeof(Vector4), sizeof(Vector4));
                mat.SetVector4("buffer2.rgba".GetShaderPropertyId(), rgba);
            }
            else if (u2.NumWords == 1)
            {
                float opacity = default;
                Buffer.MemoryCopy(u2.Values.ToPointer(), &opacity, sizeof(float), sizeof(float));
                mat.SetFloat("buffer2.opacity".GetShaderPropertyId(), opacity);
            }

            else if (u2.NumWords == 5)
            {
                Vector4 rgba = default;
                float opacity = default;
                Buffer.MemoryCopy(u2.Values.ToPointer(), &rgba, sizeof(Vector4), sizeof(Vector4));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &opacity, sizeof(float), sizeof(float));
                mat.SetVector4("buffer2.rgba".GetShaderPropertyId(), rgba);
                mat.SetFloat("buffer2.opacity".GetShaderPropertyId(), opacity);
            }
            else if (u2.NumWords == 7)
            {
                Vector4 radialGrad0 = default;
                Vector4 radialGrad1 = default;
                Buffer.MemoryCopy(u2.Values.ToPointer(), &radialGrad0, sizeof(Vector4), sizeof(Vector4));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &radialGrad1, sizeof(Vector3), sizeof(Vector3));
                radialGrad1.W = 0;
                mat.SetVector4("buffer2.radialGrad0".GetShaderPropertyId(), radialGrad0);
                mat.SetVector4("buffer2.radialGrad1".GetShaderPropertyId(), radialGrad1);
            }

            else if (u2.NumWords == 8)
            {
                float opacity = default;
                Vector4 radialGrad0 = default;
                Vector4 radialGrad1 = default;
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(float), &radialGrad0, sizeof(Vector4), sizeof(Vector4));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4) + sizeof(float), &radialGrad1, sizeof(Vector3), sizeof(Vector3));
                Buffer.MemoryCopy(u2.Values.ToPointer(), &opacity, sizeof(float), sizeof(float));
                radialGrad1.W = 0;
                mat.SetFloat("buffer2.opacity".GetShaderPropertyId(), opacity);
                mat.SetVector4("buffer2.radialGrad0".GetShaderPropertyId(), radialGrad0);
                mat.SetVector4("buffer2.radialGrad1".GetShaderPropertyId(), radialGrad1);
            }
            else if (u2.NumWords == 11)
            {
                Vector4 radialGrad0 = default;
                Vector4 radialGrad1 = default;
                Vector4 rgba = default;
                Buffer.MemoryCopy(u2.Values.ToPointer(), &rgba, sizeof(Vector4), sizeof(Vector4));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &radialGrad0, sizeof(Vector4), sizeof(Vector4));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4) + sizeof(Vector4), &radialGrad1, sizeof(Vector3), sizeof(Vector3));
                radialGrad1.W = 0;
                mat.SetVector4("buffer2.rgba".GetShaderPropertyId(), rgba);
                mat.SetVector4("buffer2.radialGrad0".GetShaderPropertyId(), radialGrad0);
                mat.SetVector4("buffer2.radialGrad1".GetShaderPropertyId(), radialGrad1);
            }
            else if (u2.NumWords == 12)
            {
                Vector4 radialGrad0 = default;
                Vector4 radialGrad1 = default;
                Vector4 rgba = default;
                float opacity = default;
                Buffer.MemoryCopy(u2.Values.ToPointer(), &rgba, sizeof(Vector4), sizeof(Vector4));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &opacity, sizeof(float), sizeof(float));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(float) + sizeof(Vector4), &radialGrad0, sizeof(Vector4), sizeof(Vector4));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(float) + sizeof(Vector4) + sizeof(Vector4), &radialGrad1, sizeof(Vector3), sizeof(Vector3));
                radialGrad1.W = 0;
                mat.SetVector4("buffer2.rgba".GetShaderPropertyId(), rgba);
                mat.SetFloat("buffer2.opacity".GetShaderPropertyId(), opacity);
                mat.SetVector4("buffer2.radialGrad0".GetShaderPropertyId(), radialGrad0);
                mat.SetVector4("buffer2.radialGrad1".GetShaderPropertyId(), radialGrad1);
            }
            else if (u2.NumWords != 0)
            {
                throw new NotSupportedException(string.Format("{0} words not supported!", u2.NumWords));
            }
            #endregion

            #region u3
            if (u3.NumWords == 1)
            {
                float blend = default;
                Buffer.MemoryCopy(u2.Values.ToPointer(), &blend, sizeof(float), sizeof(float));
                mat.SetFloat("buffer3.blend".GetShaderPropertyId(), blend);
            }
            else if (u3.NumWords == 7)
            {
                Vector4 shadowColor = default;
                Vector2 shadowOffset = default;
                float blend = default;
                Buffer.MemoryCopy(u2.Values.ToPointer(), &shadowColor, sizeof(Vector4), sizeof(Vector4));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &shadowOffset, sizeof(Vector2), sizeof(Vector2));
                Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4) + sizeof(Vector2), &blend, sizeof(float), sizeof(float));
                mat.SetVector4("buffer3.shadowColor".GetShaderPropertyId(), shadowColor);
                mat.SetVector2("buffer3.shadowOffset".GetShaderPropertyId(), shadowOffset);
                mat.SetFloat("buffer3.blend".GetShaderPropertyId(), blend);
            }
            else if (u3.NumWords != 0)
            {
                throw new NotSupportedException(string.Format("{0} words not supported!", u3.NumWords));
            }
            #endregion
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
            SetDepthStencilInfo(_currentCommandBuffer, mode);
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

        private static void FillVertexAttributes(Shader.Vertex.Format.Enum format, List<VkVertexInputAttributeDescription> v)
        {
            var attributes = Shader.AttributesForFormat(format);
            uint offset = 0;

            for (Shader.Vertex.Format.Attr.Enum i = 0; i < Shader.Vertex.Format.Attr.Enum.Count; i++)
            {
                if ((attributes & (1 << (int)i)) != 0)
                {
                    VkVertexInputAttributeDescription attr = new()
                    {
                        binding = 0,
                        location = (uint)i,
                        format = Format(Shader.TypeForAttr(i)),
                        offset = offset
                    };
                    v.Add(attr);
                    offset += (uint)Shader.SizeForType(Shader.TypeForAttr(i));
                }
            }
        }

        public void CreatePipeline(NoesisRenderTarget renderTarget)
        {
            VkFormat stencilFormat = VkFormat.Undefined;
            VkFormat colourFormat;
            if (renderTarget.ColourAA != null)
            {
                colourFormat = renderTarget.ColourAA.Texture.Format;
            }
            else
            {
                colourFormat = renderTarget.Colour.Texture.Format;
            }

            if (renderTarget.Stencil != null)
            {
                stencilFormat = renderTarget.Stencil.Texture.Format;
            }

            var shaderSetHashCode = HashCode.Combine(colourFormat, stencilFormat);

            if (!ShaderSets.Add(shaderSetHashCode))
            {
                return;
            }


            for (Shader.Enum i = 0; i < Shader.Enum.Count; i++)
            {
                var pShader = _pixelShaders[(int)i];

                if (pShader != null)
                {
                    CreatePipelines(i.ToString(), colourFormat,stencilFormat, i, pShader, 0);
                }
            }

        }

        public void CreatePipelines(VkFormat colourFormat, VkFormat stencilFormat)
        {
            var shaderSetHashCode = HashCode.Combine(colourFormat, stencilFormat);

            if (!ShaderSets.Add(shaderSetHashCode))
            {
                return;
            }


            for (Shader.Enum i = 0; i < Shader.Enum.Count; i++)
            {
                var pShader = _pixelShaders[(int)i];

                if (pShader != null)
                {
                    CreatePipelines(i.ToString(), colourFormat, stencilFormat, i, pShader, 0);
                }
            }
        }

        private void CreatePipelines(string label, VkFormat colour, VkFormat stencil, Shader.Enum shader, ShaderModule psModule, uint custom)
        {
            var vsIndex = Shader.VertexForShader(shader);
            var format = Shader.FormatForVertex(vsIndex);
            var vertexShaderModule = _vertexShaders[(int)vsIndex];
            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            configInfo.colourFormats = [colour];
            configInfo.stencilFormat = stencil;
            configInfo.depthFormat = VkFormat.Undefined;

            // Vertex Input State
            List<VkVertexInputAttributeDescription> attrs = new();

            FillVertexAttributes(format, attrs);

            configInfo.AttributeDescriptions = attrs.ToArray();

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
                rasterizationSamples = VkSampleCountFlags.Count1,
                minSampleShading = 1.0f,
                alphaToCoverageEnable = false,
                alphaToOneEnable = false
            };

            // Dynamic State
            configInfo.dynamicStateEnables = [
                VkDynamicState.Scissor,
                VkDynamicState.Viewport,
                VkDynamicState.CullMode,
                VkDynamicState.DepthTestEnable,
                VkDynamicState.StencilTestEnable,
                VkDynamicState.StencilOp,
                //VkDynamicState.StencilCompareMask,
                //VkDynamicState.StencilWriteMask,
                VkDynamicState.RasterizationSamplesEXT,
                VkDynamicState.StencilReference,
                VkDynamicState.ColorBlendEquationEXT,
                VkDynamicState.ColorBlendEnableEXT,
                VkDynamicState.PolygonModeEXT,
            ];

            configInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.TriangleList;
            configInfo.inputAssemblyInfo.primitiveRestartEnable = false;


            CreatePipelines(label, shader, vertexShaderModule.AssetName, psModule.AssetName, configInfo, custom);
        }

        private unsafe void RasterizerInfo(VkPipelineRasterizationStateCreateInfo* info, RenderState state, string label)
        {

            info->depthClampEnable = false;
            info->rasterizerDiscardEnable = false;
            info->lineWidth = 1.0f;
            info->cullMode = VkCullModeFlags.None;
            info->depthBiasEnable = false;
            info->depthBiasConstantFactor = 0.0f;
            info->depthBiasClamp = 0.0f;
            info->depthBiasSlopeFactor = 0.0f;
            
            if (state.Wireframe && mFillModeNonSolid)
            {
                label += "_Wire";
                //  info->polygonMode = VkPolygonMode.Line;
            }
            else
            {
                //    info->polygonMode = VkPolygonMode.Fill;
            }
        }

        public unsafe void SetRasterizerInfo(VkCommandBuffer commandBuffer, bool wireframe)
        {
            if (wireframe && mFillModeNonSolid)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetPolygonModeEXT(commandBuffer, VkPolygonMode.Line);
            }
            else
            {
                GraphicsDevice.DeviceAPI.vkCmdSetPolygonModeEXT(commandBuffer, VkPolygonMode.Fill);
            }
        }

        private static unsafe bool BlendInfo(VkPipelineColorBlendAttachmentState* info, RenderState state, string label)
        {
            if (state.ColorEnable)
            {
                info->colorWriteMask = VkColorComponentFlags.All;

                info->colorBlendOp = VkBlendOp.Add;
                info->alphaBlendOp = VkBlendOp.Add;

                switch (state.BlendMode)
                {
                    case BlendMode.Src:
                        break;
                    case BlendMode.SrcOver:
                        label += "_SrcOver";
                        break;
                    case BlendMode.SrcOver_Multiply:
                        label += "_SrcOver_Multiply";
                        break;
                    case BlendMode.SrcOver_Screen:
                        label += "_SrcOver_Screen";
                        break;
                    case BlendMode.SrcOver_Additive:
                        label += "_SrcOver_Additive";
                        break;

                    case BlendMode.SrcOver_Dual:
                        label += "_SrcOver_Dual";
                        break;
                    default:
                        return false;
                        //throw new NotImplementedException(string.Format("BlendMode {0} unsupported", state.BlendMode.ToString()));
                }
            }
            return true;
        }

        private static unsafe void SetBlendInfo(VkCommandBuffer commandBuffer, bool colourEnable, BlendMode blendMode)
        {
            VkBool32 blendEnabled = colourEnable;
            if (colourEnable)
            {
                VkColorBlendEquationEXT blendEquation = new()
                {
                    colorBlendOp = VkBlendOp.Add,
                    alphaBlendOp = VkBlendOp.Add
                };

                switch (blendMode)
                {
                    case BlendMode.Src:
                        blendEnabled = false;
                        break;
                    case BlendMode.SrcOver:
                        blendEnabled = true;
                        blendEquation.srcColorBlendFactor = VkBlendFactor.One;
                        blendEquation.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        blendEquation.srcAlphaBlendFactor = VkBlendFactor.One;
                        blendEquation.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    case BlendMode.SrcOver_Multiply:
                        blendEquation.srcColorBlendFactor = VkBlendFactor.DstColor;
                        blendEquation.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        blendEquation.srcAlphaBlendFactor = VkBlendFactor.One;
                        blendEquation.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    case BlendMode.SrcOver_Screen:
                        blendEnabled = true;
                        blendEquation.srcColorBlendFactor = VkBlendFactor.One;
                        blendEquation.dstColorBlendFactor = VkBlendFactor.OneMinusSrcColor;
                        blendEquation.srcAlphaBlendFactor = VkBlendFactor.One;
                        blendEquation.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    case BlendMode.SrcOver_Additive:
                        blendEnabled = true;
                        blendEquation.srcColorBlendFactor = VkBlendFactor.One;
                        blendEquation.dstColorBlendFactor = VkBlendFactor.One;
                        blendEquation.srcAlphaBlendFactor = VkBlendFactor.One;
                        blendEquation.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    case BlendMode.SrcOver_Dual:
                        blendEnabled = true;
                        blendEquation.srcColorBlendFactor = VkBlendFactor.One;
                        blendEquation.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        blendEquation.srcAlphaBlendFactor = VkBlendFactor.One;
                        blendEquation.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                        break;
                    default:
                        throw new NotImplementedException(string.Format("Cannot set dynamic BlendMode {0}", blendMode.ToString()));
                }

                GraphicsDevice.DeviceAPI.vkCmdSetColorBlendEquationEXT(commandBuffer, 0, 1, &blendEquation);
            }
            GraphicsDevice.DeviceAPI.vkCmdSetColorBlendEnableEXT(commandBuffer, 0, 1, &blendEnabled);
        }

        private static unsafe bool DepthStencilInfo(VkPipelineDepthStencilStateCreateInfo* info, RenderState state, string label)
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
                    break;
                case StencilMode.Equal_Keep:
                    label += "_Equal_Keep";
                    break;
                case StencilMode.Equal_Incr:
                    label += "_Equal_Incr";
                    break;
                case StencilMode.Equal_Decr:
                    label += "_Equal_Decr";
                    break;
                case StencilMode.Clear:
                    label += "_Clear";
                    break;
                case StencilMode.Disabled_ZTest:
                    label += "_ZTest";
                    break;
                case StencilMode.Equal_Keep_ZTest:
                    label += "_Equal_Keep_ZTest";
                    break;
                default:
                    return false;
                    //throw new NotImplementedException(string.Format("StencilMode {0} not implemented", state.StencilMode.ToString()));
            }

            return true;
        }

        private static unsafe void SetDepthStencilInfo(VkCommandBuffer commandBuffer, StencilMode mode)
        {
            VkBool32 depthTestEnable;
            VkBool32 stencilTestEnable;
            VkCompareOp stencilCompareOp;
            VkStencilOp passOp;
            switch (mode)
            {
                case StencilMode.Disabled:
                    depthTestEnable = false;
                    stencilTestEnable = false;
                    stencilCompareOp = VkCompareOp.Equal;
                    passOp = VkStencilOp.Keep;
                    break;
                case StencilMode.Equal_Keep:
                    depthTestEnable = false;
                    stencilTestEnable = true;
                    stencilCompareOp = VkCompareOp.Equal;
                    passOp = VkStencilOp.Keep;
                    break;
                case StencilMode.Equal_Incr:
                    depthTestEnable = false;
                    stencilTestEnable = true;
                    stencilCompareOp = VkCompareOp.Equal;
                    passOp = VkStencilOp.IncrementAndWrap;
                    break;
                case StencilMode.Equal_Decr:
                    depthTestEnable = false;
                    stencilTestEnable = true;
                    stencilCompareOp = VkCompareOp.Equal;
                    passOp = VkStencilOp.DecrementAndWrap;
                    break;
                case StencilMode.Clear:
                    depthTestEnable = false;
                    stencilTestEnable = true;
                    stencilCompareOp = VkCompareOp.Always;
                    passOp = VkStencilOp.Zero;
                    break;
                case StencilMode.Disabled_ZTest:
                    depthTestEnable = true;
                    stencilTestEnable = false;
                    stencilCompareOp = VkCompareOp.Equal;
                    passOp = VkStencilOp.Keep;
                    break;
                case StencilMode.Equal_Keep_ZTest:
                    depthTestEnable = true;
                    stencilTestEnable = true;
                    stencilCompareOp = VkCompareOp.Equal;
                    passOp = VkStencilOp.Keep;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Cannot set Dynamic StencilMode {0}", mode.ToString()));
            }

            GraphicsDevice.DeviceAPI.vkCmdSetDepthTestEnable(commandBuffer, depthTestEnable);
            GraphicsDevice.DeviceAPI.vkCmdSetStencilTestEnable(commandBuffer, stencilTestEnable);
            GraphicsDevice.DeviceAPI.vkCmdSetStencilOp(commandBuffer, VkStencilFaceFlags.Front, VkStencilOp.Keep, passOp, VkStencilOp.Keep, stencilCompareOp);
        }

        private unsafe void CreatePipelines(string label, Shader.Enum shader_, string vertexShader, string pixelShader, GraphicsPipelineConfigInfo configInfo, uint custom)
        {
            GraphicsDevice.InstanceAPI.vkGetPhysicalDeviceFeatures(GraphicsDevice.PhysicalDevice, out var features);
            GraphicsPipeline parent = null;
            byte shaderEnum = (byte)shader_;
            Shader shader = new();
            Buffer.MemoryCopy(&shaderEnum, &shader, 1, 1);
            for (uint i = 0; i < 256; i++)
            {
                RenderState state = new();
                byte v = (byte)i;
                Buffer.MemoryCopy(&v, &state, 1, 1);

                // if (!IsValidBlendMode(shader, (BlendMode.Enum)state.BlendMode)) continue;
                // if (!IsValidColorEnable(shader, state.ColorEnable > 0)) continue;
                // if (!IsValidWireframe(shader, state.BlendMode > 0)) continue;
                // 
                configInfo.BasePipeline = parent;

                configInfo.AllowDerivative = parent == null;

                // pipelineInfo.flags = (parent != VK_NULL_HANDLE) ? VK_PIPELINE_CREATE_DERIVATIVE_BIT :
                //     VK_PIPELINE_CREATE_ALLOW_DERIVATIVES_BIT;

                VkPipelineRasterizationStateCreateInfo rasterizer = new();
                RasterizerInfo(&rasterizer, state, label);
                configInfo.rasterizationInfo = rasterizer;

                VkPipelineDepthStencilStateCreateInfo depthStencil = new();
                if (!DepthStencilInfo(&depthStencil, state, label))
                {
                    continue;
                }
                configInfo.depthStencilInfo = depthStencil;

                VkPipelineColorBlendAttachmentState colorBlendAttachment = new();
                if (!BlendInfo(&colorBlendAttachment, state, label))
                {
                    continue;
                }
                configInfo.colourBlendAttachment = colorBlendAttachment;

                VkPipelineColorBlendStateCreateInfo colorBlending = new()
                {
                    attachmentCount = 1,
                    pAttachments = &colorBlendAttachment
                };

                // pipelineInfo.pRasterizationState = &rasterizer;
                // pipelineInfo.pDepthStencilState = &depthStencil;
                // pipelineInfo.pColorBlendState = &colorBlending;

                var pipeline = new GraphicsPipeline(string.Format("NOESIS_{0}_{1}", label, i), vertexShader, pixelShader, configInfo);
                var pipelineHash = HashPipeline(shaderEnum, state.GetHashCode(), custom);
                Pipelines.Add(pipelineHash, pipeline);

                // VkPipeline pipeline;
                // V(vkCreateGraphicsPipelines(mDevice, mPipelineCache, 1, &pipelineInfo, 0, &pipeline));
                // VK_NAME(pipeline, PIPELINE, "Noesis_%s", label.Str());
                // 
                parent ??= pipeline;
                // parent = (parent == VK_NULL_HANDLE) ? pipeline : parent;
                // 
                // uint32_t hash = HashPipeline(renderPass, shader_, (uint8_t)i, custom);
                // mPipelineMap.Insert(hash, mPipelines.Size());
                // mPipelines.PushBack(pipeline);
            }
        }
        
        private static int HashPipeline(byte id, int state, uint custom)
        {
            return HashCode.Combine(custom, id,state);
        }
    }
}
