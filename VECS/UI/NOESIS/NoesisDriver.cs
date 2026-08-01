using Noesis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;
using Vector4 = System.Numerics.Vector4;

namespace VECS.UI
{
    public class NoesisDriver : RenderDevice
    {
        private static readonly int patternId = "pattern".GetShaderPropertyId();
        private static readonly int rampsId = "ramps".GetShaderPropertyId();
        private static readonly int imageId = "image".GetShaderPropertyId();
        private static readonly int glyphsId = "glyphs".GetShaderPropertyId();
        private static readonly int shadowId = "shadow".GetShaderPropertyId();

        private static readonly int _buffer0ProjMat = "buffer0.projectionMtx".GetShaderPropertyId();
        private static readonly int _buffer1TextDim = "buffer1.textureDimensions".GetShaderPropertyId();
        private static readonly int _buffer2RGBA = "buffer2.rgba".GetShaderPropertyId();
        private static readonly int _buffer2Opac = "buffer2.opacity".GetShaderPropertyId();
        private static readonly int _buffer2RadGrad0 = "buffer2.radialGrad0".GetShaderPropertyId();
        private static readonly int _buffer2RadGrad1 = "buffer2.radialGrad1".GetShaderPropertyId();
        private static readonly int _buffer3Blend = "buffer3.blend".GetShaderPropertyId();
        private static readonly int _buffer3ShadCol = "buffer3.shadowColor".GetShaderPropertyId();
        private static readonly int _buffer3ShadOff = "buffer3.shadowOffset".GetShaderPropertyId();

        public override DeviceCaps Caps => new()
        {
            LinearRendering = false,
            DepthRangeZeroToOne = true,
            ClipSpaceYInverted = true,
            SubpixelRendering = false,
            CenterPixelOffset = 0,
            
        };

        private readonly ShaderModule[] _vertexShaders = new ShaderModule[(int)Shader.Vertex.Enum.Count];
        private readonly ShaderModule[] _pixelShaders = new ShaderModule[(int)Shader.Enum.Count];

        private readonly HashSet<int> ShaderSets = [];

        private readonly Dictionary<int, GraphicsPipeline> Pipelines = [];
        private readonly Dictionary<int, Material> Materials = [];
        private readonly Dictionary<int, uint> PipelineVariantCounts = [];

        private readonly Dictionary<int, TextureVariant> Variants = [];
        private readonly Dictionary<int,TextureSampler> Samplers = new (64);

        private readonly HashSet<Material> UsedMats = [];

        private readonly SwapChainBuffer _indexBuffer;
        private readonly SwapChainBuffer _vertexBuffer;


        private ulong _draws = 0;
        private int _drawPos = 0;
        private int _indicesFrameIndex = -1;
        private int _verticesFrameIndex = -1;
        private uint _indexCount = 0;
        private readonly List<uint> _drawStackIndices = [];
        private readonly List<uint> _drawStackVertices = [];

        private VkCommandBuffer CurrentCommandBuffer => CurrentFrameInfo.CommandBuffer;

        public int FormatHash { get; internal set; }

        public RendererFrameInfo CurrentFrameInfo;
        
        private readonly bool mFillModeNonSolid;


        public NoesisDriver()
        {

            _indexBuffer = new SwapChainBuffer(sizeof(ushort), 100, VkBufferUsageFlags.IndexBuffer, true);
            _vertexBuffer = new SwapChainBuffer(sizeof(byte), 100, VkBufferUsageFlags.VertexBuffer, true);

            GraphicsDevice.InstanceAPI.vkGetPhysicalDeviceFeatures(GraphicsDevice.PhysicalDevice, out var features);
            mFillModeNonSolid = features.fillModeNonSolid;

            LoadShaderModules();
            CreateSamplers();

            Presenter.OnSwapChainRecreation += NewSwapChain;
            Presenter.Instance.PreGraphicsPipe += PreGraphicsPipe;
        }

        private void PreGraphicsPipe(int obj)
        {
            _drawPos = 0;
            _draws = 0;
        }


        private void NewSwapChain()
        {
            _drawPos = 0;
            _indicesFrameIndex = -1;
            _verticesFrameIndex = -1;
            _indexCount = 0;
        }

        public static void ErrorCallback(Exception exception)
        {
            throw exception;
        }

        public static void LoggerCallback(LogLevel level, string channel, string message)
        {
            Console.WriteLine("{0} {1} {2}",level.ToString(),channel,message);
        }

        private void LoadShaderModules()
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
            VkSamplerCreateInfo samplerInfo = new()
            {
                mipLodBias = -0.75f
            };

            int samplerHash;
            for (MinMagFilter minmagFilter = MinMagFilter.Nearest; minmagFilter <= MinMagFilter.Linear; minmagFilter++)
            {
                for (MipFilter mipFilter = MipFilter.Disabled; mipFilter <= MipFilter.Linear; mipFilter++)
                {
                    SetMinMagFilter(minmagFilter, &samplerInfo);
                    SetMipFilter(mipFilter, &samplerInfo);

                    for (WrapMode wrapMode = WrapMode.ClampToEdge; wrapMode <= WrapMode.Mirror; wrapMode++)
                    {
                        SetAddress(wrapMode, &samplerInfo);

                        samplerHash = HashCode.Combine(minmagFilter, mipFilter, wrapMode);

                        Samplers[samplerHash] = new("NOESIS", samplerInfo);

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

            Presenter.OnSwapChainRecreation -= NewSwapChain;
            Presenter.Instance.PreGraphicsPipe -= PreGraphicsPipe;
            _indexBuffer.Dispose();
            _vertexBuffer.Dispose();
        }

        public unsafe override void BeginTile(Noesis.RenderTarget surface, Tile tile)
        {
            if (surface is NoesisRenderTarget renderTarget)
            {
                GraphicsDevice.BeginLabelCmd(CurrentCommandBuffer,string.Format("Render Tile {0}",renderTarget.Colour.Texture.AssetName));
                VkRect2D renderArea;
                renderArea.offset.x = Math.Max(0, (int)tile.X);
                renderArea.offset.y = Math.Max(0, (int)renderTarget.Colour.Height - ((int)tile.Y + (int)tile.Height));
                renderArea.extent.width = tile.Width;
                renderArea.extent.height = tile.Height;
                VkRenderingAttachmentInfo stencil = default;
                VkFormat colourFormat;
                VkFormat stencilFormat = VkFormat.Undefined;
                VkRenderingAttachmentInfo colour;
                if (renderTarget.ColourAA != null)
                {
                    colour = new()
                    {
                        imageLayout = VkImageLayout.ColorAttachmentOptimal,
                        loadOp = VkAttachmentLoadOp.Clear,
                        storeOp = VkAttachmentStoreOp.Store,
                        imageView = renderTarget.ColourAA.Texture._imageView,
                        resolveImageLayout = renderTarget.Colour.Texture._imageLayout,
                        resolveImageView = renderTarget.Colour.Texture._imageView,
                        resolveMode = VkResolveModeFlags.None,
                        clearValue = new VkClearValue(0, 0, 0, 0)
                    };
                    colourFormat = renderTarget.ColourAA.Texture.Format;
                    renderTarget.ColourAA.Texture.SetImageLayout(CurrentCommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
                else
                {
                    colour = new()
                    {
                        imageLayout = VkImageLayout.ColorAttachmentOptimal,
                        loadOp = VkAttachmentLoadOp.Clear,
                        storeOp = VkAttachmentStoreOp.Store,
                        imageView = renderTarget.Colour.Texture._imageView,
                        clearValue = new VkClearValue(0,0,0,0)
                    };

                    if (renderTarget.Colour.Texture.ImageLayout == VkImageLayout.Undefined)
                    {
                        renderTarget.Colour.Texture.SetImageLayout(CurrentCommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput);
                    }
                    else
                    {
                        renderTarget.Colour.Texture.SetImageLayout(CurrentCommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
                    }
                    colourFormat = renderTarget.Colour.Texture.Format;
                }

                if (renderTarget.Stencil != null)
                {
                    stencil = new()
                    {
                        loadOp = VkAttachmentLoadOp.Clear,
                        storeOp = VkAttachmentStoreOp.DontCare,
                        imageLayout = VkImageLayout.StencilAttachmentOptimal,
                        imageView = renderTarget.Stencil.Texture._imageView,
                        clearValue = new(0,0)
                    };

                    if (renderTarget.Stencil.Texture.ImageLayout == VkImageLayout.Undefined)
                    {
                        renderTarget.Stencil.Texture.SetImageLayout(CurrentCommandBuffer, VkImageLayout.StencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
                    }
                    else
                    {
                        renderTarget.Stencil.Texture.SetImageLayout(CurrentCommandBuffer, VkImageLayout.StencilAttachmentOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
                    }

                    stencilFormat = renderTarget.Stencil.Texture.Format;
                }

                FormatHash = HashCode.Combine(colourFormat, stencilFormat);
                VkRenderingInfo renderingInfo = new()
                {
                    colorAttachmentCount = 1,
                    pColorAttachments = &colour,
                    pStencilAttachment = renderTarget.Stencil != null ? &stencil : null,
                    renderArea = renderArea,
                    layerCount = 1,
                    flags = VkRenderingFlags.ContentsInlineKHR,
                };

                GraphicsDevice.DeviceAPI.vkCmdBeginRendering(CurrentCommandBuffer, &renderingInfo);

                GraphicsDevice.DeviceAPI.vkCmdSetScissor(CurrentCommandBuffer, 0, renderArea);
                GraphicsDevice.DeviceAPI.vkCmdSetRasterizationSamplesEXT(CurrentCommandBuffer, renderTarget.samples);
            }
        }

        public override void EndTile(Noesis.RenderTarget surface)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(CurrentCommandBuffer);
            GraphicsDevice.EndLabelCmd(CurrentCommandBuffer);
            
        }

        public override Noesis.RenderTarget CreateRenderTarget(string label, uint width, uint height, uint sampleCount, bool needsStencil)
        {
            
            NoesisRenderTarget renderTarget = new()
            {
                samples = GetSampleCount(sampleCount, GraphicsDevice.PropertiesVK10.limits)
            };
            if (needsStencil)
            {
                renderTarget.Stencil = CreateTexture(string.Format("NOESIS_{0}_STENCIL_{1}", label, Presenter.FrameCount),
                    width,
                    height,
                    PreferredFormats.STENCIL_ONLY,
                    VkSampleCountFlags.Count1,
                    VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransientAttachment,
                    VkImageAspectFlags.Stencil);
            }

            if (renderTarget.samples > VkSampleCountFlags.Count1)
            {
                renderTarget.ColourAA = CreateTexture(string.Format("NOESIS_{0}_COLOUR_AA_{1}", label, Presenter.FrameCount),
                    width,
                    height,
                    VkFormat.R8G8B8A8Unorm,
                    renderTarget.samples,
                    VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc,
                    VkImageAspectFlags.Color);

                renderTarget.Colour = CreateTexture(string.Format("NOESIS_{0}_COLOUR_{1}", label,Presenter.FrameCount),
                    width,
                    height,
                    VkFormat.R8G8B8A8Unorm,
                    VkSampleCountFlags.Count1,
                    VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
                    VkImageAspectFlags.Color);
            }
            else
            {
                renderTarget.Colour = CreateTexture(string.Format("NOESIS_{0}_COLOUR_{1}", label,Presenter.FrameCount),
                    width,
                    height,
                    VkFormat.R8G8B8A8Unorm,
                    VkSampleCountFlags.Count1,
                    VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc,
                    VkImageAspectFlags.Color);
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
                    clonedTarget.Colour = CreateTexture(string.Format("NOESIS_{0}_COLOUR_AA_{1}", label,Presenter.FrameCount),
                        width,
                        height,
                        VkFormat.R8G8B8A8Unorm,
                        VkSampleCountFlags.Count1,
                        VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
                        VkImageAspectFlags.Color);
                }
                else
                {
                    clonedTarget.Colour = CreateTexture(string.Format("NOESIS_{0}_COLOUR_{1}", label,Presenter.FrameCount),
                        width,
                        height,
                        VkFormat.R8G8B8A8Unorm,
                        VkSampleCountFlags.Count1,
                        VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc,
                        VkImageAspectFlags.Color);
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
                GraphicsDevice.DeviceAPI.vkCmdSetViewport(CurrentCommandBuffer, 0, viewport);
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

                    dst.Texture.SetImageLayout(CurrentCommandBuffer,
                        VkImageLayout.TransferDstOptimal,
                        VkPipelineStageFlags2.ColorAttachmentOutput,
                        VkPipelineStageFlags2.Transfer);

                    GraphicsDevice.DeviceAPI.vkCmdResolveImage(CurrentCommandBuffer,
                        src.Texture._vkImage,
                        VkImageLayout.TransferSrcOptimal,
                        dst.Texture._vkImage,
                        VkImageLayout.TransferDstOptimal,
                        (uint)tiles.Length,
                        regions);

                    dst.Texture.SetImageLayout(CurrentCommandBuffer,
                        VkImageLayout.ShaderReadOnlyOptimal,
                        VkPipelineStageFlags2.Transfer,
                        VkPipelineStageFlags2.FragmentShader);
                }
            }
        }

        public unsafe override nint MapIndices(uint bytes)
        {
            var frameIndex = Presenter.FrameIndex;
            if (_indicesFrameIndex != frameIndex)
            {
                _drawStackIndices.Clear();
                _drawStackIndices.Add(0);
                _indexCount = 0;
                NativeMemory.Clear(_indexBuffer.HostPtr,_indexBuffer.HostBufferSize32);
            }
            _indicesFrameIndex = frameIndex;
            uint offset = 0;
            if (_drawStackIndices.Count > 0)
            {
                offset = _drawStackIndices[^1];
            }

            if (_indexBuffer.HostBufferSize32 < offset + bytes)
            {
                _indexBuffer.Realloc((offset + bytes)*2);
            }
            _drawStackIndices.Add(offset + bytes);
            GPUBufferExtensions.WriteFromHostDelayed(_indexBuffer, Presenter.FrameIndex);
            return (nint)((byte*)_indexBuffer.HostPtr + offset);
        }

        public unsafe override nint MapVertices(uint bytes)
        {
            var frameIndex = Presenter.FrameIndex;
            if(_verticesFrameIndex != frameIndex)
            {
                _drawStackVertices.Clear();
                _drawStackVertices.Add(0);
                NativeMemory.Clear(_vertexBuffer.HostPtr,_vertexBuffer.HostBufferSize32);
            }
            _verticesFrameIndex  = frameIndex;
            uint offset = 0;
            if (_drawStackVertices.Count > 0)
            {
                offset = _drawStackVertices[^1];
            }

            if (_vertexBuffer.HostBufferSize32 < offset + bytes)
            {
                _vertexBuffer.Realloc((offset + bytes)*2);
            }
            _drawStackVertices.Add(offset + bytes);
            GPUBufferExtensions.WriteFromHostDelayed(_vertexBuffer, frameIndex);
            return (nint)((byte*)_vertexBuffer.HostPtr + offset);
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
            Texture2D texture = new(string.Format("NOESIS_{0}_{1}",label,Presenter.FrameCount), (int)width, (int)height, vkFormat, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, numLevels > 1);
            
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

                    GPUBuffer textureData = new((uint)noeTex.Texture.Format.BlockSize(), width * height, VkBufferUsageFlags.TransferSrc, true, true, false);
                    textureData.WriteToBuffer((void*)data);
                    noeTex.Texture.CopyFromBuffer(textureData, bufferCopyRegion, true);
                }
            }
        }

        public override void DrawBatch(ref Batch batch)
        {
            var state = batch.RenderState;
            var shaderV = batch.Shader.Index;
            var pixelShader = (uint)batch.PixelShader;

            var shaderHash = HashPipeline((byte)shaderV, FormatHash, pixelShader);

            if (!Pipelines.TryGetValue(shaderHash, out var pipeline))
            {
                Debugger.Break();
            }            

            var mat = GetMaterial(ref batch, pipeline);
            
            UsedMats.Add(mat);

            mat.Pipeline._uniformBuffer.Buffer.SetBuffersDirty(true);
            SetDescriptors(ref batch, mat);
            
            mat.BindCareful(CurrentFrameInfo);

            SetStencilMode(state.StencilMode);
            SetStencilRef(batch.StencilRef);
            SetRasterizerInfo(CurrentCommandBuffer, state.Wireframe);
            SetBlendInfo(CurrentCommandBuffer, state.ColorEnable, state.BlendMode);
            
            uint vertexOffset = _drawStackVertices[_drawPos] ;
            uint indexOffset = _drawStackIndices[_drawPos] / 2;
            _indexCount += batch.NumIndices * 2;
            
            var vertexBufferOffset = vertexOffset +(ulong)batch.VertexOffset ;

            Debug.Assert(vertexBufferOffset <= _vertexBuffer.VkBufferSize);
            
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(CurrentCommandBuffer, 0, _vertexBuffer.ActiveVkBuffer, vertexOffset+(ulong)batch.VertexOffset);
            GraphicsDevice.DeviceAPI.vkCmdBindIndexBuffer(CurrentCommandBuffer, _indexBuffer.ActiveVkBuffer, 0, VkIndexType.Uint16);

            uint firstIndex = batch.StartIndex + indexOffset;
            if (batch.SinglePassStereo)
            {
                GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(CurrentCommandBuffer, batch.NumIndices, 2, firstIndex, 0, 0);
            }
            else
            {
                GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(CurrentCommandBuffer, batch.NumIndices, 1, firstIndex, 0, 0);
            }
            _draws++;
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

        private TextureSampler GetSampler(in SamplerState samplerState, out int hash)
        {
            hash = HashCode.Combine(samplerState.MinMagFilter, samplerState.MipFilter, samplerState.WrapMode);

            return Samplers[hash];
        }

        private void SetTexture(Material mat, int shaderPropertyId,SamplerState samplerState, Noesis.Texture noesisTex)
        {
            if (noesisTex == null) return;
            var texture = ((NoesisTexture)noesisTex).Texture;
            var sampler = GetSampler(samplerState, out var samplerHash);
            var texSamplerHash = HashCode.Combine(texture.Hash, samplerHash);

            if (!Variants.TryGetValue(texSamplerHash, out var variant))
            {
                Variants[texSamplerHash] = variant = new(texture, sampler);
            }
            else
            {
                variant.UpdateDescriptor();
            }
            mat.SetTexture(shaderPropertyId, variant);

            var srcStage = texture.ImageLayout.GetStageFlagFromLayout();
            texture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, srcStage);
        }

        private unsafe void SetDescriptors(ref Batch batch, Material mat)
        {
            SetTexture(mat, patternId, batch.PatternSampler, batch.Pattern);
            SetTexture(mat, rampsId, batch.RampsSampler, batch.Ramps);
            SetTexture(mat, imageId, batch.ImageSampler, batch.Image);
            SetTexture(mat, glyphsId, batch.GlyphsSampler, batch.Glyphs);
            SetTexture(mat, shadowId, batch.ShadowSampler, batch.Shadow);
            

            #region u0
            var u0 = batch.VertexUniform0;
            Matrix4x4* matrices = stackalloc Matrix4x4[2];
            switch (u0.NumWords)
            {
                case 16:
                    Buffer.MemoryCopy(u0.Values.ToPointer(), matrices, sizeof(Matrix4x4)*2, sizeof(Matrix4x4));
                    mat.SetMatrix4x4(_buffer0ProjMat, matrices[0]);
                    break;
                case 32:
                    Buffer.MemoryCopy(u0.Values.ToPointer(), matrices, sizeof(Matrix4x4) * 2, sizeof(Matrix4x4) * 2);
                    mat.SetMatrix4x4Array(_buffer0ProjMat, new Span<Matrix4x4>(matrices, 2));
                    break;
                    case 0:
                    break;
                default:
                    throw new NotSupportedException(string.Format("{0} words not supported!", u0.NumWords));
            }
            #endregion

            #region u1
            var u1 = batch.VertexUniform1;
            Vector2 textureDimensions = default;
            if (u1.NumWords == 2)
            {
                Buffer.MemoryCopy(u1.Values.ToPointer(), &textureDimensions, sizeof(Vector2), sizeof(Vector2));
                mat.SetVector2(_buffer1TextDim, textureDimensions);
            }
            else if(u1.NumWords != 0)
            {
                throw new NotSupportedException(string.Format("{0} words not supported!", u0.NumWords));
            }
            #endregion

            #region u2
            var u2 = batch.PixelUniform0;
            Vector4 rgba = default;
            float opacity = default;
            Vector4 radialGrad0 = default;
            Vector4 radialGrad1 = default;
            switch (u2.NumWords)
            {
                case 4:
                        Buffer.MemoryCopy(u2.Values.ToPointer(), &rgba, sizeof(Vector4), sizeof(Vector4));
                        mat.SetVector4(_buffer2RGBA, rgba);
                        break;
                case 1:
                        Buffer.MemoryCopy(u2.Values.ToPointer(), &opacity, sizeof(float), sizeof(float));
                        mat.SetFloat(_buffer2Opac, opacity);
                        break;
                case 5:
                        Buffer.MemoryCopy(u2.Values.ToPointer(), &rgba, sizeof(Vector4), sizeof(Vector4));
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &opacity, sizeof(float), sizeof(float));
                        mat.SetVector4(_buffer2RGBA, rgba);
                        mat.SetFloat(_buffer2Opac, opacity);
                        break;
                case 7:
                        Buffer.MemoryCopy(u2.Values.ToPointer(), &radialGrad0, sizeof(Vector4), sizeof(Vector4));
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &radialGrad1, sizeof(Vector3), sizeof(Vector3));
                        radialGrad1.W = 0;
                        mat.SetVector4(_buffer2RadGrad0, radialGrad0);
                        mat.SetVector4(_buffer2RadGrad1, radialGrad1);
                        break;
                case 8:
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(float), &radialGrad0, sizeof(Vector4), sizeof(Vector4));
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4) + sizeof(float), &radialGrad1, sizeof(Vector3), sizeof(Vector3));
                        Buffer.MemoryCopy(u2.Values.ToPointer(), &opacity, sizeof(float), sizeof(float));
                        radialGrad1.W = 0;
                        mat.SetFloat(_buffer2Opac, opacity);
                        mat.SetVector4(_buffer2RadGrad0, radialGrad0);
                        mat.SetVector4(_buffer2RadGrad1, radialGrad1);
                        break;
                case 11:
                        Buffer.MemoryCopy(u2.Values.ToPointer(), &rgba, sizeof(Vector4), sizeof(Vector4));
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &radialGrad0, sizeof(Vector4), sizeof(Vector4));
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4) + sizeof(Vector4), &radialGrad1, sizeof(Vector3), sizeof(Vector3));
                        radialGrad1.W = 0;
                        mat.SetVector4(_buffer2RGBA, rgba);
                        mat.SetVector4(_buffer2RadGrad0, radialGrad0);
                        mat.SetVector4(_buffer2RadGrad1, radialGrad1);
                        break;
                case 12:
                        Buffer.MemoryCopy(u2.Values.ToPointer(), &rgba, sizeof(Vector4), sizeof(Vector4));
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(Vector4), &opacity, sizeof(float), sizeof(float));
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(float) + sizeof(Vector4), &radialGrad0, sizeof(Vector4), sizeof(Vector4));
                        Buffer.MemoryCopy((byte*)u2.Values.ToPointer() + sizeof(float) + sizeof(Vector4) + sizeof(Vector4), &radialGrad1, sizeof(Vector3), sizeof(Vector3));
                        radialGrad1.W = 0;
                        mat.SetVector4(_buffer2RGBA, rgba);
                        mat.SetFloat(_buffer2Opac, opacity);
                        mat.SetVector4(_buffer2RadGrad0, radialGrad0);
                        mat.SetVector4(_buffer2RadGrad1, radialGrad1);
                        break;
                case 0:
                    break;
                default:
                    throw new NotSupportedException(string.Format("{0} words not supported!", u2.NumWords));
            }
            #endregion

            #region u3
            var u3 = batch.PixelUniform1;
            float blend = default;
            Vector4 shadowColor = default;
            Vector2 shadowOffset = default;
            switch (u3.NumWords)
            {
                case 1:
                        Buffer.MemoryCopy(u3.Values.ToPointer(), &blend, sizeof(float), sizeof(float));
                        mat.SetFloat(_buffer3Blend, blend);
                        break;
                case 7:
                        Buffer.MemoryCopy(u3.Values.ToPointer(), &shadowColor, sizeof(Vector4), sizeof(Vector4));
                        Buffer.MemoryCopy((byte*)u3.Values.ToPointer() + sizeof(Vector4), &shadowOffset, sizeof(Vector2), sizeof(Vector2));
                        Buffer.MemoryCopy((byte*)u3.Values.ToPointer() + sizeof(Vector4) + sizeof(Vector2), &blend, sizeof(float), sizeof(float));
                        mat.SetVector4(_buffer3ShadCol, shadowColor);
                        mat.SetVector2(_buffer3ShadOff, shadowOffset);
                        mat.SetFloat(_buffer3Blend, blend);
                        break;
                case 0:
                    break;
                default:
                    throw new NotSupportedException(string.Format("{0} words not supported!", u3.NumWords));

            }
            #endregion
        }

        private void SetStencilRef(uint stencilRef)
        {
            GraphicsDevice.DeviceAPI.vkCmdSetStencilReference(CurrentCommandBuffer, VkStencilFaceFlags.FrontAndBack, stencilRef);
        }

        private void SetStencilMode(StencilMode mode)
        {
            SetDepthStencilInfo(CurrentCommandBuffer, mode);
        }

        public override void BeginOffscreenRender()
        {
            GraphicsDevice.BeginLabelCmd(CurrentCommandBuffer, "NOESIS Begin Off-Screen Render");
        }

        public override void BeginOnscreenRender()
        {
        }

        public override void EndOffscreenRender()
        {
            if(_draws > 0)
            {
                _drawPos++;
            }
            _draws = 0;
            GraphicsDevice.EndLabelCmd(CurrentCommandBuffer);
        }

        public override void EndOnscreenRender()
        {
            if(_draws > 0)
            {
                _drawPos++;
            }
            _draws = 0;
        }

        public static VkSampleCountFlags GetSampleCount(uint samples, VkPhysicalDeviceLimits limits)
        {
            // var sampleCounts = limits.framebufferColorSampleCounts & limits.framebufferStencilSampleCounts;

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

        private unsafe void CreatePipelines(string label, VkFormat colour, VkFormat stencil, Shader.Enum shader, ShaderModule psModule, uint custom)
        {
            var vsIndex = Shader.VertexForShader(shader);
            var format = Shader.FormatForVertex(vsIndex);
            var vertexShaderModule = _vertexShaders[(int)vsIndex];
            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            configInfo.colourBlendInfo.blendConstants[0] = 1;
            configInfo.colourBlendInfo.blendConstants[1] = 1;
            configInfo.colourBlendInfo.blendConstants[2] = 1;
            configInfo.colourBlendInfo.blendConstants[3] = 1;
            ;
            configInfo.colourFormats = [colour];
            configInfo.stencilFormat = stencil;
            configInfo.depthFormat = VkFormat.Undefined;

            // Vertex Input State
            List<VkVertexInputAttributeDescription> attrs = [];

            FillVertexAttributes(format, attrs);

            configInfo.AttributeDescriptions = [.. attrs];

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
                VkDynamicState.RasterizationSamplesEXT,
                VkDynamicState.StencilReference,
                VkDynamicState.ColorBlendEquationEXT,
                VkDynamicState.ColorBlendEnableEXT,
                VkDynamicState.ColorWriteEnableEXT,
                VkDynamicState.PolygonModeEXT,
            ];

            configInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.TriangleList;
            configInfo.inputAssemblyInfo.primitiveRestartEnable = false;

            var shaderSetHashCode = HashCode.Combine(colour, stencil);

            CreatePipelines(label, shader, vertexShaderModule.AssetName, psModule.AssetName, configInfo, custom, shaderSetHashCode);
        }

        private unsafe void RasterizerInfo(VkPipelineRasterizationStateCreateInfo* info)
        {

            info->depthClampEnable = false;
            info->rasterizerDiscardEnable = false;
            info->lineWidth = 1.0f;
            info->cullMode = VkCullModeFlags.None;
            info->depthBiasEnable = false;
            info->depthBiasConstantFactor = 0.0f;
            info->depthBiasClamp = 0.0f;
            info->depthBiasSlopeFactor = 0.0f;
        }

        public void SetRasterizerInfo(VkCommandBuffer commandBuffer, bool wireframe)
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

        private static unsafe bool BlendInfo(VkPipelineColorBlendAttachmentState* info)
        {
            info->colorWriteMask = VkColorComponentFlags.All;

            info->colorBlendOp = VkBlendOp.Add;
            info->alphaBlendOp = VkBlendOp.Add;
            return true;
        }

        private static unsafe void SetBlendInfo(VkCommandBuffer commandBuffer, bool colourEnable, BlendMode blendMode)
        {
            VkBool32 blendEnabled = blendMode != BlendMode.Src;
            VkBool32 colorEnabled = colourEnable;

            VkColorBlendEquationEXT blendEquation = new()
            {
                colorBlendOp = VkBlendOp.Add,
                alphaBlendOp = VkBlendOp.Add
            };

            switch (blendMode)
            {
                case BlendMode.Src:
                    break;
                case BlendMode.SrcOver:
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
                    blendEquation.srcColorBlendFactor = VkBlendFactor.One;
                    blendEquation.dstColorBlendFactor = VkBlendFactor.OneMinusSrcColor;
                    blendEquation.srcAlphaBlendFactor = VkBlendFactor.One;
                    blendEquation.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                    break;
                case BlendMode.SrcOver_Additive:
                    blendEquation.srcColorBlendFactor = VkBlendFactor.One;
                    blendEquation.dstColorBlendFactor = VkBlendFactor.One;
                    blendEquation.srcAlphaBlendFactor = VkBlendFactor.One;
                    blendEquation.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
                    break;
                case BlendMode.SrcOver_Dual:
                    blendEquation.srcColorBlendFactor = VkBlendFactor.One;
                    blendEquation.dstColorBlendFactor = VkBlendFactor.OneMinusSrc1Color;
                    blendEquation.srcAlphaBlendFactor = VkBlendFactor.One;
                    blendEquation.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrc1Alpha;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Cannot set dynamic BlendMode {0}", blendMode.ToString()));
            }

            if (blendEnabled)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetColorBlendEquationEXT(commandBuffer, 0, 1, &blendEquation);
            }

            GraphicsDevice.DeviceAPI.vkCmdSetColorWriteEnableEXT(commandBuffer, 1, &colorEnabled);
            GraphicsDevice.DeviceAPI.vkCmdSetColorBlendEnableEXT(commandBuffer, 0, 1, &blendEnabled);
        }

        private static unsafe bool DepthStencilInfo(VkPipelineDepthStencilStateCreateInfo* info)
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

            return true;
        }

        private static void SetDepthStencilInfo(VkCommandBuffer commandBuffer, StencilMode mode)
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
                    passOp = VkStencilOp.Keep;
                    stencilCompareOp = VkCompareOp.Equal;
                    break;
                case StencilMode.Equal_Keep:
                    depthTestEnable = false;
                    stencilTestEnable = true;
                    passOp = VkStencilOp.Keep;
                    stencilCompareOp = VkCompareOp.Equal;
                    break;
                case StencilMode.Equal_Incr:
                    depthTestEnable = false;
                    stencilTestEnable = true;
                    passOp = VkStencilOp.IncrementAndWrap;
                    stencilCompareOp = VkCompareOp.Equal;
                    break;
                case StencilMode.Equal_Decr:
                    depthTestEnable = false;
                    stencilTestEnable = true;
                    passOp = VkStencilOp.DecrementAndWrap;
                    stencilCompareOp = VkCompareOp.Equal;
                    break;
                case StencilMode.Clear:
                    depthTestEnable = false;
                    stencilTestEnable = true;
                    passOp = VkStencilOp.Zero;
                    stencilCompareOp = VkCompareOp.Always;
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
            GraphicsDevice.DeviceAPI.vkCmdSetStencilOp(commandBuffer, VkStencilFaceFlags.FrontAndBack, VkStencilOp.Keep, passOp, VkStencilOp.Keep, stencilCompareOp);
        }

        private unsafe void CreatePipelines(string label, Shader.Enum shader_, string vertexShader, string pixelShader, GraphicsPipelineConfigInfo configInfo, uint custom, int formatHash)
        {
            byte shaderEnum = (byte)shader_;

            VkPipelineRasterizationStateCreateInfo rasterizer = new();
            RasterizerInfo(&rasterizer);
            configInfo.rasterizationInfo = rasterizer;

            VkPipelineDepthStencilStateCreateInfo depthStencil = new();
            DepthStencilInfo(&depthStencil);
            configInfo.depthStencilInfo = depthStencil;

            VkPipelineColorBlendAttachmentState colorBlendAttachment = new();
            BlendInfo(&colorBlendAttachment);
            configInfo.colourBlendAttachment = colorBlendAttachment;

            var pipeline = GraphicsPipeline.VertexFragmentPipeline(string.Format("NOESIS_{0}_{1}_{2}", label, formatHash, custom), vertexShader, pixelShader, configInfo);
            var pipelineHash = HashPipeline(shaderEnum, formatHash, custom);
            Pipelines.Add(pipelineHash, pipeline);
        }
        
        private static int HashPipeline(byte id, int state, uint custom)
        {
            return HashCode.Combine(custom, id,state);
        }
    }
}
