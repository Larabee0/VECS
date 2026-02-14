using System;
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
        const int DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX = 1;
        public const int DIRECTIONAL_SHADOW_RESOLTION = 4096;
        public const int CASCADE_COUNT = 4;
        public const float CASCADE_SPLIT_LAMBDA = 0.95f;
        public const bool SHADOW_CULLING = false;
        public const bool SHADOW_DST_CULLING = false;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;

        private readonly Texture2DArray _shadowDepthImage;
        private readonly  VkViewport viewport = new()
        {
            width = DIRECTIONAL_SHADOW_RESOLTION,
            height = DIRECTIONAL_SHADOW_RESOLTION,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };

        private readonly  VkRect2D scissor = new(new(0, 0), new(DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION));

        private readonly Material _dirDepthOnly;
        private readonly int _matHash;

        private bool _clearedImage;

        public DirectionalLightShadows()
        {
            _shadowDepthImage = new("DirectionalShadowRT",
                DIRECTIONAL_SHADOW_RESOLTION,
                DIRECTIONAL_SHADOW_RESOLTION,
                CASCADE_COUNT,
                VkFormat.D32Sfloat,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled,
                false);

            _shadowDepthImage.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

            _dirDepthOnly = EnginePipes.DepthOnly.Default();
            _matHash = _dirDepthOnly.Hash;

            _dirDepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 0);
            _dirDepthOnly.PushConstants.SetPushConstantInt("bufferSelect", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _dirDepthOnly.PushConstants.SetPushConstantInt("baseLayerOffset", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 0);
            _dirDepthOnly.PushConstants.SetPushConstantInt("layerCount", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, CASCADE_COUNT);
            _dirDepthOnly.PushConstants.SetPushConstantInt("useLightPos", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 0);
        }

        public void AssignDirShadowTexture()
        {
            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetTextureArray(ShaderProperties.DirShadowImageId, _shadowDepthImage);
            });
        }

        public unsafe static DirectionalLightInfo GetDirectionalLight(DirectionalLightInfo src, CameraInfo cameraInfo, CameraInverseInfo cameraInverseInfo, AdditionalCameraInfo additionalCameraInfo)
        {
            DirectionalLightInfo lightingInfo = src;

            lightingInfo.CascadeCount = CASCADE_COUNT;

            var directionalShadowsBuffer = EnginePipes.DepthOnly.Default().GetStorageBuffer<Matrix4x4>("directionalShadows".GetShaderPropertyId());
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty("directionalShadows".GetShaderPropertyId(), CASCADE_COUNT);
            EnginePipes.DepthOnly.GetStorageSwapChainBuffer("directionalShadows".GetShaderPropertyId()).SetBuffersDirty(true);

            float nearClip = additionalCameraInfo.NearPlane;
            float farClip = additionalCameraInfo.FarPlane;
            float clipRange = farClip - nearClip;

            float minZ = nearClip;
            float maxZ = nearClip + clipRange;

            float range = maxZ - minZ;
            float ratio = maxZ / minZ;
            Vector4 cascadeSplits = default;
            for (int i = 0; i < CASCADE_COUNT; i++)
            {
                float p = (i + 1) / (float)CASCADE_COUNT;
                float log = minZ * MathF.Pow(ratio, p);
                float uniform = minZ + range * p;
                float d = CASCADE_SPLIT_LAMBDA * (log - uniform) + uniform;
                cascadeSplits[i] = (d - nearClip) / clipRange;
            }

            float lastSplitDist = 0.0f;
            // var invCam = cameraInverseInfo.InverseProjectionViewMatrix;
            Matrix4x4 invCam = Matrix4x4.Identity;
            Matrix4x4.Invert(cameraInfo.ViewMatrix * cameraInfo.ProjectionMatrix, out invCam);

            Vector3[] frustumCorners = new Vector3[8];
            AABB sceneBounds = new();
            if (World.DefaultWorld.EntityManager.SingletonComponent<FrameInfo>(out var sceneInfo))
            {
                sceneBounds = sceneInfo.sceneBounds;
            }
            float farPlane = Vector3.Distance(sceneBounds.Min, sceneBounds.Max);
            for (int i = 0; i < CASCADE_COUNT; i++)
            {
                float splitDist = cascadeSplits[i];

                frustumCorners[0] = new Vector3(-1.0f, 1.0f, 0.0f);
                frustumCorners[1] = new Vector3(1.0f, 1.0f, 0.0f);
                frustumCorners[2] = new Vector3(1.0f, -1.0f, 0.0f);
                frustumCorners[3] = new Vector3(-1.0f, -1.0f, 0.0f);
                frustumCorners[4] = new Vector3(-1.0f, 1.0f, 1.0f);
                frustumCorners[5] = new Vector3(1.0f, 1.0f, 1.0f);
                frustumCorners[6] = new Vector3(1.0f, -1.0f, 1.0f);
                frustumCorners[7] = new Vector3(-1.0f, -1.0f, 1.0f);

                for (int j = 0; j < 8; j++)
                {
                    Vector4 invCorner = Vector4.Transform(new Vector4(frustumCorners[j], 1.0f), invCam);
                    frustumCorners[j] = invCorner.AsVector3() / invCorner.W;
                }

                for (int j = 0; j < 4; j++)
                {
                    Vector3 dist = frustumCorners[j + 4] - frustumCorners[j];
                    frustumCorners[j + 4] = frustumCorners[j] + (dist * splitDist);
                    frustumCorners[j] = frustumCorners[j] + (dist * lastSplitDist);
                }

                Vector3 frustumCenter = new(0.0f);
                for (int j = 0; j < 8; j++)
                {
                    frustumCenter += frustumCorners[j];
                }
                frustumCenter /= 8.0f;

                float radius = 0.0f;
                for (int j = 0; j < 8; j++)
                {
                    float distance = (frustumCorners[j] - frustumCenter).Length();
                    radius = MathF.Max(radius, distance);
                }
                radius = MathF.Ceiling(radius * 16.0f) / 16.0f;

                Vector3 maxExtents = new(radius);
                Vector3 minExtents = -maxExtents;

                Vector3 lightDir = frustumCenter -  src.Direction.AsVector3() * -minExtents.Z;
                Matrix4x4 lightViewMatrix = Matrix4x4.CreateLookAt(lightDir, frustumCenter,new Vector3(0.0f, 1.0f, 0.0f));
                //Matrix4x4 lightOrthoMatrix = Matrix4x4.CreateOrthographic(radius*2, radius * 2, 0.0f, maxExtents.Z - minExtents.Z);
                Matrix4x4 lightOrthoMatrix = Matrix4x4.CreateOrthographicOffCenter(minExtents.X, maxExtents.X, minExtents.Y, maxExtents.Y, 0.0f, maxExtents.Z - minExtents.Z);

                // Store split distance and matrix in cascade
                lightingInfo.CascadeSplits[i] = (nearClip + splitDist * clipRange) * -1.0f;
                lightingInfo[i] = lightViewMatrix* lightOrthoMatrix;
                directionalShadowsBuffer[i] = lightingInfo[i];
                lastSplitDist = cascadeSplits[i];
            }

            return lightingInfo;
        }

        public static Matrix4x4 GetSpaceMatrix(LightingInfo lightingInfo, out float nearPlane, out float farPlane, out Matrix4x4 lightView, out Matrix4x4 lightProj, out Vector3 lightPos)
        {
            AABB sceneBounds = new();
            if (World.DefaultWorld.EntityManager.SingletonComponent<FrameInfo>(out var sceneInfo))
            {
                sceneBounds = sceneInfo.sceneBounds;
            }
            farPlane = Vector3.Distance(sceneBounds.Min, sceneBounds.Max);

            const float near_plane = 0.01f;
            
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


            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULLING, SHADOW_DST_CULLING, SHADOW_DEPTH_CULLING, 0, Matrix4x4.Identity, Matrix4x4.Identity);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

            _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.EarlyFragmentTests);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = _shadowDepthImage._imageView,
                imageLayout = _shadowDepthImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION),
                layerCount = CASCADE_COUNT,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);

            SetViewPort(frameInfo.CommandBuffer);

            _dirDepthOnly.PushConstants.SetPushConstantInt("bufferSelect", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            //DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, frameInfo.CommandBuffer, _matHash, 0);
            DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Back);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

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
            VkImageSubresourceRange subresourceRange = _shadowDepthImage.GetSubresourceRange();

            var existing = _shadowDepthImage.ImageLayout;
            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);
            }
            else
            {
                _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);
            }

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(frameInfo.CommandBuffer, _shadowDepthImage._vkImage, VkImageLayout.TransferDstOptimal, &clearValue, 1, &subresourceRange);

            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
            else
            {
                _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
            }
            _clearedImage = true;
        }
    }
}
