using System.Runtime.InteropServices;
using VECS.ECS.Presentation;
using Vortice.Vulkan;
using VECS.ECS.Transforms;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential,Size = 181)]
    public struct DrawCommand
    {
        public VkDrawIndexedIndirectCommand VkDraw; // 20
        public ModelMatrices Matrices; // 128
        public ModelBounds Bounds; // 32
        public bool Bloom;

        public DrawCommand(VkDrawIndexedIndirectCommand vkDraw, ModelMatrices matrices, ModelBounds bounds)
        {
            VkDraw = vkDraw;
            Matrices = matrices;
            Bounds = bounds;
        }

        public DrawCommand(DirectSubMeshIndex subMeshIndex, LocalToWorld localToWorld, WorldRenderBounds worldRenderBounds)
        {
            VkDraw =  DirectSubMesh.GetSubMeshAtIndex(subMeshIndex).IndirectCommand;
            VkDraw.instanceCount = 0;
            Matrices = new(localToWorld.Value);
            Bounds = new(worldRenderBounds);
        }

        public DrawCommand(DirectSubMeshIndex subMeshIndex, LocalToWorld localToWorld, WorldRenderBounds worldRenderBounds, bool bloom)
        {
            VkDraw = DirectSubMesh.GetSubMeshAtIndex(subMeshIndex).IndirectCommand;
            VkDraw.instanceCount = 0;
            Matrices = new(localToWorld.Value);
            Bounds = new(worldRenderBounds);
            Bloom = bloom;
        }
    }
}