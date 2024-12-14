using SDL_Vulkan_CS.ECS;

namespace SDL_Vulkan_CS.Artifact.Colour
{
    public struct TerrainShaderProperties : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;


        public int ColourTexture;
        public int SteepTexture;
        public int WaveA;
        public int WaveB;
        public int WaveC;
        public int TextureArrayIndex;
    }
}
