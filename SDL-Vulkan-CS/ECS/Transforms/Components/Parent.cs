namespace SDL_Vulkan_CS.ECS
{
    public struct Parent : IComponent
    {
        public static int ComponentId {  get; set; }
        public readonly int Id => ComponentId;

        public Entity Value;
    }
}
