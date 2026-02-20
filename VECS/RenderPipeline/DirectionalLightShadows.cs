using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
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

        public static readonly int matsPropertyId = "directionalShadows".GetShaderPropertyId();

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

        public static unsafe void GetCascadeSplits(float nearPlane, float farPlane, float* cascadeSplits)
        {
            float clipRange = farPlane - nearPlane;

            float minZ = nearPlane;
            float maxZ = nearPlane + clipRange;

            float range = maxZ - minZ;
            float ratio = maxZ / minZ;

            for (int i = 0; i < CASCADE_COUNT; i++)
            {
                float p = (i + 1) / (float)CASCADE_COUNT;
                float log = minZ * MathF.Pow(ratio, p);
                float uniform = minZ + range * p;
                float d = CASCADE_SPLIT_LAMBDA * (log - uniform) + uniform;
                cascadeSplits[i] = (d - nearPlane) / clipRange;
            }
        }

        public static AABB GetSceneBounds(EntityManager entityManager)
        {
            AABB sceneBounds = new();
            entityManager ??= World.DefaultWorld.EntityManager;
            if (entityManager.SingletonComponent<FrameInfo>(out var sceneInfo))
            {
                sceneBounds = sceneInfo.sceneBounds;
            }
            return sceneBounds;
        }

        private static Span<Matrix4x4> GetLightSpaceMatrixBuffer()
        {
            var directionalShadowsBuffer = EnginePipes.DepthOnly.Default().GetStorageBuffer<Matrix4x4>(matsPropertyId);
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(matsPropertyId, CASCADE_COUNT);
            EnginePipes.DepthOnly.GetStorageSwapChainBuffer(matsPropertyId).SetBuffersDirty(true);
            return directionalShadowsBuffer;
        }

        private unsafe static void GetFustrumCorners(Matrix4x4 inverseCamera, Vector3[] fustrumCorners)
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

        public unsafe static Matrix4x4 CalculateCropMatrix(Matrix4x4 lightViewMatrix, Matrix4x4 lightProjMatrix, Vector3[] fustrumCorners)
        {
            Matrix4x4 viewProj = lightProjMatrix * lightViewMatrix;
            AABB receiverBB = GetSceneBounds(null);
            AABB casterBB = receiverBB;
            AABB splitBB = AABB.FromCenterSize(fustrumCorners[0], Vector3.Zero);

            for (int i = 1; i < 8; i++)
            {
                splitBB.Encapsulate(fustrumCorners[i]);
            }


            splitBB = AABB.Transform(viewProj, splitBB);

            Vector3 Min = default;
            Vector3 Max = default;

            Min.X = MathF.Max(MathF.Max(casterBB.Min.X, receiverBB.Min.X), splitBB.Min.X);
            Max.X = MathF.Min(MathF.Min(casterBB.Max.X, receiverBB.Max.X), splitBB.Max.X);
            Min.Y = MathF.Max(MathF.Max(casterBB.Min.Y, receiverBB.Min.Y), splitBB.Min.Y);
            Max.Y = MathF.Min(MathF.Min(casterBB.Max.Y, receiverBB.Max.Y), splitBB.Max.Y);
            Min.Z = MathF.Min(casterBB.Min.Z, splitBB.Min.Z);
            Max.Z = MathF.Min(receiverBB.Max.Z, splitBB.Max.Z);

            AABB cropBB = AABB.FromMinMax(Min, Max);


            // Create the crop matrix.
            float scaleX, scaleY, scaleZ;
            float offsetX, offsetY, offsetZ;
            scaleX = 2.0f / (cropBB.Max.X - cropBB.Min.X);
            scaleY = 2.0f / (cropBB.Max.Y - cropBB.Min.Y);
            offsetX = -0.5f * (cropBB.Max.X + cropBB.Min.X) * scaleX;
            offsetY = -0.5f * (cropBB.Max.Y + cropBB.Min.Y) * scaleY;
            scaleZ = 1.0f / (cropBB.Max.Z - cropBB.Min.Z);
            offsetZ = -cropBB.Min.Z * scaleZ;
            Matrix4x4 cropMatrix = new(scaleX, 0.0f, 0.0f, 0.0f, 0.0f, scaleY, 0.0f, 0.0f, 0.0f, 0.0f,
                          scaleZ, 0.0f, offsetX, offsetY, offsetZ, 1.0f);

            return cropMatrix;
        }

        private static Vector4[] GetFrustumCornersWorldSpace(Matrix4x4 proj, Matrix4x4 view)
        {
            Matrix4x4.Invert(view*proj,out var inv);
        
            Vector4[] frustumCorners = new Vector4[8];
            for ( int x = 0, i = 0; x< 2; ++x)
            {
                for ( int y = 0; y< 2; ++y)
                {
                    for ( int z = 0; z< 2; ++z, i++)
                    {
                         Vector4 pt = Vector4.Transform(new Vector4(
                                2.0f * x - 1.0f,
                                2.0f * y - 1.0f,
                                2.0f * z - 1.0f,
                                1.0f),inv);
                        frustumCorners[i]=(pt / pt.W);
                    }
        }
            }
            
            return frustumCorners;
        }

        private static Matrix4x4 GetLightSpaceMatrix(float near, float far,CameraInfo cameraInfo, AdditionalCameraInfo planes, Vector3 lightDir)
        {
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(TransformExtensions.Deg2Rad * 45, 1.7777778f, MathF.Max(near,0.001f),far);

            var sceneBounds = GetSceneBounds(null);
            Vector3 center = Vector3.Zero;
            var corners = GetFrustumCornersWorldSpace(proj, cameraInfo.ViewMatrix);
            foreach (var v in corners)
            {
                center += v.AsVector3();
            }
            center /= corners.Length;

            Matrix4x4 lightView = Matrix4x4.CreateLookAt(center + lightDir, center, new(0.0f, 1.0f, 0.0f));

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            foreach (var v in corners)
            {
                var trf = Vector4.Transform(v, lightView);
                minX = MathF.Min(minX, trf.X);
                maxX = MathF.Max(maxX, trf.X);
                minY = MathF.Min(minY, trf.Y);
                maxY = MathF.Max(maxY, trf.Y);
                minZ = MathF.Min(minZ, trf.Z);
                maxZ = MathF.Max(maxZ, trf.Z);
            }

            // Tune this parameter according to the scene
            var height = sceneBounds.Max.Y - sceneBounds.Min.Y;
            float zMult = height;
            if (minZ < 0)
            {
                minZ *= zMult;
            }
            else
            {
                minZ /= zMult;
            }
            if (maxZ < 0)
            {
                maxZ /= zMult;
            }
            else
            {
                maxZ *= zMult;
            }

            Matrix4x4 lightProjection = Matrix4x4.CreateOrthographicOffCenter(minX, maxX, minY, maxY, minZ, maxZ);
            return lightView* lightProjection;
        }

        public unsafe static DirectionalLightInfo GetDirectionalLightInfoGL(DirectionalLightInfo src, CameraInfo cameraInfo, AdditionalCameraInfo cameraPlanes)
        {
            float nearClip = cameraPlanes.NearPlane;
            float farClip = cameraPlanes.FarPlane;
            float clipRange = farClip - nearClip;
            DirectionalLightInfo lightingInfo = src;
            float* cascadeSplits = stackalloc float[CASCADE_COUNT];

            GetCascadeSplits(nearClip, farClip, cascadeSplits);

            lightingInfo.CascadeCount = CASCADE_COUNT;
            Span<Matrix4x4> ret = GetLightSpaceMatrixBuffer();
            for (int i = 0; i < CASCADE_COUNT; ++i)
            {
                if (i == 0)
                {
                    ret[0] = (GetLightSpaceMatrix(0, cascadeSplits[i], cameraInfo, cameraPlanes, src.Direction.AsVector3()));
                }
                else if (i < CASCADE_COUNT)
                {
                    ret[i] = (GetLightSpaceMatrix(cascadeSplits[i - 1], cascadeSplits[i], cameraInfo, cameraPlanes, src.Direction.AsVector3()));
                }

                lightingInfo[i] = ret[i];
                lightingInfo.CascadeSplits[i] = (nearClip + cascadeSplits[i] * clipRange) * -1.0f;
            }
            return lightingInfo;
        }

        public unsafe static DirectionalLightInfo GetDirectionalLightScene(DirectionalLightInfo src, CameraInverseInfo cameraInverseInfo, AdditionalCameraInfo additionalCameraInfo)
        {
            DirectionalLightInfo lightingInfo = src;

            lightingInfo.CascadeCount = CASCADE_COUNT;
            Span<Matrix4x4> directionalShadowsBuffer = GetLightSpaceMatrixBuffer();

            float nearClip = additionalCameraInfo.NearPlane;
            float farClip = additionalCameraInfo.FarPlane;
            float clipRange = farClip - nearClip;

            float* cascadeSplits = stackalloc float[CASCADE_COUNT];

            GetCascadeSplits(nearClip, farClip, cascadeSplits);
            Vector3[] frustumCorners = new Vector3[8];

            float lastSplitDist = 0.0f;
            var invCam = cameraInverseInfo.InverseProjectionViewMatrix;

            Matrix4x4 lightViewProj = GetSpaceMatrix(src, out float lightNear, out float lightFar, out Matrix4x4 lightView, out Matrix4x4 lightProj, out Vector3 lightPos);

            for (int i = 0; i < CASCADE_COUNT; i++)
            {
                float splitDist = cascadeSplits[i];

                GetFustrumCorners(invCam, frustumCorners);

                for (int j = 0; j < 4; j++)
                {
                    Vector3 dist = frustumCorners[j + 4] - frustumCorners[j];
                    frustumCorners[j + 4] = frustumCorners[j] + (dist * splitDist);
                    frustumCorners[j] = frustumCorners[j] + (dist * lastSplitDist);
                }

                Matrix4x4 cropMatrix = CalculateCropMatrix(lightView, lightProj, frustumCorners);

                directionalShadowsBuffer[i] = lightView * lightProj * cropMatrix;

                // Store split distance and matrix in cascade
                lightingInfo.CascadeSplits[i] = (nearClip + splitDist * clipRange) * -1.0f;
                lightingInfo[i] = directionalShadowsBuffer[i];
                lastSplitDist = cascadeSplits[i];
            }

            return lightingInfo;
        }

        public unsafe static DirectionalLightInfo GetDirectionalLight(DirectionalLightInfo src, CameraInverseInfo cameraInverseInfo, AdditionalCameraInfo additionalCameraInfo)
        {
            DirectionalLightInfo lightingInfo = src;

            lightingInfo.CascadeCount = CASCADE_COUNT;
            Span<Matrix4x4> directionalShadowsBuffer = GetLightSpaceMatrixBuffer();

            float nearClip = additionalCameraInfo.NearPlane;
            float farClip = additionalCameraInfo.FarPlane;
            float clipRange = farClip - nearClip;

            float* cascadeSplits = stackalloc float[CASCADE_COUNT];

            GetCascadeSplits(nearClip, farClip, cascadeSplits);
            
            float lastSplitDist = 0.0f;
            var invCam = cameraInverseInfo.InverseProjectionViewMatrix;

            var sceneBounds = GetSceneBounds(null);

            //var height = sceneBounds.Max.Y - sceneBounds.Min.Y;
            var height = Vector3.Distance(sceneBounds.Min, sceneBounds.Max);

            Vector3[] frustumCorners = new Vector3[8];
            for (int i = 0; i < CASCADE_COUNT; i++)
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

                // Store split distance and matrix in cascade
                lightingInfo.CascadeSplits[i] = (nearClip + splitDist * clipRange) * -1.0f;
                lightingInfo[i] = lightViewMatrix * lightOrthoMatrix;
                directionalShadowsBuffer[i] = lightingInfo[i];
                lastSplitDist = cascadeSplits[i];
            }

            return lightingInfo;
        }

        public static Matrix4x4 GetSpaceMatrix(DirectionalLightInfo directionalLight, out float nearPlane, out float farPlane, out Matrix4x4 lightView, out Matrix4x4 lightProj, out Vector3 lightPos)
        {
            AABB sceneBounds = new();
            if (World.DefaultWorld.EntityManager.SingletonComponent<FrameInfo>(out var sceneInfo))
            {
                sceneBounds = sceneInfo.sceneBounds;
            }
            farPlane = Vector3.Distance(sceneBounds.Min, sceneBounds.Max);

            const float near_plane = 0.01f;
            
            var lightDir = directionalLight.Direction.AsVector3();

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
            DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);

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
