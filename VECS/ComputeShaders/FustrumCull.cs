using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
#if DEBUG
using VECS.ECS;
using VECS.ECS.Presentation;
#endif
using VECS.LowLevel;
using VECS.Presentation;
using Vortice.Vulkan;

namespace VECS
{
    [Flags]
    public enum RenderLayer : uint
    {
        None = 0,
        Default = 1,
        NoShadow = 2,
        OnlyShadow = 4,
        All = Default | NoShadow | OnlyShadow
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct VECSDrawIndexIndirectCommand
    {
        public uint indexCount;
        public uint instanceCount;
        public uint firstIndex;
        public int vertexOffset;
        public uint firstInstance;
        public RenderLayer layerFlags;

        public VECSDrawIndexIndirectCommand()
        {
            layerFlags = RenderLayer.Default;


            // uint includeMask =(uint)( RenderLayer.All);
            // uint excludeMask =(uint)( RenderLayer.OnlyShadow);
            // uint flags = (uint)(RenderLayer.Default | RenderLayer.NoShadow);
            // var include = (flags | includeMask) == includeMask;
            // var exclude = (flags | excludeMask) == excludeMask;
            // var visible = include && !exclude;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 208)]
    public struct CullData
    {
        public Vector4 left;
        public Vector4 right;
        public Vector4 bottom;
        public Vector4 top;
        public Vector4 near;
        public Vector4 far;
        public Vector2 pyramid;
        public float zNear;// 96
        public float P00; // 100
        public float P11; // 104
        public RenderLayer IncludeMask;
        public RenderLayer ExcludeMask;
        public uint drawCount;
        public int cullingEnabled;
        public int dstCulling;
        public int depthCulling;

        public Matrix4x4 View;

        public readonly Vector4 this[int i] => i switch
        {
            0 => left,
            1 => right,
            2 => bottom,
            3 => top,
            4 => near,
            5 => far,
            _ => throw new IndexOutOfRangeException(),
        };

        public readonly void SetPushConstant(PushConstantsHandler pushConstants, int setId = 0)
        {
            pushConstants.SetPushConstantUniform("cullData", setId, this);
        }

        public CullData(RenderLayer includeMask, RenderLayer excludeMask, bool cull, bool dstCull,bool depthCull, float zNear, CameraInfo camera)
        {
            Matrix4x4 viewProj = camera.ProjectionViewMatrix;

            Matrix4x4 projectionT = Matrix4x4.Transpose(viewProj);
            near = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(2);
            far = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(2);

            right = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(0);
            left = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0);

            top = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1);
            bottom = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(1);
            this.zNear = zNear;
            P00 = viewProj[0, 0];
            P11 = viewProj[1, 1];

            pyramid = new(DepthReduction.DepthPryamid.Width, DepthReduction.DepthPryamid.Height);

            cullingEnabled = cull ? 1 : 0;
            dstCulling = dstCull ? 1 : 0;
            depthCulling = depthCull ? 1 : 0;
            drawCount = 0;
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            View = camera.ViewMatrix;
        }

        public CullData(RenderLayer includeMask, RenderLayer excludeMask, bool cull, bool dstCull, bool depthCull, float zNear, Matrix4x4 projection, Matrix4x4 view)
        {
            Matrix4x4 viewProj = view * projection;

            Matrix4x4 projectionT = Matrix4x4.Transpose(viewProj);
            near = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(2);
            far = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(2);

            right = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(0);
            left = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0);

            top = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1);
            bottom = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(1);
            this.zNear = zNear;
            P00 = viewProj[0, 0];
            P11 = viewProj[1, 1];

            pyramid = new(DepthReduction.DepthPryamid.Width, DepthReduction.DepthPryamid.Height);

            cullingEnabled = cull ? 1 : 0;
            dstCulling = dstCull ? 1 : 0;
            depthCulling = depthCull ? 1 : 0;
            drawCount = 0;
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            View = view;
        }
    }

    public static class FustrumCull
    {
#if DEBUG
        public const bool CPUCulling = false;
#endif

        private static readonly int BoundsBufferId = "boundsBuffer".GetShaderPropertyId();
        private static readonly int DrawBufferId = "drawBuffer".GetShaderPropertyId();
        private static readonly int DepthPyramidId = "depthPyramid".GetShaderPropertyId();
        private static readonly int CullDataId = "cullData".GetShaderPropertyId();


        private static readonly ComputeShader _computeShader;
        private static readonly ComputeShader _textureSampler;

        private static readonly GPUBuffer<float> _textureResult;

        private static uint _variant = 0;

        public static ComputeShader Shader => _computeShader;

        static FustrumCull()
        {
            _computeShader = ComputeShader.GetOrCreate("fustrum_cull.comp");
            _textureSampler = ComputeShader.GetOrCreate("textureSampler.comp");
            _textureResult = new GPUBuffer<float>(1, VkBufferUsageFlags.StorageBuffer, true, false, false);
            _textureSampler.SetStorageBuffer("outBuffer".GetShaderPropertyId(), 0, _textureResult);
            Presenter.Instance.PostPresentationUpdate += PostPresent;
            Application.Instance.OnDestroy += static () => _textureResult.Dispose();
        }

        public static void PostPresent()
        {
            _computeShader.SetUniformBufferLength(_variant);

            Interlocked.Exchange(ref _variant, 0);
        }

        public static void Cull(VkCommandBuffer commandBuffer,int frameIndex, CullData cullData, uint drawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> drawIndirect, SwapChainBuffer<ShaderAABB> bounds)
        {
            if (_variant > 2000)
            {
                Console.WriteLine("Fustrum Cull Compute Shader invokations exceeded default max uniform count of {0}", ShaderSet.MAX_VARIANTS);
            }

            var discriptorIndex = Interlocked.Increment(ref _variant) - 1;
#if DEBUG
#pragma warning disable CS0162
            if (CPUCulling)
            {
                CPUCull(cullData, drawCount, drawIndirect, bounds);
                return;
            }

            var includeMask = cullData.IncludeMask;
            var excludeMask = cullData.ExcludeMask;
            for (int i = 0; i < drawCount; i++)
            {
                var flags = drawIndirect.HostBuffer[i].layerFlags;
                var include = (includeMask & flags) == flags;
                var exclude = (excludeMask & flags) == flags;
                var visible = include && !exclude;
            }

#pragma warning restore CS0162
#endif


            GPUCullInternal(commandBuffer,frameIndex, cullData, drawCount, drawIndirect, bounds, discriptorIndex);
            
        }
        private static void GPUCullInternal(VkCommandBuffer commandBuffer, int frameIndex, CullData cullData, uint drawCount, SwapChainBuffer drawIndirect, SwapChainBuffer bounds, uint setId)
        {
            bounds.SetUsedInstanceCount(drawCount);
            drawIndirect.SetUsedInstanceCount(drawCount);
            cullData.drawCount = drawCount;
            //cullData.SetPushConstant(_computeShader.PushConstantsHandler, (int)setId);
            _computeShader.SetUniform(CullDataId, setId, cullData);
            _computeShader.SetStorageBuffer(DrawBufferId, setId, drawIndirect);
            _computeShader.SetStorageBuffer(BoundsBufferId, setId, bounds);
            _computeShader.SetTexture(DepthPyramidId, setId, DepthReduction.DepthPryamid);
            _computeShader.Dispatch(commandBuffer, frameIndex, setId, (drawCount / 256) + 1);

            VkBufferMemoryBarrier2 barrier = new()
            {
                buffer = drawIndirect.ActiveVkBuffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.IndirectCommandRead | VkAccessFlags2.ShaderWrite
            };

            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.DrawIndirect | VkPipelineStageFlags2.ComputeShader);
        }

#if DEBUG
        private static void CPUCull(CullData cullData, uint drawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> drawIndirect, SwapChainBuffer<ShaderAABB> bounds)
        {
            Span<VECSDrawIndexIndirectCommand> drawIndirectSpan = drawIndirect.HostBuffer;
            Span<ShaderAABB> boundsSpan = bounds.HostBuffer;

            for (int i = 0; i < drawCount; i++)
            {
                AABB boundsInternal = boundsSpan[i];

                if (cullData.cullingEnabled == 0 || IsVisibleAABB(boundsSpan[i], cullData))
                {

                    if (cullData.depthCulling != 0 && DepthProj(boundsSpan[i], cullData.View, cullData.zNear, cullData.P00, cullData.P11, out var aabb, out float radius))
                    {
                        var center = Vector3.Transform(boundsInternal.Center, cullData.View);
                        float width = MathF.Abs((aabb.Z - aabb.X) * cullData.pyramid.X);
                        float height = MathF.Abs((aabb.W - aabb.Y) * cullData.pyramid.Y);
                        float level = Math.Min(9, MathF.Floor(MathF.Log2(Math.Max(width, height))));
                        Vector2 uv = (new Vector2(aabb.X, aabb.Y) + new Vector2(aabb.Z, aabb.W)) * 0.5f;
                        uv.X = 1.0f - uv.X;
                        _textureSampler.SetTexture("depthPyramid".GetShaderPropertyId(), 0, DepthReduction.DepthPryamid);
                        _textureSampler.PushConstantsHandler.SetPushConstantVector2("uv", 0, uv);
                        _textureSampler.PushConstantsHandler.SetPushConstantFloat("level", 0, level);
                        var computeCommandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();

                        _textureSampler.Dispatch(computeCommandBuffer, 0, 0, 1);
                        GraphicsDevice.EndSingleTimeMainPipe(computeCommandBuffer);
                        _textureResult.ReadToHostBuffer();  
                        var depthValue = 1-_textureResult.HostBuffer[0];
                        float depthSphere = MathF.Abs( cullData.zNear / (center.Z - radius));

                        bool visible = depthSphere >= depthValue;
                        if (visible)
                        {
                            drawIndirectSpan[i].instanceCount =1u;
                        }
                        else
                        {
                            drawIndirectSpan[i].instanceCount = 0;
                        }
                    }
                    else
                    {
                        drawIndirectSpan[i].instanceCount = 1;
                    }
                    //World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawWireCube(boundsInternal.Center, boundsInternal.Size, Quaternion.Identity, Colour.Green);
                }
                else
                {
                    drawIndirectSpan[i].instanceCount = 0;
                    //World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawWireCube(boundsInternal.Center, boundsInternal.Size, Quaternion.Identity, Colour.Red);
                }
            }
            bounds.SetUsedInstanceCount(drawCount);
            drawIndirect.SetUsedInstanceCount(drawCount);
            drawIndirect.WriteFromHostToActiveBuffer();
        }
        public static bool IsVisibleAABB(ShaderAABB bounds, CullData cullData)
        {
            var min = bounds.Min;
            var max = bounds.Max;
            min.W = 1f;
            max.W = 1f;
            int planeCount = cullData.dstCulling == 1 ? 6 : 4;
            for (int i = 0; i < planeCount; i++)
            {
                var g = cullData[i];
                float d0 = Vector4.Dot(g, min);
                float d1 = Vector4.Dot(g, new Vector4(max.X, min.Y, min.Z, 1f));
                float d2 = Vector4.Dot(g, new Vector4(min.X, max.Y, min.Z, 1f));
                float d3 = Vector4.Dot(g, new Vector4(max.X, max.Y, min.Z, 1f));

                float d4 = Vector4.Dot(g, new Vector4(min.X, min.Y, max.Z, 1f));
                float d5 = Vector4.Dot(g, new Vector4(max.X, min.Y, max.Z, 1f));
                float d6 = Vector4.Dot(g, new Vector4(min.X, max.Y, max.Z, 1f));
                float d7 = Vector4.Dot(g, max);

                if (d0 < 0.0f &&
                    d1 < 0.0f &&
                    d2 < 0.0f &&
                    d3 < 0.0f &&
                    d4 < 0.0f &&
                    d5 < 0.0f &&
                    d6 < 0.0f &&
                    d7 < 0.0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DepthProj(AABB boundingBox, Matrix4x4 view, float zNear,float p00, float p11, out Vector4 aabb, out float radius)
        {
            Vector3 center = boundingBox.Center;
            radius = Vector3.Distance(boundingBox.Min, boundingBox.Max) * 0.5f;


            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawSphere(center, radius, Colour.Green);
            center = Vector3.Transform(center, view);
            return ProjectSphere(center, radius, zNear, p00, p11, out aabb);
        }

        // 2D Polyhedral Bounds of a Clipped, Perspective-Projected 3D Sphere. Michael Mara, Morgan McGuire. 2013
        private static bool ProjectSphere(Vector3 C, float r, float znear, float P00, float P11, out Vector4 aabb)
        {
            if (-C.Z < r + znear)
            {
                aabb = default;
                return false;
            }
            C.Y *= -1;

            Vector2 cx = new Vector2(C.X,C.Z);
            Vector2 vx = new(MathF.Sqrt(Vector2.Dot(cx, cx) - r * r), r);
            Vector2 minx = new Mat2(vx.X, vx.Y, -vx.Y, vx.X) * cx;
            Vector2 maxx = new Mat2(vx.X, -vx.Y, vx.Y, vx.X) * cx;

            Vector2 cy = new Vector2(C.Y,C.Z);
            Vector2 vy = new(MathF.Sqrt(Vector2.Dot(cy, cy) - r * r), r);
            Vector2 miny = new Mat2(vy.X, vy.Y, -vy.Y, vy.X) * cy;
            Vector2 maxy = new Mat2(vy.X, -vy.Y, vy.Y, vy.X) * cy;

            aabb = new Vector4(minx.X / minx.Y * P00, miny.X / miny.Y * P11, maxx.X / maxx.Y * P00, maxy.X / maxy.Y * P11);
            aabb =  new Vector4(aabb.X,aabb.W,aabb.Z,aabb.Y) * new Vector4(0.5f, -0.5f, 0.5f, -0.5f) + new Vector4(0.5f); // clip space -> uv space

            return true;
        }

        public struct Mat2
        {
            public Vector2 c0;
            public Vector2 c1;

            public Mat2(float m00, float m01, float m10, float m11)
            {
                c0 = new Vector2(m00, m10);
                c1 = new Vector2(m01, m11);
            }

            public static Vector2 operator *(Mat2 a, Vector2 b)
            {
                return a.c0 * b.X + a.c1 * b.Y;
            }
        }
#endif        
    }
}
