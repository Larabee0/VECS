using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using BepuUtilities.Memory;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS.Physics
{
    public struct RaycastInput
    {
        public Vector3 Origin;
        public float MaximumT;
        public Vector3 Direction;
    }

    public struct RaycastHit
    {
        public Vector3 Normal;
        public float T;
        public CollidableReference Collidable;
        public bool Hit;
    }

    unsafe struct RayHitHandler : IRayHitHandler
    {
        public Buffer<RaycastHit> Hits;
        public int* IntersectionCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowTest(CollidableReference collidable)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowTest(CollidableReference collidable, int childIndex)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRayHit(in RayData ray, ref float maximumT, float t, in Vector3 normal, CollidableReference collidable, int childIndex)
        {
            maximumT = t;
            ref var hit = ref Hits[ray.Id];
            if (t < hit.T)
            {
                if (hit.T == float.MaxValue)
                    ++*IntersectionCount;
                hit.Normal = normal;
                hit.T = t;
                hit.Collidable = collidable;
                hit.Hit = true;
            }
        }
    }
}
