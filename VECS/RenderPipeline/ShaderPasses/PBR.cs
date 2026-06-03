using System;
using System.Numerics;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class PBR
    {
        public const VkFormat BRDFLUT_FORMAT = VkFormat.R8G8Unorm;
        public const VkFormat IRRADIANCE_FORMAT = VkFormat.R8G8B8A8Unorm;
        public const VkFormat PREFILTERED_CUBE_FORMAT = VkFormat.R8G8B8A8Unorm;
        public const VkImageUsageFlags BRDFLUT_USAGE_FLAGS = VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment;
        public const VkImageUsageFlags IRRADIANCE_USAGE_FLAGS = VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc;
        public const VkImageUsageFlags PREFILTERED_CUBE_USAGE_FLAGS = VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc;
        public const int BRDFLUT_DIMENTIONS = 512;
        public const int IRRADIANCE_DIMENTIONS = 512;
        public const int PREFILTERED_CUBE_DIMENTIONS = 512;

        private static Texture2D BRDFLUT_Texture;
        private static Cubemap Irradiance_Cubemap;
        private static Cubemap Prefiltered_Cubemap;

        private static GraphicsPipeline BRDFLUT_Generator;
        private static GraphicsPipeline Irradiance_Generator;
        private static GraphicsPipeline Prefiltered_Generator;

        public static void Reset()
        {
            CreateAssets();

            Irradiance_Generator.SetTexture("samplerEnv".GetShaderPropertyId(), 0, Skybox.SkyboxTexture);
            Prefiltered_Generator.SetTexture("samplerEnv".GetShaderPropertyId(), 0, Skybox.SkyboxTexture);

            var irradianceProp = "samplerIrradiance".GetShaderPropertyId();
            var prefilteredProp = "prefilteredMap".GetShaderPropertyId();
            var brdflutProp = "samplerBRDFLUT".GetShaderPropertyId();
            ShaderProperties.IgnoreUnFoundShaderProperties.Add(irradianceProp);
            ShaderProperties.IgnoreUnFoundShaderProperties.Add(prefilteredProp);
            ShaderProperties.IgnoreUnFoundShaderProperties.Add(brdflutProp);
            EngineTextures.AddTexture(irradianceProp, Irradiance_Cubemap.AsSingleTexture());
            EngineTextures.AddTexture(prefilteredProp, Prefiltered_Cubemap.AsSingleTexture());
            EngineTextures.AddTexture(brdflutProp, BRDFLUT_Texture.AsSingleTexture());


            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetCubeMap(irradianceProp, Irradiance_Cubemap);
                asset.SetCubeMap(prefilteredProp, Prefiltered_Cubemap);
                asset.SetTexture(brdflutProp, BRDFLUT_Texture);
            });
        }


        public static void CreateAssets()
        {
            BRDFLUT_Texture = new(
                "BRDFLUT",
                BRDFLUT_DIMENTIONS,
                BRDFLUT_DIMENTIONS,
                BRDFLUT_FORMAT,
                BRDFLUT_USAGE_FLAGS,
                VkSamplerAddressMode.ClampToEdge,
                0,
                false,
                VkCompareOp.Never,
                VkBorderColor.FloatOpaqueWhite,
                false);

            BRDFLUT_Texture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);

            Irradiance_Cubemap = new(
                "Irradiance",
                IRRADIANCE_DIMENTIONS,
                IRRADIANCE_FORMAT,
                VkSamplerAddressMode.ClampToEdge,
                IRRADIANCE_USAGE_FLAGS,
                true);

            Irradiance_Cubemap.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);


            Prefiltered_Cubemap = new(
                "Prefiltered_Cubemap",
                PREFILTERED_CUBE_DIMENTIONS,
                PREFILTERED_CUBE_FORMAT,
                VkSamplerAddressMode.ClampToEdge,
                PREFILTERED_CUBE_USAGE_FLAGS,
                true);

            Prefiltered_Cubemap.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.FragmentShader);

            GraphicsPipelineConfigInfo brdflut_gen_config = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            brdflut_gen_config.rasterizationInfo.cullMode = VkCullModeFlags.None;
            brdflut_gen_config.rasterizationInfo.polygonMode = VkPolygonMode.Fill;
            brdflut_gen_config.rasterizationInfo.frontFace = VkFrontFace.CounterClockwise;
            brdflut_gen_config.depthStencilInfo.depthWriteEnable = false;
            brdflut_gen_config.depthStencilInfo.depthTestEnable = false;
            brdflut_gen_config.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;
            brdflut_gen_config.colourFormats = [BRDFLUT_FORMAT];
            BRDFLUT_Generator = new("BRDFLUT_Generator", "fullscreen.vert", "genbrdflut.frag", brdflut_gen_config);


            GraphicsPipelineConfigInfo irradiance_gen_config = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            irradiance_gen_config.rasterizationInfo.cullMode = VkCullModeFlags.None;
            irradiance_gen_config.rasterizationInfo.polygonMode = VkPolygonMode.Fill;
            irradiance_gen_config.rasterizationInfo.frontFace = VkFrontFace.CounterClockwise;
            irradiance_gen_config.depthStencilInfo.depthWriteEnable = false;
            irradiance_gen_config.depthStencilInfo.depthTestEnable = false;
            irradiance_gen_config.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;
            irradiance_gen_config.colourFormats = [IRRADIANCE_FORMAT];
            Irradiance_Generator = new("Irradiance_Generator", "filtercube.vert", "irradiancecube.frag", irradiance_gen_config);

            GraphicsPipelineConfigInfo prefiltered_cube_gen_config = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            prefiltered_cube_gen_config.rasterizationInfo.cullMode = VkCullModeFlags.None;
            prefiltered_cube_gen_config.rasterizationInfo.polygonMode = VkPolygonMode.Fill;
            prefiltered_cube_gen_config.rasterizationInfo.frontFace = VkFrontFace.CounterClockwise;
            prefiltered_cube_gen_config.depthStencilInfo.depthWriteEnable = false;
            prefiltered_cube_gen_config.depthStencilInfo.depthTestEnable = false;
            prefiltered_cube_gen_config.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;
            prefiltered_cube_gen_config.colourFormats = [PREFILTERED_CUBE_FORMAT];
            Prefiltered_Generator = new("Prefiltered_Cube_Generator", "filtercube.vert", "prefilterenvmap.frag", prefiltered_cube_gen_config);
        }

        public static unsafe void Generate_BRDFLUT(RendererFrameInfo frameInfo)
        {
            var commandBuffer = AuxiliaryCommandBufferManager.Record();
            
            BRDFLUT_Texture.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);

            VkRenderingAttachmentInfo colourAttachments = new()
            {
                imageView = BRDFLUT_Texture._imageView,
                imageLayout = BRDFLUT_Texture._imageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0, 0, 0, 1)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)BRDFLUT_Texture.Width, (uint)BRDFLUT_Texture.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachments,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            VkViewport viewport = new(0, 0, BRDFLUT_DIMENTIONS, BRDFLUT_DIMENTIONS, 0.0f, 1.0f);
            VkRect2D scissor = new(0, 0, BRDFLUT_DIMENTIONS, BRDFLUT_DIMENTIONS);

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);


            GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, viewport);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, scissor);

            BRDFLUT_Generator.Default().Bind(new(frameInfo.FrameIndex, frameInfo.CameraCount, frameInfo.MainCamera, frameInfo.DeltaTime, frameInfo.NewSwapChain, commandBuffer, frameInfo.CullData, frameInfo.LightingInfo));
            GraphicsDevice.DeviceAPI.vkCmdDraw(commandBuffer, 3, 1, 0, 0);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            BRDFLUT_Texture.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);

            AuxiliaryCommandBufferManager.Submit();
        }

        public static unsafe void Generate_Irradiance(RendererFrameInfo frameInfo)
        {
            var commandBuffer = AuxiliaryCommandBufferManager.Record();

            frameInfo = new(frameInfo.FrameIndex, frameInfo.CameraCount, frameInfo.MainCamera, frameInfo.DeltaTime, frameInfo.NewSwapChain, commandBuffer, frameInfo.CullData, frameInfo.LightingInfo);

            Irradiance_Cubemap.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);

            VkRenderingAttachmentInfo colourAttachments = new()
            {
                imageView = Irradiance_Cubemap._imageView,
                imageLayout = Irradiance_Cubemap._imageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0.0f, 0.0f, 0.0f, 0.0f)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)Irradiance_Cubemap.Width, (uint)Irradiance_Cubemap.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachments,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };


            Matrix4x4* matrices = stackalloc Matrix4x4[] {
			    // POSITIVE_X
                Matrix4x4.CreateRotationY( TransformExtensions.Deg2Rad * 90.0f)* Matrix4x4.CreateRotationX(TransformExtensions.Deg2Rad * 180.0f),
			    // NEGATIVE_X
                Matrix4x4.CreateRotationY( TransformExtensions.Deg2Rad * -90.0f)* Matrix4x4.CreateRotationX(TransformExtensions.Deg2Rad * 180.0f),
			    // POSITIVE_Y
			    Matrix4x4.CreateRotationY(TransformExtensions.Deg2Rad *-90.0f),
			    // NEGATIVE_Y
                Matrix4x4.CreateRotationY(TransformExtensions.Deg2Rad *90.0f),
			    // POSITIVE_Z
			    Matrix4x4.CreateRotationZ(TransformExtensions.Deg2Rad *180.0f),
			    // NEGATIVE_Z
			    Matrix4x4.CreateRotationZ(TransformExtensions.Deg2Rad *180.0f),
            };

            VkViewport viewport = new(0, 0, IRRADIANCE_DIMENTIONS, IRRADIANCE_DIMENTIONS, 0.0f, 1.0f);
            VkRect2D scissor = new(0, 0, IRRADIANCE_DIMENTIONS, IRRADIANCE_DIMENTIONS);
            var persectve = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, IRRADIANCE_DIMENTIONS);

            Irradiance_Generator.PushConstants.SetPushConstantFloat("floatA", 0, (2.0f * MathF.PI) / 180.0f);
            Irradiance_Generator.PushConstants.SetPushConstantFloat("floatB", 0, (0.5f * MathF.PI) / 64.0f);


            for (int i = 0; i < 6; i++)
            {
                colourAttachments.imageView = Irradiance_Cubemap.FaceImageViews[i];
                GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
                GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, viewport);
                GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, scissor);

                Irradiance_Generator.PushConstants.SetPushConstantMatrix4x4("mvp", 0, matrices[i] * persectve);
                Irradiance_Generator.BindAll(frameInfo, 0);
                Skybox._cube.SimpleBindAndDraw(commandBuffer);

                GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            }
            Irradiance_Cubemap.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);

            Irradiance_Cubemap.RegenerateMipMaps(commandBuffer);
            AuxiliaryCommandBufferManager.Submit();
        }

        public static unsafe void Generate_Prefiltered_Cubemap(RendererFrameInfo frameInfo)
        {
            var commandBuffer = AuxiliaryCommandBufferManager.Record();
            frameInfo = new(frameInfo.FrameIndex, frameInfo.CameraCount, frameInfo.MainCamera, frameInfo.DeltaTime, frameInfo.NewSwapChain, commandBuffer, frameInfo.CullData, frameInfo.LightingInfo);
            Prefiltered_Cubemap.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);

            VkRenderingAttachmentInfo colourAttachments = new()
            {
                imageView = Prefiltered_Cubemap._imageView,
                imageLayout = Prefiltered_Cubemap._imageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0.0f, 0.0f, 0.0f, 0.0f)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)Prefiltered_Cubemap.Width, (uint)Prefiltered_Cubemap.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachments,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };


            Matrix4x4* matrices = stackalloc Matrix4x4[] {
			    // POSITIVE_X
                Matrix4x4.CreateRotationY( TransformExtensions.Deg2Rad * 90.0f)* Matrix4x4.CreateRotationX(TransformExtensions.Deg2Rad * 180.0f),
			    // NEGATIVE_X
                Matrix4x4.CreateRotationY( TransformExtensions.Deg2Rad * -90.0f)* Matrix4x4.CreateRotationX(TransformExtensions.Deg2Rad * 180.0f),
			    // POSITIVE_Y
			    Matrix4x4.CreateRotationY(TransformExtensions.Deg2Rad *-90.0f),
			    // NEGATIVE_Y
                Matrix4x4.CreateRotationY(TransformExtensions.Deg2Rad *90.0f),
			    // POSITIVE_Z
			    Matrix4x4.CreateRotationZ(TransformExtensions.Deg2Rad *180.0f),
			    // NEGATIVE_Z
			    Matrix4x4.CreateRotationZ(TransformExtensions.Deg2Rad *180.0f),
            };

            VkViewport viewport = new(0, 0, PREFILTERED_CUBE_DIMENTIONS, PREFILTERED_CUBE_DIMENTIONS, 0.0f, 1.0f);
            VkRect2D scissor = new(0, 0, PREFILTERED_CUBE_DIMENTIONS, PREFILTERED_CUBE_DIMENTIONS);
            var persectve = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, PREFILTERED_CUBE_DIMENTIONS);

            Prefiltered_Generator.PushConstants.SetPushConstantFloat("floatA", 0, (2.0f * MathF.PI) / 180.0f);
            Prefiltered_Generator.PushConstants.SetPushConstantUInt("numSamples", 0, 32);


            for (int i = 0; i < 6; i++)
            {
                colourAttachments.imageView = Prefiltered_Cubemap.FaceImageViews[i];
                GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
                GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, viewport);
                GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, scissor);

                Prefiltered_Generator.PushConstants.SetPushConstantMatrix4x4("mvp", 0, matrices[i] * persectve);
                Prefiltered_Generator.BindAll(frameInfo, 0);
                Skybox._cube.SimpleBindAndDraw(commandBuffer);

                GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            }
            Prefiltered_Cubemap.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);

            Prefiltered_Cubemap.RegenerateMipMaps(commandBuffer);
            AuxiliaryCommandBufferManager.Submit();
        }
    }
}
