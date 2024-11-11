using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.Artifact
{
    public struct Camera : IComponent
    {
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 InverseViewMatrix;
    }

    public struct CameraPerspective : IComponent
    {
        public float FOV;
        public float ClipNear;
        public float ClipFar;
    }

    public struct CameraOrthographic : IComponent
    {
        public float width;
        public float height;
        public float ClipNear;
        public float ClipFar;
    }
}
