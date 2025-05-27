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
    }
}
