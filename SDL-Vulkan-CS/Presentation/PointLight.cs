using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS
{
    public struct PointLight
    {
        public const int MAX_LIGHTS = 10;

        public Vector4 Position; // ignore w
        public Vector4 Colour; // w is intensity
    }
}
