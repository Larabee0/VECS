namespace SDL_Vulkan_CS.ECS.Presentation
{
    public  struct DoNotRender : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }
}
