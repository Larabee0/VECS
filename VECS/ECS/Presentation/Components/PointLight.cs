using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct PointLight : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    
        public Vector4 Colour;
        public float Radius;

        public float Intensity
        {
            readonly get => Colour.W; set => Colour.W = value;
        }
    }
}
