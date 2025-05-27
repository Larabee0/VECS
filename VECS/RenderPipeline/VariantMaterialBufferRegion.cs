using System;
using System.Collections.Generic;

namespace VECS
{
    public struct VariantMaterialBufferRegion
    {
        public BufferRegion MeshSubRegion;
        public int Material;
        public int Variant;
        public int Entity;
        public int DirectMesh;

        public VariantMaterialBufferRegion(BufferRegion region,int material, int variant, int entity)
        {
            MeshSubRegion = region;
            Material = material;
            Variant = variant;
            Entity = entity;
        }

        public VariantMaterialBufferRegion(BufferRegion region, int material, int variant, int entity, int directMesh)
        {
            MeshSubRegion = region;
            Material = material;
            Variant = variant;
            Entity = entity;
            DirectMesh = directMesh;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is VariantMaterialBufferRegion command &&
                   EqualityComparer<BufferRegion>.Default.Equals(MeshSubRegion, command.MeshSubRegion) &&
                   Material == command.Material &&
                   Variant == command.Variant &&
                   Entity == command.Entity &&
                   DirectMesh == command.DirectMesh;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(MeshSubRegion, Material, Variant, Entity,DirectMesh);
        }
        public static bool operator ==(VariantMaterialBufferRegion left, VariantMaterialBufferRegion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VariantMaterialBufferRegion left, VariantMaterialBufferRegion right)
        {
            return !(left == right);
        }
    }
}
