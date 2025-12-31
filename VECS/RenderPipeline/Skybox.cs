using System.Numerics;
using VECS.GraphicsPipelines;
using Vortice.Vulkan;

namespace VECS
{
    public class Skybox
    {
        public static Cubemap SkyboxTexture;

        private DirectSubMesh _cube;
        private readonly Material _skybox;
        private readonly Material _skyboxDepthOnly;

        public Skybox()
        {
            SkyboxTexture = new Cubemap("Kurt", TextureLoader.GetTextureInDefaultPath("Skyboxes/Red"), VkSamplerAddressMode.ClampToEdge,false);
            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            pipelineConfig.rasterizationInfo.frontFace = VkFrontFace.Clockwise;

            _skybox = new Material("Skybox", "skybox.vert", "skybox.frag", pipelineConfig);
            pipelineConfig.depthStencilInfo.depthTestEnable = true;
            pipelineConfig.depthStencilInfo.depthWriteEnable = true;
            pipelineConfig.colourFormats = [];
            _skyboxDepthOnly = new Material("SkyboxDepthOnly","skybox.vert",pipelineConfig);

            _skybox.SetCubeMap("samplerCubeMap".GetShaderPropertyId(), 0, SkyboxTexture);
            _cube = AssetDataBase<DirectSubMesh>.GetNamed("quad-cube-UV.1");
        }

        public void RenderSkyboxPass(RendererFrameInfo frameInfo)
        {
            _skybox.PushConstants.SetPushConstantUniform("ubo", new UBO(frameInfo.CameraInfo));
            _cube ??= AssetDataBase<DirectSubMesh>.GetNamed("quad-cube-UV.1");
            Presenter.Instance.ForwardRenderer.BeginForwardRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Clear);

            _skybox.BindAll(frameInfo, 0);
            _cube.SimpleBindAndDraw(frameInfo.CommandBuffer);

            Presenter.Instance.ForwardRenderer.EndForwardRendering(frameInfo.CommandBuffer);
        }

        public void RenderSkyboxDepthOnly(RendererFrameInfo frameInfo)
        {
            _skybox.PushConstants.SetPushConstantUniform("ubo", new UBO(frameInfo.CameraInfo));
            _cube ??= AssetDataBase<DirectSubMesh>.GetNamed("quad-cube-UV.1");
            Presenter.Instance.ForwardRenderer.BeginForwardDepthOnlyRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Clear);

            _skyboxDepthOnly.BindAll(frameInfo, 0);
            _cube.SimpleBindAndDraw(frameInfo.CommandBuffer);

            Presenter.Instance.ForwardRenderer.EndForwardDepthOnlyRendering(frameInfo.CommandBuffer);
        }

        public void RenderSkybox(RendererFrameInfo frameInfo)
        {
            _skybox.PushConstants.SetPushConstantUniform("ubo", new UBO(frameInfo.CameraInfo));
            _cube ??= AssetDataBase<DirectSubMesh>.GetNamed("quad-cube-UV.1");
            _skybox.BindAll(frameInfo, 0);
            _cube.SimpleBindAndDraw(frameInfo.CommandBuffer);
        }

        private readonly struct UBO
        {
            public readonly Matrix4x4 Projection;
            public readonly Matrix4x4 Model;

            public UBO(CameraInfo cameraInfo)
            {
                Projection = cameraInfo.ProjectionMatrix;
                Model = cameraInfo.ViewMatrix;
            }
        }
    }
}
