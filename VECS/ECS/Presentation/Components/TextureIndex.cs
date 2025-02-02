namespace VECS.ECS.Presentation
{
    /// <summary>
    /// stores a reference to a texture2d <see cref="Texture2d.Textures"/>
    /// </summary>
    public struct TextureIndex : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;
    }
}
