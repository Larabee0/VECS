using System;
using System.Numerics;

namespace SDL_Vulkan_CS.Artifact
{
    public class ColourGradient
    {
        public GradientPoint[] gradientPoints;

        public Vector4 Evaluate(float t)
        {
            int firstColourIndex = 0;
            int secondColourIndex = 0;
            if (gradientPoints.Length == 0)
            {
                return Vector4.One;
            }
            for (int i = 0; i < gradientPoints.Length; i++)
            {
                if (t > gradientPoints[i].startPercent)
                {
                    firstColourIndex = Math.Max(0, i - 1);
                }
                if (t <= gradientPoints[i].startPercent)
                {
                    secondColourIndex = i;
                    break;
                }
            }

            Vector4 a = gradientPoints[firstColourIndex].colour;
            Vector4 b = gradientPoints[secondColourIndex].colour;

            float localT = SystemNumericsExtensions.InverseLerp(gradientPoints[firstColourIndex].startPercent, gradientPoints[secondColourIndex].startPercent, t);

            return Vector4.Lerp(a, b, localT);
        }

        public struct GradientPoint
        {
            public Vector4 colour;
            public float startPercent;

            public GradientPoint(Vector4 colour, float startPercent)
            {
                this.colour = colour;
                this.startPercent = startPercent;
            }
        }
    }
}
