using Noesis;
using System;
using System.Collections.Generic;

namespace VECS.UI
{
    public class NoesisTextureProvider : TextureProvider
    {

        private readonly Dictionary<string, WeakReference<Texture2D>> _cache = new(StringComparer.OrdinalIgnoreCase);

        public override TextureInfo GetTextureInfo(Uri uri)
        {

            Texture2D texture = GetTexture(uri.OriginalString);

            return new TextureInfo(texture.Width, texture.Height);
        }

        public override Noesis.Texture LoadTexture(Uri uri)
        {
            Texture2D texture = GetTexture(uri.OriginalString);

            return new NoesisTexture(texture,false,true);

        }

        private Texture2D GetTexture(string filename)
        {
            if (_cache.TryGetValue(filename, out var weakReference) &&
                weakReference.TryGetTarget(out var cachedTexture) &&
                !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            string fullPath = System.IO.Path.Combine(Asset.AssetsPath, filename);

            
            Texture2D texture = TextureLoader.Load2D(fullPath,Vortice.Vulkan.VkFormat.R8G8B8A8Unorm);

            _cache[filename] = new WeakReference<Texture2D>(texture);

            return texture;
        }
    }
}
