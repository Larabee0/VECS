using System.Numerics;
using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Globally accessible uniform buffer
    /// This holds things like the camera data and lights
    /// Point lights are defined up to a max lights value
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 244)]
    public unsafe struct GlobalUbo
    {
        public unsafe static int SizeInBytes => (sizeof(Matrix4x4) * 3) + sizeof(Vector4) + (sizeof(PointLight) * Presenter.MAX_LIGHTS) + sizeof(int);

        public Matrix4x4 Projection;
        public Matrix4x4 View;
        public Matrix4x4 InverseView;
        public Vector4 AmbientLightColour;

        public PointLight PointLights;
        public int NumLights;

    }
}
