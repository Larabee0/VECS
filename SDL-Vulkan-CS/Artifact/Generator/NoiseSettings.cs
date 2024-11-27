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
            return SimpleNosieFilter.Evaluate(this,point);
        }
    }

    public class RigidNoiseSettings : SimpleNoiseSettings
    {
        public float weightMultiplier = 0.8f;

        public override float Evaluate(Vector3 point)
        {
            return RigidNoiseFilter.Evaluate(this,point);
        }
    }
}
