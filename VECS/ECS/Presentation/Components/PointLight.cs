using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct PointLight : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;

        public float Constant;
        public float Linear;
        public float Quadratic;
        public float Range;
    }

    public enum ShadowUpdate
    {
        Always,
        OnDemand,
        Never
    }

    public enum ShadowMapResolution
    {
        TwoFiftySix,
        FiveTwelve,
        TenTwentyFour,
        TwentyFourtyEight,
        FouryNinteySix,
        EightOneNineTwo
    }

    public static class ShadowMapResolutionExtension
    {
        public static int GetResolution(this ShadowMapResolution resolution) => resolution switch
        {
            ShadowMapResolution.FiveTwelve => 512,
            ShadowMapResolution.TenTwentyFour => 1024,
            ShadowMapResolution.TwentyFourtyEight => 2048,
            ShadowMapResolution.FouryNinteySix => 4096,
            ShadowMapResolution.EightOneNineTwo => 8192,
            _ => 256,
        };
    }

    public struct UpdateShadow : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }

    public struct UpdatePointLight : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }

    public struct ShadowInfo : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public ShadowUpdate UpdateBehaviour;
        public int Resolution;
    }

    public struct ShadowImage : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int ShadowTextureId;
    }

    public struct PointLightFrameInfo : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int PointLightCount;
        public int PointLightShadowCount;
    }
}
