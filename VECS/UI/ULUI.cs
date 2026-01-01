using System;
using UltralightNet;
using UltralightNet.AppCore;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{
    
    public static class ULUI
    {
        private static readonly ULConfig config = new()
        {
            UserStylesheet = "body { background: blue; color: white; } h1 {color: white; background: blue; }"
        };
        private static readonly ULViewConfig viewConfig = new()
        {
            IsAccelerated = true,
            IsTransparent = true,
        };

        private static readonly string _defaultHTML = "";// "<h1>Hello World!<br><i>omg italics?</i></h1>";

        private static Renderer _ulRenderer;
        private static View _ulView;
        private static ULBitmap _bitmap;

        private static GPUBuffer uiCopyBuffer;
        private static Texture2D uiOutputTex;

        private static UltralightVulkanDriver UltralightVulkanDriver;

        public static  void Initialise()
        {
            if(viewConfig.IsAccelerated)
            {
                ULPlatform.GPUDriver = UltralightVulkanDriver = new UltralightVulkanDriver();
            }

            ULPlatform.SetDefaultFontLoader = true;
            ULPlatform.SetDefaultFileSystem = true;
            ULPlatform.EnableDefaultLogger = true;
            ULPlatform.ErrorWrongThread = false;
            AppCoreMethods.SetPlatformFontLoader();

            _ulRenderer = ULPlatform.CreateRenderer(config);

            _ulView = _ulRenderer.CreateView(1280, 720, viewConfig);
            _ulView.HTML = _defaultHTML;

            //ulView.URL = "https://www.google.com/";
            if (!viewConfig.IsAccelerated)
            {
                _bitmap = _ulView.Surface.Value.Bitmap;

                EngineMaterials.Blit.SetTexture("inputTexture".GetShaderPropertyId(), 0, uiOutputTex);
                uiCopyBuffer = new GPUBuffer(1280 * 720, (uint)Vulkan.BlockSize(VkFormat.B8G8R8A8Unorm), VkBufferUsageFlags.TransferSrc, true, true, false);
                uiOutputTex = new("UI_Out", 1280, 720, VkFormat.B8G8R8A8Unorm, VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled, false);
                Console.WriteLine(_bitmap.Format.ToString());
            }
        }

        public static void CleanUp()
        {
            UltralightVulkanDriver?.Dispose();
            uiOutputTex?.Dispose();
            uiCopyBuffer?.Dispose();
            _bitmap?.Dispose();
            _ulView?.Dispose();
            _ulRenderer?.Dispose();
        }

        public static unsafe void CopyUIToTexture(VkCommandBuffer commandBuffer)
        {
            uiOutputTex.SetImageLayout(commandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);

            uiOutputTex.CopyFromBuffer(commandBuffer, uiCopyBuffer);

            uiOutputTex.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
        }

        public static unsafe void BlitCamera(RendererFrameInfo frameInfo, Texture2D camera)
        {
            if (!viewConfig.IsAccelerated)
            {
                CopyUIToTexture(frameInfo.CommandBuffer);
            }
            else
            {
                _ulRenderer.Render();
                UltralightVulkanDriver.ExecuteCommandList(frameInfo);
                EngineMaterials.Blit.SetTexture("inputTexture".GetShaderPropertyId(), 0, UltralightVulkanDriver.GetViewTexture(_ulView));
            }
            VkRenderingAttachmentInfo* renderingAttachmentInfo = stackalloc VkRenderingAttachmentInfo[]
            {
                new ()
                {
                    clearValue = new(0, 0, 0, 0),
                    loadOp = VkAttachmentLoadOp.Load,
                    storeOp = VkAttachmentStoreOp.Store,
                    imageLayout = camera.ImageLayout,
                    imageView = camera._imageView
                },
                new ()
                {
                    clearValue = new(0, 0, 0, 0),
                    loadOp = VkAttachmentLoadOp.Load,
                    storeOp = VkAttachmentStoreOp.Store,
                    imageLayout = camera.ImageLayout,
                    imageView = camera._imageView
                }
            };
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)camera.Width, (uint)camera.Height),
                layerCount = 1,
                colorAttachmentCount = 2,
                pColorAttachments = renderingAttachmentInfo
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);
            EngineMaterials.Blit.BindAll(frameInfo,0);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        public static unsafe void UpdateUI()
        {
            _ulRenderer.Update();
            if (!viewConfig.IsAccelerated)
            {
                _ulRenderer.Render();
                //_ulView.Surface.Value.Bitmap.SwapRedBlueChannels();
                var pixels = _ulView.Surface.Value.Bitmap.RawPixels;
                uiCopyBuffer.WriteToBuffer(pixels, _ulView.Surface.Value.Size);
            }
        }
    }
}
