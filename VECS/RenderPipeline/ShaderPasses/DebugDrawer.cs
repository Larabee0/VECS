using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class DebugDrawer
    {
        public const int MAX_LINES = 1000;
        private static readonly int ColourBufferId = "colourBuffer".GetShaderPropertyId();
        private static readonly int MatricesBufferId = "matricesBuffer".GetShaderPropertyId();
        
        private static readonly Vector2 _min = new(-1, -1);
        private static readonly Vector2 _max = new(1, 1);
        private static readonly Vector4[] _fustrumVerts = new Vector4[16];
        private static GPUBuffer<Vector3> _circleBuffer;
        private static SwapChainBuffer<Vector3> _frustrumBuffer;
        private static GPUBuffer<Vector3> _cubeBuffer;
        private static SwapChainBuffer<Matrix3x2> _lineBuffer;
        private static SwapChainBuffer<ModelMatrices> _matrices;
        private static SwapChainBuffer<Vector4> _colours;
                
        private static SwapChainBuffer<VkDrawIndirectCommand> _drawBuffer;
                
        private static readonly Queue<Line> _lineQueue = new();
                
        private static readonly Queue<DrawCube> _wireCubes = new();
                
        private static readonly Queue<Sphere> _wireSpheres = new();
        private static readonly Queue<Fustrum> _fustrums = new();

        private static int _drawIndex;
        private static int _drawBufferIndex;

        internal static void Reset()
        {
            CleanUp();

            _circleBuffer = new(32, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true, false, false);
            
            _frustrumBuffer = new(16 * 1000, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true, false);
            _cubeBuffer = new(16, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true, false, true);
            _lineBuffer = new(MAX_LINES, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, true, false);
            _drawBuffer = new(Pipeline.MAX_VARIANTS, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.IndirectBuffer, true);
            _matrices = new(32, VkBufferUsageFlags.StorageBuffer, true);
            _colours = new(32, VkBufferUsageFlags.StorageBuffer, true);

            _circleBuffer.SetDebugName("DebugDrawer_WireCircle_mesh");
            _cubeBuffer.SetDebugName("DebugDrawer_WireCube_Mesh");
            _lineBuffer.SetDebugName("DebugDrawer_Line_Mesh");
            _drawBuffer.SetDebugName("DebugDrawer_DrawCmds");
            _matrices.SetDebugName("DebugDrawer_Transforms");
            _colours.SetDebugName("DebugDrawer_Colours");

            _lineBuffer.HostBuffer.Fill(new Matrix3x2(0, 0, 0, 0, 0, 0));
            CreateWireCube();
            CreateWireCircle();
            EnginePipes.WireFrame.SetStorageBuffer(ShaderProperties.MatricesBufferId, _matrices);
            EnginePipes.WireFrame.SetStorageBuffer(ShaderProperties.ColourBufferId, _colours);
        }

        internal static void AddToRenderGraph()
        {
            RenderGraph.AddPass("DebugLines", PassType.ColourDepthStencil, ["OpaqueOutput"], ["MainColourAttachment", "BrightObjectAttachment", "LinesOutput"],LinePass);
            RenderGraph.AddPass("DebugWireCubes", PassType.ColourDepthStencil, ["LinesOutput"], ["MainColourAttachment", "BrightObjectAttachment", "WireCubeOutput"], WireCubesPass);
            RenderGraph.AddPass("DebugWireSpheres", PassType.ColourDepthStencil, ["WireCubeOutput"], ["MainColourAttachment", "BrightObjectAttachment", "WireSphereOutput"], WireSpheresPass);
            RenderGraph.AddPass("DebugFustrums", PassType.ColourDepthStencil, ["WireSphereOutput"], ["MainColourAttachment", "BrightObjectAttachment", "OpaqueOutput"], FustrumPass);
        }

        internal static void CleanUp()
        {
            _circleBuffer?.EnqueueForDisposal();
            _frustrumBuffer?.Dispose();
            _cubeBuffer?.EnqueueForDisposal();
            _lineBuffer?.Dispose();

            _matrices?.Dispose();
            _colours?.Dispose();

            _drawBuffer?.Dispose();
        }

        internal static void PrePresent()
        {
            if ((_lineQueue.Count > 0)
                || (_wireCubes.Count > 0)
                || (_wireSpheres.Count > 0)
                || (_fustrums.Count > 0))
            {
                var drawCount = _wireCubes.Count + _wireSpheres.Count + _lineQueue.Count;

                if (_matrices.InstanceCount32 < drawCount)
                {
                    _matrices.Realloc((uint)drawCount);
                    _colours.Realloc((uint)drawCount);
                }
            }
        _drawIndex = 0;
        _drawBufferIndex = 0;
        SetandWriteBuffers();
        }

        private static void LinePass(RendererFrameInfo frameInfo)
        {
            if (_lineQueue.Count == 0) return;


            var matrices = _matrices.HostBuffer;
            var colours = _colours.HostBuffer;
            var draws = _drawBuffer.HostBuffer;
            var lineBuffer = _lineBuffer.HostBuffer;
            while (_lineQueue.TryDequeue(out var line))
            {
                lineBuffer[_drawIndex] = line.Vertices;
                matrices[_drawIndex] = Matrix4x4.Identity;
                colours[_drawIndex] = line.Colour.ToColour();

                draws[_drawBufferIndex] = new()
                {
                    firstVertex = (uint)_drawBufferIndex * 2,
                    firstInstance = (uint)_drawBufferIndex,
                    vertexCount = 2,
                    instanceCount = 1
                };
                _drawIndex++;
                _drawBufferIndex++;
            }
            GPUBufferExtensions.WriteFromHostDelayed(_lineBuffer, Presenter.FrameIndex);
            Presenter.Instance.Renderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _lineBuffer.ActiveVkBuffer);
            DrawIndirect(frameInfo, 0, _drawBufferIndex);
            Presenter.Instance.Renderer.EndForwardRendering(frameInfo);
        }

        private static void WireCubesPass(RendererFrameInfo frameInfo)
        {
            if (_wireCubes.Count == 0) return;

            var matrices = _matrices.HostBuffer;
            var colours = _colours.HostBuffer;
            var draws = _drawBuffer.HostBuffer;
            int drawOffset = _drawIndex;
            draws[_drawBufferIndex] = new()
            {
                firstVertex = 0,
                firstInstance = (uint)_drawBufferIndex,
                vertexCount = 16,
                instanceCount = (uint)_wireCubes.Count
            };

            while (_wireCubes.TryDequeue(out var aabb))
            {
                matrices[_drawIndex] = TransformExtensions.TRS(aabb.Center, aabb.Orientation, aabb.Size);
                colours[_drawIndex] = aabb.Colour.ToColour();
                _drawIndex++;
            }
            _drawBufferIndex++;
            Presenter.Instance.Renderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _cubeBuffer.VkBuffer);
            DrawIndirect(frameInfo, drawOffset, 1);
            Presenter.Instance.Renderer.EndForwardRendering(frameInfo);
        }

        private static void WireSpheresPass(RendererFrameInfo frameInfo)
        {
            if (_wireSpheres.Count == 0) return;

            var matrices = _matrices.HostBuffer;
            var colours = _colours.HostBuffer;
            var draws = _drawBuffer.HostBuffer;
            int offset = _drawBufferIndex;
            draws[_drawBufferIndex] = new()
            {
                vertexCount = 32,
                firstVertex = 0,
                firstInstance = (uint)_drawIndex,
                instanceCount = (uint)_wireSpheres.Count * 4
            };

            while (_wireSpheres.TryDequeue(out var sphere))
            {
                var center = sphere.Bounds.AsVector3();
                var radius = new Vector3(sphere.Bounds.W <= 0 ? 1 : sphere.Bounds.W);
                var a = TransformExtensions.TRS(center, new Vector3(), radius);
                var b = TransformExtensions.TRS(center, new Vector3(TransformExtensions.Deg2Rad * 90f, 0, 0), radius);
                var c = TransformExtensions.TRS(center, new Vector3(0, TransformExtensions.Deg2Rad * 90f, 0), radius);
                var d = TransformExtensions.TRS(center, new Vector3(0, 0, TransformExtensions.Deg2Rad * 90f), radius);

                matrices[_drawIndex] = a;
                matrices[_drawIndex + 1] = b;
                matrices[_drawIndex + 2] = c;
                matrices[_drawIndex + 3] = d;
                colours[_drawIndex] = sphere.Colour.ToColour();
                colours[_drawIndex + 1] = sphere.Colour.ToColour();
                colours[_drawIndex + 2] = sphere.Colour.ToColour();
                colours[_drawIndex + 3] = sphere.Colour.ToColour();

                _drawIndex += 4;
            }
            _drawBufferIndex++;
            Presenter.Instance.Renderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);

            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _circleBuffer.VkBuffer);
            DrawIndirect(frameInfo, offset, 1);
            Presenter.Instance.Renderer.EndForwardRendering(frameInfo);
        }

        private static void FustrumPass(RendererFrameInfo frameInfo)
        {
            if (_fustrums.Count == 0) return;
            Matrix4x4 view = CameraSystem.GetViewMatrix(Matrix4x4.Identity);
            Matrix4x4 projection;
            int vertexOffset = 0;
            var matrices = _matrices.HostBuffer;
            var colours = _colours.HostBuffer;
            var draws = _drawBuffer.HostBuffer;
            var drawCount = _fustrums.Count;
            var indirectStartIndex = _drawBufferIndex;
            while (_fustrums.TryDequeue(out var fustrum))
            {
                Matrix4x4.Invert(view * fustrum.Projection, out projection);

                // scale = 1
                _fustrumVerts[vertexOffset + 0] = Vector4.Transform(new Vector4(_min, 1, 1), projection);
                _fustrumVerts[vertexOffset + 1] = Vector4.Transform(new Vector4(_min.X, _max.Y, 1, 1), projection);
                _fustrumVerts[vertexOffset + 2] = Vector4.Transform(new Vector4(_max, 1, 1), projection);
                _fustrumVerts[vertexOffset + 3] = Vector4.Transform(new Vector4(_max.X, _min.Y, 1, 1), projection);
                _fustrumVerts[vertexOffset + 4] = Vector4.Transform(new Vector4(_min, 1, 1), projection);

                _fustrumVerts[vertexOffset + 11] = Vector4.Transform(new Vector4(_min.X, _max.Y, 1, 1), projection);
                _fustrumVerts[vertexOffset + 12] = Vector4.Transform(new Vector4(_max, 1, 1), projection);

                _fustrumVerts[vertexOffset + 15] = Vector4.Transform(new Vector4(_max.X, _min.Y, 1, 1), projection);
                // scale = -1
                _fustrumVerts[vertexOffset + 5] = Vector4.Transform(new Vector4(_min, -1, 1), projection);
                _fustrumVerts[vertexOffset + 6] = Vector4.Transform(new Vector4(_min.X, _max.Y, -1, 1), projection);
                _fustrumVerts[vertexOffset + 7] = Vector4.Transform(new Vector4(_max, -1, 1), projection);
                _fustrumVerts[vertexOffset + 8] = Vector4.Transform(new Vector4(_max.X, _min.Y, -1, 1), projection);
                _fustrumVerts[vertexOffset + 9] = Vector4.Transform(new Vector4(_min.X, _min.Y, -1, 1), projection);
                _fustrumVerts[vertexOffset + 10] = Vector4.Transform(new Vector4(_min.X, _max.Y, -1, 1), projection);
                _fustrumVerts[vertexOffset + 13] = Vector4.Transform(new Vector4(_max, -1, 1), projection);
                _fustrumVerts[vertexOffset + 14] = Vector4.Transform(new Vector4(_max.X, _min.Y, -1, 1), projection);
                var buffer = _frustrumBuffer.HostBuffer;
                for (int j = 0; j < 16; j++)
                {
                    Vector4 vertex = _fustrumVerts[vertexOffset + j];
                    vertex.X /= vertex.W;
                    vertex.Y /= vertex.W;
                    vertex.Z /= vertex.W;
                    vertex.W = 1.0f;
                    buffer[vertexOffset + j] = vertex.AsVector3();
                    _fustrumVerts[vertexOffset + j] = vertex;
                }

                matrices[_drawIndex] = fustrum.LTW;
                colours[_drawIndex] = fustrum.Colour.ToColour();

                draws[_drawBufferIndex] = new()
                {
                    firstInstance = (uint)_drawIndex,
                    firstVertex = (uint)vertexOffset,
                    instanceCount = 1,
                    vertexCount = 16
                };
                _drawIndex++;
                _drawBufferIndex++;
                vertexOffset += 16;
            }
            GPUBufferExtensions.WriteFromHostDelayed(_frustrumBuffer, Presenter.FrameIndex);

            Presenter.Instance.Renderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);

            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _frustrumBuffer.ActiveVkBuffer);
            DrawIndirect(frameInfo, indirectStartIndex, drawCount);
            Presenter.Instance.Renderer.EndForwardRendering(frameInfo);
        }


        private static void SetandWriteBuffers()
        {
            if (_lineQueue.Count == 0 && _wireCubes.Count == 0 && _wireSpheres.Count == 0 && _fustrums.Count == 0) return;
            
            var drawCount = _wireCubes.Count + _wireSpheres.Count + _lineQueue.Count + _fustrums.Count;

            EnginePipes.WireFrame.SetDescriptorStorageBufferLengthFromProperty(ShaderProperties.MatricesBufferId, (uint)drawCount);
            EnginePipes.WireFrame.SetDescriptorStorageBufferLengthFromProperty(ShaderProperties.ColourBufferId, (uint)drawCount);

            EnginePipes.WireFrame.GetStorageSwapChainBuffer(MatricesBufferId).SetBuffersDirty(true);
            EnginePipes.WireFrame.GetStorageSwapChainBuffer(ColourBufferId).SetBuffersDirty(true);

            GPUBufferExtensions.WriteFromHostDelayed(_drawBuffer, Presenter.FrameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(_matrices, Presenter.FrameIndex);
            GPUBufferExtensions.WriteFromHostDelayed(_colours, Presenter.FrameIndex);
        }

        private unsafe static void DrawIndirect(RendererFrameInfo frameInfo, int offset, int count)
        {
            EnginePipes.WireFrame.BindAll(frameInfo, 0);
            GraphicsDevice.DeviceAPI.vkCmdDrawIndirect(frameInfo.CommandBuffer, _drawBuffer.ActiveVkBuffer, (uint)offset * (uint)sizeof(VkDrawIndirectCommand), (uint)count, (uint)sizeof(VkDrawIndirectCommand));
        }

        private static void CreateWireCube()
        {
            Vector3 min = new(-0.5f, -0.5f, -0.5f);
            Vector3 max = new(0.5f, 0.5f, 0.5f);
            var verts = _cubeBuffer.HostBuffer;
            verts[0] = min;
            verts[1] = new Vector3(min.X, max.Y, min.Z);
            verts[2] = new Vector3(max.X, max.Y, min.Z);
            verts[3] = new Vector3(max.X, min.Y, min.Z);
            verts[4] = min;

            verts[5] = new Vector3(min.X, min.Y, max.Z);
            verts[6] = new Vector3(min.X, max.Y, max.Z);
            verts[7] = max;
            verts[8] = new Vector3(max.X, min.Y, max.Z);
            verts[9] = new Vector3(min.X, min.Y, max.Z);
            verts[10] = new Vector3(min.X, max.Y, max.Z);

            verts[11] = new Vector3(min.X, max.Y, min.Z);
            verts[12] = new Vector3(max.X, max.Y, min.Z);

            verts[13] = max;
            verts[14] = new Vector3(max.X, min.Y, max.Z);

            verts[15] = new Vector3(max.X, min.Y, min.Z);

            _cubeBuffer.WriteFromHostBuffer();
        }
        private static void CreateWireCircle()
        {
            var vertices = _circleBuffer.HostBuffer;
            float radians = 0;
            float radPerStep = (TransformExtensions.Deg2Rad * 360f) / 31f;

            for (int i = 0; i < vertices.Length - 1; i++)
            {
                Vector3 dir = new(MathF.Sin(radians), -MathF.Cos(radians), 0);

                vertices[i] = Vector3.Zero + dir * 1f;

                radians += radPerStep;
            }
            vertices[^1] = (Vector3.Zero + new Vector3(MathF.Sin(0), -MathF.Cos(0), 0)) * 1f;

            _circleBuffer.WriteFromHostBuffer();
        }
        public static void DrawLine(Vector3 start, Vector3 end)
        {
            DrawLine(start, end, Colour.White);
        }

        public static void DrawLine(Vector3 start, Vector3 end, Colour colour)
        {
            _lineQueue.Enqueue(new Line(start, end, colour));
        }

        public static void DrawSphere(Vector3 center, float radius)
        {
            DrawSphere(center, radius, Colour.White);
        }

        public static void DrawSphere(Vector3 center, float radius, Colour colour)
        {
            _wireSpheres.Enqueue(new(new(center, radius), colour));
        }

        public static void DrawWireCube(Vector3 center, Vector3 size, Quaternion orientation)
        {
            DrawWireCube(center, size, orientation, Colour.White);
        }

        public static void DrawWireCube(Vector3 center, Vector3 size, Quaternion orientation, Colour colour)
        {
            _wireCubes.Enqueue(new DrawCube(center, size, orientation, colour));
        }

        public static void DrawFustrum(Matrix4x4 projection, Matrix4x4 ltw, Colour colour)
        {
            _fustrums.Enqueue(new(projection,ltw, colour));
        }

        private readonly struct DrawCube
        {
            public readonly Vector3 Center;
            public readonly Vector3 Size;
            public readonly Quaternion Orientation;
            public readonly Colour Colour;

            public DrawCube(Vector3 center, Vector3 size, Quaternion orientation, Colour colour)
            {
                Center = center;
                Size = size;
                Orientation = orientation;
                Colour = colour;
            }
        }

        private readonly struct Line
        {
            public readonly Vector3 Start;
            public readonly Vector3 End;
            public readonly Colour Colour;
            public readonly Matrix3x2 Vertices => new(Start.X, Start.Y, Start.Z, End.X, End.Y, End.Z);

            public Line(Vector3 start, Vector3 end, Colour colour)
            {
                Start = start;
                End = end;
                Colour = colour;
            }
        }

        private readonly struct Sphere
        {
            public readonly Vector4 Bounds;
            public readonly Colour Colour;

            public Sphere(Vector4 bounds, Colour colour)
            {
                Bounds = bounds;
                Colour = colour;
            }
        }

        private readonly struct Fustrum
        {
            public readonly Matrix4x4 Projection;
            public readonly Matrix4x4 LTW;
            public readonly Colour Colour;

            public Fustrum(Matrix4x4 projection, Matrix4x4 ltw, Colour colour)
            {
                Projection = projection;
                LTW = ltw;
                Colour = colour;
            }
        }

    }
}
