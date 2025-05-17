using BepuPhysics;
using BepuUtilities;
using System;
using System.Numerics;

namespace VECS.ECS.Physics
{
    public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        /// <summary>
        /// Gravity to apply to dynamic bodies in the simulation.
        /// </summary>
        public Vector3 Gravity;
        /// <summary>
        /// Fraction of dynamic body linear velocity to remove per unit of time. Values range from 0 to 1. 0 is fully undamped, while values very close to 1 will remove most velocity.
        /// </summary>
        public float LinearDamping;
        /// <summary>
        /// Fraction of dynamic body angular velocity to remove per unit of time. Values range from 0 to 1. 0 is fully undamped, while values very close to 1 will remove most velocity.
        /// </summary>
        public float AngularDamping;


        private Vector3Wide gravityWideDt;
        private Vector<float> linearDampingDt;
        private Vector<float> angularDampingDt;

        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

        public readonly bool AllowSubstepsForUnconstrainedBodies => false;

        public readonly bool IntegrateVelocityForKinematics => false;

        public PoseIntegratorCallbacks(PhysicsSettings settings) : this()
        {
            Gravity = settings.Gravity;
            LinearDamping = settings.LinearDamping;
            AngularDamping = settings.AngularDamping;
        }


        public readonly void Initialize(Simulation simulation)
        {
        }

        public void PrepareForIntegration(float dt)
        {
            linearDampingDt = new Vector<float>(MathF.Pow(MathHelper.Clamp(1 - LinearDamping, 0, 1), dt));
            angularDampingDt = new Vector<float>(MathF.Pow(MathHelper.Clamp(1 - AngularDamping, 0, 1), dt));
            gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
        }
        /// <summary>
        /// Callback for a bundle of bodies being integrated.
        /// </summary>
        /// <param name="bodyIndices">Indices of the bodies being integrated in this bundle.</param>
        /// <param name="position">Current body positions.</param>
        /// <param name="orientation">Current body orientations.</param>
        /// <param name="localInertia">Body's current local inertia.</param>
        /// <param name="integrationMask">Mask indicating which lanes are active in the bundle. Active lanes will contain 0xFFFFFFFF, inactive lanes will contain 0.</param>
        /// <param name="workerIndex">Index of the worker thread processing this bundle.</param>
        /// <param name="dt">Durations to integrate the velocity over. Can vary over lanes.</param>
        /// <param name="velocity">Velocity of bodies in the bundle. Any changes to lanes which are not active by the integrationMask will be discarded.</param>
        public readonly void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {   
            //This is a handy spot to implement things like position dependent gravity or per-body damping.
            //This implementation uses a single damping value for all bodies that allows it to be precomputed.
            //We don't have to check for kinematics; IntegrateVelocityForKinematics returns false, so we'll never see them in this callback.
            //Note that these are SIMD operations and "Wide" types. There are Vector<float>.Count lanes of execution being evaluated simultaneously.
            //The types are laid out in array-of-structures-of-arrays (AOSOA) format. That's because this function is frequently called from vectorized contexts within the solver.
            //Transforming to "array of structures" (AOS) format for the callback and then back to AOSOA would involve a lot of overhead, so instead the callback works on the AOSOA representation directly.
            velocity.Linear = (velocity.Linear + gravityWideDt) * linearDampingDt;
            velocity.Angular *= angularDampingDt;
        }

    }
}
