using System.Numerics;
using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    public enum FilterType { Simple, Rigid };

    public class SimpleNoiseSettings
    {
        public FilterType filterType;

        public float strength = 1;
        public int numLayers = 1;
        public float baseRoughness = 1;
        public float roughness = 2;
        public float persistence = 0.5f;
        public Vector3 centre;
        public float offset = 0;

        public float minValue = 0;
        public bool gradientWeight = true;
        public float gradientWeightMul = 0.1f;

        public bool enabled = true;
        public bool useFirstlayerAsMask = false;

        public virtual float Evaluate(Vector3 point)
        {
            return SimpleNosieFilter.Evaluate(this, point);
        }

        public virtual GlobalNoiseSettings GetSettings()
        {
            return new GlobalNoiseSettings()
            {
                filterType = (int)filterType,
                strength = strength,
                numLayers = numLayers,
                baseRoughness = baseRoughness,
                roughness = roughness,
                persistence = persistence,
                centre = (centre),
                offset = offset,
                minValue = minValue,
                gradientWeight = gradientWeight ? 1 : 0,
                gradientWeightMul = gradientWeightMul,
                enabled = enabled ? 1 : 0,
                useFirstlayerAsMask = useFirstlayerAsMask ? 1 : 0
            };
        }
    }

    public class RigidNoiseSettings : SimpleNoiseSettings
    {
        public float weightMultiplier = 0.8f;

        public override float Evaluate(Vector3 point)
        {
            return RigidNoiseFilter.Evaluate(this,point);
        }

        public override GlobalNoiseSettings GetSettings()
        {
            var settings = base.GetSettings();
            settings.weightMultiplier = weightMultiplier;
            return settings;
        }
    }


    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct GlobalNoiseSettings
    {
        public int filterType; // 64

        public float strength; // 60
        public int numLayers; // 56
        public float baseRoughness; // 52
        public float roughness; // 48
        public float persistence; // 44
        public Vector3 centre; // 40
        public float offset;  // 28

        public float minValue;  // 24
        public int gradientWeight; // 20
        public float gradientWeightMul; // 16

        public int enabled; // 12
        public int useFirstlayerAsMask; // 8

        public float weightMultiplier; // 4
    }
}
