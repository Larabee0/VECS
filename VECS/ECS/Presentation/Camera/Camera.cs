using System.Numerics;

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
            InverseViewMatrix = Matrix4x4.Identity
        };

        public static int ComponentId { get; set; }

        public readonly int Id => ComponentId;

        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 InverseViewMatrix;
    }

    /// <summary>
    /// main camera tag
    /// </summary>
    public struct MainCamera : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }
}
