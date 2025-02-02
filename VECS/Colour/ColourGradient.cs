using System;
using System.Numerics;

namespace VECS
{
    public class ColourGradient
    {
        public GradientPoint[] gradientPoints;
        public AlphaPoint[] alphaPoints;
        
        public Vector4 Evaluate(float t, bool alphaIsTexture = false, int textureCount = 0)
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
                    firstColourIndex = i;
                }
                if (t <= gradientPoints[i].startPercent)
                {
                    secondColourIndex = i;
                    break;
                }
            }

            Vector4 a = gradientPoints[firstColourIndex].colour;
            Vector4 b = gradientPoints[secondColourIndex].colour;

            float localT = NumericsExtensions.InverseLerp(gradientPoints[firstColourIndex].startPercent, gradientPoints[secondColourIndex].startPercent, t);

            var colourOut = Vector4.Lerp(a, b, localT);


            if (alphaPoints != null && alphaPoints.Length > 0)
            {
                firstColourIndex = 0;
                secondColourIndex = 0;
                for (int i = 0; i < alphaPoints.Length; i++)
                {
                    if (t > alphaPoints[i].startPercent)
                    {
                        firstColourIndex = i;
                    }
                    if (t <= alphaPoints[i].startPercent)
                    {
                        secondColourIndex = i;
                        break;
                    }
                }

                localT = NumericsExtensions.InverseLerp(alphaPoints[firstColourIndex].startPercent, alphaPoints[secondColourIndex].startPercent, t);
                colourOut.W = NumericsExtensions.Lerp(alphaPoints[firstColourIndex].alpha, alphaPoints[secondColourIndex].alpha, localT);
                if (alphaIsTexture && textureCount > 0)
                {
                    colourOut.W = MathF.Round(colourOut.W);
                }

            }
            return colourOut;
        }

        public struct GradientPoint
        {
            public Vector4 colour;
            public float startPercent;
            
            public GradientPoint() { }
            public GradientPoint(Vector4 colour, float startPercent)
            {
                this.colour = colour;
                this.startPercent = startPercent;
            }

            public GradientPoint(string hexCode, float startPercent)
            {
                colour = ColourTypeConversion.FromHex(hexCode);
                this.startPercent = startPercent;
            }

            public GradientPoint(Random random, string hexCodeA, string hexCodeB, float startPercentA, float startPercentB)
            {
                colour = ColourTypeConversion.RandomColourFromRange(random, hexCodeA, hexCodeB);
                
                startPercent = NumericsExtensions.Lerp(startPercentA, startPercentB, random.NextSingle());
            }
        }
        public struct AlphaPoint
        {
            public float alpha;
            public float startPercent;

            public AlphaPoint() { }
            public AlphaPoint(float alpha, float startPercent)
            {
                this.alpha = alpha;
                this.startPercent = startPercent;
            }
            public AlphaPoint(Random random, float alpha, float startPercentA,float startPercentB)
            {
                this.alpha = alpha;
                startPercent = NumericsExtensions.Lerp(startPercentA, startPercentB, random.NextSingle());
            }
        }
    }
}
