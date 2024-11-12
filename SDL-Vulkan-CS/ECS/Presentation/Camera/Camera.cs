using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS
{
    public struct Camera : IComponent
    {
        public static Camera Identity => new()
        {
            ProjectionMatrix = Matrix4x4.Identity,
            ViewMatrix = Matrix4x4.Identity,
            InverseViewMatrix = Matrix4x4.Identity
        };

        public static int ComponentId { get; set; }

        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 InverseViewMatrix;
    }

    public struct MainCamera : IComponent
    {
        public static int ComponentId { get; set; }
    }
}
