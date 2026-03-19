using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class PreferredFormats
    {
        private readonly static VkFormat[] DEPTH_FORMATS =
        [
            VkFormat.D16Unorm,
            VkFormat.D32Sfloat
        ];

        private readonly static bool[] DEPTH_FORMATS_SUPPORTED = new bool[DEPTH_FORMATS.Length];

        private readonly static VkFormat[] DEPTH_STENCIL_FORMATS =
        [
            VkFormat.D16UnormS8Uint,
            VkFormat.D24UnormS8Uint,
            VkFormat.D32SfloatS8Uint
        ];

        private readonly static bool[] DEPTH_STENCIL_FORMATS_SUPPORTED = new bool[DEPTH_STENCIL_FORMATS.Length];

        public static VkFormat LOW_PRECISION_DEPTH_ONLY { get; private set; }
        public static VkFormat HIGH_PRECISION_DEPTH_ONLY { get; private set; }

        public static VkFormat LOW_PRECISION_DEPTH_STENCIL { get; private set; }
        public static VkFormat HIGH_PRECISION_DEPTH_STENCIL { get; private set; }

        public const VkFormat STENCIL_ONLY = VkFormat.S8Uint;

        internal static void UpdateDepthFormatPreferences()
        {
            for (int i = 0; i < DEPTH_STENCIL_FORMATS.Length; i++)
            {
                DEPTH_STENCIL_FORMATS_SUPPORTED[i] = QueryFormat(DEPTH_STENCIL_FORMATS[i], VkFormatFeatureFlags.DepthStencilAttachment);
            }

            for (int i = 0; i < DEPTH_FORMATS.Length; i++)
            {
                DEPTH_FORMATS_SUPPORTED[i] = QueryFormat(DEPTH_FORMATS[i], VkFormatFeatureFlags.DepthStencilAttachment);
            }



            for (int i = 0; i < DEPTH_STENCIL_FORMATS.Length; i++)
            {
                if (DEPTH_STENCIL_FORMATS_SUPPORTED[i])
                {
                    LOW_PRECISION_DEPTH_STENCIL = DEPTH_STENCIL_FORMATS[i];
                    break;
                }
            }

            for (int i = DEPTH_STENCIL_FORMATS.Length - 1; i >= 0; i--)
            {
                if (DEPTH_STENCIL_FORMATS_SUPPORTED[i])
                {
                    HIGH_PRECISION_DEPTH_STENCIL = DEPTH_STENCIL_FORMATS[i];
                    break;
                }
            }



            for (int i = 0; i < DEPTH_FORMATS.Length; i++)
            {
                if (DEPTH_FORMATS_SUPPORTED[i])
                {
                    LOW_PRECISION_DEPTH_ONLY = DEPTH_FORMATS[i];
                    break;
                }
            }

            for (int i = DEPTH_FORMATS.Length - 1; i >= 0; i--)
            {
                if (DEPTH_FORMATS_SUPPORTED[i])
                {
                    HIGH_PRECISION_DEPTH_ONLY = DEPTH_FORMATS[i];
                    break;
                }
            }
        }

        private static bool QueryFormat(VkFormat format, VkFormatFeatureFlags featureFlags)
        {
            GraphicsDevice.InstanceAPI.vkGetPhysicalDeviceFormatProperties(GraphicsDevice.PhysicalDevice,format, out VkFormatProperties formatProperties);
            return (formatProperties.optimalTilingFeatures & featureFlags) == featureFlags;
        }
    }
}
