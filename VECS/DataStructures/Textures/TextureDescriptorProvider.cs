using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public interface ITextureProvider
    {
        public int ImageCount { get; }
        public Texture First { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Texture GetTexture(int index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetTexture(Texture texture, int index);

        public void Dispose();
    }

    public class SingleTexture : ITextureProvider
    {
        public int ImageCount => 1;
        public Texture First { get; set; }

        public SingleTexture(Texture texture)
        {
            First = texture;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Texture GetTexture(int index) => index switch
        {
            0 => First,
            _ => throw new IndexOutOfRangeException(),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetTexture(Texture texture, int index)
        {
            bool alreadySet = First != texture;
            First = index switch
            {
                0 => texture,
                _ => throw new IndexOutOfRangeException(),
            };
            return alreadySet;
        }


        public void Dispose()
        {
            First.Dispose();
        }

        public static explicit operator SingleTexture(Texture texture)
        {
            return new(texture);
        }

        public static implicit operator Texture(SingleTexture texture)
        {
            return texture.First;
        }
    }

    public class BindingArrayTexture : ITextureProvider
    {
        public int ImageCount => ArrayTextures.Length;

        public Texture First { get { return ArrayTextures[0]; } set { ArrayTextures[0] = value; } }

        public Texture[] ArrayTextures;

        public BindingArrayTexture(int capacity)
        {
            Debug.Assert(capacity > 1);
            ArrayTextures = new Texture[capacity];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Texture GetTexture(int index)
        {
            return ArrayTextures[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetTexture(Texture texture, int index)
        {
            bool alreadySet = ArrayTextures[index] != texture;
            ArrayTextures[index] = texture;
            return alreadySet;
        }

        public void Dispose()
        {
            for (int i = 0; i < ArrayTextures.Length; i++)
            {
                ArrayTextures[i]?.Dispose();
            }
        }
    }
}

