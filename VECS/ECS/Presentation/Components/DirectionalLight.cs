using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct DirectionalLight : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public DirectionalLightInfo Value;
    }
}
