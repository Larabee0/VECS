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
        const int DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX = 1;
        public static VkFormat DIRECTIONAL_SHADOW_FORMAT => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;
        public const int MAX_CASCADE_COUNT = 4;
        public const float CASCADE_SPLIT_LAMBDA = 0.95f;

        public static readonly int matsPropertyId = ShaderProperties.DirShadowMatsId;

        private static readonly Matrix4x4[] _viewMatrices = new Matrix4x4[MAX_CASCADE_COUNT];
        private static readonly Matrix4x4[] _projMatrices = new Matrix4x4[MAX_CASCADE_COUNT];

        public DirectionalLightShadows() : base(1)
        {
            _shadowDepthTextures.SetTexture( new Texture2DArray("DirectionalShadowRT",
                1,
                1,
                MAX_CASCADE_COUNT,
                DIRECTIONAL_SHADOW_FORMAT,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled,
                false),0);

            EngineTextures.AddOrUpdateTexture(ShaderProperties.DirShadowImageId, _shadowDepthTextures);
            AssignShadowTextures(ShaderProperties.DirShadowImageId);

            _shadowDepthTextures.First.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);
            
            _depthOnly.PushConstants.SetPushConstantInt("bufferSelect", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnly.PushConstants.SetPushConstantInt("layerCount", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnly.PushConstants.SetPushConstantInt("layerOffset", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 0);

            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("bufferSelect", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("layerCount", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("layerOffset", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 0);

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
        private static void GetFustrumCorners(Matrix4x4 inverseCamera, Vector3[] fustrumCorners)
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

        public unsafe static DirectionalLightUniform GetDirectionalLight(DirectionalLightUniform src, CameraInverseInfo cameraInverseInfo, AdditionalCameraInfo additionalCameraInfo)
        {
            DirectionalLightUniform lightingInfo = src;

            lightingInfo.CascadeCount = MAX_CASCADE_COUNT;
            var directionalShadowsBuffer = ((SwapChainBuffer<Matrix4x4>)EngineBuffers.TryGetBuffer(matsPropertyId)).HostBuffer;

            float nearClip = additionalCameraInfo.NearPlane;
            float farClip = additionalCameraInfo.FarPlane;
            float clipRange = farClip - nearClip;

            float* cascadeSplits = stackalloc float[MAX_CASCADE_COUNT];

            GetCascadeSplits(nearClip, farClip, cascadeSplits);
            
            float lastSplitDist = 0.0f;
            var invCam = cameraInverseInfo.InverseProjectionViewMatrix;

            var sceneBounds = GetSceneBounds(null);

            //var height = sceneBounds.Max.Y - sceneBounds.Min.Y;
            var height = Vector3.Distance(sceneBounds.Min, sceneBounds.Max);

            Vector3[] frustumCorners = new Vector3[8];
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
                _viewMatrices[i] = lightViewMatrix;
                _projMatrices[i] = lightOrthoMatrix;
                // Store split distance and matrix in cascade
                lightingInfo.CascadeSplits[i] = (nearClip + splitDist * clipRange) * -1.0f;
                lightingInfo[i] = lightViewMatrix * lightOrthoMatrix;
                directionalShadowsBuffer[i] = lightingInfo[i];
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
            GPUBufferExtensions.WriteFromHostDelayed(mats, frameInfo.FrameIndex);
        }

        public unsafe void DirectionalShadowPass(in RendererFrameInfo frameInfo, DirectionalLightUniform dirUniform)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Directional Light Shadow Pass");
            Texture2DArray arrayTex = (Texture2DArray)_shadowDepthTextures.First;
            arrayTex.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.EarlyFragmentTests);

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
                renderArea = new(0, 0, (uint)arrayTex.Width, (uint)arrayTex.Height),
                layerCount = 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };

            for (int i = 0; i < Math.Min(MAX_CASCADE_COUNT,dirUniform.CascadeCount); i++)
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Cascade {0}", i));
                depth.imageView = arrayTex.AdditionalImageViews[i];
                depthBufferCullInfo = new(
                    SHADOW_INCLUDE_MASK,
                    SHADOW_EXCLUDE_MASK,
                    SHADOW_CULL_MODE,
                    0,
                    _projMatrices[i],
                    _viewMatrices[i]
                );
                DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);
                DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);
                GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);


                SetViewPort(frameInfo.CommandBuffer, (uint)arrayTex.Width);

                _depthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, i);
                _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("matrixStartIndex", DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, i);

                DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);
                GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
            arrayTex.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }
    }
}
