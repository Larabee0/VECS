using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct DirectionalLight : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public DirectionalLightUniform Value;
    }
    public struct DirectionalLightFrameInfo : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int DirectionalLightCount;
        public int DirectionalLightShadowCount;
    }
}
