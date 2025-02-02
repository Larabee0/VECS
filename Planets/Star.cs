using VECS.ECS;
using System.Numerics;

namespace Planets
{
    public struct Star : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector4 Colour;
        public Vector4 DrawColour;

        public float Intensity;
        public float Radius;
        public readonly Vector4 PointLightColour => new(Colour.X,Colour.Y,Colour.Z,Intensity);
    }
}
