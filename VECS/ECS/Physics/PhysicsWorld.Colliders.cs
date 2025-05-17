using BepuPhysics;
using BepuPhysics.Collidables;

namespace VECS.ECS.Physics
{
    public sealed partial class PhysicsWorld
    {
        public Shapes Shapes => Simulation.Shapes;

        public TypedIndex Add<TShape>(in TShape shape) where TShape : unmanaged, IShape
        {
            return Shapes.Add(shape);
        }

        public StaticHandle AddStatic(StaticDescription desc)
        {
            return Simulation.Statics.Add(desc);
        }
    }
}
