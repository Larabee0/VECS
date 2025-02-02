namespace VECS.ECS.Presentation
{
    /// <summary>
    /// Indicates the entity is an orthographic camera
    /// Stores orthographic camera settings
    /// </summary>
    public struct CameraOrthographic : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float width;
        public float height;
        public float ClipNear;
        public float ClipFar;

    }
}
