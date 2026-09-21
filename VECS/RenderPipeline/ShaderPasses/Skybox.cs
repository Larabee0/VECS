using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VECS
{
    public class Skybox : IRenderPass
    {
        private readonly static int SkyboxTextureProperty = "samplerCubeMap".GetShaderPropertyId();
        private readonly IRenderer _activeRenderer;

        public static DirectSubMesh Cube { get; private set;  }
        private static Material _skybox;

        private static Cubemap _skyboxTexture;
        private bool Skybox_Enabled = true;

        public static Cubemap SkyboxTexture
        {
            get => _skyboxTexture;
            set
            {
                _skyboxTexture = value;
                _skybox.SetCubeMap(SkyboxTextureProperty, SkyboxTexture);
            }
        }

        public Skybox(IRenderer activeRenderer)
        {
            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            pipelineConfig.rasterizationInfo.frontFace = VkFrontFace.Clockwise;
            pipelineConfig.depthStencilInfo.depthTestEnable = true;

            _skybox ??= GraphicsPipeline.VertexFragmentPipeline("Skybox", "skybox.vert", "skybox.frag", pipelineConfig).Default();
            Cube ??= MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("cube-UV.obj"),null)[0];

            SkyboxTexture ??= TextureLoader.LoadCubemap(Path.Combine(TextureLoader.DefaultTexturePath, "Skyboxes", "GL_Skybox", "GL_Skybox.TexDef.ktx"), VkFormat.Bc7UnormBlock);
            _activeRenderer = activeRenderer;
        }

        public void SetEnabled(bool enabled)
        {

            if (enabled == Skybox_Enabled) return;
            Skybox_Enabled = enabled;
            if (Skybox_Enabled)
            {
                RenderGraph.EnablePass("Skybox");
            }
            else
            {
                RenderGraph.DisablePass("Skybox");
            }
        }

        public void AddToRenderGraph()
        {
            RenderGraph.AddPass("Skybox", PassType.Compute, PassCategory.PostRendering, ["ForwardPass", "DeferredCompositePass", "TransaprentComposite"], [RenderGraph.MainColourAttachment], [RenderGraph.MainColourAttachment], RenderSkybox);
        }


        public void RecreateRenderTargets()
        {

        }

        public void PrePresent()
        {

        }

        public void RenderSkybox(RendererFrameInfo frameInfo)
        {
            if (SkyboxTexture == null || Cube == null||frameInfo.TargetCamera == -1) return;

            _activeRenderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);

            var camera = ((SwapChainBuffer<CameraData>)EngineBuffers.TryGetBuffer(ShaderProperties.CameraDataId)).HostBuffer[frameInfo.TargetCamera];
            _skybox.PushConstants.SetPushConstantUniform("viewProj",0, GetSkyboxMatrix(camera));
            _skybox.Bind(frameInfo);
            Cube.SimpleBindAndDraw(frameInfo.CommandBuffer);

            _activeRenderer.EndForwardRendering(frameInfo);
        }

        public static Matrix4x4 GetSkyboxMatrix(in CameraData cameraInfo)
        {
            var view = cameraInfo.ViewMatrix;
            view.Translation = Vector3.Zero;
            return view * cameraInfo.ProjectionMatrix;
        }
    }
}
