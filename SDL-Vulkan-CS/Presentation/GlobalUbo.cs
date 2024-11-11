using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS
{
    public struct GlobalUbo
    {
        public unsafe static int SizeInBytes => (sizeof(Matrix4x4) * 3) + sizeof(Vector4) + (sizeof(PointLight) * PointLight.MAX_LIGHTS) + sizeof(int);

        public Matrix4x4 Projection;
        public Matrix4x4 View;
        public Matrix4x4 InverseView;
        public Vector4 AmbientLightColour;
        public PointLight[] PointLights = new PointLight[PointLight.MAX_LIGHTS];
        public int NumLights;

        public GlobalUbo()
        {
        }
    }
}
