using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public struct RendererFrameInfo
    { 
        public static readonly RendererFrameInfo Null = new() { FrameIndex = -1 };

        public int FrameIndex;
        public float DeltaTime;
        public VkCommandBuffer commandBuffer;
        public VkDescriptorSet GlobalDescriptorSet;
        public DescriptorPool FrameDescriptorPool;


        public static bool operator ==(RendererFrameInfo left, RendererFrameInfo right)
        {
            return left.FrameIndex == right.FrameIndex && left.DeltaTime == right.DeltaTime;
        }

        public static bool operator !=(RendererFrameInfo left, RendererFrameInfo right) => !(left == right);

        public readonly bool Equals(RendererFrameInfo other)
        {
            return this == other;
        }

        public override readonly bool Equals(object obj)
        {
            return (obj is RendererFrameInfo other) && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(FrameIndex, DeltaTime);
        }
    }

}
