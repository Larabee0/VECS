using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using TeximpNet;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class TextureLoader
    {
        public static string DefaultTexturePath => Path.Combine(Application.ExecutingDirectory, "Assets/Textures");
        
            
        public static string GetTextureInDefaultPath(string file)
        {
            return Path.Combine(DefaultTexturePath, file);
        }

        public static Surface[] LoadBulk(string[] filePaths)
        {
            Surface[] surfaces = new Surface[filePaths.Length];

            for (int i = 0; i < filePaths.Length; i++)
            {
                var surface = LoadToSurface(filePaths[i]);
                Debug.Assert(surface != null, string.Format("Texture loader returned null for: {0}", filePaths[i]));

                if (surface.ImageType != ImageType.Bitmap || surface.BitsPerPixel != 32)
                {
                    throw new Exception("Provided image surface is not in the right format");
                }
                surfaces[i] = surface;
            }
            return surfaces;
        }

        public static Surface LoadToSurface(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            Surface image = Surface.LoadFromFile(filePath);

            if (image == null)
            {
                return null;
            }

            if (image.ImageType != ImageType.Bitmap || image.BitsPerPixel != 32)
                image.ConvertTo(ImageConversion.To32Bits);

            return image;
        }

        public static unsafe GPUBuffer<Colour> CopySurfaceToStagingBuffer(Surface surface)
        {
            if (surface.ImageType != ImageType.Bitmap || surface.BitsPerPixel != 32 || !GraphicsDevice.Initialised)
            {
                throw new Exception("Provided image surface is not in the right format, or device is null");
            }

            var stagingBuffer = new GPUBuffer<Colour>((uint)surface.Width * (uint)surface.Height, VkBufferUsageFlags.TransferSrc, true,false,false);

            Colour* pMappedData;

            stagingBuffer.Map(&pMappedData);

            CopyColor(new IntPtr(pMappedData), surface);

            stagingBuffer.Unmap();


            return stagingBuffer;
        }

        public static unsafe GPUBuffer<Colour> CopySurfacesToStagingBuffer(Surface[] surfaces)
        {
            // validate texture dimentions are uniform
            uint width = (uint)surfaces[0].Width;
            uint height = (uint)surfaces[0].Height;
            for (int i = 1; i < surfaces.Length; i++)
            {
                if (surfaces[i].Width != width || surfaces[i].Height != height)
                {
                    throw new Exception("Texture array Texture dimention mismatch! All textures in the array must have the same dimentions!");
                }
            }

            var stagingBuffer = new GPUBuffer<Colour>((uint)surfaces[0].Width * (uint)surfaces[0].Height * (uint)surfaces.Length, VkBufferUsageFlags.TransferSrc, true, false, false);
            Colour[] singleImageColourData = new Colour[(int)(width * height)];
            uint singleImageSize = (uint)(width * height * sizeof(Colour));
            ulong copyStartOffset = 0;
            for (int i = 0; i < surfaces.Length; i++)
            {
                fixed (Colour* pSingleImageColourData = singleImageColourData)
                {
                    CopyColor(new IntPtr(pSingleImageColourData), surfaces[i]);
                    stagingBuffer.WriteToBuffer(pSingleImageColourData, singleImageSize, copyStartOffset);
                }
                copyStartOffset += singleImageSize;
            }

            return stagingBuffer;
        }
        
        private static unsafe void CopyColor(IntPtr dstPtr, Surface src)
        {
            int texelSize = Colour.SizeInBytes;

            int width = src.Width;
            int height = src.Height;
            int dstPitch = width * texelSize;
            bool swizzle = Surface.IsBGRAOrder;

            int pitch = Math.Min(src.Pitch, dstPitch);

            if (swizzle)
            {
                //For each scanline...
                for (int row = 0; row < height; row++)
                {
                    Colour* dPtr = (Colour*)dstPtr.ToPointer();
                    Colour* sPtr = (Colour*)src.GetScanLine(row).ToPointer();

                    //Copy each pixel, swizzle components...
                    for (int count = 0; count < pitch; count += texelSize)
                    {
                        Colour v = *sPtr++;
                        (v.B, v.R) = (v.R, v.B);
                        *dPtr++ = v;
                    }

                    //Advance to next scanline...
                    dstPtr += dstPitch;
                }
            }
            else
            {
                //For each scanline...
                for (int row = 0; row < height; row++)
                {
                    IntPtr sPtr = src.GetScanLine(row);

                    //Copy entirely...
                    MemoryHelper.CopyMemory(dstPtr, sPtr, pitch);

                    //Advance to next scanline...
                    dstPtr += dstPitch;
                }
            }
        }
    }
}