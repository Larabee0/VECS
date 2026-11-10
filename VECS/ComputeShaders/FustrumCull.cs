using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
#if DEBUG
using VECS.ECS;
using VECS.ECS.Presentation;
#endif
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    [Flags]
    public enum RenderLayer : byte
    {
        None = 0,
        Default = 1,
        NoShadow = 2,
        OnlyShadow = 4,
        Transparent = 8,
        All = Default | NoShadow | OnlyShadow | Transparent
    }

    [Flags]
    public enum CullModeFlags : byte
    {
        None = 0,
        Fustrum = 1,
        Distance = 2,
        Depth = 4,
        All = Fustrum | Distance | Depth
    }

    public enum CullShape : byte
    {
        Fustrum = 0,
        Sphere = 1,
        AABB = 2,
    }

    [Flags]
    public enum Layer : ushort
    {
        None = 0,
        One = 1,
        two = 2,
        Three = 4,
        Four = 8,
        Five = 16,
        Six = 32,
        All = One | two | Three | Four | Five | Six

    }



    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VECSDrawIndexIndirectCommand
    {
        public uint indexCount;
        public uint instanceCount;
        public uint firstIndex;
        public int vertexOffset;
        public uint firstInstance;
        public uint absMatrixIndex;
        public RenderLayer layerFlags;
        public byte drawnFlag;

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

    [StructLayout(LayoutKind.Sequential, Size = 512)]
    public struct CullData
    {
        public Matrix4x4 ViewProj;
        public Matrix4x4 ViewProj1;
        public Matrix4x4 ViewProj2;
        public Matrix4x4 ViewProj3;
        public Matrix4x4 ViewProj4;
        public Matrix4x4 ViewProj5;
        public Matrix4x4 View;
        public Vector4 Sphere;
        public Vector2 Pyramid;
        public float ZNear;
        public float P00;
        public float P11;
        public uint DrawCount;
        public uint ProjectionSelect;
        public ushort ProjectionOffset;
        public ushort LayerCount;
        public RenderLayer IncludeMask;
        public RenderLayer ExcludeMask;
        public CullModeFlags CullMode;
        public CullShape CullShape;

        public readonly Vector4 this[int i] => i switch
        {
            0 => ViewProj.GetMatrixColumn(3) + ViewProj.GetMatrixColumn(0),
            1 => ViewProj.GetMatrixColumn(3) - ViewProj.GetMatrixColumn(0),
            2 => ViewProj.GetMatrixColumn(3) - ViewProj.GetMatrixColumn(1),
            3 => ViewProj.GetMatrixColumn(3) + ViewProj.GetMatrixColumn(1),
            4 => ViewProj.GetMatrixColumn(3) - ViewProj.GetMatrixColumn(2),
            5 => ViewProj.GetMatrixColumn(3) + ViewProj.GetMatrixColumn(2),
            _ => throw new IndexOutOfRangeException(),
        };

        public CullData(RenderLayer includeMask, RenderLayer excludeMask, CullModeFlags cullMode, float zNear, CameraData camera)
        {
            ViewProj = camera.ProjectionMatrix;
            //ViewProj = Matrix4x4.Transpose(ViewProj);
            ZNear = zNear;
            P00 = ViewProj[0, 0];
            P11 = ViewProj[1, 1];

            Pyramid = new(DepthReduction.DepthPryamid.Width, DepthReduction.DepthPryamid.Height);

            CullMode = cullMode;
            DrawCount = 0;
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            CullShape = CullShape.Fustrum;
            View = camera.ViewMatrix;
        }

        public CullData(RenderLayer includeMask, RenderLayer excludeMask, CullModeFlags cullMode, float zNear, Matrix4x4 projection, Matrix4x4 view)
        {
            ViewProj = view * projection;
            //ViewProj = Matrix4x4.Transpose(ViewProj);
            ZNear = zNear;
            P00 = ViewProj[0, 0];
            P11 = ViewProj[1, 1];

            Pyramid = new(DepthReduction.DepthPryamid.Width, DepthReduction.DepthPryamid.Height);

            CullMode = cullMode;
            DrawCount = 0;
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            CullShape = CullShape.Fustrum;
            View = view;
        }

        public CullData(RenderLayer includeMask, RenderLayer excludeMask, CullModeFlags cullMode, Vector4 sphere)
        {
            ViewProj = Matrix4x4.Identity;
            Sphere = sphere;
            ZNear = 0;
            P00 = ViewProj[0, 0];
            P11 = ViewProj[1, 1];

            Pyramid = new(DepthReduction.DepthPryamid.Width, DepthReduction.DepthPryamid.Height);

            CullMode = cullMode;
            DrawCount = 0;
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            CullShape = CullShape.Sphere;

            View = Matrix4x4.Identity;
        }

        public CullData(RenderLayer includeMask, RenderLayer excludeMask, CullModeFlags cullMode, Vector4 sphere, Span<Matrix4x4> projectionMatrices, Span<Matrix4x4> viewMatrices)
        {
            Debug.Assert(viewMatrices.Length >= 6);
            Debug.Assert(projectionMatrices.Length >= 6);
            View = viewMatrices[0];
            ViewProj = viewMatrices[0] * projectionMatrices[0];
            ViewProj1 = viewMatrices[1] * projectionMatrices[1];
            ViewProj2 = viewMatrices[2] * projectionMatrices[2];
            ViewProj3 = viewMatrices[3] * projectionMatrices[3];
            ViewProj4 = viewMatrices[4] * projectionMatrices[4];
            ViewProj5 = viewMatrices[5] * projectionMatrices[5];

            Sphere = sphere;
            ZNear = 0;
            P00 = ViewProj[0, 0];
            P11 = ViewProj[1, 1];

            Pyramid = new(DepthReduction.DepthPryamid.Width, DepthReduction.DepthPryamid.Height);

            CullMode = cullMode;
            DrawCount = 0;
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            CullShape = CullShape.Sphere;

        }

        public CullData(RenderLayer includeMask, RenderLayer excludeMask, CullModeFlags cullMode, uint projectionSelect, ushort projectionOffset, Matrix4x4 cubeProjectionMatrix, Span<Matrix4x4> viewMatrices)
        {
            Debug.Assert(viewMatrices.Length >= 6);
            View = viewMatrices[0];
            ViewProj = viewMatrices[0] *  cubeProjectionMatrix;
            ViewProj1 = viewMatrices[1] * cubeProjectionMatrix;
            ViewProj2 = viewMatrices[2] * cubeProjectionMatrix;
            ViewProj3 = viewMatrices[3] * cubeProjectionMatrix;
            ViewProj4 = viewMatrices[4] * cubeProjectionMatrix;
            ViewProj5 = viewMatrices[5] * cubeProjectionMatrix;

            ZNear = 0;
            P00 = ViewProj[0, 0];
            P11 = ViewProj[1, 1];

            Pyramid = new(DepthReduction.DepthPryamid.Width, DepthReduction.DepthPryamid.Height);

            CullMode = cullMode;
            DrawCount = 0;
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            CullShape = CullShape.Fustrum;
            ProjectionSelect = projectionSelect;
            ProjectionOffset = projectionOffset;
            LayerCount = 6;
        }

        public CullData(RenderLayer includeMask, RenderLayer excludeMask, CullModeFlags cullMode, uint projectionSelect, ushort projectionOffset, ushort layerCount, float zNear, Span<Matrix4x4> projectionMatrices, Span<Matrix4x4> viewMatrices)
        {
            Debug.Assert(viewMatrices.Length >= 4);
            Debug.Assert(projectionMatrices.Length >= 4);
            View = viewMatrices[0];
            ViewProj = viewMatrices[0] * projectionMatrices[0];
            ViewProj1 = viewMatrices[1] * projectionMatrices[1];
            ViewProj2 = viewMatrices[2] * projectionMatrices[2];
            ViewProj3 = viewMatrices[3] * projectionMatrices[3];

            ZNear = zNear;
            P00 = ViewProj[0, 0];
            P11 = ViewProj[1, 1];


            Pyramid = new(DepthReduction.DepthPryamid.Width, DepthReduction.DepthPryamid.Height);

            CullMode = cullMode;
            DrawCount = 0;
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            ProjectionSelect = projectionSelect;
            ProjectionOffset = projectionOffset;
            LayerCount = layerCount;
            CullShape = CullShape.Fustrum;

        }
    }

    public static class FustrumCull
    {
#if DEBUG
        public const bool CPUCulling = false;
#endif
        public static readonly int DrawBufferId = "drawBuffer".GetShaderPropertyId();
        private static readonly int DepthPyramidId = "depthPyramid".GetShaderPropertyId();
        public static readonly int CullDataId = "cullData".GetShaderPropertyId();


        private static readonly ComputePipeline _computeShader;
        private static readonly ComputePipeline _textureSampler;
#if DEBUG
        private static readonly GPUBuffer<float> _textureResult;
#endif

        private static uint _invocation = 0;

        public static ComputePipeline Shader => _computeShader;

        static FustrumCull()
        {
            _computeShader = ComputePipeline.GetOrCreate("fustrum_cull.comp");
#if DEBUG
            _textureSampler = ComputePipeline.GetOrCreate("textureSampler.comp");
            _textureResult = new GPUBuffer<float>(16, VkBufferUsageFlags.StorageBuffer, true, false, false);
            _textureSampler.Default().SetStorageBuffer("outBuffer".GetShaderPropertyId(), _textureResult);
            Application.Instance.OnDestroy += static () => _textureResult.Dispose();
#endif
            Presenter.Instance.PostPresentationUpdate += PostPresent;
        }

        public static void PostPresent()
        {
            Interlocked.Exchange(ref _invocation, 0);
        }

        public static void Cull(VkCommandBuffer commandBuffer,int frameIndex, CullData cullData, uint drawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> drawIndirect, SwapChainBuffer bounds)
        {
            
            var variantIndex = Interlocked.Increment(ref _invocation) - 1;
            /*
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


            bool fustrumCulling = (1 | (byte)cullData.cullMode) == (byte)cullData.cullMode;
            bool distanceCulling = (2 | (byte)cullData.cullMode) == (byte)cullData.cullMode;
            bool depthCulling = (4 | (byte)cullData.cullMode) == (byte)cullData.cullMode;

#pragma warning restore CS0162
#endif
            */

            GPUCullInternal(commandBuffer,frameIndex, cullData,0, drawCount, drawIndirect, bounds, variantIndex);

        }
        public static void Cull(VkCommandBuffer commandBuffer, int frameIndex, CullData cullData, uint indirectOffset, uint drawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> drawIndirect, SwapChainBuffer bounds)
        {

            var variantIndex = Interlocked.Increment(ref _invocation) - 1;
            /*
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


            bool fustrumCulling = (1 | (byte)cullData.cullMode) == (byte)cullData.cullMode;
            bool distanceCulling = (2 | (byte)cullData.cullMode) == (byte)cullData.cullMode;
            bool depthCulling = (4 | (byte)cullData.cullMode) == (byte)cullData.cullMode;

#pragma warning restore CS0162
#endif
            */

            GPUCullInternal(commandBuffer, frameIndex, cullData, indirectOffset, drawCount, drawIndirect, bounds, variantIndex);

        }
        private static void GPUCullInternal(VkCommandBuffer commandBuffer, int frameIndex, CullData cullData, uint indirectCmdOffset, uint drawCount, SwapChainBuffer drawIndirect, SwapChainBuffer bounds, uint variantIndex)
        {
            //bounds.SetUsedInstanceCount(drawCount);
            //drawIndirect.SetUsedInstanceCount(indirectCmdOffset+drawCount);
            cullData.DrawCount = drawCount;

            var invokeVariant = _computeShader.GetOrCreateVariant(variantIndex);

            invokeVariant.SetUniform(CullDataId, cullData);
            invokeVariant.SetStorageBuffer(DrawBufferId, drawIndirect, indirectCmdOffset, drawCount);
            invokeVariant.SetStorageBuffer(ShaderProperties.BoundsBufferId, bounds);
            invokeVariant.SetTexture(DepthPyramidId, DepthReduction.DepthPryamid);
            Pipeline._descriptorReWrite = true;
            invokeVariant.Dispatch(commandBuffer, frameIndex, drawCount);

            VkBufferMemoryBarrier2 barrier = new()
            {
                buffer = drawIndirect.ActiveVkBuffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.IndirectCommandRead | VkAccessFlags2.ShaderWrite,
                srcStageMask =  VkPipelineStageFlags2.ComputeShader,
                dstStageMask = VkPipelineStageFlags2.DrawIndirect | VkPipelineStageFlags2.ComputeShader
            };

            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier);
        }


#if DEBUG
        private static unsafe void CPUCull(CullData cullData, uint drawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> drawIndirect, SwapChainBuffer bounds)
        {
            Span<VECSDrawIndexIndirectCommand> drawIndirectSpan = drawIndirect.HostBuffer;
            Span<ShaderAABB> boundsSpan = new(bounds.HostPtr,(int)bounds.HostBufferSize32 / bounds.InstanceSize32);

            for (int i = 0; i < drawCount; i++)
            {
                AABB boundsInternal = boundsSpan[i];

                if (cullData.CullMode == 0 || IsVisibleAABB(boundsSpan[i], cullData))
                {

                    if (cullData.CullMode.HasFlag(CullModeFlags.Depth) && DepthProj(boundsSpan[i], cullData.View, cullData.ZNear, cullData.P00, cullData.P11, out var aabb, out float radius))
                    {
                        var center = Vector3.Transform(boundsInternal.Center, cullData.View);
                        float width = MathF.Abs((aabb.Z - aabb.X) * cullData.Pyramid.X);
                        float height = MathF.Abs((aabb.W - aabb.Y) * cullData.Pyramid.Y);
                        float level = Math.Min(9, MathF.Floor(MathF.Log2(Math.Max(width, height))));
                        Vector2 uv = (new Vector2(aabb.X, aabb.Y) + new Vector2(aabb.Z, aabb.W)) * 0.5f;
                        uv.X = 1.0f - uv.X;
                        _textureSampler.Default().SetTexture("depthPyramid".GetShaderPropertyId(), DepthReduction.DepthPryamid);
                        _textureSampler.PushConstants.SetPushConstantVector2("uv", 0, uv);
                        _textureSampler.PushConstants.SetPushConstantFloat("level", 0, level);
                        var computeCommandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();

                        _textureSampler.Dispatch(computeCommandBuffer, 0, 0, 1);
                        GraphicsDevice.EndSingleTimeMainPipe(computeCommandBuffer);
                        _textureResult.ReadToHostBuffer();  
                        var depthValue = 1-_textureResult.HostBuffer[0];
                        float depthSphere = MathF.Abs( cullData.ZNear / (center.Z - radius));

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
            int planeCount = cullData.CullMode.HasFlag(CullModeFlags.Distance) ? 6 : 4;
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


            DebugDrawer.DrawSphere(center, radius, Colour.Green);
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

            Vector2 cx = new(C.X, C.Z);
            Vector2 vx = new(MathF.Sqrt(Vector2.Dot(cx, cx) - r * r), r);
            Vector2 minx = new Mat2(vx.X, vx.Y, -vx.Y, vx.X) * cx;
            Vector2 maxx = new Mat2(vx.X, -vx.Y, vx.Y, vx.X) * cx;

            Vector2 cy = new(C.Y, C.Z);
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

    internal static class MeshTaskCull
    {
        private static readonly ComputePipeline _computeShader;

        private static uint _invocation = 0;

        static MeshTaskCull()
        {
            _computeShader = ComputePipeline.GetOrCreate("mesh_task_cull.comp");
            Presenter.Instance.PostPresentationUpdate += PostPresent;
        }

        private static void PostPresent()
        {
            Interlocked.Exchange(ref _invocation, 0);
        }

        public static void Cull(VkCommandBuffer commandBuffer, int frameIndex, CullData cullData,uint indirectCmdOffset, uint drawCount, SwapChainBuffer<MeshIndirectDraw> indirectBuffer, SwapChainBuffer bounds)
        {
            var variantIndex = Interlocked.Increment(ref _invocation) - 1;

            cullData.DrawCount = drawCount;
            
            var invokeVariant = _computeShader.GetOrCreateVariant(variantIndex);

            invokeVariant.SetUniform(FustrumCull.CullDataId, cullData);
            invokeVariant.SetStorageBuffer(FustrumCull.DrawBufferId, indirectBuffer, indirectCmdOffset, drawCount);
            invokeVariant.SetStorageBuffer("meshTaskCommands".GetShaderPropertyId(), DrawBlob._meshTaskCommands);
            invokeVariant.SetStorageBuffer("meshTaskCullData".GetShaderPropertyId(), DrawBlob._meshTaskCullLocalData);
            invokeVariant.SetStorageBuffer(ShaderProperties.BoundsBufferId, bounds);
            invokeVariant.Dispatch(commandBuffer, frameIndex, drawCount * _computeShader.LocalSize.X);
            VkBufferMemoryBarrier2 barrier = new()
            {
                buffer = indirectBuffer.ActiveVkBuffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.IndirectCommandRead,
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                dstStageMask = VkPipelineStageFlags2.DrawIndirect | VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.TaskShaderEXT | VkPipelineStageFlags2.MeshShaderEXT
            };

            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier);

            barrier = new()
            {
                buffer = DrawBlob._meshTaskCommands.ActiveVkBuffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.IndirectCommandRead,
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                dstStageMask = VkPipelineStageFlags2.DrawIndirect | VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.TaskShaderEXT | VkPipelineStageFlags2.MeshShaderEXT
            };
            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier);

            barrier = new()
            {
                buffer = DrawBlob._meshTaskCullLocalData.ActiveVkBuffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.ShaderRead,
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                dstStageMask = VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.TaskShaderEXT
            };
            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier);

        }
    }

}
