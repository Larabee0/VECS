using System.Collections.Generic;
using VECS.ECS;

namespace VECS
{
    public readonly struct SortByDepthOnly : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.DepthOnlyHash.CompareTo(y.DepthOnlyHash);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            var val = x.DepthOnlyHash.CompareTo(y.DepthOnlyHash);
            if(val != 0) return val;
            return x.MeshHash.CompareTo(y.MeshHash);
        }
    }

    public readonly struct SortByColour : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.ColourHash.CompareTo(y.ColourHash);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            var val = x.ColourHash.CompareTo(y.ColourHash);
            if (val != 0) return val;
            return x.MeshHash.CompareTo(y.MeshHash);
        }
    }

    public readonly struct MaterialProviderFrozen
    {
        public readonly ulong DepthOnlyHash;

        public readonly ulong ColourHash;

        public readonly ulong MeshHash;

        public readonly int EntityIndex;

        public readonly bool HasDepthOnly => DepthOnlyHash != 0;

        public readonly bool IsValid => EntityIndex != 0 && (DepthOnlyHash != 0 || ColourHash != 0);

        public MaterialProviderFrozen(MaterialProvider provider, int entityIndex, ulong meshHash)
        {
            DepthOnlyHash = provider.DepthOnlyHash;

            ColourHash = provider.ColourHash;

            MeshHash = meshHash;

            EntityIndex = entityIndex;
        }
    }


    public class MaterialProvider : Asset
    {
        public Material DepthOnly;
        public Material DepthOnly_Mesh;

        public Material Colour;

        public Material Colour_Mesh;

        public bool HasDepthOnly => DepthOnly != null || DepthOnly_Mesh != null;
        public bool HasAnyColour => Colour != null || Colour_Mesh != null;

        public bool IsValid => HasDepthOnly || HasAnyColour;

        public bool DepthMeshShaderFallback => (DepthOnly_Mesh != null && DepthOnly != null) || DepthOnly != null;
        public bool ColourMeshShaderFallback => (Colour_Mesh != null && Colour != null) || Colour != null || (Colour == null && Colour_Mesh != null);
        
        public bool HasRequiredMeshShaderFallbacks => (HasDepthOnly && DepthMeshShaderFallback) || (HasAnyColour && ColourMeshShaderFallback);


        public ulong DepthOnlyHash => DepthOnly.CombinedHash;
        public ulong MeshDepthOnlyHash => DepthOnly_Mesh.CombinedHash;

        public ulong ColourHash => Colour.CombinedHash;
        public ulong MeshColourHash => Colour_Mesh.CombinedHash;

        public MaterialProviderFrozen GetFrozen(int entityIndex, ulong meshData)
        {
            return new(this,entityIndex, meshData);
        }


        public MaterialProvider()
        {
            
        }

        private MaterialProvider(string name)
        {
            AssetName = name;
        }

        public MaterialProvider(string name, Material colour) : this(name)
        {
            Colour = colour;
        }

        public MaterialProvider(Material depthOnly, string name) : this(name)
        {
            DepthOnly = depthOnly;
        }

        public MaterialProvider(string name,  Material depthOnly, Material colour) : this(name)
        {
            DepthOnly = depthOnly;
            Colour = colour;
        }

    }
    public struct MaterialProviderComponent : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;

        public CullOverrides CullOverrides;
        public RenderLayer LayerFlags;
    }

}