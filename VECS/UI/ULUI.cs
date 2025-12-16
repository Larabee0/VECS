using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            UserStylesheet = "body { background: purple; color: white; } h1 {color: white; background: blue; }"
        };
        private static readonly ULViewConfig viewConfig = new()
        {
            IsAccelerated = false,
            IsTransparent = true,
        };

        private static readonly string _defaultHTML = "<h1>Hello World!<br><i>omg italics?</i></h1>";

        private static Renderer _ulRenderer;
        private static View _ulView;
        private static ULBitmap _bitmap;

        private static GPUBuffer uiCopyBuffer;
        private static Texture2D uiOutputTex;

        public static  void Initialise()
        {
            ULPlatform.SetDefaultFontLoader = true;
            ULPlatform.SetDefaultFileSystem = true;
            ULPlatform.EnableDefaultLogger = true;
            AppCoreMethods.SetPlatformFontLoader();

            _ulRenderer = ULPlatform.CreateRenderer(config);

            _ulView = _ulRenderer.CreateView(1280, 720, viewConfig);
                        _ulView.HTML = _defaultHTML;
            //ulView.URL = "https://www.google.com/";
            _bitmap = _ulView.Surface.Value.Bitmap;

            uiCopyBuffer = new GPUBuffer(1280 * 720, (uint)Vulkan.BlockSize(VkFormat.B8G8R8A8Unorm), VkBufferUsageFlags.TransferSrc, true, true, false);
            uiOutputTex = new("UI_Out", 1280, 720, VkFormat.B8G8R8A8Unorm, VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled,false);
            EngineMaterials.Blit.SetTexture("inputTexture".GetShaderPropertyId(), 0, uiOutputTex);
            Console.WriteLine(_bitmap.Format.ToString());
        }

        public static void CleanUp()
        {
            uiOutputTex.Dispose();
            uiCopyBuffer.Dispose();
            _bitmap.Dispose();
            _ulView.Dispose();
            _ulRenderer.Dispose();
        }

        public static unsafe void CopyUIToTexture(VkCommandBuffer commandBuffer)
        {
            uiOutputTex.SetImageLayout(commandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);

            uiOutputTex.CopyFromBuffer(commandBuffer, uiCopyBuffer);

            uiOutputTex.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
        }

        public static unsafe void BlitCamera(RendererFrameInfo frameInfo, Texture2D camera)
        {
            VkRenderingAttachmentInfo renderingAttachmentInfo = new()
            {
                clearValue = new(0,0,0,0),
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                imageLayout = camera.ImageLayout,
                imageView = camera._imageView
            };
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)camera.Width, (uint)camera.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &renderingAttachmentInfo
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);
            EngineMaterials.Blit.BindAll(frameInfo,0);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        public static unsafe void UpdateUI()
        {
            _ulRenderer.Update();
            _ulRenderer.Render();
            //_ulView.Surface.Value.Bitmap.SwapRedBlueChannels();
            var pixels = _ulView.Surface.Value.Bitmap.RawPixels;
            uiCopyBuffer.WriteToBuffer(pixels, _ulView.Surface.Value.Size);
        }

        public static void RenderUI()
        {
        }
    }
}
