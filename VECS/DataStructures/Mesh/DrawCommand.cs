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
        public ShaderAABB Bounds; // 32
        public bool Bloom;

        public DrawCommand(DirectSubMeshIndex subMeshIndex, LocalToWorld localToWorld, WorldRenderBounds worldRenderBounds)
        {
            VkDraw =  DirectSubMesh.GetSubMeshAtIndex(subMeshIndex).IndirectCommand;
            VkDraw.instanceCount = 0;
            Matrices = new(localToWorld.Value);
            Bounds = worldRenderBounds.Value;
        }

        public DrawCommand(DirectSubMeshIndex subMeshIndex, LocalToWorld localToWorld, WorldRenderBounds worldRenderBounds, bool bloom)
        {
            VkDraw = DirectSubMesh.GetSubMeshAtIndex(subMeshIndex).IndirectCommand;
            VkDraw.instanceCount = 0;
            Matrices = new(localToWorld.Value);
            Bounds = worldRenderBounds.Value;
            Bloom = bloom;
        }
    }
}