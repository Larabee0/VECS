namespace VECS
{
    public struct MaterialDrawCommand
    {
        public int Material;
        public int Variant;
        public BufferRegion StorageBufferRegion;
        public int Entity;
        public int DirectMesh;
        public BufferRegion MeshSubRegion;
        public bool Bloom;

        public int BufferStart => StorageBufferRegion.StartIndex;
        public int BufferCount => StorageBufferRegion.Count;

        public int MeshStart => MeshSubRegion.StartIndex;
        public int MeshCount => MeshSubRegion.Count;

        public MaterialDrawCommand(int material, int variant, BufferRegion storageBufferRegion, int entity, int directMesh, BufferRegion meshSubRegion, bool bloom)
        {
            Material = material;
            Variant = variant;
            StorageBufferRegion = storageBufferRegion;
            Entity = entity;
            DirectMesh = directMesh;
            MeshSubRegion = meshSubRegion;
            Bloom = bloom;
        }

        public MaterialDrawCommand(EarlyDrawCommand earlyDrawCommand,BufferRegion storageBufferRegion,BufferRegion meshSubRegion)
        {
            Material = earlyDrawCommand.MaterialIndex;
            Variant = earlyDrawCommand.MaterialVariant;
            Entity = earlyDrawCommand.MaterialEntity;
            DirectMesh = earlyDrawCommand.DirectMesh;

            StorageBufferRegion = storageBufferRegion;
            MeshSubRegion = meshSubRegion;
        }

        public static bool Equal(MaterialDrawCommand a, MaterialDrawCommand b)
        {
            return a.DirectMesh == b.DirectMesh &&
                a.Material == b.Material
                && a.Entity == b.Entity
                && a.MeshSubRegion.StartIndex == b.MeshSubRegion.StartIndex
                && a.MeshSubRegion.Count == b.MeshSubRegion.Count
                && a.Bloom == b.Bloom
                && a.StorageBufferRegion.StartIndex == b.StorageBufferRegion.StartIndex
                && a.StorageBufferRegion.Count == b.StorageBufferRegion.Count;
        }
    }
}
