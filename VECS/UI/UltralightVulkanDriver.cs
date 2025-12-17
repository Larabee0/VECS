using System;
using System.Numerics;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UltralightNet;
using UltralightNet.Platform;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;
using SDL3;
using System.Collections.Generic;

namespace VECS.UI
{
    internal class UltralightVulkanDriver : IGPUDriverSynchronized
    {
        private class TextureWithStagingBuffer : IDisposable
        {
            public Texture2D Texture;

            public SwapChainBuffer TextureStagingBuffer;

            public TextureWithStagingBuffer(uint id, int width, int height,VkFormat format, VkImageUsageFlags usageFlags)
            {
                Texture = new("UL_Tex_" + id.ToString(),
                width,
                height,
                format,
                usageFlags,
                false);

                TextureStagingBuffer = new((uint)(width * height), (uint)Vulkan.BlockSize(format), VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, true, false);
            }

            public unsafe void Update(ULBitmap bitmap)
            {
                var activeBuffer = TextureStagingBuffer.ActiveGPUBuffer;
                activeBuffer.WriteToBuffer(bitmap.LockPixels(), bitmap.Size);
                bitmap.UnlockPixels();
                Texture.CopyFromBuffer(TextureStagingBuffer.ActiveGPUBuffer);
            }

            public void Dispose()
            {
                TextureStagingBuffer.Dispose();
            }
        }

        private class GeometryBuffer : IDisposable
        {
            public SwapChainBuffer Buffer;

            public uint VertexBufferSize;
            public uint IndexBufferSize;
            public uint IndexBufferOffset;

            public GeometryBuffer(SwapChainBuffer buffer, uint vertexBufferSize, uint indexBufferSize, uint indexBufferOffset)
            {
                Buffer = buffer;
                VertexBufferSize = vertexBufferSize;
                IndexBufferSize = indexBufferSize;
                IndexBufferOffset = indexBufferOffset;
            }

            public unsafe void Update(ULVertexBuffer vertexBuffer, ULIndexBuffer indexBuffer)
            {
                VertexBufferSize = vertexBuffer.size;
                IndexBufferSize = indexBuffer.size;
                IndexBufferOffset = vertexBuffer.size;
                var ptr = Buffer.HostPtr;
                System.Buffer.MemoryCopy(vertexBuffer.data, ptr, Buffer.HostBufferSize, vertexBuffer.size);

                ptr = (new UIntPtr(ptr) + vertexBuffer.size).ToPointer();

                System.Buffer.MemoryCopy(indexBuffer.data, ptr, Buffer.HostBufferSize - vertexBuffer.size, indexBuffer.size);
            }

            public void Dispose()
            {
                Buffer.Dispose();
            }
        }

        private class ULResourceLibrary<T> : IDisposable where T : IDisposable
        {
            private readonly ConcurrentDictionary<uint, T> _resources = [];
            private readonly ConcurrentStack<uint> _freeResourceIds = [];

            private uint _topResourceId = 0;

            public T this[uint resourceId] => _resources[resourceId];

            public bool TryAddResource(uint resourceId, T resource)
            {
                return _resources.TryAdd(resourceId, resource);
            }

            public bool TryDestroyResoure(uint resourceId, out T resource)
            {
                if (_resources.TryRemove(resourceId, out resource))
                {
                    _freeResourceIds.Push(resourceId);
                    return true;
                }
                return false;
            }

            public uint NextResourceId()
            {
                if (_freeResourceIds.TryPop(out var freeTextureIds))
                {
                    return freeTextureIds;
                }
                else
                {
                    return Interlocked.Increment(ref _topResourceId);
                }
            }

            public void Dispose()
            {
                foreach (var resource in _resources.Values)
                {
                    resource.Dispose();
                }
            }
        }

        public const VkFormat ImageFormat = VkFormat.B8G8R8A8Unorm;

        private readonly ULResourceLibrary<TextureWithStagingBuffer> _textureLibrary = new();
        private readonly ULResourceLibrary<TextureWithStagingBuffer> _renderBufferLibrary = new();
        private readonly ULResourceLibrary<GeometryBuffer> _geometryLibrary = new();

        private readonly Material _ulFill;
        private readonly Material _ulFillNoBlend;
        private readonly Material _ulFillPath;

        private readonly Queue<ULCommand> _commands = [];

        public unsafe UltralightVulkanDriver()
        {
            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            
            // common
            configInfo.colourFormats[0] = ImageFormat;

            configInfo.BindingDescriptions = [
                new()
                {
                    binding = 0,
                    stride = 140,
                    inputRate = VkVertexInputRate.Vertex
                }
            ];
            configInfo.AttributeDescriptions = [
                new (){
                    binding = 0,
                    location = 0,
                    format = VkFormat.R32G32Sfloat,
                    offset = 0
                }, new(){
                    binding = 0,
                    location = 1,
                    format = VkFormat.R8G8B8A8Unorm,
                    offset = 8
                }, new(){
                    binding = 0,
                    location = 2,
                    format = VkFormat.R32G32Sfloat,
                    offset = 12
                }, new(){ // in_ObjCoord
					binding = 0,
                    location = 3,
                    format = VkFormat.R32G32Sfloat,
                    offset = 20
                }, new(){ // in_Data0
					binding = 0,
                    location = 4,
                    format = VkFormat.R32G32B32A32Sfloat,
                    offset = 28
                }, new(){ // in_Data1
					binding = 0,
                    location = 5,
                    format = VkFormat.R32G32B32A32Sfloat,
                    offset = 44
                }, new(){ // in_Data2
					binding = 0,
                    location = 6,
                    format = VkFormat.R32G32B32A32Sfloat,
                    offset = 60
                }, new(){ // in_Data3
					binding = 0,
                    location = 7,
                    format = VkFormat.R32G32B32A32Sfloat,
                    offset = 76
                }, new(){ // in_Data4
					binding = 0,
                    location = 8,
                    format = VkFormat.R32G32B32A32Sfloat,
                    offset = 92
                }, new(){ // in_Data5
					binding = 0,
                    location = 9,
                    format = VkFormat.R32G32B32A32Sfloat,
                    offset = 108
                }, new(){ // in_Data6
					binding = 0,
                    location = 10,
                    format = VkFormat.R32G32B32A32Sfloat,
                    offset = 124
                }
            ];

            configInfo.rasterizationInfo.cullMode = VkCullModeFlags.None;

            configInfo.depthStencilInfo.depthTestEnable = false;
            configInfo.depthStencilInfo.depthCompareOp = VkCompareOp.Never;

            configInfo.colourBlendAttachment.blendEnable = true;
            configInfo.colourBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
            configInfo.colourBlendAttachment.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;


            _ulFill = new Material("UL_Fill", "ul_fill.vert", "ul_fill.frag", configInfo);

            // path uses blending ig
            var pathConfigInfo = configInfo;

            // no blend
            configInfo.colourBlendAttachment.blendEnable = false;
            configInfo.colourBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
            configInfo.colourBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.One;
            configInfo.colourBlendAttachment.srcColorBlendFactor = VkBlendFactor.One;
            configInfo.colourBlendAttachment.dstColorBlendFactor = VkBlendFactor.One;

            configInfo.colourBlendInfo.logicOp = VkLogicOp.Clear;

            _ulFillNoBlend = new Material("UL_FillNoBlend", "ul_fill.vert", "ul_fill.frag", configInfo);

            pathConfigInfo.BindingDescriptions = [
                 new()
                 {
                    binding = 0,
                    stride = 20,
                    inputRate = VkVertexInputRate.Vertex
                 }
            ];

            pathConfigInfo.AttributeDescriptions = [
                new (){
                    binding = 0,
                    location = 0,
                    format = VkFormat.R32G32Sfloat,
                    offset = 0
                }, new(){
                    binding = 0,
                    location = 1,
                    format = VkFormat.R8G8B8A8Unorm,
                    offset = 8
                }, new(){
                    binding = 0,
                    location = 2,
                    format = VkFormat.R32G32Sfloat,
                    offset = 12
                },
            ];

            _ulFillPath = new Material("UL_Fill_Path", "ul_fill_path.vert", "ul_fill_path.frag", pathConfigInfo);
        }

        #region RenderBuffer
        public void CreateRenderBuffer(uint renderBufferId, ULRenderBuffer renderBuffer)
        {
            Console.WriteLine("Create Render Buffer");
            var textureEntry = _textureLibrary[renderBuffer.TextureId];

            if (!_renderBufferLibrary.TryAddResource(renderBufferId, textureEntry))
            {
                throw new InvalidOperationException(string.Format("UL RenderBufferId {0} Already exists in _renderBufferLibrary!", renderBufferId));
            }
        }

        public void DestroyRenderBuffer(uint renderBufferId)
        {
            Console.WriteLine("Destroy RenderBuffer");
            if (!_renderBufferLibrary.TryDestroyResoure(renderBufferId, out _))
            {
                throw new InvalidOperationException(string.Format("Failed to remove RenderBufferId {0}", renderBufferId));
            }
        }

        public uint NextRenderBufferId()
        {
            return _renderBufferLibrary.NextResourceId();
        }
        #endregion

        #region Texture
        public void CreateTexture(uint textureId, ULBitmap bitmap)
        {
            Console.WriteLine("Create Texure");
            bool isRenderTarget = bitmap.IsEmpty;

            TextureWithStagingBuffer ulBitmap = new(textureId,
                (int)bitmap.Width,
                (int)bitmap.Height,
                bitmap.Format == ULBitmapFormat.BGRA8_UNORM_SRGB ? ImageFormat : VkFormat.R8Unorm,
                VkImageUsageFlags.Sampled|(isRenderTarget ? VkImageUsageFlags.ColorAttachment : VkImageUsageFlags.TransferDst));

            

            if (!_textureLibrary.TryAddResource(textureId, ulBitmap))
            {
                throw new InvalidOperationException(string.Format("UL TextureId {0} Already exists in _textureLibrary!", textureId));
            }

            if (!isRenderTarget)
            {
                UpdateTexture(textureId, bitmap);
            }
        }

        public void DestroyTexture(uint textureId)
        {
            Console.WriteLine("Destroy Texture");
            if (_textureLibrary.TryDestroyResoure(textureId,out var texture))
            {
                texture.Dispose();
            }
            else
            {
                throw new InvalidOperationException(string.Format("Failed to remove TextureId {0}",textureId));
            }
        }

        public uint NextTextureId()
        {
            return _textureLibrary.NextResourceId();
        }

        public void UpdateTexture(uint textureId, ULBitmap bitmap)
        {
            _textureLibrary[textureId].Update(bitmap);
        }
        #endregion

        #region Geometry
        public void CreateGeometry(uint geometryId, ULVertexBuffer vertexBuffer, ULIndexBuffer indexBuffer)
        {
            // Debug.Assert(vertexBuffer.size % 256 == 0, "nonCoherentAtomSize");
            // Debug.Assert(indexBuffer.size % 256 == 0, "nonCoherentAtomSize");
            Console.WriteLine("Create Geometry");
            SwapChainBuffer geometryBuffer = new SwapChainBuffer(
                vertexBuffer.size + indexBuffer.size,
                1,
                VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
                true);

            if (!_geometryLibrary.TryAddResource(geometryId, new(geometryBuffer, vertexBuffer.size, indexBuffer.size, vertexBuffer.size)))
            {
                throw new InvalidOperationException(string.Format("UL GeometryId {0} Already exists in _geometryLibrary!", geometryId));
            }

            UpdateGeometry(geometryId, vertexBuffer, indexBuffer);
        }

        public void DestroyGeometry(uint geometryId)
        {
            Console.WriteLine("Destroy Geometry");
            if (_geometryLibrary.TryDestroyResoure(geometryId,out var geometry))
            {
                geometry.Dispose();
            }
            else
            {
                throw new InvalidOperationException(string.Format("Failed to remove GeometryId {0}", geometryId));
            }
        }

        public void UpdateGeometry(uint geometryId, ULVertexBuffer vertexBuffer, ULIndexBuffer indexBuffer)
        {
            _geometryLibrary[geometryId].Update(vertexBuffer,indexBuffer);
        }

        public uint NextGeometryId()
        {
            return _geometryLibrary.NextResourceId();
        }
        #endregion

        private readonly struct RenderPassInfo
        {
            public readonly bool Clear;
            public readonly uint RenderBuffer;
            public readonly VkExtent2D Dimentions;

            public RenderPassInfo(bool clear, uint renderBuffer, VkExtent2D dimentions)
            {
                Clear = clear;
                RenderBuffer = renderBuffer;
                Dimentions = dimentions;
            }

            public RenderPassInfo(ULCommand command)
            {
                Clear = command.CommandType is ULCommandType.ClearRenderBuffer;
                RenderBuffer = command.GPUState.RenderBufferId;
                Dimentions = new(command.GPUState.ViewportWidth, command.GPUState.ViewportHeight);
            }
        }

        public unsafe void UpdateCommandList(ULCommandList commandList)
        {
            Console.WriteLine("UL Update CommandList");
            var commands = commandList.AsSpan();
            _commands.EnsureCapacity(commands.Length);
            for (int i = 0; i < commands.Length; i++)
            {
                _commands.Enqueue(commands[i]);
            }

        }

        public Texture2D GetViewTexture(View view)
        {
            var id = view.RenderTarget.TextureId;
            return _textureLibrary[id].Texture;
        }

        public unsafe void ExecuteCommandList(RendererFrameInfo frameInfo)
        {
            if(_commands.Count == 0) return;
            Console.WriteLine("UL ExecuteCommandList {0}");
            uint currentRenderBuffer = 0;
            int uniformBufferId = 0;
            var commandBuffer = frameInfo.CommandBuffer;
            while (_commands.Count > 0)
            {
                var command = _commands.Dequeue();
                Debug.Assert(command.CommandType is ULCommandType.ClearRenderBuffer or ULCommandType.DrawGeometry);
                var gpuState = command.GPUState;
                Debug.Assert(gpuState.RenderBufferId is not 0);
                //Debug.Assert((command.CommandType is ULCommandType.DrawGeometry) && (gpuState.ShaderType is ULShaderType.Fill or ULShaderType.FillPath));

                BeginRenderPass(commandBuffer, ref currentRenderBuffer, new(command));

                if (command.CommandType is ULCommandType.DrawGeometry)
                {
                    Material mat = null;
                    if (gpuState.ShaderType == ULShaderType.Fill)
                    {

                        mat = gpuState.EnableBlend ? _ulFill : _ulFillNoBlend;
                        var texutre1 = _textureLibrary[gpuState.Texture1Id].Texture;
                        mat.SetTexture("Texture1".GetShaderPropertyId(), uniformBufferId, texutre1);
                        if (gpuState.Texture2Id != 0)
                        {
                            mat.SetTexture("Texture2".GetShaderPropertyId(), uniformBufferId, _textureLibrary[gpuState.Texture2Id].Texture);
                        }
                        else
                        {
                            mat.SetTexture("Texture2".GetShaderPropertyId(), uniformBufferId, texutre1);
                        }
                    }
                    else if (gpuState.ShaderType == ULShaderType.FillPath)
                    {
                        mat = _ulFillPath;
                    }
                    Draw(frameInfo, command, uniformBufferId, mat);
                    uniformBufferId++;
                }
            }

            if (currentRenderBuffer != 0)
            {
                GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            }
        }

        private unsafe void Draw(RendererFrameInfo frameInfo, ULCommand command, int variant,Material mat)
        {
            var gpuState = command.GPUState;
            var commandBuffer = frameInfo.CommandBuffer;
            mat.SetMatrix4x4("uni.Transform".GetShaderPropertyId(), variant, gpuState.Transform.ApplyProjection(gpuState.ViewportWidth, gpuState.ViewportHeight, false));
            mat.SetUint("uni.ClipSize".GetShaderPropertyId(), variant, gpuState.ClipSize);
            mat.SetFloatArray("uni.Scalar4".GetShaderPropertyId(), variant, gpuState.Scalar);
            if (gpuState.ClipSize > 0)
            {
                mat.SetMatrix4x4Array("uni.Clip".GetShaderPropertyId(), variant, gpuState.Clip);
            }
            mat.BindAll(frameInfo, variant);
            var geometry = _geometryLibrary[command.GeometryId];
            var vkBuffer = geometry.Buffer.ActiveVkBuffer;

            GraphicsDevice.DeviceAPI.vkCmdBindIndexBuffer(commandBuffer, vkBuffer, geometry.IndexBufferOffset, VkIndexType.Uint32);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(commandBuffer, 0,vkBuffer);
            GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(commandBuffer, command.IndicesCount, 1, command.IndicesOffset, 0, 0);
        }

        private unsafe void BeginRenderPass(VkCommandBuffer commandBuffer, ref uint currentRenderBuffer, RenderPassInfo passInfo)
        {
            if (passInfo.Clear && currentRenderBuffer == passInfo.RenderBuffer)
            {
                Debug.Assert(currentRenderBuffer != passInfo.RenderBuffer, "Double Ultralight RenderBuffer clear"); // this shouldn't happen
            }

            if (currentRenderBuffer == passInfo.RenderBuffer)
            {
                return;
            }
            else if (currentRenderBuffer is not 0)
            {
                GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            }

            var renderBufferTarget = _renderBufferLibrary[passInfo.RenderBuffer];

            if(renderBufferTarget.Texture.ImageLayout != VkImageLayout.ColorAttachmentOptimal)
            {
                renderBufferTarget.Texture.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Transfer | VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            }

            VkClearColorValue clearColorValue = new(0,0,0,0);

            VkRenderingAttachmentInfo attachmentInfo = new()
            {
                imageLayout = renderBufferTarget.Texture.ImageLayout,
                imageView = renderBufferTarget.Texture._imageView,
                clearValue = new(clearColorValue),
                loadOp = passInfo.Clear ? VkAttachmentLoadOp.Clear : VkAttachmentLoadOp.Load,
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(new(0,0), passInfo.Dimentions),
                colorAttachmentCount = 1,
                layerCount = 1,
                pColorAttachments = &attachmentInfo
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            currentRenderBuffer = passInfo.RenderBuffer;
        }

        public void BeginSynchronize()
        {
            Console.WriteLine("UL Begin Sync");
        }

        public void EndSynchronize()
        {
            Console.WriteLine("UL End Sync");
        }

        public void Dispose()
        {
            _textureLibrary.Dispose();
            _geometryLibrary.Dispose();
        }
    }
}
