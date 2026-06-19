using Hexa.NET.ImGui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{
    public class IMGUI : IDisposable
    {
        private class TextureVariant
        {
            public Texture2D Texture;
            public Material Variant;
        }

        private const float FONT_SCALE = 1.0f;
        private static readonly int fontSamplerId = "fontSampler".GetShaderPropertyId();
        private static readonly int inputTextureId = "inputTexture".GetShaderPropertyId();
        internal static readonly Queue<Material> _freeVariants = [];

        private readonly SDL3Window _outputWindow;

        private ImGuiContextPtr _context;

        private SwapChainBuffer _vertexBuffer;
        private SwapChainBuffer _indexBuffer;

        private readonly RenderTarget _outputTarget;

        private Material _blitVariant;

        private readonly Dictionary<ImTextureID, TextureVariant> _textureVariants = [];
        private unsafe readonly Dictionary<uint, FontPtr> _fonts = [];

        public Vector4 ClearColour;

        private struct FontPtr
        {
            public unsafe ImFont* Font;
        }
        
        public unsafe IMGUI(SDL3Window targetWindow)
        {
            _outputWindow = targetWindow;
            _context = ImGui.CreateContext();
            
            ImGui.SetCurrentContext(_context);

            ImGui.GetPlatformIO().RendererTextureMaxHeight = 4096;
            ImGui.GetPlatformIO().RendererTextureMaxWidth = 4096;
            ImGui.GetIO().FontAllowUserScaling = true;
            ImGui.GetIO().ConfigDpiScaleFonts = true;
            ImGui.GetIO().BackendFlags = ImGuiBackendFlags.RendererHasTextures;
            
            //SetStyle(0);
            //ImGui.GetStyle().FontSizeBase = 20.0f;
            ImGui.GetIO().Fonts.AddFontDefault();
            //ImGui.GetIO().Fonts.AddFontDefault();  // Load embedded scalable font.
            Resize(_outputWindow.WindowExtent.width, _outputWindow.WindowExtent.height);

            _context = ImGui.GetCurrentContext();

            _outputTarget = new(_outputWindow.WindowName, (int)_outputWindow.WindowExtent.width, (int)_outputWindow.WindowExtent.height,VkFormat.R8G8B8A8Unorm, VkSamplerAddressMode.ClampToEdge);


        }

        public unsafe uint AddFontTTF(string fontPath, int size)
        {
            Debug.Assert(Path.Exists(fontPath));
            ImGui.SetCurrentContext(_context);
                        
            ImFont* imFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontPath, size);

            _fonts[imFont->FontId] = new() { Font = imFont };
            _context = ImGui.GetCurrentContext();

            return imFont->FontId;
        }

        public unsafe ImFont* GetFont(uint fontId)
        {
            return _fonts[fontId].Font;
        }

        public void AddTexture(ImTextureID textureID, Texture2D texture)
        {
            ImGui.SetCurrentContext(_context);
            if (!_freeVariants.TryDequeue(out var variant))
            {
                variant = EnginePipes.IMGUI.Create(string.Format("IMGUI_VAR_{0}", textureID.ToString()));
            }
            _textureVariants[textureID] = new()
            {
                Texture = texture,
                Variant = variant
            };
            variant.SetTexture(fontSamplerId, texture);

            _context = ImGui.GetCurrentContext();
        }

        public Texture2D GetTexture(ImTextureID imTextureID)
        {
            if(_textureVariants.TryGetValue(imTextureID, out TextureVariant value))
            {
                return value.Texture;
            }
            return null;
        }

        public bool HasTexture(ImTextureID imTextureID)
        {
            return _textureVariants.ContainsKey(imTextureID);
        }

        private static void Resize(uint width, uint height)
        {
            ImGui.GetIO().DisplaySize = new(width, height);
            ImGui.GetIO().DisplayFramebufferScale = new(1.0f);
        }

        private static void SetStyle(int index)
        {
            switch (index)
            {
                case 0:
                    ImGui.StyleColorsClassic();
                    break;
                case 1:
                    ImGui.StyleColorsDark();
                    break;
                case 2:
                    ImGui.StyleColorsLight();
                    break;
            }
        }

        private unsafe void ProcessTextureUpdates(ImDrawDataPtr drawData)
        {
            if (drawData.Textures.Data == null) return;

            for (int i = 0; i < drawData.Textures.Size; i++)
            {
                ImTextureDataPtr textureData = drawData.Textures.Data[i];
                UpdateTexture(textureData);
            }
        }

        protected virtual void UpdateTexture(ImTextureDataPtr textureData)
        {
            switch (textureData.Status)
            {
                case ImTextureStatus.WantCreate:
                    CreateTexture(textureData);
                    break;

                case ImTextureStatus.WantUpdates:
                    UpdateTextureData(textureData);
                    break;

                case ImTextureStatus.WantDestroy:
                    DestroyTexture(textureData);
                    break;

                case ImTextureStatus.Ok:
                    // Nothing to do
                    break;
            }
        }

        private void CreateTexture(ImTextureDataPtr textureData)
        {
            VkFormat format = textureData.Format == ImTextureFormat.Rgba32 ? VkFormat.R8G8B8A8Unorm : VkFormat.A8Unorm;                      
            var texture = new Texture2D(
                string.Format("IMGUI_{1}_{0}",textureData.UniqueID.ToString(),_outputWindow.WindowName),
                textureData.Width,
                textureData.Height,
                format,
                VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                VkSamplerAddressMode.ClampToEdge,
                0,
                false,
                VkCompareOp.Never,
                false);

            if(!_freeVariants.TryDequeue(out var variant))
            {
                variant = EnginePipes.IMGUI.Create(string.Format("IMGUI_VAR_{1}_{0}", textureData.TexID.Handle.ToString(), _outputWindow.WindowName));
            }

            

            _textureVariants[textureData.TexID] = new()
            {
                Texture = texture,
                Variant = variant
            };

            
            variant.SetTexture(fontSamplerId, texture);
            UpdateTextureData(textureData);
        }

        private unsafe void UpdateTextureData(ImTextureDataPtr textureData)
        {
            IntPtr texId = textureData.GetTexID();
            if (!_textureVariants.TryGetValue(texId, out var textureVariant))
            {
                return;
            }

            var texture = textureVariant.Texture;

            VkFormat newFormat = textureData.Format == ImTextureFormat.Rgba32 ? VkFormat.R8G8B8A8Unorm : VkFormat.A8Unorm;
            if (texture.Width != textureData.Width || texture.Height != textureData.Height || texture.Format != newFormat)
            {
                _textureVariants.Remove(texId);
                _freeVariants.Enqueue(textureVariant.Variant);
                texture.Dispose();
                CreateTexture(textureData);
                return;
            }

            if (textureData.Pixels != null)
            {
                uint pixelCount = (uint)(textureData.Width * textureData.Height);
                uint bytesPerPixel = textureData.Format == ImTextureFormat.Rgba32 ? 4u : 1;
                uint dataSize = pixelCount * bytesPerPixel;
                GPUBuffer stagingBuffer = new(bytesPerPixel, pixelCount, VkBufferUsageFlags.TransferSrc, true, false, false);
                Buffer.MemoryCopy(textureData.Pixels, stagingBuffer.HostPtr, dataSize, dataSize);
                //GPUBufferExtensions.WriteFromHostDelayed(stagingBuffer, 0, Vulkan.VK_WHOLE_SIZE);
                stagingBuffer.WriteFromHostBuffer();
                texture.CopyFromBuffer(stagingBuffer,true);
            }
            textureData.SetStatus(ImTextureStatus.Ok);
        }

        private void DestroyTexture(ImTextureDataPtr textureData)
        {
            IntPtr texId = textureData.GetTexID();
            if (_textureVariants.TryGetValue(texId, out var textureVariant))
            {
                _freeVariants.Enqueue(textureVariant.Variant);
                textureVariant.Texture.Dispose();
                _textureVariants.Remove(texId);
            }
        }

        private static void NewFrame()
        {
            //ImGui.ShowDemoWindow();
        }

        public void Update()
        {
            ImGui.SetCurrentContext(_context);
            var io = ImGui.GetIO();

            if(_outputWindow.WindowExtent.width != _outputTarget.Target.Width || _outputWindow.WindowExtent.height != _outputTarget.Target.Height)
            {
                io.DisplaySize = new(_outputWindow.WindowExtent.width, _outputWindow.WindowExtent.height);
                _outputTarget.Resize((int)_outputWindow.WindowExtent.width, (int)_outputWindow.WindowExtent.height);

                _blitVariant?.SetTexture(inputTextureId, _outputTarget.Target);
            }

            io.DeltaTime = Time.DeltaTime;
            io.MousePos = _outputWindow.InputManager.MousePos;
            
            io.MouseDown[0] = _outputWindow.InputManager.GetMouseButton(0);
            io.MouseDown[1] = _outputWindow.InputManager.GetMouseButton(1);
            io.MouseDown[2] = _outputWindow.InputManager.GetMouseButton(2);
            if(_context.FrameCount  != 0 && _context.FrameCount != _context.FrameCountEnded)
            {
                ImGui.EndFrame();
            }
            ImGui.NewFrame();
            _context = ImGui.GetCurrentContext();
        }

        private unsafe void UpdateBuffers()
        {
            var drawData = ImGui.GetDrawData();

            uint vertexCount = (uint)drawData.TotalVtxCount;
            uint indexCount = (uint)drawData.TotalIdxCount;

            if ((vertexCount == 0) || (indexCount == 0))
            {
                return;
            }
            
            _vertexBuffer ??= new SwapChainBuffer((uint)sizeof(ImDrawVert), vertexCount, VkBufferUsageFlags.VertexBuffer, true);
            _indexBuffer ??= new SwapChainBuffer(sizeof(ushort), indexCount, VkBufferUsageFlags.IndexBuffer, true);

            if(_vertexBuffer.InstanceCount < vertexCount)
            {
                _vertexBuffer.Realloc(vertexCount);
            }

            if (_vertexBuffer.InstanceCount < indexCount)
            {
                _indexBuffer.Realloc(indexCount);
            }

            ImDrawVert* ptr_vertex = (ImDrawVert*)_vertexBuffer.HostPtr;
            ushort* ptr_index = (ushort*)_indexBuffer.HostPtr;
            for (int i = 0; i < drawData.CmdListsCount; i++)
            {
                var cmdList = drawData.CmdLists[i];

                Buffer.MemoryCopy(cmdList.VtxBuffer.Data, ptr_vertex,  cmdList.VtxBuffer.Size * sizeof(ImDrawVert), cmdList.VtxBuffer.Size * sizeof(ImDrawVert));
                Buffer.MemoryCopy(cmdList.IdxBuffer.Data, ptr_index,  cmdList.IdxBuffer.Size * sizeof(ushort), cmdList.IdxBuffer.Size * sizeof(ushort));
                ptr_vertex += cmdList.VtxBuffer.Size;
                ptr_index += cmdList.IdxBuffer.Size;
            }
            _vertexBuffer.SetUsedInstanceCount(vertexCount);
            _indexBuffer.SetUsedInstanceCount(indexCount);
        }

        public unsafe void Draw(RendererFrameInfo frameInfo)
        {
            if (_blitVariant == null)
            {
                _blitVariant = EnginePipes.Blit.Create(string.Format("{0}_Blit", _outputWindow.WindowName));
                _blitVariant.SetTexture(inputTextureId, _outputTarget.Target);
            }

            ImGui.SetCurrentContext(_context);

            ImGui.Render();
            var imDrawData = ImGui.GetDrawData();
            ProcessTextureUpdates(imDrawData);
            UpdateBuffers();
            if (imDrawData.CmdListsCount <= 0)
            {
                return;
            }

            var io = ImGui.GetIO();
            Vector2 scale = new(2.0f / io.DisplaySize.X, 2.0f / io.DisplaySize.Y);
            Vector2 translate = new(-1);
            int vertexOffset = 0;
            uint indexOffset = 0;
            var vertexBuffer = _vertexBuffer[Presenter.FrameIndex].VkBuffer;
            ulong offset = 0;
            Material mat;

            GPUBufferExtensions.WriteFromHostDelayed(_vertexBuffer, Presenter.FrameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(_indexBuffer, Presenter.FrameIndex);



            if (_outputTarget.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                _outputTarget.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            else if (_outputTarget.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                _outputTarget.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
            }

            VkRenderingAttachmentInfo colourAttachments = new()
            {
                imageView = _outputTarget.VkImageView,
                imageLayout = _outputTarget.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(ClearColour.X, ClearColour.Y, ClearColour.Z, ClearColour.W)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)_outputTarget.Target.Width, (uint)_outputTarget.Target.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachments,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);

            GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, new VkRect2D(new VkOffset2D(0, 0), new VkExtent2D(_outputTarget.Target.Width, _outputTarget.Target.Height)));
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, 0, io.DisplaySize.X, io.DisplaySize.Y);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffers(frameInfo.CommandBuffer, 0, 1, &vertexBuffer, &offset);
            GraphicsDevice.DeviceAPI.vkCmdBindIndexBuffer(frameInfo.CommandBuffer, _indexBuffer[Presenter.FrameIndex].VkBuffer, 0, VkIndexType.Uint16);
            
            for (int i = 0; i < imDrawData.CmdListsCount; i++)
            {
                var cmd_list = imDrawData.CmdLists[i];

                for (int j = 0; j < cmd_list.CmdBuffer.Size; j++)
                {
                    var pcmd = cmd_list.CmdBuffer[j];
                    if (pcmd.ElemCount == 0)
                    {
                        continue;
                    }
                    ImTextureRef textureRef = pcmd.TexRef;
                    var texId = textureRef.GetTexID();
                    if (!_textureVariants.TryGetValue(texId, out var textureVariant))
                    {
                        throw new InvalidOperationException($"Could not find a texture with id '{texId}', please check your bindings");
                    }
                    mat = textureVariant.Variant;
                    var variantIndex = (int)mat.VariantIndex;

                    mat.PushConstants.SetPushConstantVector2("scale", variantIndex, scale);
                    mat.PushConstants.SetPushConstantVector2("translate", variantIndex, translate);
                    mat.Bind(frameInfo);


                    VkRect2D scissorRect = new();
                    scissorRect.offset.x = Math.Max((int)pcmd.ClipRect.X, 0);
                    scissorRect.offset.y = Math.Max((int)pcmd.ClipRect.Y, 0);
                    scissorRect.extent.width = (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X);
                    scissorRect.extent.height = (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y);
                    GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, 1, &scissorRect);
                    GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(frameInfo.CommandBuffer, pcmd.ElemCount, 1, indexOffset, vertexOffset, 0);
                    indexOffset += pcmd.ElemCount;
                }
                vertexOffset += cmd_list.VtxBuffer.Size;
            }
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            _context = ImGui.GetCurrentContext();
        }

        public unsafe void OverlayToActiveTarget(RendererFrameInfo frameInfo, RenderTarget renderTarget)
        {
            if (_outputTarget.ImageLayout == VkImageLayout.ColorAttachmentOptimal)
            {
                _outputTarget.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
            }
            else if (_outputTarget.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                _outputTarget.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.FragmentShader);
            }

            var targetLayout = renderTarget.ImageLayout;


            if(targetLayout != VkImageLayout.ColorAttachmentOptimal)
            {
                if (renderTarget.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
                {
                    renderTarget.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
                else if (renderTarget.ImageLayout == VkImageLayout.TransferSrcOptimal)
                {
                    renderTarget.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
            }


            VkRenderingAttachmentInfo colourAttachments = new()
            {
                imageView = renderTarget.VkImageView,
                imageLayout = renderTarget.ImageLayout,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0, 0, 0, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)renderTarget.Target.Width, (uint)renderTarget.Target.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachments,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);
            
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, renderTarget.Target.Height, renderTarget.Target.Width, -renderTarget.Target.Height);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, new VkRect2D(new VkOffset2D(0, 0), new VkExtent2D(renderTarget.Target.Width, renderTarget.Target.Height)));

            _blitVariant.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            if (targetLayout != VkImageLayout.ColorAttachmentOptimal)
            {
                if (targetLayout == VkImageLayout.ShaderReadOnlyOptimal)
                {
                    renderTarget.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
                else if (targetLayout == VkImageLayout.TransferSrcOptimal)
                {
                    renderTarget.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);
                }
            }
        }

        public void BlitToImage(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            if (_outputTarget.ImageLayout == VkImageLayout.ColorAttachmentOptimal)
            {
                _outputTarget.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);
            }
            else if (_outputTarget.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                _outputTarget.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Blit);
            }

            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, _outputTarget.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), _outputTarget.VkImage, _outputTarget.ImageLayout, dst, VkImageLayout.TransferDstOptimal);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ImGui.DestroyContext(_context);
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
