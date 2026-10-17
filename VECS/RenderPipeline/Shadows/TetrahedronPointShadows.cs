using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class TetrahedronPointShadows : LightShadowBase
    {
        public readonly static int TetraHedronPointShadowPropertyId = "tetraPLShadow".GetShaderPropertyId();
        public readonly static int TetraHedronPLMatsId = "tetraShadowMats".GetShaderPropertyId();
        public readonly static int TetraHedronPLPlanesId = "tetraShadowPlanes".GetShaderPropertyId();

        private readonly static Matrix4x4 RotationA = Matrix4x4.CreateRotationX(27.36780516f.ToRadians()) * Matrix4x4.CreateRotationY(180f.ToRadians());

        // $ yxz |  Matrix4x4.CreateRotationY(0.0f.ToRadians()) * Matrix4x4.CreateRotationX(27.36780516f.ToRadians()) * Matrix4x4.CreateRotationZ(90f.ToRadians());
        // $ zxy |  Matrix4x4.CreateRotationZ(90f.ToRadians()) * Matrix4x4.CreateRotationX(27.36780516f.ToRadians()) * Matrix4x4.CreateRotationY(0.0f.ToRadians());

        // $ xyz | Matrix4x4.CreateRotationX(27.36780516f.ToRadians()) * Matrix4x4.CreateRotationY(0.0f.ToRadians()) * Matrix4x4.CreateRotationZ(90f.ToRadians());
        // $ zyx | Matrix4x4.CreateRotationZ(90f.ToRadians()) * Matrix4x4.CreateRotationY(0.0f.ToRadians()) * Matrix4x4.CreateRotationX(27.36780516f.ToRadians());

        // $ yzx | Matrix4x4.CreateRotationY(0.0f.ToRadians()) * Matrix4x4.CreateRotationZ(90f.ToRadians()) * Matrix4x4.CreateRotationX(27.36780516f.ToRadians());
        // $ xzy | Matrix4x4.CreateRotationX(27.36780516f.ToRadians()) * Matrix4x4.CreateRotationZ(90f.ToRadians()) * Matrix4x4.CreateRotationY(0.0f.ToRadians());

        // original zxy | Matrix4x4.CreateRotationZ(90f.ToRadians()) * Matrix4x4.CreateRotationX(27.36780516f.ToRadians()) * Matrix4x4.CreateRotationY(0.0f.ToRadians());
        private readonly static Matrix4x4 RotationB = Matrix4x4.CreateRotationZ(90f.ToRadians()) * Matrix4x4.CreateRotationX(27.36780516f.ToRadians()) * Matrix4x4.CreateRotationY(0.0f.ToRadians());
        private readonly static Matrix4x4 RotationC = Matrix4x4.CreateRotationX(-27.36780516f.ToRadians()) * Matrix4x4.CreateRotationY(270f.ToRadians());
        private readonly static Matrix4x4 RotationD = Matrix4x4.CreateRotationZ(90f.ToRadians()) * Matrix4x4.CreateRotationX(-27.36780516f.ToRadians()) * Matrix4x4.CreateRotationY(90f.ToRadians());

        private const float ALPHA = 1.99273682f;
        private const float BETA = 2.78596497f;
        private const float FOV0 = 143.98570868f;
        private const float FOV1 = 125.26438968f;

        private readonly static Matrix4x4 Projection0 = Perspective(new(FOV0 + ALPHA, FOV1 + BETA), 0.01f, 7f);
        private readonly static Matrix4x4 Projection1 = Perspective(new(FOV1 + BETA, FOV0 + ALPHA), 0.01f, 7f);

        private readonly static Vector2 FOV_A = new(143.98570868f, 125.26438968f);
        private readonly static Vector2 FOV_B = new(125.26438968f, 143.98570868f);
        private readonly static Vector2 FOV_C = new(143.98570868f, 125.26438968f);
        private readonly static Vector2 FOV_D = new(125.26438968f, 143.98570868f);


        private const float r = 0.0625f;

        private readonly ComputeVariant _tile_debug_visualiser;
        private readonly RenderTarget _tileDebug;

        private readonly Material _blit;

        public TetrahedronPointShadows() : base(1)
        {
            _shadowDepthTextures.SetTexture(new Texture2D("TetrahedronShadowRT",
                4096, 4096,
                PreferredFormats.LOW_PRECISION_DEPTH_ONLY,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled,
                VkSamplerAddressMode.ClampToBorder,
                false),
            0);

            EngineTextures.AddOrUpdateTexture(TetraHedronPointShadowPropertyId, _shadowDepthTextures);

            _shadowDepthTextures.First.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests);

            RenderGraph.AddPass("TetrahedronPointShadows", PassType.Render, PassCategory.FixedMap, [], [], ["TetrahedronShadowAttachment"], ShadowPass);
            _blit = EnginePipes.Blit.Create("TerahedronDebug");
            _tile_debug_visualiser = ComputePipeline.GetOrCreate("tile_shadow_debug.comp").Default();
            RenderGraph.AddResource(new("Tile_Debug",
                Presenter.MainColourFormat,
                new(Screen.Width, Screen.Height),
                VkImageUsageFlags.Storage | VkImageUsageFlags.Sampled,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.ColorAttachmentOptimal,
                VkImageLayout.General,
                VkImageLayout.General
                
                ));
            _tileDebug =  RenderGraph.GetOrCreateResource("Tile_Debug", new(Screen.Width, Screen.Height));
            _tile_debug_visualiser.SetTexture("outImage".GetShaderPropertyId(), _tileDebug.Target);
            _tile_debug_visualiser.SetTextures(DeferredRenderer.G_PositionPropertyId, EngineTextures.TryGetTexture(DeferredRenderer.G_PositionPropertyId));
            _blit.SetTexture("inputTexture".GetShaderPropertyId(), _tileDebug.Target);

            RenderGraph.AddPass("Shadow_Tetrahedron_Debug", PassType.Compute, PassCategory.PostRendering, ["DeferredObjectsPass", "ForwardPass"], ["G_PositionAttachment"], ["Tile_Debug"],DebugPass);

            _tile_debug_visualiser.SetVector4("faceVectors.faceA".GetShaderPropertyId(), new(Face_A, 1));
            _tile_debug_visualiser.SetVector4("faceVectors.faceB".GetShaderPropertyId(), new(Face_B, 1));
            _tile_debug_visualiser.SetVector4("faceVectors.faceC".GetShaderPropertyId(), new(Face_C, 1));
            _tile_debug_visualiser.SetVector4("faceVectors.faceD".GetShaderPropertyId(), new(Face_D, 1));
        }

        private readonly static Vector3 Face_A = RotationA.Forward(); // down
        private readonly static Vector3 Face_B = RotationB.Forward(); // away
        private readonly static Vector3 Face_C = RotationC.Forward(); // left
        private readonly static Vector3 Face_D = RotationD.Forward(); // right
       // static Vector3 plPosition = new(0, 0, 0);
       //  private readonly static Vector3 Face_A = -new Vector3(0f, -0.57735026f, 0.81649661f);
       //  private readonly static Vector3 Face_B = -new Vector3(0f, -0.57735026f, -0.81649661f);
       //  private readonly static Vector3 Face_C = -new Vector3(-0.81649661f, 0.57735026f, 0f);
       //  private readonly static Vector3 Face_D = -new Vector3(0.81649661f, 0.57735026f, 0f);

        private readonly static Vector3 C_0 = -Face_B;
        private readonly static Vector3 C_1 = -Face_C;
        private readonly static Vector3 C_2 = -Face_A; 
        private readonly static Vector3 C_3 = -Face_D;


        private readonly static Vector3 Forward_Face_A = new(0f, -0.57735026f, 0.81649661f);
        private readonly static Vector3 Forward_Face_B = new(0f, -0.57735026f, -0.81649661f);
        private readonly static Vector3 Forward_Face_C = new(-0.81649661f, 0.57735026f, 0f);
        private readonly static Vector3 Forward_Face_D = new(0.81649661f, 0.57735026f, 0f);

        private readonly static Vector3 Up_C_0 = -Forward_Face_B;
        private readonly static Vector3 Up_C_1 = -Forward_Face_C;
        private readonly static Vector3 Up_C_2 = -Forward_Face_A;
        private readonly static Vector3 Up_C_3 = -Forward_Face_D;

        private Vector4 A_CP_0;
        private Vector4 A_CP_1;
        private Vector4 A_CP_2;
        private Vector4 B_CP_0;
        private Vector4 B_CP_1;
        private Vector4 B_CP_2;
        private Vector4 C_CP_0;
        private Vector4 C_CP_1;
        private Vector4 C_CP_2;
        private Vector4 D_CP_0;
        private Vector4 D_CP_1;
        private Vector4 D_CP_2;

        private void DebugPass(RendererFrameInfo frameInfo)
        {
            Span<Vector4> clippingPlanes = ((SwapChainBuffer<Vector4>)EngineBuffers.TryGetBuffer(TetraHedronPLPlanesId)).HostBuffer;

            Vector3 plPosition = new(-8, 1, 0);
            A_CP_0 = GetClippingPlane(ALPHA, Up_C_3, Up_C_0, Forward_Face_C, Forward_Face_A);
            A_CP_1 = GetClippingPlane(ALPHA, Up_C_1, Up_C_3, Forward_Face_B, Forward_Face_A);
            A_CP_2 = GetClippingPlane(ALPHA, Up_C_0, Up_C_1, Forward_Face_D, Forward_Face_A);
            B_CP_0 = GetClippingPlane(ALPHA, Up_C_3, Up_C_1, Forward_Face_A, Forward_Face_B);
            B_CP_1 = GetClippingPlane(ALPHA, Up_C_2, Up_C_3, Forward_Face_C, Forward_Face_B);
            B_CP_2 = GetClippingPlane(ALPHA, Up_C_1, Up_C_2, Forward_Face_D, Forward_Face_B);
            C_CP_0 = GetClippingPlane(ALPHA, Up_C_0, Up_C_3, Forward_Face_A, Forward_Face_C);
            C_CP_1 = GetClippingPlane(ALPHA, Up_C_3, Up_C_2, Forward_Face_B, Forward_Face_C);
            C_CP_2 = GetClippingPlane(ALPHA, Up_C_2, Up_C_0, Forward_Face_D, Forward_Face_C);
            D_CP_0 = GetClippingPlane(ALPHA, Up_C_1, Up_C_0, Forward_Face_A, Forward_Face_D);
            D_CP_1 = GetClippingPlane(ALPHA, Up_C_2, Up_C_1, Forward_Face_B, Forward_Face_D);
            D_CP_2 = GetClippingPlane(ALPHA, Up_C_0, Up_C_2, Forward_Face_C, Forward_Face_D);
            clippingPlanes[0] = GetClippingPlane(ALPHA, C_3, C_0, Face_C, Face_A);
            clippingPlanes[1] = GetClippingPlane(ALPHA, C_1, C_3, Face_B, Face_A);
            clippingPlanes[2] = GetClippingPlane(ALPHA, C_0, C_1, Face_D, Face_A);

            clippingPlanes[3] = GetClippingPlane(ALPHA, C_3, C_1, Face_A, Face_B);
            clippingPlanes[4] = GetClippingPlane(ALPHA, C_2, C_3, Face_C, Face_B);
            clippingPlanes[5] = GetClippingPlane(ALPHA, C_1, C_2, Face_D, Face_B);

            clippingPlanes[6] = GetClippingPlane(ALPHA, C_0, C_3, Face_A, Face_C);
            clippingPlanes[7] = GetClippingPlane(ALPHA, C_3, C_2, Face_B, Face_C);
            clippingPlanes[8] = GetClippingPlane(ALPHA, C_2, C_0, Face_D, Face_C);

            clippingPlanes[09] = GetClippingPlane(ALPHA, C_1, C_0, Face_A, Face_D);
            clippingPlanes[10] = GetClippingPlane(ALPHA, C_2, C_1, Face_B, Face_D);
            clippingPlanes[11] = GetClippingPlane(ALPHA, C_0, C_2, Face_C, Face_D);
            _tile_debug_visualiser.PushConstantsHandler.SetPushConstantVector4(ShaderProperties.OutputImageSize_PushConstantId, 0, new(frameInfo.OutputRect.extent.width, frameInfo.OutputRect.extent.height, 1.0f / frameInfo.OutputRect.extent.width, 1.0f / frameInfo.OutputRect.extent.height));
            _tile_debug_visualiser.PushConstantsHandler.SetPushConstantInt("sideIndex", 0, 0);
            _tile_debug_visualiser.PushConstantsHandler.SetPushConstantInt("useConst", 0, 0);

            _tile_debug_visualiser.Dispatch(frameInfo.CommandBuffer, Presenter.FrameIndex, frameInfo.OutputRect.extent.width, frameInfo.OutputRect.extent.height);

            _tileDebug.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);

            Presenter.Instance.Renderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);
            _blit.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            Presenter.Instance.Renderer.EndForwardRendering(frameInfo);
        }

        private unsafe void ShadowPass(RendererFrameInfo frameInfo)
        {
            if (ReassignTextures)
            {
                AssignShadowTextures(TetraHedronPointShadowPropertyId);
            }

            PreShadowPass(frameInfo);

            Vector3 plPosition = new(-8, 1, 0);
            float farPlane = 7;

            ProduceShadowMatrices(plPosition, farPlane, 1f, new Vector2(0));

            Texture2D tex = (Texture2D)_shadowDepthTextures.First;

            tex.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = tex._imageView,
                imageLayout = tex.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1,0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0,0, 4096, 4096),
                layerCount  = 1,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };

            DepthOnlyPushConstants depthOnlyPushConstants = new() { BufferSelect = 4 };
            Span<DepthOnlyPushConstants> pushBuffer = new(ref depthOnlyPushConstants);
            var commandBuffer = frameInfo.CommandBuffer;
            Span<Matrix4x4> views = stackalloc Matrix4x4[4];
            Span<Matrix4x4> projs = stackalloc Matrix4x4[4];
            ViewMatrices(plPosition, views);
            ProjectionMatrices(farPlane, projs);
            for (int i = 0; i < 4; i++)
            {
                depthOnlyPushConstants.MatrixStartIndex = i;
                //GraphicsDevice.BeginLabelCmd(commandBuffer, $"Cascade {i}");
                CullData depthBufferCullInfo = new(
                    SHADOW_INCLUDE_MASK,
                    SHADOW_EXCLUDE_MASK,
                    SHADOW_CULL_MODE,
                    0.01f,
                    projs[i],
                    views[i]
                );

                CullShadow(commandBuffer, depthBufferCullInfo);

                GraphicsDevice.BeginLabelCmd(commandBuffer, "Depth Pass");
                GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
                
                SetViewPort(commandBuffer, 4096);
                DrawDepthOnly(commandBuffer, pushBuffer, VkCullModeFlags.None);
                    
                GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);

                GraphicsDevice.EndLabelCmd(commandBuffer);
                //GraphicsDevice.EndLabelCmd(commandBuffer);
                //depth.loadOp = VkAttachmentLoadOp.Load;
            }
            tex.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);

        }

        public override void PreShadowPass(in RendererFrameInfo frameInfo)
        {
            var mats = EngineBuffers.TryGetBuffer(TetraHedronPLMatsId);
            mats.SetBuffersDirty(true);
            GPUBufferExtensions.WriteFromHostDelayed(mats, Presenter.FrameIndex);
            var planes = EngineBuffers.TryGetBuffer(TetraHedronPLPlanesId);
            planes.SetBuffersDirty(true);
            GPUBufferExtensions.WriteFromHostDelayed(planes, Presenter.FrameIndex);
        }

        public override bool SetShadowTexture(int i, int resolution)
        {
            throw new NotImplementedException();
        }

        private static void ProduceShadowMatrices(Vector3 position, float farPlane, float texSize, Vector2 texCoords)
        {
            Span<Matrix4x4> texture = stackalloc Matrix4x4[4];
            Span<Matrix4x4> projection = stackalloc Matrix4x4[4];
            Span<Matrix4x4> views = stackalloc Matrix4x4[4];
            Span<Matrix4x4> shadowMatrices = ((SwapChainBuffer<Matrix4x4>)EngineBuffers.TryGetBuffer(TetraHedronPLMatsId)).HostBuffer;

            TextureMatrices(texCoords, texSize, texture);
            ProjectionMatrices(farPlane, projection);
            ViewMatrices(position, views);

            Span<Colour> colours = [Colour.Red, Colour.Blue, Colour.Green, Colour.White];


            for (int i = 0; i < 4; i++)
            {
                string proj = projection[i].ToHumanString();
                string view = views[i].ToHumanString();
                string tex = texture[i].ToHumanString();
                shadowMatrices[i] = views[i] * projection[i];// * texture[i];
            }
            var translation = Matrix4x4.CreateTranslation(position);
        }

        private static Vector4 GetClippingPlane(float alpha, Vector3 right, Vector3 left, Vector3 up, Vector3 forward)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(left,right));
            Vector3 rotationAxis = Vector3.Normalize(Vector3.Cross(up,forward));
            return new(Vector3.Normalize(Vector3.Transform(normal, new Quaternion(rotationAxis, -alpha.ToRadians()))), 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TextureMatrices(Vector2 coord, float size, Span<Matrix4x4> textures)
        {
            //ma
            textures[0] = Matrix4x4.Transpose(new Matrix4x4()
            {
                M11 = size,
                M22 = (size * 0.5f),
                M33 = 1,
                M44 = 1,
                M14 = coord.X,
                M24 = coord.Y - (size * 0.5f),
            });
            // mb
            textures[1] = Matrix4x4.Transpose(new()
            {
                M11 = (size * 0.5f),
                M22 = size,
                M33 = 1,
                M44 = 1,
                M14 = coord.X + (size * 0.5f),
                M24 = coord.Y
            });
            // mc
            textures[2] = Matrix4x4.Transpose(new()
            {
                M11 = size,
                M22 = (size * 0.5f),
                M33 = 1,
                M44 = 1,
                M14 = coord.X,
                M24 = coord.Y + (size * 0.5f)
            });
            // md
            textures[3] = Matrix4x4.Transpose(new()
            {
                M11 = (size * 0.5f),
                M22 = size,
                M33 = 1,
                M44 = 1,
                M14 = coord.X - (size * 0.5f),
                M24 = coord.Y
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ViewMatrices(Vector3 position, Span<Matrix4x4> views)
        {
            views[0] = Matrix4x4.CreateLookAt(position, position + RotationA.Forward(), RotationA.Up());
            views[1] = Matrix4x4.CreateLookAt(position, position + RotationB.Forward(), RotationB.Up());
            views[2] = Matrix4x4.CreateLookAt(position, position + RotationC.Forward(), RotationC.Up());
            views[3] = Matrix4x4.CreateLookAt(position, position + RotationD.Forward(), RotationD.Up());

            DebugDrawer.DrawLine(position, position + RotationA.Forward(), Colour.Red);
            DebugDrawer.DrawLine(position, position + RotationB.Forward(), Colour.Green);
            DebugDrawer.DrawLine(position, position + RotationC.Forward(), Colour.Blue);
            DebugDrawer.DrawLine(position, position + RotationD.Forward(), Colour.White);

            DebugDrawer.DrawLine(position, position + (0.5f * -RotationA.Up()), Colour.Red);
            DebugDrawer.DrawLine(position, position + (0.5f * RotationB.Up()), Colour.Green);
            DebugDrawer.DrawLine(position, position + (0.5f * RotationC.Up()), Colour.Blue);
            DebugDrawer.DrawLine(position, position + (0.5f * RotationD.Up()), Colour.White);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ProjectionMatrices(float farPlane, Span<Matrix4x4> projections)
        {
            projections[0] = Projection0;
            projections[1] = Projection1;
            projections[2] = Projection0;
            projections[3] = Projection1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Matrix4x4 GetPerspectiveFOVHV(float farPlane, Vector2 fov)
        {
            return Matrix4x4.CreatePerspective(1f / MathF.Tan(fov.X * 0.5f), 1f / MathF.Tan(fov.Y * 0.5f), 0.1f, farPlane);
        }

        private static Matrix4x4 GetPerspectiveDX9(float farplane, Vector2 fov)
        {
            fov *= TransformExtensions.Deg2Rad;
           float w = 1f / MathF.Tan(fov.X * 0.5f);  // 1/tan(x) == cot(x)
           float h = 1f / MathF.Tan(fov.Y * 0.5f);   // 1/tan(x) == cot(x)
           float Q = farplane / (farplane - 0.1f);
            Matrix4x4 ret = new();

            ret[0, 0] = w;
            ret[1, 1] = h;
            ret[2,2] = -Q;
            ret[3, 2] = Q * -0.1f;
            ret[2, 3] = -1;

            return ret;
        }

        private static Matrix4x4 Perspective(Vector2 fov, float n, float f)
        {
            float left, right, top, bottom;

            top = n * MathF.Tan(fov.Y.ToRadians() * 0.5f);
            bottom = -top;
            right = n * MathF.Tan(fov.X.ToRadians() * 0.5f);
            left = -right;
            Matrix4x4 matrix = new();

            matrix[0, 0] = (2.0f * n) / (right - left);
            matrix[1, 1] = (2.0f * n) / (top - bottom);

            matrix[2, 0] = (right + left) / (right - left);
            matrix[2, 1] = (top + bottom) / (top - bottom);
            if (f >= 0)
            {
                matrix[2, 2] = -(f + n) / (f - n);
                matrix[3, 2] = -(2.0f * f * n) / (f - n);
            }
            else
            {
                const float infiniteValue = 0.999f;
                matrix[2,2] = -infiniteValue;
                matrix[3,2] = -2.0f * n * infiniteValue;
            }
            matrix[2,3] = -1.0f;
            return matrix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 GetAlphaBeta(float farPlane, Vector2 fov, Matrix4x4 undialatedInverseProjA)
        {
            Span<Vector3> v = stackalloc Vector3[4];

            Span<Vector3> centers =
            [
                new Vector3(-1, 0, -1),
                new Vector3(1, 0, -1),
                new Vector3(0, -1, -1),
                new Vector3(0, 1, -1)
            ];

            Span<Vector3> offsets =
            [
                new Vector3(-r, 0, 0),
                new Vector3(r, 0, 0),
                new Vector3(0, -r, 0),
                new Vector3(0, r, 0)
            ];
            for (int i = 0; i < 4; i++)
            {
                centers[i] += offsets[i];
                v[i] = Vector3.Normalize(Vector3.Transform(centers[i], undialatedInverseProjA));
            }
            Vector2 dilatedFov = new(MathF.Acos(Vector3.Dot(v[0], v[1])) * 180f / MathF.PI,
            MathF.Acos(Vector3.Dot(v[2], v[3])) * 180f / MathF.PI);
            return dilatedFov - fov;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 Swap(Vector2 a)
        {
            (a.X, a.Y) = (a.Y, a.X);
            return a;
        }
    }
}
