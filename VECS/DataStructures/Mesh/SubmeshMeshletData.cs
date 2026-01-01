namespace VECS
{
    public struct SubmeshMeshletData
    {
        public int MeshletOffset;
        public int MeshletCount;
        public int VertexCount;
        public int TriangleCount;
        public int VertexOffset;
        public int TriangleOffset;

        public SubmeshMeshletData(){}

        public SubmeshMeshletData(uint meshletCount)
        {
            MeshletCount = (int)meshletCount;
            TriangleCount = MeshletCount * (int)MeshExtensions.MAX_MESHLET_TRIS * 3;
            VertexCount = MeshletCount * (int)MeshExtensions.MAX_MESHLET_VERTS;
        }
    }
}
