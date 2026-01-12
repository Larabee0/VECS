using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ShadowInternal
    {
        public unsafe ShadowInternal()
        {
            DrawBlob.AllInOneMats.Add(EngineMaterials.ShadowOffscreen.Hash);
        }

        public void RenderShadowsSinglePass(RendererFrameInfo frameInfo)
        {
            Presenter.Instance.ShadowImage.RenderShadowsSinglePass(frameInfo);
        }
    }
}
