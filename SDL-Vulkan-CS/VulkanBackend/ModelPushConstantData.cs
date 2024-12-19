using System.Numerics;
using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS.VulkanBackend
{
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct ModelPushConstantData
    {
        public Matrix4x4 ModelMatrix;
        public Matrix4x4 NormalMatrix;
        public ModelPushConstantData(Matrix4x4 modelMatrix)
        {
            ModelMatrix = modelMatrix;
            if (Matrix4x4.Invert(modelMatrix, out NormalMatrix))
            {
                NormalMatrix = Matrix4x4.Transpose(NormalMatrix);
            }
        }
    }
}
