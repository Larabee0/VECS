using VECS.ECS;
using System.Numerics;

namespace VECS.Artifact.Colour
{
    public struct ElevationMinMax : IComponent
    {
        public static int ComponentId { get; set; }
        public int Id => ComponentId;

        public Vector2 Value;

    }
}
