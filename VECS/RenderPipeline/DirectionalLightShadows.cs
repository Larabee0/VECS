using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.ECS.Presentation;
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

        private readonly RenderTarget _shadowDepthImage;
        private readonly  VkViewport viewport = new()
        {
            width = DIRECTIONAL_SHADOW_RESOLTION,
            height = DIRECTIONAL_SHADOW_RESOLTION,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };

        private readonly  VkRect2D scissor = new(new(0, 0), new(DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION));

        private readonly Material _shadowDepth;
        private readonly int _matHash;

        private bool _clearedImage;

        public DirectionalLightShadows()
        {
            _shadowDepthImage = new("DirectionalShadowRT", DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION, VkFormat.D32Sfloat);

            _shadowDepthImage.Target.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

            _shadowDepth = EnginePipes.ShadowOffscreen.Default();
            _matHash = _shadowDepth.Hash;

            _shadowDepth.PushConstants.SetPushConstantInt("matrixOffset", 0,0);
            _shadowDepth.PushConstants.SetPushConstantInt("baseLayerOffset", 0, 0);
            _shadowDepth.PushConstants.SetPushConstantInt("faceCount", 0, 1);
            _shadowDepth.PushConstants.SetPushConstantInt("lightIndex", 0, 0);
            _shadowDepth.PushConstants.SetPushConstantInt("writeDepth", 0, 0);
        }

        public void AssignDirShadowTexture()
        {
            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetTexture(ShaderProperties.DirShadowImageId, _shadowDepthImage.Target);
            });
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
            var mats = _shadowDepth.GetStorageSwapChainBuffer(PointLightShadows.matsPropertyId);
            var lights = _shadowDepth.GetStorageSwapChainBuffer(PointLightShadows.lightInfoPropertyId);
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

            DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, frameInfo.CommandBuffer, _matHash, 0);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

            _clearedImage = false;
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

        internal unsafe void ClearImage(RendererFrameInfo frameInfo)
        {
            if (_clearedImage) return;
            VkClearDepthStencilValue clearValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = _shadowDepthImage.Target.GetSubresourceRange();

            var existing = _shadowDepthImage.ImageLayout;
            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);
            }
            else
            {
                _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);
            }

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(frameInfo.CommandBuffer, _shadowDepthImage.VkImage, VkImageLayout.TransferDstOptimal, &clearValue, 1, &subresourceRange);

            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
            else
            {
                _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
            }
            _clearedImage = true;
        }
    }
}
