namespace VECS
{
    public struct MaterialDrawCommand
    {
        public int Material;
        public int Variant;
        public int Entity;
        public int DirectMesh;
        public BufferRegion MeshSubRegion;

        public readonly int MeshStart => MeshSubRegion.StartIndex;
        public readonly int MeshCount => MeshSubRegion.Count;

        public MaterialDrawCommand(int material, int variant, int entity, int directMesh, BufferRegion meshSubRegion)
        {
            Material = material;
            Variant = variant;
            Entity = entity;
            DirectMesh = directMesh;
            MeshSubRegion = meshSubRegion;
        }

        public static bool Equal(MaterialDrawCommand a, MaterialDrawCommand b)
        {
            return a.DirectMesh == b.DirectMesh &&
                a.Material == b.Material
                && a.Entity == b.Entity
                && a.MeshSubRegion.StartIndex == b.MeshSubRegion.StartIndex
                && a.MeshSubRegion.Count == b.MeshSubRegion.Count;
        }
    }
}
