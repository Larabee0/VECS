using System;
using System.Numerics;
using VECS.ECS.Presentation;

namespace VECS
{
    public struct EarlyDrawCommand : IComparable
    {
        public int DirectMesh;
        public int SubMesh;
        public int MaterialIndex;
        public int MaterialVariant;
        public int MaterialEntity;
        public DrawCommand DrawCommand;
        public Vector4 Colour;
        public bool Bloom;

        public EarlyDrawCommand(DrawCommand drawCommand,RenderMesh renderMesh)
        {
            DrawCommand = drawCommand;
            DirectMesh = renderMesh.Mesh.DirectMesh;
            SubMesh = renderMesh.Mesh.SubMeshIndex;
            MaterialIndex = renderMesh.Material.Material;
            MaterialVariant = renderMesh.Material.Variant;
            MaterialEntity = renderMesh.Material.Entity;
            Colour = renderMesh.Colour;
        }

        public static bool MateriallyDifferent(EarlyDrawCommand a, EarlyDrawCommand b)
        {
            return a.DirectMesh != b.DirectMesh || a.MaterialIndex != b.MaterialIndex || a.MaterialVariant != b.MaterialVariant || a.MaterialEntity != b.MaterialEntity;
        }

        public readonly int CompareTo(object obj)
        {
            if(obj is EarlyDrawCommand b)
            {
                var material = MaterialIndex.CompareTo(b.MaterialIndex);
                if(material != 0) return material;
                var variant = MaterialVariant.CompareTo(b.MaterialVariant);
                if (variant != 0) return variant;
                var entity = MaterialEntity.CompareTo(b.MaterialEntity);
                if (entity != 0) return entity;
                var directMesh = DirectMesh.CompareTo(b.DirectMesh);
                if(directMesh != 0) return directMesh;
                var subMesh = SubMesh.CompareTo(b.SubMesh);
                return subMesh;
            }

            throw new ArgumentException(string.Format("Object is not a {0}", typeof(EarlyDrawCommand)));
        }
    }
}
