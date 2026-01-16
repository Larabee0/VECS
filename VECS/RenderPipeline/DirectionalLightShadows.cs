using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DirectionalLightShadows
    {
        public const int DIRECTIONAL_SHADOW_RESOLTION = 1024;
        public const bool SHADOW_CULLING = false;
        public const bool SHADOW_DST_CULLING = false;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;

        // private readonly Material _shadowDepthOnly;
        private readonly RenderTarget _shadowDepthImage;
        private readonly  VkViewport viewport = new()
        {
            width = DIRECTIONAL_SHADOW_RESOLTION,
            height = DIRECTIONAL_SHADOW_RESOLTION,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };

        private readonly  VkRect2D scissor = new(new(0, 0), new(DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION));
        public DirectionalLightShadows()
        {
            _shadowDepthImage = new("DirectionalShadowRT", DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION, VkFormat.D32Sfloat);

            _shadowDepthImage.Target.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

            // var shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            // shadowConfig.colourFormats = [];
            // shadowConfig.depthFormat = _shadowDepthImage.Target.Format;
            // shadowConfig.stencilFormat = VkFormat.Undefined;
            // shadowConfig.depthStencilInfo.depthWriteEnable = true;
            // shadowConfig.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;
            // shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.Front;
            // shadowConfig.rasterizationInfo.depthBiasEnable = true;
            // shadowConfig.rasterizationInfo.depthBiasConstantFactor = 1.25f;
            // shadowConfig.rasterizationInfo.depthBiasSlopeFactor = 1.75f;
            // _shadowDepthOnly = new("ShadowDepthOnly", "shadow_depth.vert", shadowConfig);

            // DrawBlob.AllInOneMats.Add(_shadowDepthOnly.Hash);

            var shadowOffscreen = EngineMaterials.ShadowOffscreen;
            shadowOffscreen.PushConstants.SetPushConstantInt("matrixOffset", 0);
            shadowOffscreen.PushConstants.SetPushConstantInt("baseLayerOffset", 0);
            shadowOffscreen.PushConstants.SetPushConstantInt("faceCount", 1);
            shadowOffscreen.PushConstants.SetPushConstantInt("lightIndex", 0);
            shadowOffscreen.PushConstants.SetPushConstantInt("writeDepth", 1);
        }

        public void AssignDirShadowTexture()
        {

            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                for (int i = 0; i < asset.VariantCount; i++)
                {
                    asset.SetTexture(ShaderPropertyInfo.DirShadowImageId, i, _shadowDepthImage.Target);
                }
            });
            var texProp = "texSampler".GetShaderPropertyId();

            AssetDataBase<Material>.GetNamed("UnlitTextured")?.SetTexture(texProp, 0, _shadowDepthImage.Target);
        }

        public static Matrix4x4 GetSpaceMatrix(LightingInfo lightingInfo, out float nearPlane, out float farPlane, out Matrix4x4 lightView, out Matrix4x4 lightProj, out Vector3 lightPos)
        {
            AABB sceneBounds = new();
            if (World.DefaultWorld.EntityManager.SingletonComponent<FrameInfo>(out var sceneInfo))
            {
                sceneBounds = sceneInfo.sceneBounds;
            }

            const float near_plane = 0.01f;
            farPlane = Vector3.Distance(sceneBounds.Min,sceneBounds.Max);
            
            var lightDir = lightingInfo.DirectionalLight.Direction.AsVector3();

            var shadowFocus = sceneBounds.Center + (lightDir* ( farPlane*0.5f));

            lightDir = -lightDir;
            lightPos =  new(){
                X = shadowFocus.X + lightDir.X * farPlane,
                Y = shadowFocus.Y + lightDir.Y * farPlane,
                Z = shadowFocus.Z + lightDir.Z * farPlane
            };
            
            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawLine(lightPos, shadowFocus, Colour.Blue);
            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawSphere(lightPos, 1, Colour.Red);
            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawSphere(shadowFocus, 1, Colour.Green);

            lightProj = Matrix4x4.CreateOrthographic(farPlane, farPlane, near_plane, farPlane);
            
            if(lightDir == new Vector3(0, 1, 0))
            {
                lightView = Matrix4x4.CreateLookAt(lightPos, shadowFocus, new(0, 0, 1));
            }
            else
            {
                lightView = Matrix4x4.CreateLookAt(lightPos, shadowFocus, new(0, 1, 0));
            }

            nearPlane = near_plane;
            return lightView * lightProj;
        }

        public unsafe void DirectionalShadowPass(in RendererFrameInfo frameInfo)
        {
            AssignDirShadowTexture();
            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);
            var shadowOffscreen = EngineMaterials.ShadowOffscreen;
            var mats = shadowOffscreen.GetStorageSwapChainBuffer(PointLightShadows.matsPropertyId);
            var lights = shadowOffscreen.GetStorageSwapChainBuffer(PointLightShadows.lightInfoPropertyId);
            mats.UnsafeSet(0, GetSpaceMatrix(frameInfo.LightingInfo, out var near_plane, out var farPlane, out var lightView, out var lightProj, out var lightPos));
            lights.UnsafeSet(0, new Vector4(lightPos,farPlane));


            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULLING, SHADOW_DST_CULLING, SHADOW_DEPTH_CULLING, near_plane, lightProj, lightView);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

            _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.EarlyFragmentTests);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = _shadowDepthImage.VkImageView,
                imageLayout = _shadowDepthImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION),
                layerCount = 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);

            SetViewPort(frameInfo.CommandBuffer);

            // DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, frameInfo.CommandBuffer, _shadowDepthOnly.Hash);

            DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, frameInfo.CommandBuffer, shadowOffscreen.Hash,0);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void SetViewPort(VkCommandBuffer commandBuffer)
        {
            fixed (VkViewport* pViewport = &viewport)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, 1, pViewport);
            }
            fixed (VkRect2D* pScissor = &scissor)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, 1, pScissor);
            }
        }
    }
}
