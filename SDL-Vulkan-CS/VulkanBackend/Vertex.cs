using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Vertex struct defines serveral vertex parameters
    /// Position, Colour, Normal and UV
    /// 
    /// A vertex is 44 bytes atomically. but likely has an extra 4 bytes of padding
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 28)]
    public struct Vertex : IEqualityComparer<Vertex>
    {
        public static unsafe int SizeInBytes => sizeof(Vertex);

        public Vector3 Position; // offset 0
        public Vector3 Normal; // offset 12
        public float Elevation; // offset 16

        public Vertex(Vector3 position, Vector3 colour)
        {

            Position = position;
        }


        public static Vertex Average(Vertex a, Vertex b)
        {
            return new Vertex()
            {
                Position = (a.Position + b.Position) * 0.5f,
                //Normal = Vector3.Normalize((a.Normal + b.Normal) * 0.5f),
                //Elevation = (a.Elevation + b.Elevation) * 0.5f
            };
        }

        public static bool operator ==(Vertex left, Vertex right)
        {
            return left.Position == right.Position;
        }

        public static bool operator !=(Vertex left, Vertex right) => !(left == right);

        public readonly bool Equals(Vertex x, Vertex y)
        {
            return x == y;
        }

        public readonly bool Equals(Vertex other)
        {
            return this == other;
        }

        public readonly int GetHashCode([DisallowNull] Vertex obj)
        {
            return obj.GetHashCode();
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Position);
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
                new VkVertexInputAttributeDescription(0, VkFormat.R32G32B32Sfloat, (uint)Marshal.OffsetOf<Vertex>(nameof(Position))), // position

                new VkVertexInputAttributeDescription(1, VkFormat.R32G32B32Sfloat, (uint)Marshal.OffsetOf<Vertex>(nameof(Normal))), // normal

                new VkVertexInputAttributeDescription(2, VkFormat.R32Sfloat, (uint)Marshal.OffsetOf<Vertex>(nameof(Elevation))) // Elevation
            ];

            return attributeDescriptions;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Vertex vertex && vertex.Equals(this);
        }
    }
}
