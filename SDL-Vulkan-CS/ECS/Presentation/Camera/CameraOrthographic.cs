using SDL_Vulkan_CS.ECS;

namespace SDL_Vulkan_CS
{
    public struct CameraOrthographic : IComponent
    {
        public static int ComponentId { get; set; }

        public float width;
        public float height;
        public float ClipNear;
        public float ClipFar;

    }
}
