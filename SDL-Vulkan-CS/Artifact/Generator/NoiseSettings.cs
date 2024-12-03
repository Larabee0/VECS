using System.Numerics;

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
                //centre = new(centre,0),
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

    public struct GlobalNoiseSettings
    {
        public int filterType;

        public float strength;
        public int numLayers;
        public float baseRoughness;
        public float roughness;
        public float persistence;
        //public Vector4 centre;
        public float offset;

        public float minValue;
        public int gradientWeight;
        public float gradientWeightMul;

        public int enabled;
        public int useFirstlayerAsMask;

        public float weightMultiplier;
    }
}
