using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct RenderMesh : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public DirectSubMeshIndex Mesh;
        public MaterialIndex Material;
        public Vector4 Colour;
        public CullOverrides CullOverrides;
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
