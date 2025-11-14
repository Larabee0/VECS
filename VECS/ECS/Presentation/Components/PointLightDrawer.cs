using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct PointLightDrawer : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector4 DrawColour;

        public float Radius;

        public float Intensity
        {
            readonly get => DrawColour.W; set => DrawColour.W = value;
        }

    }
}
