using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class ComputeBounds
    {
        private const float QUANTIIZE_FACTOR = 32768.0f;

        private static readonly string ParamsHeightId = "height";
        private static readonly string ParamsBufferLengthId = "bufferLength";
        private static readonly string ParamsDepthId = "depth";
        private static readonly string ParamsWidthId = "width";
        private static readonly string ParamsSetIndexId = "setIndex";
        private static readonly string ParamsVertexOffsetId = "vertexOffset";

        private static readonly int VertexBufferId = "vertexBuffer".GetHashCode();
        private static readonly int MinMaxBufferId = "minMaxBuffer".GetHashCode();


        private static readonly ComputeShaderV2 _calculateBounds;

        private static readonly SwapChainBuffer<int> _minMaxBuffer;

        private class BoundsRecal
        {
            public DirectSubMesh SubMesh;
            public uint DescriptorIndex;
            public int SubmittedFrameIndex;

            public BoundsRecal(DirectSubMesh subMesh, uint descriptorIndex, int submittedFrameIndex)
            {
                SubMesh = subMesh;
                DescriptorIndex = descriptorIndex;
                SubmittedFrameIndex = submittedFrameIndex;
            }
        }
        private static readonly ConcurrentQueue<DirectMesh>[] _boundsRecalQueue = new ConcurrentQueue<DirectMesh>[SwapChain.MAX_CONCURRENT_FRAMES];

        private static readonly ConcurrentQueue<BoundsRecal> _boundsResultQueue = new();

        internal static uint _variant = 0;

        static ComputeBounds()
        {
            _calculateBounds = ComputeShaderV2.GetOrCreate("calculate_mesh_bounds.comp");

            _minMaxBuffer = new SwapChainBuffer<int>(6 * 2000, VkBufferUsageFlags.StorageBuffer, true);

            Presenter.Instance.PostPresentationUpdate += PostPresent;
            Presenter.Instance.PreGraphicsPipe += CheckResults;
            Application.Instance.OnDestroy += CleanUp;

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                _boundsRecalQueue[i] = new();
            }
        }

        private static void CheckResults(int frameIndex)
        {
            while (_boundsResultQueue.TryPeek(out var recal) && recal.SubmittedFrameIndex == frameIndex)
            {
                _boundsResultQueue.TryDequeue(out recal);
                var minMax = ReadElevationMinMax(frameIndex, (int)recal.DescriptorIndex);
                recal.SubMesh.SetBounds(minMax.Item1, minMax.Item2);
            }

            DispatchAll(SwapChain.CurrentMainCommandBuffer, frameIndex);
        }

        private static void CleanUp()
        {
            _minMaxBuffer.Dispose();
        }

        public static void PostPresent()
        {
            Interlocked.Exchange(ref _variant, 0);
        }

        public static void ResetMinMax(int setIndex)
        {
            Span<int> minMaxBuffer = _minMaxBuffer.HostBuffer;
            setIndex *= 6;
            minMaxBuffer[setIndex + 0] = int.MaxValue;
            minMaxBuffer[setIndex + 1] = int.MaxValue;
            minMaxBuffer[setIndex + 2] = int.MaxValue;
            minMaxBuffer[setIndex + 3] = int.MinValue;
            minMaxBuffer[setIndex + 4] = int.MinValue;
            minMaxBuffer[setIndex + 5] = int.MinValue;
        }

        public static (Vector3, Vector3) ReadElevationMinMax(int frameIndex, int setIndex)
        {
            _minMaxBuffer.ReadToHostFromBuffer(frameIndex);
            Span<int> minMaxBuffer = _minMaxBuffer.HostBuffer;
            setIndex *= 6;
            var min = new Vector3(minMaxBuffer[setIndex + 0] / QUANTIIZE_FACTOR, minMaxBuffer[setIndex + 1] / QUANTIIZE_FACTOR, minMaxBuffer[setIndex + 2] / QUANTIIZE_FACTOR);
            var max = new Vector3(minMaxBuffer[setIndex + 3] / QUANTIIZE_FACTOR, minMaxBuffer[setIndex + 4] / QUANTIIZE_FACTOR, minMaxBuffer[setIndex + 5] / QUANTIIZE_FACTOR);

            return (min, max);
        }

        public static void DispatchAll(DirectMesh mesh)
        {
            _boundsRecalQueue[Presenter.Instance.FrameIndex].Enqueue(mesh);
        }

        private static void DispatchAllNextFrame(DirectMesh mesh)
        {
            _boundsRecalQueue[Presenter.Instance.NextFrameIndex].Enqueue(mesh);
        }

        public static void DispatchAll(VkCommandBuffer commandBuffer, int frameIndex)
        {
            while (_boundsRecalQueue[frameIndex].TryDequeue(out var mesh))
            {
                if (mesh.IsDisposed) continue;
                DispatchAll(commandBuffer, frameIndex,mesh);
            }
        }

        public unsafe static void DispatchAll(VkCommandBuffer commandBuffer, int frameIndex, DirectMesh mesh)
        {
            if (_variant > 2000)
            {
                // previously exceeded max invokcations this frame, kick it to next frame
                DispatchAllNextFrame(mesh);
                return;
            }

            var firstDescriptor = Interlocked.Add(ref _variant, (uint)mesh.DirectSubMeshes.Length) - (uint)mesh.DirectSubMeshes.Length;

            if(firstDescriptor + (uint)mesh.DirectSubMeshes.Length > 2000)
            {
                // execution would exceeded max invokcations this frame, kick it to next frame
                DispatchAllNextFrame(mesh);
                return;
            }

            var vertexPositionBuffer = mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Position);
            VkBufferMemoryBarrier2 barrier = new(_minMaxBuffer.ActiveVkBuffer, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite);
            for (uint i = 0; i < mesh.DirectSubMeshes.Length; i++)
            {
                var subMesh = mesh.DirectSubMeshes[i];
                Vector2UInt workGroupXY = ComputeShaderV2.CompensateForWorkGroupLimits(subMesh.VertexCount);

                _calculateBounds.SetStorageBuffer(VertexBufferId, firstDescriptor+i, vertexPositionBuffer);
                Prepare(firstDescriptor + i, subMesh);
                _calculateBounds.Dispatch(commandBuffer, frameIndex, firstDescriptor + i, workGroupXY.X, workGroupXY.Y, 1);
                
                MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier);
                _boundsResultQueue.Enqueue(new(subMesh, firstDescriptor + i, frameIndex));
            }
            _minMaxBuffer.WriteFromHostToActiveBuffer();
            barrier = new(_minMaxBuffer.ActiveVkBuffer, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Host, VkAccessFlags2.HostRead);
            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier);
        }

        private static unsafe void Prepare(uint setId,DirectSubMesh subMesh)
        {
            ResetMinMax((int)setId);
            uint componsatedBufferLength = subMesh.VertexCount;
            uint divider = (uint)(int)MathF.Ceiling((float)componsatedBufferLength / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(componsatedBufferLength, GraphicsDevice.MaxWorkGroupX);

            _calculateBounds.PushConstantsHandler.SetPushConstantUInt(ParamsBufferLengthId, (int)setId, subMesh.VertexCount);
            _calculateBounds.PushConstantsHandler.SetPushConstantUInt(ParamsDepthId, (int)setId, 1);
            if (divider == 1)
            {
                _calculateBounds.PushConstantsHandler.SetPushConstantUInt(ParamsWidthId, (int)setId, componsatedBufferLength);
                _calculateBounds.PushConstantsHandler.SetPushConstantUInt(ParamsHeightId, (int)setId, 1);
            }
            else
            {
                _calculateBounds.PushConstantsHandler.SetPushConstantUInt(ParamsWidthId, (int)setId, workGroupX);
                _calculateBounds.PushConstantsHandler.SetPushConstantUInt(ParamsHeightId, (int)setId, divider);
            }

            _calculateBounds.PushConstantsHandler.SetPushConstantUInt(ParamsSetIndexId, (int)setId, (uint)(setId * 6));
            _calculateBounds.PushConstantsHandler.SetPushConstantUInt(ParamsVertexOffsetId, (int)setId, (uint)subMesh.IndirectCommand.vertexOffset);

            _calculateBounds.SetStorageBuffer(MinMaxBufferId, setId, _minMaxBuffer);
        }
    }
}
