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

        public Bounds(ModelBounds modelBounds)
        {

            SetMinMax(modelBounds.Min.AsVector3(), modelBounds.Max.AsVector3());
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds Transform(Matrix4x4 transform, Bounds aabb)
        {
            // stolen unity bounds transform assumes column major, this stupid engine uses row major
            transform = Matrix4x4.Transpose(transform);
            var transformed = Transform(new Matrix3x3(transform), aabb);
            transformed.Min += transform.GetMatrixColumn(3).AsVector3();
            transformed.Max += transform.GetMatrixColumn(3).AsVector3();
            return transformed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds Transform(Matrix3x3 transform, Bounds aabb)
        {
            // From Christer Ericson's Real-Time Collision Detection on page 86 and 87.
            // We want the transformed minimum and maximums of the AABB. Multiplying a 3x3 matrix on the left of a
            // column vector looks like so:
            //
            // [ c0.x c1.x c2.x ] [ x ]   [ c0.x * x + c1.x * y + c2.x * z ]
            // [ c0.y c1.y c2.y ] [ y ] = [ c0.y * x + c1.y * y + c2.y * z ]
            // [ c0.z c1.z c2.z ] [ z ]   [ c0.z * x + c1.z * y + c2.z * z ]
            //
            // The column vectors we will use are the input AABB's min and max. Simply multiplying those two vectors
            // with the transformation matrix won't guarantee we get the new min and max since those are only two
            // points out of eight in the AABB and one of the other six may set the new min or max.
            //
            // To ensure we get the correct min and max, we must transform all eight points. But it's not necessary
            // to actually perform eight matrix multiplies to get our final result. Instead, we can build the min and
            // max incrementally by computing each term in the above matrix multiply separately then summing the min
            // (or max). For instance, to find the new minimum contributed by the original min and max x component, we
            // compute this:
            //
            // newMin.x = min(c0.x * Min.x, c0.x * Max.x);
            // newMin.y = min(c0.y * Min.x, c0.y * Max.x);
            // newMin.z = min(c0.z * Min.x, c0.z * Max.x);
            //
            // Then we add minimum contributed by the original min and max y components:
            //
            // newMin.x += min(c1.x * Min.y, c1.x * Max.y);
            // newMin.y += min(c1.y * Min.y, c1.y * Max.y);
            // newMin.z += min(c1.z * Min.y, c1.z * Max.y);
            //
            // And so on. Translation can be handled by simply initializing the new min and max with the translation
            // amount since it does not affect the min and max bounds in local space.
            var t1 = transform.c0 * new Vector3(aabb.Min.X, aabb.Min.X, aabb.Min.X);
            var t2 = transform.c0 * new Vector3(aabb.Max.X, aabb.Max.X, aabb.Max.X);
            var minMask = NumericsExtensions.Less( t1 , t2);
            var transformed = Bounds.FromMinMax(NumericsExtensions.Select(t2, t1, minMask), NumericsExtensions.Select(t2, t1, !minMask));
            t1 = transform.c1 * new Vector3(aabb.Min.Y,aabb.Min.Y,aabb.Min.Y);
            t2 = transform.c1 * new Vector3(aabb.Max.Y, aabb.Max.Y, aabb.Max.Y);
            minMask = NumericsExtensions.Less(t1, t2);
            transformed.Min += NumericsExtensions.Select(t2, t1, minMask);
            transformed.Max += NumericsExtensions.Select(t2, t1, !minMask);
            t1 = transform.c2 * new Vector3(aabb.Min.Z,aabb.Min.Z,aabb.Min.Z);
            t2 = transform.c2 * new Vector3(aabb.Max.Z, aabb.Max.Z, aabb.Max.Z);
            minMask = NumericsExtensions.Less(t1, t2);
            transformed.Min += NumericsExtensions.Select(t2, t1, minMask);
            transformed.Max += NumericsExtensions.Select(t2, t1, !minMask);
            return transformed;
        }
    }
}