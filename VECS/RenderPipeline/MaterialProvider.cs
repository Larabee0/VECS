using Noesis;
using System;
using System.Collections.Generic;

namespace VECS
{
    public readonly struct SortByDepthPipeline : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.DepthOnlyPipelineHash.CompareTo(y.DepthOnlyPipelineHash);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            return x.DepthOnlyPipelineHash.CompareTo(y.DepthOnlyPipelineHash);
        }

    }

    public readonly struct SortByDepthMaterial : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.DepthOnlyHash.CompareTo(y.DepthOnlyHash);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            return x.DepthOnlyHash.CompareTo(y.DepthOnlyHash);
        }

    }

    public readonly struct SortByForwardPipeline : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.ForwardPipelineHash.CompareTo(y.ForwardPipelineHash);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            return x.ForwardPipelineHash.CompareTo(y.ForwardPipelineHash);
        }

    }

    public readonly struct SortByForwardMaterial : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.ForwardHash.CompareTo(y.ForwardHash);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            return x.ForwardHash.CompareTo(y.ForwardHash);
        }

    }

    public readonly struct SortByDeferredPipeline : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.DeferredPipelineHash.CompareTo(y.DeferredPipelineHash);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            return x.DeferredPipelineHash.CompareTo(y.DeferredPipelineHash);
        }

    }

    public readonly struct SortByDeferredMaterial : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.DeferredHash.CompareTo(y.DeferredHash);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            return x.DeferredHash.CompareTo(y.DeferredHash);
        }

    }

    public readonly struct SortByTransparent : IComparer<MaterialProvider>, IComparer<MaterialProviderFrozen>
    {
        public readonly int Compare(MaterialProvider x, MaterialProvider y)
        {
            return x.IsTransparent.CompareTo(y.IsTransparent);
        }

        public readonly int Compare(MaterialProviderFrozen x, MaterialProviderFrozen y)
        {
            return x.IsTransparent.CompareTo(y.IsTransparent);
        }

    }

    public readonly struct MaterialProviderFrozen
    {
        public readonly int DepthOnlyPipelineHash;
        public readonly int DepthOnlyHash;

        public readonly int ForwardPipelineHash;
        public readonly int ForwardHash;

        public readonly int DeferredPipelineHash;
        public readonly int DeferredHash;

        public readonly int EntityIndex;


        public readonly bool IsTransparent;
        public readonly bool IsDepthOnly => DeferredPipelineHash == 0 && ForwardPipelineHash == 0 && DepthOnlyHash != 0 && DepthOnlyPipelineHash != 0; 
        public readonly bool IsDeferred => DeferredPipelineHash != 0 && DeferredHash != 0;
        public readonly bool IsForward => !IsTransparent && !IsDeferred && ForwardPipelineHash != 0 && ForwardHash != 0;

        public MaterialProviderFrozen(MaterialProvider provider, int entityIndex)
        {
            DepthOnlyPipelineHash = provider.DepthOnlyPipelineHash;
            DepthOnlyHash = provider.DepthOnlyHash;

            ForwardPipelineHash = provider.ForwardPipelineHash;
            ForwardHash = provider.ForwardHash;

            DeferredPipelineHash = provider.DeferredPipelineHash;
            DeferredHash = provider.DeferredHash;

            IsTransparent = provider.IsTransparent;

            EntityIndex = entityIndex;
        }
    }


    public class MaterialProvider : Asset
    {
        public Material DepthOnly;
        public Material DepthOnly_Mesh;

        public Material Forward;
        public Material Deferred;

        public Material Forward_Mesh;
        public Material Deferred_Mesh;

        public bool IsTransparent => (Forward != null && Forward.Pipeline.Transparent) || (Forward_Mesh != null && Forward_Mesh.Pipeline.Transparent);

        public bool HasDepthOnly => DepthOnly != null || DepthOnly_Mesh != null;
        public bool HasAnyForward => Forward != null || Forward_Mesh != null;
        public bool HasAnyDeferred => Deferred != null || Deferred_Mesh != null;

        public bool IsValid => HasDepthOnly || HasAnyForward || HasAnyDeferred;

        public bool DepthMeshShaderFallback => (DepthOnly_Mesh != null && DepthOnly != null) || DepthOnly != null;
        public bool ForwardMeshShaderFallback => (Forward_Mesh != null && Forward != null) || Forward != null || (Forward == null && Forward_Mesh != null);
        public bool DeferredMeshShaderFallback => (Deferred_Mesh != null && Deferred != null) || Deferred != null || (Deferred == null && Deferred_Mesh != null);
        
        public bool HasRequiredMeshShaderFallbacks => (HasDepthOnly && DepthMeshShaderFallback) || (HasAnyForward && ForwardMeshShaderFallback) || (HasAnyDeferred && DeferredMeshShaderFallback);

        public int DepthOnlyPipelineHash => DepthOnly == null ? 0 : DepthOnly.Pipeline.Hash;
        public int MeshDepthOnlyPipelineHash => DepthOnly_Mesh == null ? 0 : DepthOnly_Mesh.Pipeline.Hash;

        public int ForwardPipelineHash => Forward == null ? 0 : Forward.Pipeline.Hash;
        public int MeshForwardPipelineHash => Forward_Mesh == null ? 0 : Forward_Mesh.Pipeline.Hash;

        public int DeferredPipelineHash => Deferred == null ? 0 : Deferred.Pipeline.Hash;
        public int MeshDeferredPipelineHash => Deferred_Mesh == null ? 0 : Deferred_Mesh.Pipeline.Hash;

        public int DeferredForwardPipelineHash => HashCode.Combine(Deferred.Pipeline.Hash, Forward.Pipeline.Hash);
        public int MeshDeferredForwardPipelineHash => HashCode.Combine(Deferred_Mesh.Pipeline.Hash, Forward_Mesh.Pipeline.Hash);


        public int DepthOnlyHash => DepthOnly.Hash;
        public int MeshDepthOnlyHash => DepthOnly_Mesh.Hash;

        public int ForwardHash => Forward.Hash;
        public int MeshForwardHash => Forward_Mesh.Hash;

        public int DeferredHash => Deferred.Hash;
        public int MeshDeferredHash => Deferred_Mesh.Hash;

        public int DeferredForwardHash => HashCode.Combine(Deferred.Hash, Forward.Hash);
        public int MeshDeferredForwardHash => HashCode.Combine(Deferred_Mesh.Hash, Forward_Mesh.Hash);

        public MaterialProviderFrozen GetFrozen(int entityIndex)
        {
            return new(this,entityIndex);
        }


        public MaterialProvider()
        {
            
        }

        private MaterialProvider(string name)
        {
            AssetName = name;
        }

        public MaterialProvider(string name, Material forward) : this(name)
        {
            Forward = forward;
        }

        public MaterialProvider(Material depthOnly, string name) : this(name)
        {
            DepthOnly = depthOnly;
        }

        public MaterialProvider(string name,  Material depthOnly, Material forward) : this(name)
        {
            DepthOnly = depthOnly;
            Forward = forward;
        }

        public MaterialProvider(string name, Material depthOnly, Material deferred, Material forward) : this(name)
        {
            DepthOnly = depthOnly;
            Forward = forward;
            Deferred = deferred;
        }

    }
}