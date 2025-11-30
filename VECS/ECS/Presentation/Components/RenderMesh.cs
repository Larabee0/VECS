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

        public int MeshHash => Mesh.Hash;
        public int SubMesh => Mesh.SubMesh;
        public int MatHash => Material.Hash;
        public int MatVar => Material.Variant;
        public int MatEntity => Material.Entity;
    }
}
