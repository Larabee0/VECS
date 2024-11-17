using System;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Vertex struct defines serveral vertex parameters
    /// Position, Colour, Normal and UV
    /// 
    /// A vertex is 44 bytes atomically. but likely has an extra 4 bytes of padding
    /// </summary>
    public struct Vertex
    {
        public static unsafe int SizeInBytes => sizeof(Vertex);
        public static unsafe uint PositionOffset => 0;
        public static unsafe uint ColourOffset => PositionOffset + (uint)sizeof(Vector3);
        public static unsafe uint NormalOffset => ColourOffset + (uint)sizeof(Vector3);
        public static unsafe uint UVOffset => NormalOffset + (uint)sizeof(Vector2);

        public Vector3 Position; // offset 0
        public Vector3 Colour; // offset 12
        public Vector3 Normal; // offset 24
        public Vector2 UV; // offset 36

        public Vertex(Vector3 position, Vector3 colour)
        {
            Position = position;
            Colour = colour;
        }


        public static bool operator ==(Vertex left, Vertex right)
        {
            return left.Position == right.Position
                && left.Colour == right.Colour
                && left.Normal == right.Normal
                && left.UV == right.UV;
        }

        public static bool operator !=(Vertex left, Vertex right) => !(left == right);

        public override readonly bool Equals(object obj)
        {
            return (obj is Vertex other) && Equals(other);
        }

        public readonly bool Equals(Vertex other)
        {
            return this == other;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Position, Colour, Normal, UV);
        }

        /// <summary>
        /// Binding descriptors are needed for a graphics pipeline if it wants to use this vertex struct
        /// </summary>
        /// <returns></returns>
        public static VkVertexInputBindingDescription[] GetBindingDescriptions()
        {
            VkVertexInputBindingDescription[] bindingDescriptions =
            [
                new VkVertexInputBindingDescription()
                {
                    binding = 0,
                    stride = (uint)SizeInBytes,
                    inputRate = VkVertexInputRate.Vertex
                },
            ];
            return bindingDescriptions;
        }

        /// <summary>
        /// Attribute descriptors are needed for a graphics pipeline if it wants to use this vertex struct
        /// </summary>
        /// <returns></returns>
        public static VkVertexInputAttributeDescription[] GetAttributeDescriptions()
        {
            VkVertexInputAttributeDescription[] attributeDescriptions =
            [
                new VkVertexInputAttributeDescription(0, VkFormat.R32G32B32Sfloat, PositionOffset), // position

                new VkVertexInputAttributeDescription(1, VkFormat.R32G32B32Sfloat, ColourOffset), // colour

                new VkVertexInputAttributeDescription(2, VkFormat.R32G32B32Sfloat, NormalOffset), // normal

                new VkVertexInputAttributeDescription(3, VkFormat.R32G32Sfloat, UVOffset) // uv
            ];

            return attributeDescriptions;
        }
    }
}
