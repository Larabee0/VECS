using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UltralightNet;
using UltralightNet.AppCore;
using UltralightNet.JavaScript;
using UltralightNet.JavaScript.Low;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{

    public static class ULUI
    {
        private static readonly ULConfig config = new()
        {
            UserStylesheet = "body { background: blue; color: white; } h1 {color: white; background: blue; }",
        };
        private static readonly ULViewConfig viewConfig = new()
        {
            IsAccelerated = true,
            IsTransparent = true
        };

        private static readonly string _defaultHTML = "<html><head><script>function ShowMessage(message){document.getElementById('msg').innerHTML = message;}</script></head><body><div id=\"msg\"></div></body></html>"; //"<html>\r\n  <head>\r\n  </head>\r\n  <body>\r\n    <button onclick=\"OnButtonClick();\">Click Me</button>\r\n    <div id=\"result\"></div>\r\n  </body>\r\n</html>";// "<h1>Hello World!<br><i>omg italics?</i></h1><button onclick=\"OnButtonClick();\">Click Me</button>";

        private static Renderer _ulRenderer;
        private static View _ulView;
        private static ULBitmap _bitmap;

        private static GPUBuffer uiCopyBuffer;
        private static Texture2D uiOutputTex;

        private static UltralightVulkanDriver UltralightVulkanDriver;

        private static Material UIBlit;
        private static readonly int InputTexturePropertyId = "inputTexture".GetShaderPropertyId();

        public static void Initialise()
        {
            if (viewConfig.IsAccelerated)
            {
                ULPlatform.GPUDriver = UltralightVulkanDriver = new UltralightVulkanDriver();
            }
            ULPlatform.SetDefaultFontLoader = true;
            ULPlatform.SetDefaultFileSystem = true;
            ULPlatform.EnableDefaultLogger = true;
            ULPlatform.ErrorWrongThread = false;
            AppCoreMethods.SetPlatformFontLoader();

            _ulRenderer = ULPlatform.CreateRenderer(config);
            
            _ulView = _ulRenderer.CreateView(SwapChain.Instance.SwapChainExtent.width, SwapChain.Instance.SwapChainExtent.height, viewConfig);
            _ulView.HTML = _defaultHTML;
            UIBlit = EnginePipes.Blit.Create("UIBlitter");

            //ulView.URL = "https://www.google.com/";
            if (!viewConfig.IsAccelerated)
            {
                _bitmap = _ulView.Surface.Value.Bitmap;

                UIBlit.SetTexture(InputTexturePropertyId, uiOutputTex);
                uiCopyBuffer = new GPUBuffer(1280 * 720, (uint)Vulkan.BlockSize(VkFormat.B8G8R8A8Unorm), VkBufferUsageFlags.TransferSrc, true, true, false);
                uiOutputTex = new("UI_Out", 1280, 720, VkFormat.B8G8R8A8Unorm, VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled, false);
                Console.WriteLine(_bitmap.Format.ToString());
            }
        }

        public static void CleanUp()
        {
            UltralightVulkanDriver?.Dispose();
            uiOutputTex?.Dispose();
            uiCopyBuffer?.EnqueueForDisposal();
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

        public static void UpdateCommandList()
        {
            _ulRenderer.Render();
        }

        public static unsafe void BlitCamera(RendererFrameInfo frameInfo, Texture2D camera)
        {
            if (!viewConfig.IsAccelerated)
            {
                CopyUIToTexture(frameInfo.CommandBuffer);
            }
            else
            {
                UltralightVulkanDriver.ExecuteCommandList(frameInfo);
                UIBlit.SetTexture(InputTexturePropertyId, UltralightVulkanDriver.GetViewTexture(_ulView));
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
            UIBlit.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        public static unsafe void UpdateUI()
        {
            _ulRenderer.Update();
            if (!viewConfig.IsAccelerated)
            {
                _ulRenderer.Render();
                var pixels = _ulView.Surface.Value.Bitmap.RawPixels;
                uiCopyBuffer.WriteToBuffer(pixels, _ulView.Surface.Value.Size);
            }
        }
    }
}
