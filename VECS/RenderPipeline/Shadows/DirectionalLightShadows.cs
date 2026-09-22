using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DirectionalLightShadows : LightShadowBase
    {
        public const int DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX = 1;
        public static VkFormat DIRECTIONAL_SHADOW_FORMAT => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;
        public const int MAX_CASCADE_COUNT = 4;
        public const float CASCADE_SPLIT_LAMBDA = 0.95f;

        public static readonly int matsPropertyId = ShaderProperties.DirShadowMatsId;

        private static readonly Matrix4x4[] _viewMatrices = new Matrix4x4[MAX_CASCADE_COUNT * Presenter.MAX_CAMERAS * 20];
        private static readonly Matrix4x4[] _projMatrices = new Matrix4x4[MAX_CASCADE_COUNT * Presenter.MAX_CAMERAS * 20];

        public DirectionalLightShadows() : base(1)
        {
            _shadowDepthTextures.SetTexture(new Texture2DArray("DirectionalShadowRT",
                1,
                1,
                MAX_CASCADE_COUNT,
                DIRECTIONAL_SHADOW_FORMAT,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled,
                false),
            0);

            EngineTextures.AddOrUpdateTexture(ShaderProperties.DirShadowImageId, _shadowDepthTextures);
            AssignShadowTextures(ShaderProperties.DirShadowImageId);

            _shadowDepthTextures.First.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);
            
            _depthOnly.PushConstants.SetPushConstantInt("bufferSelect", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnly.PushConstants.SetPushConstantInt("layerCount", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnly.PushConstants.SetPushConstantInt("layerOffset", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 0);

            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("bufferSelect", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("layerCount", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("layerOffset", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 0);

            RenderGraph.AddPass("DirectionalLightShadows", PassType.Render,PassCategory.PreRendering, [], [], ["DirectionalShadowAttachments"], ShadowPass);
        }

        private void ShadowPass(RendererFrameInfo frameInfo)
        {
            if (ReassignTextures)
            {
                AssignShadowTextures(ShaderProperties.DirShadowImageId);
            }

            PreShadowPass(frameInfo);
            var hostBuffer = (SwapChainBuffer<DirectionalLightShadowUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.DirectionalLightShadowBufferId);
            GPUBufferExtensions.WriteFromHostDelayed(hostBuffer, Presenter.FrameIndex);

            if(Clear)
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Clear Shadow {0}", 0));
                ClearImage(frameInfo, 0);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
            else
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Render Shadow {0}", 0));
                DirectionalShadowPass(frameInfo, hostBuffer.HostBuffer[frameInfo.TargetCamera]);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void GetCascadeSplits(float nearPlane, float farPlane, float* cascadeSplits)
        {
            float clipRange = farPlane - nearPlane;

            float minZ = nearPlane;
            float maxZ = nearPlane + clipRange;

            float range = maxZ - minZ;
            float ratio = maxZ / minZ;

            for (int i = 0; i < MAX_CASCADE_COUNT; i++)
            {
                float p = (i + 1) / (float)MAX_CASCADE_COUNT;
                float log = minZ * MathF.Pow(ratio, p);
                float uniform = minZ + range * p;
                float d = CASCADE_SPLIT_LAMBDA * (log - uniform) + uniform;
                cascadeSplits[i] = (d - nearPlane) / clipRange;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AABB GetSceneBounds(EntityManager entityManager)
        {
            AABB sceneBounds = new();
            entityManager ??= World.DefaultWorld.EntityManager;
            if (entityManager.SingletonComponent<FrameInfo>(out var sceneInfo))
            {
                sceneBounds = sceneInfo.sceneBounds;
            }
            return sceneBounds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void GetFustrumCorners(Matrix4x4 inverseCamera, Vector3* fustrumCorners)
        {
            fustrumCorners[0] = new Vector3(-1.0f, 1.0f, 0.0f);
            fustrumCorners[1] = new Vector3(1.0f, 1.0f, 0.0f);
            fustrumCorners[2] = new Vector3(1.0f, -1.0f, 0.0f);
            fustrumCorners[3] = new Vector3(-1.0f, -1.0f, 0.0f);
            fustrumCorners[4] = new Vector3(-1.0f, 1.0f, 1.0f);
            fustrumCorners[5] = new Vector3(1.0f, 1.0f, 1.0f);
            fustrumCorners[6] = new Vector3(1.0f, -1.0f, 1.0f);
            fustrumCorners[7] = new Vector3(-1.0f, -1.0f, 1.0f);

            for (int i = 0; i < 8; i++)
            {
                Vector4 invCorner = Vector4.Transform(new Vector4(fustrumCorners[i], 1.0f), inverseCamera);
                fustrumCorners[i] = invCorner.AsVector3() / invCorner.W;
            }
        }

        public unsafe static DirectionalLightShadowUniform GetDirectionalLight(DirectionalLightUniform src, int lightIndex, CameraData cameraData, int offset, float scale)
        {
            DirectionalLightShadowUniform lightingInfo = default;
            lightingInfo.LightIndex = lightIndex;
            offset *= MAX_CASCADE_COUNT;
            lightingInfo.CascadeCount = MAX_CASCADE_COUNT;
            lightingInfo.Scale = new(Math.Max(1.0f,scale));
            
            var directionalShadowsBuffer = ((SwapChainBuffer<Matrix4x4>)EngineBuffers.TryGetBuffer(matsPropertyId)).HostBuffer;

            float nearClip = cameraData.NearPlane;
            float farClip = cameraData.FarPlane;
            float clipRange = farClip - nearClip;

            float* cascadeSplits = stackalloc float[MAX_CASCADE_COUNT];

            GetCascadeSplits(nearClip, farClip, cascadeSplits);
            
            float lastSplitDist = 0.0f;
            var invCam = cameraData.InverseProjectionViewMatrix;

            var sceneBounds = GetSceneBounds(null);

            //var height = sceneBounds.Max.Y - sceneBounds.Min.Y;
            var height = Vector3.Distance(sceneBounds.Min, sceneBounds.Max);

            Vector3* frustumCorners = stackalloc Vector3[8];
            for (int i = 0; i < MAX_CASCADE_COUNT; i++)
            {
                float splitDist = cascadeSplits[i];

                GetFustrumCorners(invCam, frustumCorners);

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

                if (MathF.Abs(maxExtents.Z - minExtents.Z) < height)
                {
                    float range = MathF.Abs(maxExtents.Z - minExtents.Z);

                    float diff = height - range;
                    float half = diff * 0.5f;

                    if (minExtents.Z < 0)
                    {
                        minExtents.Z -= half;
                    }
                    else
                    {
                        minExtents.Z += half;
                    }
                    if (maxExtents.Z < 0)
                    {
                        maxExtents.Z -= half;
                    }
                    else
                    {
                        maxExtents.Z += half;
                    }
                    range = MathF.Abs(maxExtents.Z - minExtents.Z);
                }
                Vector3 lightDir = frustumCenter - src.Direction.AsVector3() * -minExtents.Z;
                
                Matrix4x4 lightViewMatrix = Matrix4x4.CreateLookAt(lightDir, frustumCenter, new Vector3(0.0f, 1.0f, 0.0f));
                Matrix4x4 lightOrthoMatrix = Matrix4x4.CreateOrthographicOffCenter(minExtents.X, maxExtents.X, minExtents.Y, maxExtents.Y, 0.0f, maxExtents.Z - minExtents.Z);
                _viewMatrices[offset +i] = lightViewMatrix;
                _projMatrices[offset + i] = lightOrthoMatrix;
                // Store split distance and matrix in cascade
                lightingInfo.CascadeSplits[i] = (nearClip + splitDist * clipRange) * -1.0f;
                lightingInfo[i] = lightViewMatrix * lightOrthoMatrix;
                directionalShadowsBuffer[offset + i] = lightingInfo[i];
                lastSplitDist = cascadeSplits[i];
            }

            return lightingInfo;
        }

        public override bool SetShadowTexture(int i, int resolution)
        {
            var textureArray = (Texture2DArray)_shadowDepthTextures.First;
            if (textureArray.Width != resolution)
            {
                textureArray.Reinitialise(resolution);
                return true;
            }
            return false;
        }

        public override void PreShadowPass(in RendererFrameInfo frameInfo)
        {
            var mats = EngineBuffers.TryGetBuffer(matsPropertyId);
            mats.SetBuffersDirty(true);
            GPUBufferExtensions.WriteFromHostDelayed(mats, Presenter.FrameIndex);
        }

        public unsafe void DirectionalShadowPass(in RendererFrameInfo frameInfo, DirectionalLightShadowUniform dirUniform)
        {
            Texture2DArray arrayTex = (Texture2DArray)_shadowDepthTextures.First;
            arrayTex.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal);
            var renderScale  = Math.Max(1.0f, dirUniform.Scale.X);
            CullData depthBufferCullInfo;
            VkRenderingAttachmentInfo depth = new()
            {
                imageView = arrayTex._imageView,
                imageLayout = VkImageLayout.DepthAttachmentOptimal,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)(arrayTex.Width * renderScale), (uint)(arrayTex.Height * renderScale)),
                layerCount = 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };

            int cameraOffset = frameInfo.TargetCamera * MAX_CASCADE_COUNT;

            for (int i = 0; i < Math.Min(MAX_CASCADE_COUNT,dirUniform.CascadeCount); i++)
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Cascade {0}", i));
                depth.imageView = arrayTex.AdditionalImageViews[i];
                depthBufferCullInfo = new(
                    SHADOW_INCLUDE_MASK,
                    SHADOW_EXCLUDE_MASK,
                    SHADOW_CULL_MODE,
                    0,
                    _projMatrices[cameraOffset + i],
                    _viewMatrices[cameraOffset + i]
                );
                
                CullShadow(frameInfo.CommandBuffer, depthBufferCullInfo);

                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Depth Pass");
                GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);


                SetViewPort(frameInfo.CommandBuffer, (uint)(arrayTex.Width * renderScale));

                _depthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, cameraOffset + i);
                _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("matrixStartIndex", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, cameraOffset + i);
                // _depthOnly.PushConstants.SetPushConstantUInt("cameraIndex", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, (uint)frameInfo.TargetCamera);
                // _depthOnlyAlphaClipping.PushConstants.SetPushConstantUInt("cameraIndex", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, (uint)frameInfo.TargetCamera);

                DrawDepthOnly(frameInfo.CommandBuffer, DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);

                GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
            arrayTex.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
        }
    }
}
