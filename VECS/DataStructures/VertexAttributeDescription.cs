using Vortice.Vulkan;

namespace VECS
{
    public readonly struct VertexAttributeDescription
    {
        public readonly VertexAttribute attribute;
        public readonly VertexAttributeFormat format;
        public readonly uint binding;
        public readonly uint location;
        public readonly uint offset;
        public readonly uint AttributeFloatSize => format.GetAttributeFloatSize();
        public readonly uint AttributeByteSize => format.GetAttributeByteSize();
        public readonly VkVertexInputAttributeDescription VkVertexInputAttribute => new()
        {
            format = format.GetVkFormat(),
            binding = binding,
            location = location,
            offset = offset
        };

        public VertexAttributeDescription(VertexAttribute attribute, VertexAttributeFormat format)
        {
            this.attribute = attribute;
            this.format = format;
        }

        public VertexAttributeDescription(VertexAttribute attribute, VertexAttributeFormat format, uint offset, uint binding, uint location)
        {
            this.attribute = attribute;
            this.format = format;
            this.binding = binding;
            this.location = location;
            this.offset = offset;
        }
    }
}