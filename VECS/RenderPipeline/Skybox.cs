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

        public Skybox()
        {
            SkyboxTexture = new Cubemap("GL_Skybox", TextureLoader.GetTextureInDefaultPath("Skyboxes/GL_Skybox"), VkSamplerAddressMode.ClampToEdge);
            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;
            pipelineConfig.rasterizationInfo.frontFace = VkFrontFace.CounterClockwise;
            pipelineConfig.depthStencilInfo.depthTestEnable = false;

            _skybox = new Material("Skybox", "skybox.vert", "skybox.frag", pipelineConfig);

            _skybox.SetCubeMap("samplerCubeMap".GetShaderPropertyId(), 0, SkyboxTexture);
            _cube = AssetDataBase<DirectSubMesh>.GetNamed("quad-cube-UV.1");
        }

        public void RenderSkybox(RendererFrameInfo frameInfo)
        {
            _skybox.PushConstants.SetPushConstantUniform("ubo", new UBO(frameInfo.CameraInfo));
            _cube ??= AssetDataBase<DirectSubMesh>.GetNamed("quad-cube-UV.1");
            Presenter.Instance.ForwardRenderer.BeginForwardRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Clear);

            _skybox.BindAll(frameInfo, 0);
            _cube.SimpleBindAndDraw(frameInfo.CommandBuffer);

            Presenter.Instance.ForwardRenderer.EndForwardRendering(frameInfo.CommandBuffer);
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
