namespace VECS.ECS.Presentation
{
    /// <summary>
    /// Stores the screen aspect ratio for the access by the camera system
    /// </summary>
    public struct FrameInfo : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float screenAspect;
    }
}
