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
    public class DirectionalLightShadows : LightShadowBase
    {
        public const int DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX = 1;
        public static VkFormat DIRECTIONAL_SHADOW_FORMAT => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;
        public const int MAX_CASCADE_COUNT = 4;
        public const float CASCADE_SPLIT_LAMBDA = 0.95f;

        public static readonly int matsPropertyId = ShaderProperties.DirShadowMatsId;

        private static readonly Matrix4x4[] _viewMatrices = new Matrix4x4[MAX_CASCADE_COUNT * Presenter.MAX_CAMERAS * 20];
        private static readonly Matrix4x4[] _projMatrices = new Matrix4x4[MAX_CASCADE_COUNT * Presenter.MAX_CAMERAS * 20];

        private readonly Texture2D _singleDirShadowRT;

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
                0
            );
            //_singleDirShadowRT = new Texture2D("SingleDirectionalShadowRT", 4096, 4096, DIRECTIONAL_SHADOW_FORMAT, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, VkSamplerAddressMode.ClampToBorder, false);
            EngineTextures.AddOrUpdateTexture(ShaderProperties.DirShadowImageId, _shadowDepthTextures);
            AssignShadowTextures(ShaderProperties.DirShadowImageId);

            _shadowDepthTextures.First.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

            _depthOnly.PushConstants.SetPushConstantInt(Buffer_Select_PushConstantId, DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);

            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt(Buffer_Select_PushConstantId, DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);

            if (GraphicsDevice.MeshShading)
            {
                _meshDepthOnly.PushConstants.SetPushConstantInt(Buffer_Select_PushConstantId, DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
                _meshDepthOnlyAlphaClipping.PushConstants.SetPushConstantInt(Buffer_Select_PushConstantId, DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            }

            RenderGraph.AddPass("DirectionalLightShadows", PassType.Render, PassCategory.PreRendering, [], [], ["DirectionalShadowAttachments"], ShadowPass);
        }

        private static Matrix4x4 GetCascadeTextureMatrix(int cascadeIndex)
        {
            const float size = 0.5f;

            switch (cascadeIndex)
            {
                case 0:
                    return Matrix4x4.Transpose(new Matrix4x4()
                    {
                        M11 = size,
                        M22 = size,
                        M33 = 1,
                        M44 = 1,
                        M14 = 0,
                        M24 = 0
                    });
                case 1:
                    return Matrix4x4.Transpose(new Matrix4x4()
                    {
                        M11 = size,
                        M22 = size,
                        M33 = 1,
                        M44 = 1,
                        M14 = size,
                        M24 = -size
                    });
                case 2:
                    return Matrix4x4.Transpose(new Matrix4x4()
                    {
                        M11 = size,
                        M22 = size,
                        M33 = 1,
                        M44 = 1,
                        M14 = -size,
                        M24 = size
                    });
                case 3:
                    return Matrix4x4.Transpose(new Matrix4x4()
                    {
                        M11 = size,
                        M22 = size,
                        M33 = 1,
                        M44 = 1,
                        M14 = size,
                        M24 = size
                    });
                default:
                    return Matrix4x4.Identity;
            }
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

            if (Clear)
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
        private static void GetCascadeSplits(float nearPlane, float farPlane, Span<float> cascadeSplits)
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
        private static void GetFustrumCorners(Matrix4x4 inverseCamera, Span<Vector3> fustrumCorners)
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

        public static DirectionalLightShadowUniform GetDirectionalLight(DirectionalLightUniform src, int lightIndex, CameraData cameraData, int offset, float scale)
        {
            DirectionalLightShadowUniform lightingInfo = default;
            lightingInfo.LightIndex = lightIndex;
            offset *= MAX_CASCADE_COUNT;
            lightingInfo.CascadeCount = MAX_CASCADE_COUNT;
            lightingInfo.Scale = new(Math.Max(1.0f, scale));

            var directionalShadowsBuffer = ((SwapChainBuffer<Matrix4x4>)EngineBuffers.TryGetBuffer(matsPropertyId)).HostBuffer;

            float nearClip = cameraData.NearPlane;
            float farClip = cameraData.FarPlane;
            float clipRange = farClip - nearClip;

            Span<float> cascadeSplits = stackalloc float[MAX_CASCADE_COUNT];

            GetCascadeSplits(nearClip, farClip, cascadeSplits);

            float lastSplitDist = 0.0f;
            var invCam = cameraData.InverseProjectionViewMatrix;

            var sceneBounds = GetSceneBounds(null);

            //var height = sceneBounds.Max.Y - sceneBounds.Min.Y;
            var height = Vector3.Distance(sceneBounds.Min, sceneBounds.Max);

            Span<Vector3> frustumCorners = stackalloc Vector3[8];
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
                _viewMatrices[offset + i] = lightViewMatrix;
                _projMatrices[offset + i] = lightOrthoMatrix;
                var str = lightOrthoMatrix.ToHumanString();
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
            var renderScale = Math.Max(1.0f, dirUniform.Scale.X);

            int cameraOffset = frameInfo.TargetCamera * MAX_CASCADE_COUNT;

            if (dirUniform.CascadeCount > 1)
            {
                var passCount = Math.Min(MAX_CASCADE_COUNT, dirUniform.CascadeCount);
                /*
                VkCommandBuffer* secondaries = stackalloc VkCommandBuffer[passCount];
                Application.ParallelFor(passCount, (i) =>
                {
                    secondaries[i] = GraphicsDevice.GetSecondaryCommandBuffer();
                    GraphicsDevice.BeginSecondaryCommandBuffer(secondaries[i]);
                    SinglePass(i, cameraOffset, renderScale, secondaries[i], arrayTex);
                    GraphicsDevice.DeviceAPI.vkEndCommandBuffer(secondaries[i]);
                });

                GraphicsDevice.DeviceAPI.vkCmdExecuteCommands(frameInfo.CommandBuffer, (uint)passCount, secondaries);
                */
                if (GraphicsDevice.MeshShading)
                {
                    // for supporting non-mesh shading depth materials need to add a way to only draw mesh materials in the first pass and only draw vertex ones in the next pass
                    
                    MeshShadingPass(cameraOffset, passCount, renderScale, frameInfo.CommandBuffer, arrayTex);

                    if (DrawBlob.HasDrawItems(Depth_Only_Queue_Name, DrawType.Vertex))
                    {
                        GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Vertex Passes");
                        for (int i = 0; i < passCount; i++)
                        {
                            SinglePass(i, cameraOffset, renderScale, frameInfo.CommandBuffer, arrayTex, VkAttachmentLoadOp.Load);
                        }
                        GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
                    }
                }
                else
                {
                    for (int i = 0; i < passCount; i++)
                    {
                        SinglePass(i, cameraOffset, renderScale, frameInfo.CommandBuffer, arrayTex);
                    }
                }
            }
            else
            {
                SinglePass(0, cameraOffset, renderScale, frameInfo.CommandBuffer, arrayTex);
            }

            arrayTex.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
        }

        private unsafe void SingleTextureBeginRendering(VkCommandBuffer commandBuffer, float renderScale, VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear)
        {
            GraphicsDevice.BeginLabelCmd(commandBuffer, "Depth Pass");
            _singleDirShadowRT.SetImageLayoutAuto(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal);

            VkRenderingAttachmentInfo depth = GetAttchmentInfo(_singleDirShadowRT._imageView, loadOp);
            VkRenderingInfo renderingInfo = GetRenderingInfo(depth, renderScale, 1, (uint)_singleDirShadowRT.Width);
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer, (uint)(_singleDirShadowRT.Width * renderScale));
        }

        private unsafe void MeshShadingPass(int cameraOffset, int cascadeCount, float renderScale, VkCommandBuffer commandBuffer, Texture2DArray arrayTex)
        {
            CullData depthBufferCullInfo = new(
                SHADOW_INCLUDE_MASK,
                SHADOW_EXCLUDE_MASK,
                SHADOW_CULL_MODE,
                DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX,
                (ushort)cameraOffset,
                (ushort)cascadeCount,
                0,
                _projMatrices.AsSpan(cameraOffset, 4),
                _viewMatrices.AsSpan(cameraOffset, 4)
            );

            bool draw = CullShadow(commandBuffer, depthBufferCullInfo, DrawType.Mesh);

            GraphicsDevice.BeginLabelCmd(commandBuffer, "Depth Pass Mesh Shader");
            VkRenderingAttachmentInfo depth = GetAttchmentInfo(arrayTex._imageView, VkAttachmentLoadOp.Clear);
            VkRenderingInfo renderingInfo = GetRenderingInfo(depth, renderScale, (uint)cascadeCount, (uint)arrayTex.Width);
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer, (uint)(arrayTex.Width * renderScale));
            
            Span<DepthOnlyPushConstants> pushBuffer = stackalloc DepthOnlyPushConstants[1];
            DrawDepthOnly(commandBuffer, pushBuffer, VkCullModeFlags.Front,draw ? DrawType.Mesh : DrawType.None);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);

        }

        private void SinglePass(int cascadeIndex, int cameraOffset, float renderScale, VkCommandBuffer commandBuffer, Texture2DArray arrayTex, VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear)
        {
            GraphicsDevice.BeginLabelCmd(commandBuffer, string.Format("Cascade {0}", cascadeIndex));

            CullData depthBufferCullInfo = new(
                SHADOW_INCLUDE_MASK,
                SHADOW_EXCLUDE_MASK,
                SHADOW_CULL_MODE,
                0,
                _projMatrices[cameraOffset + cascadeIndex],
                _viewMatrices[cameraOffset + cascadeIndex]
            )
            {
                ProjectionSelect = DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX,
                ProjectionOffset = (ushort)(cameraOffset + cascadeIndex),
                LayerCount = 1
            };

            bool draw = CullShadow(commandBuffer, depthBufferCullInfo, DrawType.Vertex);
            BeginRenderingTexArray(cascadeIndex, renderScale, commandBuffer, arrayTex, loadOp);
            //SingleTextureBeginRendering(commandBuffer, renderScale, loadOp);

            Span<DepthOnlyPushConstants> pushBuffer = [new() { BufferSelect = DIRECTIONAL_SHADOWS_PUSH_CONSTANT_INDEX, MatrixStartIndex = cameraOffset + cascadeIndex }];
            DrawDepthOnly(commandBuffer, pushBuffer, VkCullModeFlags.Front, draw ? DrawType.Vertex : DrawType.None);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            //_singleDirShadowRT.SetImageLayoutAuto(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
            GraphicsDevice.EndLabelCmd(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);
        }

        private static unsafe void BeginRenderingTexArray(int cascadeIndex, float renderScale, VkCommandBuffer commandBuffer, Texture2DArray arrayTex, VkAttachmentLoadOp loadOp)
        {
            GraphicsDevice.BeginLabelCmd(commandBuffer, "Depth Pass");
            VkRenderingAttachmentInfo depth = GetAttchmentInfo(arrayTex.AdditionalImageViews[cascadeIndex], loadOp);
            VkRenderingInfo renderingInfo = GetRenderingInfo(depth, renderScale, 1, (uint)arrayTex.Width);
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer, (uint)(arrayTex.Width * renderScale));
        }
    }
}
