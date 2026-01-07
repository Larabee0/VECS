using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct DirectionalLight : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector4 Colour;
        public Vector3 Direction;
        public float AmbientStrength;
        public float DiffuseStrength;
        public float SpecularStrength;
    }
}
