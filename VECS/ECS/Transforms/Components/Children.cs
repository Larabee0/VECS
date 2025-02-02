namespace VECS.ECS.Transforms
{
    public struct Children : IComponent
    {
        public static int ComponentId { get; set; }
        public int Id => ComponentId;

        public Entity[] Value;
    }
}
