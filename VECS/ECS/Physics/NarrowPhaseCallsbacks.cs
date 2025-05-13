using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using System;
using System.Runtime.CompilerServices;

namespace VECS.Physics
{
    public struct NarrowPhaseCallsbacks : INarrowPhaseCallbacks
    {
        public SpringSettings ContactSpringiness;
        public float MaximumRecoveryVelocity;
        public float FrictionCoefficient;

        public NarrowPhaseCallsbacks(PhysicsSettings settings)
        {
            ContactSpringiness = settings.SpringSettings;
            MaximumRecoveryVelocity = settings.MaximumRecoveryVelocity;
            FrictionCoefficient = settings.FrictionCoefficient;
        }


        public void Initialize(Simulation simulation)
        {
            if (ContactSpringiness.AngularFrequency == 0 && ContactSpringiness.TwiceDampingRatio == 0)
            {
                ContactSpringiness = new(30, 1);
                MaximumRecoveryVelocity = 2f;
                FrictionCoefficient = 1f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        {
            return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            return true;
        }

        public readonly bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial.FrictionCoefficient = FrictionCoefficient;
            pairMaterial.MaximumRecoveryVelocity = MaximumRecoveryVelocity;
            pairMaterial.SpringSettings = ContactSpringiness;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            return true;
        }

        public readonly void Dispose()
        {

        }

    }
}
