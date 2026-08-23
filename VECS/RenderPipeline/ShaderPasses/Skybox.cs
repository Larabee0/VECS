using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VECS
{
    public static class Skybox
    {
        private readonly static int SkyboxTextureProperty = "samplerCubeMap".GetShaderPropertyId();        
        public static DirectSubMesh Cube { get; private set;  }
        private static Material _skybox;

        private static Cubemap _skyboxTexture;
        public static Cubemap SkyboxTexture
        {
            get => _skyboxTexture;
            set
            {
                _skyboxTexture = value;
                _skybox.SetCubeMap(SkyboxTextureProperty, SkyboxTexture);
            }
        }

        public static void StartSkybox()
        {
            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            pipelineConfig.rasterizationInfo.frontFace = VkFrontFace.Clockwise;
            pipelineConfig.depthStencilInfo.depthTestEnable = true;

            _skybox = GraphicsPipeline.VertexFragmentPipeline("Skybox", "skybox.vert", "skybox.frag", pipelineConfig).Default();
            Cube = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("cube-UV.obj"),null)[0];

            SkyboxTexture = TextureLoader.LoadCubemap(Path.Combine(TextureLoader.DefaultTexturePath, "Skyboxes", "GL_Skybox", "GL_Skybox.TexDef.ktx"), VkFormat.Bc7UnormBlock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RenderSkybox(RendererFrameInfo frameInfo)
        {
            if (SkyboxTexture == null || Cube == null) return;
            var camera = ((SwapChainBuffer<CameraData>)EngineBuffers.TryGetBuffer(ShaderProperties.CameraDataId)).HostBuffer[frameInfo.MainCamera];
            _skybox.PushConstants.SetPushConstantUniform("viewProj",0, GetSkyboxMatrix(camera));
            _skybox.Bind(frameInfo);
            Cube.SimpleBindAndDraw(frameInfo.CommandBuffer);
        }

        public static Matrix4x4 GetSkyboxMatrix(in CameraData cameraInfo)
        {
            var view = cameraInfo.ViewMatrix;
            view.Translation = Vector3.Zero;
            return view * cameraInfo.ProjectionMatrix;
        }
    }
}
