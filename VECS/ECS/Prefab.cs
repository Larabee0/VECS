namespace VECS.ECS
{
    public struct Prefab : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }
}
