using System;
using System.Diagnostics;
using System.Numerics;
using VECS.ECS.Presentation;

namespace VECS
{
    /// <summary>
    /// Address Property
    ///  mat   var   ent   drm   sbm
    /// 0 000|0 000|0 000|0 000|0 000
    /// 
    /// ulong draw addresses are a ulong divided up funnily (1844 6744 0737 0955 1615)
    /// 
    /// Each property is given 4 digits for cmds.
    /// The max number for each 9999, apart from mat which is capped at <see cref="MAX_MATERIAL_COUNT"/>
    /// 
    /// Theorically this supports 1843 * 9999 * 9999 * 9999 * 9999 (1.84x10^19) Unique combinations of draw command.
    /// If this limit is hit as a hard limit then like wow ok, this would need updating to uint128 I guess.
    /// If 1843 materials is not enough then you need to rexamine your render stack, cos thats a lot of pipeline binds
    /// (or move to uint128)
    /// 
    /// </summary>
    public struct EarlyDrawCommand : IComparable
    {
        public const int MAX_MATERIAL_COUNT = 1843;

        private static readonly int UnknownDrawCmd = -1;
        public int DirectMesh;
        public int SubMesh;
        public int MaterialIndex;
        public int MaterialVariant;
        public int MaterialEntity;
        public DrawCommand DrawCommand;
        public Vector4 Colour;
        public bool Bloom;

        public readonly ulong DrawAddress;
        private readonly int _cachedHashCode;

        public EarlyDrawCommand(DrawCommand drawCommand,RenderMesh renderMesh)
        {
            DrawCommand = drawCommand;
            DirectMesh = renderMesh.Mesh.DirectMesh;
            SubMesh = renderMesh.Mesh.SubMeshIndex;
            MaterialIndex = renderMesh.Material.Material;
            MaterialVariant = renderMesh.Material.Variant;
            MaterialEntity = renderMesh.Material.Entity;
            Colour = renderMesh.Colour;
            _cachedHashCode = HashCode.Combine(MaterialIndex, MaterialVariant, MaterialEntity, DirectMesh, SubMesh);

            DrawAddress = (ulong)MaterialIndex    * 10000000000000000;
            DrawAddress += (ulong)MaterialVariant * 1000000000000;
            DrawAddress += (ulong)MaterialEntity  * 100000000;
            DrawAddress += (ulong)DirectMesh      * 10000;
            DrawAddress += (ulong)SubMesh;
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

        public override readonly int GetHashCode()
        {
            Debug.Assert(_cachedHashCode != UnknownDrawCmd,"Invalid hash code for early draw");
            return _cachedHashCode;
        }
    }
}
