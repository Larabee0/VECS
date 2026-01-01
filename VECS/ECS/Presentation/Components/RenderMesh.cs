using System;
using System.Numerics;

namespace VECS.ECS.Presentation
{
    public readonly struct MainColourRenderBuffer : IRenderBuffer
    {
        public readonly static Type BufferElementType = typeof(Vector4);
        public readonly static int ColourShaderPropertyId = "colourBuffer".GetShaderPropertyId();
        public unsafe readonly static uint BufferElementSize = (uint)sizeof(Vector4);
        public readonly Type ElementType => BufferElementType;

        public readonly uint ElementSize => BufferElementSize;

        public readonly int BufferShaderPropertyId => ColourShaderPropertyId;

        public readonly int ComponentId => MainColour.ComponentId;

        public readonly unsafe void CopyIn(void* ptr, IComponent component)
        {
            var cast = (MainColour)component;
            ((Vector4*)ptr)[0] = cast.Value;
        }

        public readonly unsafe void DefaultIn(void* ptr)
        {
            ((Vector4*)ptr)[0] = Vector4.One;
        }
    }

    public struct MainColour : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector4 Value;
    }

    public struct RenderMesh : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public DirectSubMeshIndex Mesh;
        public MaterialIndex Material;
        public CullOverrides CullOverrides;
        public RenderLayer LayerFlags;

        public RenderMesh()
        {
            LayerFlags = RenderLayer.Default;
        }


#if DEBUG
        public readonly int MeshHash => Mesh.Hash;
        public readonly int SubMesh => Mesh.SubMesh;
        public readonly int MatHash => Material.Hash;
        public readonly int MatVar => Material.Variant;
        public readonly int MatEntity => Material.Entity;
        public readonly DirectMesh DEBUG_DirectMesh => AssetDataBase<DirectMesh>.GetHashedSilentFail(Mesh.Hash);
        public readonly DirectSubMesh DEBUG_DirectSubMesh => DirectSubMesh.GetSubMeshAtIndex(Mesh);
        public readonly Material DEBUG_Mat => AssetDataBase<Material>.GetHashedSilentFail(MatHash);
#endif
        public static bool ShouldMakeNewDrawCmd(RenderMesh a, RenderMesh b)
        {
            return a.Mesh.Hash != b.Mesh.Hash || a.Material.Hash != b.Material.Hash || a.Material.Variant != b.Material.Variant || a.Material.Entity != b.Material.Entity;
        }
    }
}
