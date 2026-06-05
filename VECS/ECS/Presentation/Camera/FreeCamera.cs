namespace VECS.ECS.Presentation
{
    public struct FreeCamera : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float AngleX;
        public float AngleY;
    }
}
