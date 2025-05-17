namespace VECS.ECS.Physics
{
    public struct DynamicBodyTag : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float Mass;
    }
}
