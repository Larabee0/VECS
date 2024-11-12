using SDL_Vulkan_CS.ECS;

namespace SDL_Vulkan_CS
{
    public struct CameraPerspective : IComponent
    {
        public static int ComponentId { get; set; }

        public float FOV;
        public float ClipNear;
        public float ClipFar;
    }
}
