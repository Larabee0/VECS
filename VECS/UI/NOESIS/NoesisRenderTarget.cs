using Vortice.Vulkan;

namespace VECS.UI
{
    public class NoesisRenderTarget : Noesis.RenderTarget
    {
        public NoesisTexture Colour;
        public NoesisTexture ColourAA;
        public NoesisTexture Stencil;
        public uint ColourAttachmentCount => Stencil == null ? 1u : 2u;

        public VkSampleCountFlags samples = VkSampleCountFlags.Count1;

        public override Noesis.Texture Texture => Colour;
    }
}
