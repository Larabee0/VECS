using SDL_Vulkan_CS.ECS;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Indicates the entity is an orthographic camera
    /// Stores orthographic camera settings
    /// </summary>
    public struct CameraOrthographic : IComponent
    {
        public static int ComponentId { get; set; }

        public float width;
        public float height;
        public float ClipNear;
        public float ClipFar;

    }
}
