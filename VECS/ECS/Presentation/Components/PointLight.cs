using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct PointLight : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector4 Direction;
        public Vector4 Colour;
        public float CutOff;
        public float OuterCutOff;
        public float Constant;
        public float Linear;
        public float Quadratic;
        public float AmbientStrength;
        public float DiffuseStrength;
        public float SpecularStrength;
    }
}
