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
            UserStylesheet = "body { background: purple; }"
        };
        private static readonly ULViewConfig viewConfig = new()
        {
            IsAccelerated = false,
            IsTransparent = false,
        };

        private static readonly string _defaultHTML = "<h1>Hello World!</h1>";

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

            _ulView = _ulRenderer.CreateView(500, 500, viewConfig);
            _ulView.HTML = _defaultHTML;
            _bitmap = _ulView.Surface.Value.Bitmap;

            uiCopyBuffer = new GPUBuffer(500 * 500, (uint)Vulkan.BlockSize(VkFormat.B8G8R8A8Snorm), VkBufferUsageFlags.TransferSrc, true, true, false);
            uiOutputTex = new("UI_Out", 500, 500, VkFormat.B8G8R8A8Snorm, VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled);

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
            uiOutputTex.SetImageLayout(commandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.Transfer);

            uiOutputTex.CopyFromBuffer(commandBuffer, uiCopyBuffer);

            uiOutputTex.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.Blit);
        }

        public static unsafe void BlitCamera(VkCommandBuffer commandBuffer, Texture2D camera)
        {
            camera.SetImageLayout(commandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);
            VkImageBlit2 regions = new()
            {
                srcSubresource = new(uiOutputTex._aspectFlags, 0, 0, 1),
                dstSubresource = new(camera._aspectFlags, 0, 0, 1)
            };

            regions.srcOffsets[1].x = 500;
            regions.srcOffsets[1].y = 500;
            regions.srcOffsets[1].z = 1;

            regions.dstOffsets[1].x = camera.Width;
            regions.dstOffsets[1].y = camera.Height;
            regions.dstOffsets[1].z = 1;

            VkBlitImageInfo2 imageBlit2 = new()
            {
                srcImage = uiOutputTex._vkImage,
                dstImage = camera._vkImage,
                srcImageLayout = VkImageLayout.TransferSrcOptimal,
                dstImageLayout = VkImageLayout.TransferDstOptimal,
                filter = VkFilter.Linear,
                regionCount = 1,
                pRegions = &regions
            };
            GraphicsDevice.DeviceAPI.vkCmdBlitImage2(commandBuffer, &imageBlit2);
            camera.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.Blit);
        }

        public static unsafe void UpdateUI()
        {
            _ulRenderer.Update();
            _ulRenderer.Render();
            var pixels = _ulView.Surface.Value.Bitmap.RawPixels;
            uiCopyBuffer.WriteToBuffer(pixels, _ulView.Surface.Value.Size);
        }

        public static void RenderUI()
        {
        }
    }
}
