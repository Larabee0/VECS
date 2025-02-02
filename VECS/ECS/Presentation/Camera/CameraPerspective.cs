namespace VECS.ECS.Presentation
{
    /// <summary>
    /// Indicates the entity is an Perspective camera
    /// Stores Perspective camera settings
    /// </summary>
    public struct CameraPerspective : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float FOV;
        public float ClipNear;
        public float ClipFar;
    }
}
