using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UltralightNet;
using UltralightNet.Platform;
using VECS.LowLevel;
using Vortice.Vulkan;

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
                Buffer.ActiveGPUBuffer.WriteToBuffer(vertexBuffer.data, vertexBuffer.size, 0);
                Buffer.ActiveGPUBuffer.WriteToBuffer(indexBuffer.data, indexBuffer.size, vertexBuffer.size);
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

        private VkCommandBuffer[] CommandBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES];
        private int frameIndex = 0;

        public unsafe UltralightVulkanDriver()
        {
            VkCommandBufferAllocateInfo allocInfo = new()
            {
                commandBufferCount = SwapChain.MAX_CONCURRENT_FRAMES_UINT,
                commandPool = GraphicsDevice.MainCommandPool,
                level = VkCommandBufferLevel.Secondary
            };
            fixed (VkCommandBuffer* pCommandBuffers = &CommandBuffers[0]) {
                GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(GraphicsDevice.Device, &allocInfo,pCommandBuffers);
            } 
        }

        #region RenderBuffer
        public void CreateRenderBuffer(uint renderBufferId, ULRenderBuffer renderBuffer)
        {
            var textureEntry = _textureLibrary[renderBuffer.TextureId];

            if (!_renderBufferLibrary.TryAddResource(renderBufferId, textureEntry))
            {
                throw new InvalidOperationException(string.Format("UL RenderBufferId {0} Already exists in _renderBufferLibrary!", renderBufferId));
            }
        }

        public void DestroyRenderBuffer(uint renderBufferId)
        {
            if(!_renderBufferLibrary.TryDestroyResoure(renderBufferId, out _))
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
            if(_textureLibrary.TryDestroyResoure(textureId,out var texture))
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
            Debug.Assert(vertexBuffer.size % 256 == 0, "nonCoherentAtomSize");
            Debug.Assert(indexBuffer.size % 256 == 0, "nonCoherentAtomSize");

            SwapChainBuffer geometryBuffer = new SwapChainBuffer(
                vertexBuffer.size + indexBuffer.size,
                1,
                VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
                true,
                false);

            if (!_geometryLibrary.TryAddResource(geometryId, new(geometryBuffer, vertexBuffer.size, indexBuffer.size, vertexBuffer.size)))
            {
                throw new InvalidOperationException(string.Format("UL GeometryId {0} Already exists in _geometryLibrary!", geometryId));
            }

            UpdateGeometry(geometryId, vertexBuffer, indexBuffer);
        }

        public void DestroyGeometry(uint geometryId)
        {
            if(_geometryLibrary.TryDestroyResoure(geometryId,out var geometry))
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

        public unsafe void UpdateCommandList(ULCommandList commandList)
        {
            uint currentRenderBuffer = 0;
            
            var commands = commandList.AsSpan();
            var commandBuffer = CommandBuffers[frameIndex];
            foreach (var command in commands)
            {
                Debug.Assert(command.CommandType is ULCommandType.ClearRenderBuffer or ULCommandType.DrawGeometry);
                Debug.Assert(command.GPUState.RenderBufferId is not 0);

                BeginRenderPass(commandBuffer, ref currentRenderBuffer, command.CommandType is ULCommandType.ClearRenderBuffer, command.GPUState.RenderBufferId, new(command.GPUState.ViewportWidth, command.GPUState.ViewportHeight));
                if (command.CommandType is ULCommandType.DrawGeometry)
                {
                    var geometry = _geometryLibrary[command.GeometryId];
                    var vkBuffer = geometry.Buffer.ActiveVkBuffer;
                    GraphicsDevice.DeviceAPI.vkCmdBindIndexBuffer(commandBuffer, vkBuffer, geometry.IndexBufferOffset, VkIndexType.Uint32);
                    GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(commandBuffer, 0, vkBuffer);
                    GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(commandBuffer, command.IndicesCount, 1, command.IndicesOffset, 0, 0);
                }
            }

            if (currentRenderBuffer is not 0)
                GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
        }

        private unsafe void BeginRenderPass(VkCommandBuffer commandBuffer, ref uint currentRenderBuffer, bool clear, uint renderBuffer, VkExtent2D dimensions)
        {
            if (clear && currentRenderBuffer == renderBuffer)
            {
                Debug.Assert(currentRenderBuffer != renderBuffer, "Double Ultralight RenderBuffer clear"); // this shouldn't happen
            }

            if (currentRenderBuffer == renderBuffer)
            {
                return;
            }
            else if (currentRenderBuffer is not 0)
            {
                GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            }

            var renderBufferTarget = _renderBufferLibrary[renderBuffer];

            if(renderBufferTarget.Texture.ImageLayout != VkImageLayout.ColorAttachmentOptimal)
            {
                renderBufferTarget.Texture.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Transfer | VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            }

            VkClearColorValue clearColorValue = new(209f / 255f, 113f / 255f, 177f / 255f);

            VkRenderingAttachmentInfo attachmentInfo = new()
            {
                imageLayout = renderBufferTarget.Texture.ImageLayout,
                imageView = renderBufferTarget.Texture._imageView,
                clearValue = new(clearColorValue),
                loadOp = clear ? VkAttachmentLoadOp.Clear : VkAttachmentLoadOp.Load,
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(new(0,0),dimensions),
                colorAttachmentCount = 1,
                layerCount = 1,
                pColorAttachments = &attachmentInfo
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
        }

        public void BeginSynchronize()
        {
            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(CommandBuffers[frameIndex], VkCommandBufferUsageFlags.None);
        }

        public void EndSynchronize()
        {
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(CommandBuffers[frameIndex]);
        }

        public void Dispose()
        {
            _textureLibrary.Dispose();
            _geometryLibrary.Dispose();
        }
    }
}
