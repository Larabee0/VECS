using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.GraphicsPipelines;
using Vortice.Vulkan;

namespace VECS
{
    public static class Skybox
    {
        private readonly static int SkyboxTextureProperty = "samplerCubeMap".GetShaderPropertyId();        
        private readonly static DirectSubMesh _cube;
        private readonly static Material _skybox;

        private static Cubemap _skyboxTexture;
        public static Cubemap SkyboxTexture
        {
            get => _skyboxTexture;
            set
            {
                _skyboxTexture = value;
                _skybox.SetCubeMap(SkyboxTextureProperty, 0, SkyboxTexture);
            }
        }

        static Skybox()
        {
            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            pipelineConfig.rasterizationInfo.frontFace = VkFrontFace.Clockwise;
            pipelineConfig.depthStencilInfo.depthTestEnable = true;

            _skybox = new Material("Skybox", "skybox.vert", "skybox.frag", pipelineConfig);
            
            _cube = AssetDataBase<DirectSubMesh>.GetNamed("quad-cube-UV.1");
            SkyboxTexture = new Cubemap("Kurt", TextureLoader.GetTextureInDefaultPath("Skyboxes/Red"), VkSamplerAddressMode.ClampToEdge, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RenderSkybox(RendererFrameInfo frameInfo)
        {
            if (SkyboxTexture == null || _cube == null) return;
            _skybox.PushConstants.SetPushConstantUniform("ubo", new UBO(frameInfo.CameraInfo));
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
