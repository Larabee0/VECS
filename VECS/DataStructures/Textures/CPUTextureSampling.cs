using System;
using System.Diagnostics;
using System.Numerics;

namespace VECS
{
    public static class CPUTextureSampling
    {
        public static Colour GetPixel(this Texture2D texture, int x, int y, int mipLevel = 0)
        {
            var res = texture.GetMipResolution(mipLevel);
            return texture.GetPixels(mipLevel)[y * res.width + x];
        }

        public static void SetPixel(this Texture2D texture, int x, int y, Colour colour, int mipLevel = 0)
        {
            var res = texture.GetMipResolution(mipLevel);
            var colours = texture.GetPixels(mipLevel);
            colours[y * res.width + x] = colour;
            texture.SetPixels(colours,mipLevel);
        }

        public unsafe static Colour GetPixelBilinear(this Texture2D texture, int u, int v, int mipLevel = 0)
        {
            var res = texture.GetMipResolution(mipLevel);            
            var pixels = texture.GetPixels(mipLevel);
            fixed(Colour* pPixels = &pixels[0])
                return BillinearInterPolation(pPixels, u, v, (int)res.width, (int)res.height);
        }

        public unsafe static Colour[] GetPixels(this Texture2D texture, int mipLevel = 0)
        {
            if (texture._hostBuffer == null || texture._hostBuffer.VkBufferSize != texture._vkBufferSizeRequirement)
            {
                texture.CreateHostBuffer(true);
            }

            var mipLevelMemoryOffset = texture.MipStartOffset(mipLevel);
            var mipLevelPixelCount = texture.GetMipLength(mipLevel);
            var mipLevelByteCount = mipLevelPixelCount * texture.BufferInstanceSize;

            var offsetPtr = IntPtr.Add(new IntPtr(texture._hostBuffer.HostPtr), mipLevelMemoryOffset).ToPointer();
            Colour[] managedColours = new Colour[mipLevelPixelCount];
            if (texture.BufferInstanceSize == sizeof(Colour))
            {
                Debug.Assert(mipLevelByteCount % sizeof(Colour) == 0, string.Format("Critical Memory error! MipMapLevel size is {0} which is not divisble by size of Colour ({1})", mipLevelByteCount, sizeof(Colour)));
                var span = new Span<Colour>(offsetPtr, mipLevelPixelCount);
                span.CopyTo(managedColours);
            }
            else if (texture.BufferInstanceSize == sizeof(Vector4))
            {
                Debug.Assert(mipLevelByteCount % sizeof(Vector4) == 0, string.Format("Critical Memory error! MipMapLevel size is {0} which is not divisble by size of Vector4 (Linear RGB) ({1})", mipLevelByteCount, sizeof(Vector4)));
                var span = new Span<Vector4>(offsetPtr, mipLevelPixelCount);

                for (int i = 0; i < mipLevelPixelCount; i++)
                {
                    managedColours[i] = span[i].ToVkColor();
                }
            }
            else
            {
                throw new NotImplementedException("Currnetly non-RGBA colours and colours that aren't 32-bit/64-bit colours aren't native supported, use direct ptr");
                // var componentCount = Vulkan.ComponentCount(_imageFormat);

                // if (componentCount > 4)
                // {
                //     throw new NotImplementedException("Formats with more than 4 components are not currently supported in vecs for conversation to the Colour type, please use the underlying memory pointer instead!");
                // }

                // var packing = Vulkan.Packed(_imageFormat);

                // byte[] componentBitsPerPixel = TextureExtensions.GetBitsPerPixel(_imageFormat);

                // Int128 workingMem = 0;
                // var unsafePtr = _hostBuffer.HostPtr;

                // for (int i = 0; i < mipLevelPixelCount; i++)
                // {
                //     NativeMemory.Copy(unsafePtr, &workingMem, (uint)BufferInstanceSize);
                //     int totalShift = 0;
                //     for (int j = 0; j < componentBitsPerPixel.Length; j++)
                //     {
                //         Int128 copy = workingMem;
                //         copy >>= totalShift;
                //         totalShift += componentBitsPerPixel[i];
                //     }
                // }

            }

            return managedColours;
        }

        public unsafe static void SetPixels(this Texture2D texture,Colour[] pixels, int mipLevel)
        {
            if (texture._hostBuffer == null || texture._hostBuffer.VkBufferSize != texture._vkBufferSizeRequirement)
            {
                texture.CreateHostBuffer(true);
            }

            var mipLevelMemoryOffset = texture.MipStartOffset(mipLevel);
            var mipLevelPixelCount = texture.GetMipLength(mipLevel);
            var mipLevelByteCount = mipLevelPixelCount * texture.BufferInstanceSize;

            var offsetPtr = IntPtr.Add(new IntPtr(texture._hostBuffer.HostPtr), mipLevelMemoryOffset).ToPointer();
            if (texture.BufferInstanceSize == sizeof(Colour))
            {
                Debug.Assert(mipLevelByteCount % sizeof(Colour) == 0, string.Format("Critical Memory error! MipMapLevel size is {0} which is not divisble by size of Colour ({1})", mipLevelByteCount, sizeof(Colour)));
                var span = new Span<Colour>(offsetPtr, mipLevelPixelCount);
                pixels.CopyTo(span);
            }
            else if (texture.BufferInstanceSize == sizeof(Vector4))
            {
                Debug.Assert(mipLevelByteCount % sizeof(Vector4) == 0, string.Format("Critical Memory error! MipMapLevel size is {0} which is not divisble by size of Vector4 (Linear RGB) ({1})", mipLevelByteCount, sizeof(Vector4)));
                var span = new Span<Vector4>(offsetPtr, mipLevelPixelCount);

                for (int i = 0; i < mipLevelPixelCount && i < pixels.Length; i++)
                {
                    span[i] = pixels[i].ToColour();
                }
            }
            else
            {
                throw new NotImplementedException("Currnetly non-RGBA colours and colours that aren't 32-bit/64-bit colours aren't native supported, use direct ptr");
            }
        }

        public static unsafe Colour BillinearInterPolation(Colour* pixels, float u, float v, int w, int h)
        {
            u = u > 0 ? u % 1 : 1 + (u % 1);
            v = v > 0 ? v % 1 : 1 + (v % 1);
            float pixelXCoordinate = u * w - 0.5f;
            float pixelYCoordinate = (1f - v) * h - 0.5f;

            pixelXCoordinate = pixelXCoordinate < 0 ? w - pixelXCoordinate : pixelXCoordinate;
            pixelYCoordinate = pixelYCoordinate < 0 ? h - pixelYCoordinate : pixelYCoordinate;

            int x = (int)MathF.Floor(pixelXCoordinate);
            int y = (int)MathF.Floor(pixelYCoordinate);

            float pX = pixelXCoordinate - x;
            float pY = pixelYCoordinate - y;

            Vector2 px = new((float)(1 - pX), (float)pX);
            Vector2 py = new((float)(1 - pY), (float)pY);
            int adder;
            int index;
            float p;
            Vector4 filteredPixel = Vector4.Zero;

            for (int i = 0, j; i < 2; i++)
            {
                for (j = 0; j < 2; j++)
                {
                    p = px[i] * py[j];
                    if (p == 0)
                    {
                        continue;
                    }
                    Vector2Int coordinates = new()
                    {
                        X = (x + i) % w,
                        Y = (y + j) % w
                    };
                    adder = Math.Clamp(Math.Abs(coordinates.Y), 0, w * h) * w;
                    index = Math.Clamp(Math.Abs(coordinates.X) + adder, 0, w * h);
                    filteredPixel += pixels[index].ToColour() * p;
                }
            }
            return filteredPixel.ToVkColor();
        }
    }
}