using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public struct Bounds
    {
        public Vector3 center;
        public Vector3 extents;
        public  Vector3 Min
        {
            readonly get => center - extents;
            set
            {
                SetMinMax(value, Max);
            }
        }

        public Vector3 Max
        {
            readonly get => center + extents;
            set
            {
                SetMinMax(Min, value);
            }
        }

        public Vector3 Size
        {   
            readonly get => extents * 2f;
            set => extents = value * 0.5f;
        }

        public Bounds(Vector3 center, Vector3 extents)
        {
            this.center = center;
            this.extents = extents;
        }

        public static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            Bounds aabb = default;
            aabb.SetMinMax(min, max);
            return aabb;
        }

        public void Encapsulate(Vector3 point)
        {
            SetMinMax(Vector3.Min(Min,point),Vector3.Max(Max,point));
        }

        public void SetMinMax(Vector3 min, Vector3 max)
        {
            extents = (max - min) * 0.5f;
            center = min + extents;
        }

        public void Encapsulate(Bounds bounds)
        {
            Encapsulate(bounds.center - bounds.extents);
            Encapsulate(bounds.center + bounds.extents);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Intersects(Bounds bounds)
        {
            return Min.X <= bounds.Max.X && Max.X >= bounds.Min.X && Min.Y <= bounds.Max.Y && Max.Y >= bounds.Min.Y && Min.Z <= bounds.Max.Z && Max.Z >= bounds.Min.Z;
        }
    }
}