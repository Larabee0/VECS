using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using BepuUtilities.Memory;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS.ECS.Physics
{
    public struct RaycastInput
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float MaxDst;

        public readonly Vector3 RayEnd => GetPoint(MaxDst);

        public readonly bool Valid => MaxDst > 0 && Direction != Vector3.Zero;

        public RaycastInput(Vector3 origin, Vector3 direction) : this()
        {
            Origin = origin;
            Direction = direction;
        }

        public RaycastInput(Vector3 origin, Vector3 direction, float maxDst)
        {
            Origin = origin;
            MaxDst = maxDst;
            Direction = direction;
        }

        public readonly Vector3 GetPoint(float distance)
        {
            return Origin + Direction * distance;
        }
    }

    public struct RaycastHit
    {
        public static RaycastHit Null => new() { Hit = false, T = float.MaxValue };

        public Vector3 Normal;
        public float T;
        public CollidableReference Collidable;
        public bool Hit;
    }

    unsafe struct RayHitHandler : IRayHitHandler
    {
        public Buffer<RaycastHit> Hits;
        public int* IntersectionCount;

        public readonly void ClearAll()
        {
            Hits.FillBuffer(RaycastHit.Null);
        }

        public readonly void ClearOne()
        {
            Hits.FillBuffer(RaycastHit.Null, 1);
        }

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
            //maximumT = t;
            ref var hit = ref Hits[ray.Id];
            if (t < hit.T && t > 0)
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
