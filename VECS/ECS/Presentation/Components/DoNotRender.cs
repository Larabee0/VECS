namespace VECS.ECS.Presentation
{
    public  struct DoNotRender : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }
}
