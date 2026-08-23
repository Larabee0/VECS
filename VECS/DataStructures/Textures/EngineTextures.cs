
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Vulkan;

namespace VECS
{
    public static class EngineTextures
    {
        public readonly static Texture2D MissingTexture;
        public readonly static Texture2D Zeroed;
        public readonly static Texture2D Black;
        public readonly static Texture2D Gray;
        public readonly static Texture2D Normal;
        public readonly static Texture2D Red;
        public readonly static Texture2D White;

        private readonly static ConcurrentDictionary<int, ITextureProvider> _engineTextures = new();

        static EngineTextures()
        {
            Colour[] copyFrom = new Colour[4 * 4];
            Array.Fill(copyFrom, Colour.Black);

            var pink = new Colour(251, 60, 249, 255);

            copyFrom[0] = pink;
            copyFrom[1] = pink;
            copyFrom[4] = pink;
            copyFrom[5] = pink;

            copyFrom[10] = pink;
            copyFrom[11] = pink;
            copyFrom[14] = pink;
            copyFrom[15] = pink;

            MissingTexture = new("Fallback", 4, 4, VkFormat.R8G8B8A8Unorm, VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.Sampled | VkImageUsageFlags.Storage, true);
            MissingTexture.CopyFromArray(copyFrom);
            MissingTexture.CreateHostBuffer(true);

            Array.Fill(copyFrom, Colour.Clear);
            Zeroed = new("Clear", 4, 4);
            Zeroed.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.Black);
            Black = new("Black", 4, 4);
            Black.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, new Vector4(0.5f, 0.5f, 0.5f, 1f).ToVkColor());
            Gray = new("Gray", 4, 4);
            Gray.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, new Vector4(0.5f, 0.5f, 1f, 1f).ToVkColor());
            Normal = new("Normal", 4, 4);
            Normal.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.Red);
            Red = new("Red", 4, 4);
            Red.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.White);
            White = new("White", 4, 4);
            White.CopyFromArray(copyFrom);
            Console.WriteLine("Created Default Textures");

        }


        public static ITextureProvider TryGetTexture(int propertyId)
        {
            _engineTextures.TryGetValue(propertyId, out var texture);
            return texture;
        }

        public static void AddTexture(int propertyId, ITextureProvider texture)
        {
            if (!_engineTextures.TryAdd(propertyId, texture))
            {
                throw new ArgumentException(string.Format("Key {0} already exists in the enginetexture dictionary, use Updatetexture to replace it", propertyId));
            }
        }

        public static void UpdateTexture(int propertyId, Texture texture, int index = 0)
        {
            if (_engineTextures.TryGetValue(propertyId, out var existing))
            {
                existing.SetTexture(texture, index);
                return;
            }
            throw new KeyNotFoundException(string.Format("Key {0} has no texture assocaited with it, use Addtexture to add it", propertyId));
        }

        public static void AddOrUpdateTexture(int propertyId, ITextureProvider texture)
        {
            if (propertyId == 0) return;
            _engineTextures.AddOrUpdate(propertyId, texture, (key, value) =>
            {
                value.Dispose();
                return texture;
            });
        }

        public static void RemoveTexture(int propertyId, bool disposeAfterRemove = true)
        {
            if (_engineTextures.TryRemove(propertyId, out var buffer) && disposeAfterRemove)
            {
                buffer.Dispose();
            }
        }
    }
}
