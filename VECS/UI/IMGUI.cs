using Assimp;
using BepuPhysics.Trees;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.Vulkan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{
    public class IMGUI : IDisposable
    {
        private const float FONT_SCALE = 1.0f;
        private ImGuiContextPtr _context;
        private ImGuiStylePtr _vulkanStyle;

        private GraphicsPipeline _imgui;
        private Texture2D _fontAtlas_VK_EXAMPLE;
        private SwapChainBuffer _vertexBuffer;
        private SwapChainBuffer _indexBuffer;

        private readonly Dictionary<ImTextureID, Texture2D> _textures = [];
        private int _nextTexId = 1;

        public IMGUI()
        {
            _context = ImGui.CreateContext();
            
            ImGui.SetCurrentContext(_context);

            ImGui.GetIO().ConfigDpiScaleFonts = true;

            ImGui.GetIO().BackendFlags = ImGuiBackendFlags.RendererHasTextures;

            ImGui.GetPlatformIO().RendererTextureMaxHeight = 4096;
            ImGui.GetPlatformIO().RendererTextureMaxWidth = 4096;

            Init();
            //VKExampleIniti();
            CreatePipeline();
        }

        private void Init()
        {
            SetStyle(0);
            Resize();
        }

        private void Resize()
        {
            ImGui.GetIO().DisplaySize = new(Screen.Width, Screen.Height);
            ImGui.GetIO().DisplayFramebufferScale = new(1.0f);
        }

        private void SetStyle(int index)
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

        public virtual void UpdateTexture(ImTextureDataPtr textureData)
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

        private unsafe void CreateTexture(ImTextureDataPtr textureData)
        {
            VkFormat format = textureData.Format == ImTextureFormat.Rgba32 ? VkFormat.R8G8B8A8Unorm : VkFormat.A8Unorm;                      
            _textures[textureData.TexID] = new Texture2D(textureData.UniqueID.ToString(), textureData.Width, textureData.Height, format, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, false);
            UpdateTextureData(textureData);
        }

        private unsafe void UpdateTextureData(ImTextureDataPtr textureData)
        {
            IntPtr texId = textureData.GetTexID();
            if (!_textures.TryGetValue(texId, out var texture))
            {
                return;
            }

            VkFormat newFormat = textureData.Format == ImTextureFormat.Rgba32 ? VkFormat.R8G8B8A8Unorm : VkFormat.A8Unorm;
            if (texture.Width != textureData.Width || texture.Height != textureData.Height || texture.Format != newFormat)
            {
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
                texture.CopyFrombufferNow(stagingBuffer);
                stagingBuffer.Dispose();
            }
            textureData.SetStatus(ImTextureStatus.Ok);
        }

        private void DestroyTexture(ImTextureDataPtr textureData)
        {
            IntPtr texId = textureData.GetTexID();
            if (_textures.TryGetValue(texId, out var texture))
            {
                texture.Dispose();
                _textures.Remove(texId);
            }
        }

        private unsafe void VKExampleIniti()
        {
            var io = ImGui.GetIO();
            var textData = io.Fonts.TexData;

            int texWidth = textData.Width;
            int texHeight = textData.Height;
            var pixels = textData.Pixels;
            var format = textData.Format switch
            {
                ImTextureFormat.Rgba32 => VkFormat.R32G32B32A32Sfloat,
                ImTextureFormat.Alpha8 => VkFormat.A8Unorm,
                _ => throw new NotImplementedException(),
            };
            _fontAtlas_VK_EXAMPLE = new("IMGUI_FontAtlas", texWidth, texHeight, format, VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst, false);
            GPUBuffer stagingBuffer = new((uint)Vulkan.BlockSize(format), (uint)(texWidth * texHeight), VkBufferUsageFlags.TransferSrc, true, false, false);
            Debug.Assert((int)stagingBuffer.HostBufferSize32 == textData.GetSizeInBytes());
            Buffer.MemoryCopy(pixels, stagingBuffer.HostPtr, stagingBuffer.HostBufferSize32, textData.GetSizeInBytes());
            TextureExtensions.CopyFromBuffer(_fontAtlas_VK_EXAMPLE, stagingBuffer, true);

            CreatePipeline();

            _imgui.Default().SetTexture("fontSampler".GetShaderPropertyId(), _fontAtlas_VK_EXAMPLE);
        }

        private unsafe void CreatePipeline()
        {
            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref configInfo);
            configInfo.depthStencilInfo.depthTestEnable = false;
            configInfo.depthStencilInfo.depthWriteEnable = false;
            configInfo.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;

            configInfo.colourBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;

            configInfo.BindingDescriptions = [
                new()
                {
                    binding = 0,
                    stride = 20,
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
                    format = VkFormat.R32G32Sfloat,
                    offset = 8
                }, new(){
                    binding = 0,
                    location = 2,
                    format = VkFormat.R32G32B32A32Sfloat,
                    offset = 16
                }
            ];

            _imgui = new("IMGUI_Pipe", "imgui.vert", "imgui.frag", configInfo);
        }

        private void NewFrame()
        {
            ImGui.NewFrame();
            ImGui.ShowDemoWindow();

            // This does not render the UI to the screen, but gathers the draw data for the UI frame that we'll use to render it
            ImGui.Render();
        }

        public void Update()
        {

            var io = ImGui.GetIO();
            io.DisplaySize = new(Screen.Width, Screen.Height);
            io.DeltaTime = Time.DeltaTime;
            io.MousePos = InputManager.Instance.MousePos;
            
            io.MouseDown[0] =InputManager.Instance.GetMouseButton(0);
            io.MouseDown[1] =InputManager.Instance.GetMouseButton(1);
            io.MouseDown[2] = InputManager.Instance.GetMouseButton(2);
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
            
            _vertexBuffer ??= new SwapChainBuffer<ImDrawVert>(vertexCount, VkBufferUsageFlags.VertexBuffer, true);
            _indexBuffer ??= new SwapChainBuffer<ushort>(indexCount, VkBufferUsageFlags.IndexBuffer, true);

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

                Buffer.MemoryCopy(ptr_vertex, cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size * sizeof(ImDrawVert), cmdList.VtxBuffer.Size * sizeof(ImDrawVert));
                Buffer.MemoryCopy(ptr_index, cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size * sizeof(ushort), cmdList.IdxBuffer.Size * sizeof(ushort));
                ptr_vertex += cmdList.VtxBuffer.Size;
                ptr_index += cmdList.IdxBuffer.Size;
            }
            _vertexBuffer.SetUsedInstanceCount(vertexCount);
            _indexBuffer.SetUsedInstanceCount(indexCount);
        }

        public unsafe void Draw(RendererFrameInfo frameInfo)
        {
            Update();
            NewFrame();
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
            var vertexBuffer = _vertexBuffer[frameInfo.FrameIndex].VkBuffer;
            ulong offset = 0;
            var mat = _imgui.Default();



            GPUBufferExtensions.WriteFromHostDelayed(_vertexBuffer, frameInfo.FrameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(_indexBuffer, frameInfo.FrameIndex);

            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, 0, io.DisplaySize.X, io.DisplaySize.Y);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffers(frameInfo.CommandBuffer, 0, 1, &vertexBuffer, &offset);
            GraphicsDevice.DeviceAPI.vkCmdBindIndexBuffer(frameInfo.CommandBuffer, _indexBuffer[frameInfo.FrameIndex].VkBuffer, 0, VkIndexType.Uint16);
            ImTextureID texId = ImTextureID.Null;
            uint variantIndex = 0;
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
                    var nextTexId = textureRef.GetTexID();
                    if (!_textures.TryGetValue(nextTexId, out var texture))
                    {
                        throw new InvalidOperationException($"Could not find a texture with id '{texId}', please check your bindings");
                    }

                    if( nextTexId != texId  && texId != ImTextureID.Null)
                    {
                        variantIndex++;
                    }
                    texId = nextTexId;
                    mat = _imgui.GetOrCreateVariant(variantIndex);
                    mat.SetTexture("fontSampler".GetShaderPropertyId(), texture);

                    mat.PushConstants.SetPushConstantVector2("scale", 0, scale);
                    mat.PushConstants.SetPushConstantVector2("translate", 0, translate);
                    mat.Bind(frameInfo);


                    VkRect2D scissorRect = new();
                    scissorRect.offset.x = Math.Max((int)(pcmd.ClipRect.X), 0);
                    scissorRect.offset.y = Math.Max((int)(pcmd.ClipRect.Y), 0);
                    scissorRect.extent.width = (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X);
                    scissorRect.extent.height = (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y);
                    GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, 1, &scissorRect);
                    GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(frameInfo.CommandBuffer, pcmd.ElemCount, 1, indexOffset, vertexOffset, 0);
                    indexOffset += pcmd.ElemCount;
                }
                vertexOffset += cmd_list.VtxBuffer.Size;

            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ImGui.DestroyContext(ImGui.GetCurrentContext());
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
