using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct SpotLight : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;

        public float Constant;
        public float Linear;
        public float Quadratic;

        public float cutOff;
        public float outerCutOff;
    }
}
