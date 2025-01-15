using System.Numerics;
using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS.ECS.Presentation
{
    [StructLayout(LayoutKind.Sequential,Size =172)]
    public struct CullParams
    {
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
        public bool OcclusionCulling;
        public bool FrustrumCulling;
        public float DrawDist;
        public bool Aabb;
        public Vector4 AabbMin;
        public Vector4 AabbMax;
    }

    [StructLayout(LayoutKind.Sequential, Size = 96)]
    public struct  DrawCullData
    {
        //public Matrix4x4 ViewMat;
        public float P00;
        public float P11;
        public float Znear;
        public float Zfar; // symmetric projection parameters
        public Vector4 Frustum; // data for left/right/top/bottom frustum planes
        public float LodBase;
        public float LodStep; // lod distance i = base * pow(step, i)
        public float PyramidWidth;
        public float PyramidHeight; // depth pyramid size in texels

        public int DrawCount;
        
        public int CullingEnabled;
        public int LodEnabled;
        public int OcclusionEnabled;
        public int DistanceCheck;
        public int AABBcheck;
        public float AabbMin_x;
        public float AabbMin_y;
        public float AabbMin_z;
        public float AabbMax_x;
        public float AabbMax_y;
        public float AabbMax_z;
    }
}
