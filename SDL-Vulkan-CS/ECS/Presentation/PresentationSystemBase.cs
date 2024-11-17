using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Base presentation system defines extra update calls when the frame render cycle occurs which parses in extra data for renderering
    /// </summary>
    public abstract class PresentationSystemBase : SystemBase
    {
        protected GraphicsDevice _graphicsDevice;
        protected VkDescriptorSetLayout _globalSetLayout;
        protected VkRenderPass _renderPass;

        public PresentationSystemBase()
        {
            if (Presenter.Instance == null)
            {
                throw new Exception("Cannot Create render system when the presenter is uninitialised!");
            }

            _graphicsDevice = GraphicsDevice.Instance;
            _renderPass = Presenter.Instance.RenderPass;
            _globalSetLayout = Presenter.Instance.GlobalSetLayout;
        }

        public PresentationSystemBase(GraphicsDevice device, VkRenderPass renderPass, VkDescriptorSetLayout globalSetLayout)
        {
            _graphicsDevice = device;
            _renderPass = renderPass;
            _globalSetLayout = globalSetLayout;
        }

        public abstract void OnPresent(EntityManager entityManager, RendererFrameInfo rendererFrameInfo);

        public virtual void OnPostPresentation(EntityManager entityManager)
        {
            
        }
    }
}
