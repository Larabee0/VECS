using System;
using System.Diagnostics;
using System.Numerics;
using VECS.ECS;
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
        private const int UnknownDrawCmd = -1;
        public readonly int DirectMesh => RenderMesh.Mesh.Hash;
        public readonly int SubMesh => RenderMesh.Mesh.SubMesh;
        public readonly int MaterialIndex => RenderMesh.Material.Hash;
        public readonly int MaterialVariant=>RenderMesh.Material.Variant;
        public readonly int MaterialEntity =>RenderMesh.Material.Entity;
        public readonly Entity Entity;
        public readonly RenderMesh RenderMesh;
        public readonly DrawCommand DrawCommand;
        public readonly Vector4 Colour => RenderMesh.Colour;
        public readonly bool Bloom => DrawCommand.Bloom;

        public readonly ulong DrawAddress;
        private readonly int _cachedHashCode;

        public EarlyDrawCommand(Entity entity, DrawCommand drawCommand,RenderMesh renderMesh)
        {
            Entity = entity;
            DrawCommand = drawCommand;
            RenderMesh = renderMesh;
            RenderMesh.Mesh.Hash = AssetDataBase<DirectMesh>.GetCurrentIndexOfHashed(DirectMesh);
            RenderMesh.Material.Hash = AssetDataBase<MaterialV2>.GetCurrentIndexOfHashed(MaterialIndex);

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


        public static bool ShouldMakeNewDrawCmd(RenderMesh a, RenderMesh b)
        {
            return a.Mesh.Hash != b.Mesh.Hash || a.Material.Hash != b.Material.Hash || a.Material.Variant != b.Material.Variant || a.Material.Entity != b.Material.Entity;
        }

        public readonly int CompareTo(object obj)
        {
            if(obj is EarlyDrawCommand b)
            {
                return DrawAddress.CompareTo(b.DrawAddress);
            }

            throw new ArgumentException(string.Format("Object is not a {0}", typeof(EarlyDrawCommand)));
        }

        public override readonly int GetHashCode()
        {
            Debug.Assert(_cachedHashCode != UnknownDrawCmd,string.Format("Invalid hash code for early draw {0}",_cachedHashCode));
            return _cachedHashCode;
        }
    }
}
