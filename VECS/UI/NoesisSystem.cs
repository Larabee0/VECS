using Noesis;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{
    public class NoesisSystem : PresentationSystemBase
    {
        private NoesisViewWrapper MainView;
        public override void OnCreate(EntityManager entityManager)
        {
            FrameworkElement controlTreeRoot = (FrameworkElement)GUI.LoadXaml(System.IO.Path.Combine(Asset.AssetsPath,"GUI","Text.xaml"));

            MainView = new NoesisViewWrapper(controlTreeRoot, Application.Instance.NoesisDriver)
            {
                RenderFlags = RenderFlags.PPAA | RenderFlags.FlipY
            };

            MainView.SetSize(Screen.Width, Screen.Height);

            Application.Instance.NoesisDriver.CreatePipelines(Presenter.Instance.ForwardRenderer.MainColourAttachment.Target.Format, Vortice.Vulkan.VkFormat.Undefined);
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            MainView.Update(Time.DeltaTime);
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            Application.Instance.NoesisDriver._currentFrameInfo = default;
            MainView.PreRender();
        }

        public unsafe override void OnPostTransparentPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            Application.Instance.NoesisDriver._currentFrameInfo = frameInfo;
            Presenter.Instance.ForwardRenderer.BeginForwardRendering(frameInfo.CommandBuffer, Vortice.Vulkan.VkAttachmentLoadOp.Load,true,true);
            GraphicsDevice.DeviceAPI.vkCmdSetRasterizationSamplesEXT(frameInfo.CommandBuffer, Vortice.Vulkan.VkSampleCountFlags.Count1);
            MainView.Render();
            Presenter.Instance.ForwardRenderer.EndForwardRendering(frameInfo.CommandBuffer);
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            MainView.View.Renderer.Shutdown();
            MainView.View.Dispose();
            GUI.Shutdown();
        }
    }
}
