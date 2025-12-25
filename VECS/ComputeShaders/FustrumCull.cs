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
    [StructLayout(LayoutKind.Sequential, Size = 132)]
    public struct CullData
    {
        public Vector4 left;
        public Vector4 right;
        public Vector4 bottom;
        public Vector4 top;
        public Vector4 near;
        public Vector4 far;
        public float zNear;
        public float P00;
        public float P11;
        public Vector2 pyramid;
        public uint drawCount;
        public int cullingEnabled;
        public int dstCulling;
        public int depthCulling;

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

        public CullData(bool cull, bool dstCull,bool depthCull, float zNear, Matrix4x4 projection)
        {
            Matrix4x4 projectionT = Matrix4x4.Transpose(projection);
            near = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(2);
            far = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(2);

            right = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(0);
            left = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0);

            top = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1);
            bottom = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(1);
            this.zNear = zNear;
            P00 = projection[0, 0];
            P11 = projection[1, 1];

            cullingEnabled = cull ? 1 : 0;
            dstCulling = dstCull ? 1 : 0;
            depthCulling = depthCull ? 1 : 0;
            drawCount = 0;
        }

        public CullData(int cull, int dstCull, int depthCull, Matrix4x4 projection)
        {
            Matrix4x4 projectionT = Matrix4x4.Transpose(projection);
            near = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(2);
            far = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(2);

            right = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(0);
            left = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0);

            top = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1);
            bottom = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(1);

            cullingEnabled = cull;
            dstCulling = dstCull;
            depthCulling = depthCull;
            drawCount = 0;
        }

        public CullData(bool cull, bool dstCull, bool depthCull, uint draws, Matrix4x4 projection)
        {
            Matrix4x4 projectionT = Matrix4x4.Transpose(projection);
            near = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(2);
            far = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(2);

            right = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(0);
            left = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0);

            top = projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1);
            bottom = projectionT.GetMatrixRow(3) - projectionT.GetMatrixRow(1);

            cullingEnabled = cull ? 1 : 0;
            dstCulling = dstCull ? 1 : 0;
            depthCulling = depthCull ? 1 : 0;
            drawCount = draws;
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


        private static readonly ComputeShader _computeShader;

        private static uint _variant = 0;

        public static ComputeShader Shader => _computeShader;

        static FustrumCull()
        {
            _computeShader = ComputeShader.GetOrCreate("fustrum_cull.comp");
            Presenter.Instance.PostPresentationUpdate += PostPresent;
        }

        public static void PostPresent()
        {
            Interlocked.Exchange(ref _variant, 0);
        }

        public static void Cull(VkCommandBuffer commandBuffer,int frameIndex, CullData cullData, uint drawCount, SwapChainBuffer<VkDrawIndexedIndirectCommand> drawIndirect, SwapChainBuffer<ShaderAABB> bounds)
        {
            if (_variant > 2000)
            {
                Console.WriteLine("Fustrum Cull Compute Shader invokations exceeded default max uniform count of {0}", Material.MAX_VARIANTS);
            }

            var discriptorIndex = Interlocked.Increment(ref _variant) - 1;
#if DEBUG
#pragma warning disable CS0162
            if (CPUCulling)
            {
                CPUCull(cullData, drawCount, drawIndirect, bounds);
                return;
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
            cullData.SetPushConstant(_computeShader.PushConstantsHandler, (int)setId);
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
        private static void CPUCull(CullData cullData, uint drawCount, SwapChainBuffer<VkDrawIndexedIndirectCommand> drawIndirect, SwapChainBuffer<ShaderAABB> bounds)
        {
            Span<VkDrawIndexedIndirectCommand> drawIndirectSpan = drawIndirect.HostBuffer;
            Span<ShaderAABB> boundsSpan = bounds.HostBuffer;


            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawWireCube(cullData.near.AsVector3(), Vector3.One, Quaternion.Identity, Colour.Blue);
            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawWireCube(cullData.far.AsVector3(), Vector3.One, Quaternion.Identity, Colour.White);

            for (int i = 0; i < drawCount; i++)
            {
                AABB boundsInternal = boundsSpan[i];

                if (cullData.cullingEnabled == 0 || IsVisibleAABB(boundsSpan[i], cullData))
                {
                    drawIndirectSpan[i].instanceCount = 1;
                    World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawWireCube(boundsInternal.Center, boundsInternal.Size, Quaternion.Identity, Colour.Green);
                }
                else
                {
                    drawIndirectSpan[i].instanceCount = 0;
                    World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawWireCube(boundsInternal.Center, boundsInternal.Size, Quaternion.Identity, Colour.Red);
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
#endif        
    }
}
