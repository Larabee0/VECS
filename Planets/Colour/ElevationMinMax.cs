using VECS.ECS;
using System.Numerics;

namespace Planets.Colour
{
    public struct ElevationMinMax : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector2 Value;

    }
}
