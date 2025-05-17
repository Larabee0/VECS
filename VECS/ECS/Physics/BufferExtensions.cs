using System;
using System.Runtime.CompilerServices;
using BepuUtilities.Memory;

namespace VECS
{
    public static class BufferExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void FillBuffer<T>(this Buffer<T> buffer, T value) where T : unmanaged
        {
            FillBuffer(buffer, value, 0, buffer.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void FillBuffer<T>(this Buffer<T> buffer, T value, int length) where T : unmanaged
        {
            FillBuffer(buffer, value, 0, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void FillBuffer<T>(this Buffer<T> buffer, T value, int startIndex, int length) where T : unmanaged
        {
            IntPtr intPtr = new(buffer.Memory);
            intPtr = IntPtr.Add(intPtr, sizeof(T) * startIndex);
            Span<T> span = new(intPtr.ToPointer(), Math.Min(buffer.Length- startIndex, length));
            span.Fill(value);
        }
    }
}
