using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VECS
{
    public interface ITextureProvider
    {
        public int ImageCount { get; }
        public Texture First { get; set; }
        public bool AnyEmpty { get; }
        public bool AnyDisposed { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Texture GetTexture(int index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetTexture(Texture texture, int index);

        public void Dispose();
        VkImageMemoryBarrier2 GetImageLayoutBarrierAuto(VkImageLayout newLayout);
        void SetImageLayoutSilent(VkImageLayout newLayout);
        void SetImageLayoutAuto(VkCommandBuffer commandBuffer, VkImageLayout newLayout);
        void SetImageLayout(VkCommandBuffer commandBuffer, VkImageLayout newLayout, VkPipelineStageFlags2 srcStage, VkPipelineStageFlags2 dstStage);
        public void SetImageLayout(VkImageLayout newLayout, VkPipelineStageFlags2 srcStage, VkPipelineStageFlags2 dstStage);
        int GetImageLayoutBarrierAuto(Span<VkImageMemoryBarrier2> barrierDst, VkImageLayout newLayout);
    }

    public class SingleTexture : ITextureProvider
    {
        public int ImageCount => 1;
        public Texture First { get; set; }

        public bool AnyEmpty => First == null;
        public bool AnyDisposed => First == null || First.IsDisposed;

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
            bool updated = First != texture;
            First = index switch
            {
                0 => texture,
                _ => throw new IndexOutOfRangeException(),
            };
            return updated;
        }


        public void Dispose()
        {
            First.Dispose();
        }

        public VkImageMemoryBarrier2 GetImageLayoutBarrierAuto(VkImageLayout newLayout)
        {
            return First.GetImageLayoutBarrierAuto(newLayout);
        }

        public void SetImageLayoutSilent(VkImageLayout newLayout)
        {
            First.SetImageLayoutSilent(newLayout);
        }

        public void SetImageLayoutAuto(VkCommandBuffer commandBuffer, VkImageLayout newLayout)
        {
            First.SetImageLayoutAuto(commandBuffer, newLayout);
        }

        public void SetImageLayout(VkCommandBuffer commandBuffer, VkImageLayout newLayout, VkPipelineStageFlags2 srcStage, VkPipelineStageFlags2 dstStage)
        {
            First.SetImageLayout(commandBuffer, newLayout, srcStage, dstStage);
        }

        public void SetImageLayout(VkImageLayout newLayout, VkPipelineStageFlags2 srcStage, VkPipelineStageFlags2 dstStage)
        {
            First.SetImageLayout(newLayout, srcStage, dstStage);
        }

        public int GetImageLayoutBarrierAuto(Span<VkImageMemoryBarrier2> barrierDst, VkImageLayout newLayout)
        {
            if(newLayout != First.ImageLayout)
            {
                barrierDst[0] = GetImageLayoutBarrierAuto(newLayout);
                return 1;
            }
            return 0;
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
        public bool AnyEmpty
        {
            get
            {
                if (ArrayTextures.Length == 0)
                {
                    return true;
                }

                for (int i = 0; i < ArrayTextures.Length; i++)
                {
                    if (ArrayTextures[i] == null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool AnyDisposed
        {
            get
            {

                if (ArrayTextures.Length == 0)
                {
                    return true;
                }

                for (int i = 0; i < ArrayTextures.Length; i++)
                {
                    if (ArrayTextures[i] == null || ArrayTextures[i].IsDisposed)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

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
            bool updated = ArrayTextures[index] != texture;
            ArrayTextures[index] = texture;
            return updated;
        }

        public void Dispose()
        {
            for (int i = 0; i < ArrayTextures.Length; i++)
            {
                ArrayTextures[i]?.Dispose();
            }
        }

        public VkImageMemoryBarrier2 GetImageLayoutBarrierAuto(VkImageLayout newLayout)
        {
            return First.GetImageLayoutBarrierAuto(newLayout);
        }

        public void SetImageLayoutSilent(VkImageLayout newLayout)
        {
            for (int i = 0; i < ArrayTextures.Length; i++)
            {
                ArrayTextures[i].SetImageLayoutSilent(newLayout);
            }
        }

        public void SetImageLayoutAuto(VkCommandBuffer commandBuffer, VkImageLayout newLayout)
        {
            Span<VkImageMemoryBarrier2> barriers = stackalloc VkImageMemoryBarrier2[ArrayTextures.Length];
            for (int i = 0; i < ArrayTextures.Length; i++)
            {
                barriers[i] = ArrayTextures[i].GetImageLayoutBarrierAuto(newLayout);
                ArrayTextures[i].SetImageLayoutSilent(newLayout);
            }
            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, barriers);
        }

        public void SetImageLayout(VkCommandBuffer commandBuffer, VkImageLayout newLayout, VkPipelineStageFlags2 srcStage, VkPipelineStageFlags2 dstStage)
        {
            Span<VkImageMemoryBarrier2> barriers = stackalloc VkImageMemoryBarrier2[ArrayTextures.Length];
            for (int i = 0; i < ArrayTextures.Length; i++)
            {
                barriers[i] = ArrayTextures[i].GetImageLayoutBarrier(newLayout,ArrayTextures[i].GetSubresourceRange(), srcStage, dstStage);
                ArrayTextures[i].SetImageLayoutSilent(newLayout);
            }
            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, barriers);
        }

        public void SetImageLayout(VkImageLayout newLayout, VkPipelineStageFlags2 srcStage, VkPipelineStageFlags2 dstStage)
        {
            for (int i = 0; i < ArrayTextures.Length; i++)
            {
                ArrayTextures[i].SetImageLayout(newLayout, srcStage, dstStage);
            }
        }

        public int GetImageLayoutBarrierAuto(Span<VkImageMemoryBarrier2> barrierDst, VkImageLayout newLayout)
        {
            Debug.Assert(barrierDst.Length == ArrayTextures.Length);
            int j = 0;

            for (int i = 0; i < ArrayTextures.Length; i++)
            {
                var img = ArrayTextures[i];
                if(img.ImageLayout != newLayout)
                {
                    barrierDst[j] = img.GetImageLayoutBarrierAuto(newLayout);
                    j++;
                }
            }
            return j;
        }
    }
}

