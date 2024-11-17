using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.VulkanBackend
{
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct SimplePushConstantData
    {
        public Matrix4x4 ModelMatrix;
        public Matrix4x4 NormalMatrix;
        public SimplePushConstantData(Matrix4x4 modelMatrix)
        {
            ModelMatrix = modelMatrix;
            if(Matrix4x4.Invert(modelMatrix, out NormalMatrix))
            {
                NormalMatrix = Matrix4x4.Transpose(NormalMatrix);
            }
        }
    }
}
