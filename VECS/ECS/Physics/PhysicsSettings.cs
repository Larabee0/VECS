using BepuPhysics.Constraints;
using System.Numerics;

namespace VECS.Physics
{
    public class PhysicsSettings
    {
        public SpringSettings SpringSettings;
        public float MaximumRecoveryVelocity;
        public float FrictionCoefficient;
        public Vector3 Gravity;
        public float LinearDamping;
        public float AngularDamping;

        public readonly static PhysicsSettings Default = new()
        {
            SpringSettings = new SpringSettings(30,1),
            MaximumRecoveryVelocity = 2f,
            FrictionCoefficient = 1f,
            Gravity = Vector3.Zero,
            LinearDamping = 0.03f,
            AngularDamping = 0.03f
        };
    }
}
