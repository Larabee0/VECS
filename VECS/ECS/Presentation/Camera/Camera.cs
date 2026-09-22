using System.Numerics;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    /// <summary>
    /// Camera data used by the <see cref="Presenter"/> class to set camera properties in the global uniform buffer.
    /// </summary>
    public struct Camera : IComponent
    {
        public static Camera Identity => new()
        {
            ProjectionMatrix = Matrix4x4.Identity,
            ViewMatrix = Matrix4x4.Identity,
            InverseViewMatrix = Matrix4x4.Identity,
            ClipNear = 0.3f,
            ClipFar = 1000f
        };

        public static int ComponentId { get; set; }

        public readonly int Id => ComponentId;
        
        [ReadOnlyInspector]
        public Matrix4x4 ProjectionMatrix;
        [ReadOnlyInspector]
        public Matrix4x4 ViewMatrix;
        [ReadOnlyInspector]
        public Matrix4x4 InverseViewMatrix;
        [ReadOnlyInspector]
        public CullModeFlags CullMode;
        [ReadOnlyInspector]
        public float ClipNear;
        [ReadOnlyInspector]
        public float ClipFar;
        [ReadOnlyInspector]
        public int CameraIndex;
    }

    public struct CameraOutputOverride : IComponent
    {
        public static CameraOutputOverride Identity => new()
        {
            ViewportRect = new(0,0,1,1),
            TargetTexture = 0,
            DisplayIndex = 0,
        };

        public static int ComponentId { get; set; }

        public readonly int Id => ComponentId;

        public Rect ViewportRect;
        public int TargetTexture;
        public int DisplayIndex;

        public uint CubemapFace;

        public OutputType OutputType;
    }

    public enum OutputType
    {
        Tex2D,
        TexCube
    }


    /// <summary>
    /// main camera tag
    /// </summary>
    public struct MainCamera : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }

    public struct CameraDirectionalShadowScale : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float Value;
    }
}
