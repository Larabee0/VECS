using System.Numerics;

namespace SDL_Vulkan_CS.Artifact
{   

    // Description : Array and textureless GLSL 2D/3D/4D simplex 
    //               noise functions.
    //      Author : Ian McEwan, Ashima Arts.
    //  Maintainer : stegu
    //     Lastmod : 20201014 (stegu)
    //     License : Copyright (C) 2011 Ashima Arts. All rights reserved.
    //               Distributed under the MIT License. See LICENSE file.
    //               https://github.com/ashima/webgl-noise
    //               https://github.com/stegu/webgl-noise
    // 
    /// <summary>
    /// Conversion of https://github.com/ashima/webgl-noise/blob/6abed1e77ed1e18b181627c35f688eb30c9fe75e/src/noise3Dgrad.glsl#L30
    /// to C# by William Vickers Hastings
    /// </summary>
    public static class noise3Dgrad
    {
        private static Vector2 C = new(1.0f / 6.0f, 1.0f / 3.0f);
        private static Vector4 D = new(0.0f, 0.5f, 1.0f, 2.0f);

        private static Vector3 mod289(Vector3 x)
        {
            return x - NumericsExtensions.Floor(x * (1.0f / 289.0f)) * 289.0f;
        }

        private static Vector4 mod289(Vector4 x)
        {
            return x - NumericsExtensions.Floor(x * (1.0f / 289.0f)) * 289.0f;
        }

        private static Vector4 permute(Vector4 x)
        {
            return mod289(((x * 34.0f) + new Vector4(10.0f)) * x);
        }

        private static Vector4 taylorInvSqrt(Vector4 r)
        {
            return new Vector4(1.79284291400159f) - 0.85373472095314f * r;
        }

        public static float snoise(Vector3 v, out Vector3 gradient)
        {
            // First corner
            Vector3 i = NumericsExtensions.Floor(v + new Vector3(Vector3.Dot(v, new Vector3(C.Y))));
            Vector3 x0 = v - i + new Vector3(Vector3.Dot(i, new Vector3(C.X)));

            
            // Other corners
            Vector3 g = NumericsExtensions.Step(new Vector3(x0.Y, x0.Z, x0.X), x0);
            Vector3 l = Vector3.One - g;
            Vector3 i1 = Vector3.Min(g, new Vector3(l.Z,l.X,l.Y));
            Vector3 i2 = Vector3.Max(g, new Vector3(l.Z,l.X,l.Y));


            Vector3 x1 = x0 - i1 + new Vector3(C.X);
            Vector3 x2 = x0 - i2 + new Vector3(C.Y);
            Vector3 x3 = x0 - new Vector3(D.Y);

            i = mod289(i);

            Vector4 p = permute(permute(permute(
                  new Vector4(i.Z) + new Vector4(0.0f, i1.Z, i2.Z, 1.0f))
                + new Vector4(i.Y) + new Vector4(0.0f, i1.Y, i2.Y, 1.0f))
                + new Vector4(i.X) + new Vector4(0.0f, i1.X, i2.X, 1.0f));

            // Gradients: 7x7 points over a square, mapped onto an octahedron.
            // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
            float n_ = 0.142857142857f; // 1.0/7.0
            Vector3 ns = n_ * new Vector3(D.W,D.Y,D.Z) - new Vector3(D.X,D.Z,D.X);

            Vector4 j = p - 49.0f * NumericsExtensions.Floor(p * ns.Z * ns.Z);


            Vector4 x_ = NumericsExtensions.Floor(j * ns.Z);
            Vector4 y_ = NumericsExtensions.Floor(j - 7.0f * x_);

            Vector4 x = x_ * ns.X + new Vector4(ns.Y);
            Vector4 y = y_ * ns.X + new Vector4(ns.Y);
            Vector4 h = Vector4.One - Vector4.Abs(x) - Vector4.Abs(y);

            Vector4 b0 = new(x.X, x.Y, y.X, y.Y);
            Vector4 b1 = new(x.Z, x.W, y.Z, y.W);

            Vector4 s0 = NumericsExtensions.Floor(b0) * 2.0f + Vector4.One;
            Vector4 s1 = NumericsExtensions.Floor(b1) * 2.0f + Vector4.One;
            Vector4 sh = -NumericsExtensions.Step(h, Vector4.Zero);

            Vector4 a0 = new Vector4(b0.X, b0.Z, b0.Y, b0.W) + new Vector4(s0.X, s0.Z, s0.Y, s0.W) * new Vector4(sh.X, sh.X, sh.Y, sh.Y);
            Vector4 a1 = new Vector4(b1.X, b1.Z, b1.Y, b1.W) + new Vector4(s1.X, s1.Z, s1.Y, s1.W) * new Vector4(sh.Z, sh.Z, sh.W, sh.W);


            Vector3 p0 = new(a0.X, a0.Y, h.X);
            Vector3 p1 = new(a0.Z, a0.W, h.Y);
            Vector3 p2 = new(a1.X, a1.Y, h.Z);
            Vector3 p3 = new(a1.Z, a1.W, h.W);

            //Normalise gradients
            Vector4 norm = taylorInvSqrt(new Vector4(Vector3.Dot(p0, p0), Vector3.Dot(p1, p1), Vector3.Dot(p2, p2), Vector3.Dot(p3, p3)));
            p0 *= norm.X;
            p1 *= norm.Y;
            p2 *= norm.Z;
            p3 *= norm.W;

            // Mix final noise value
            Vector4 m = Vector4.Max(new Vector4(0.5f) - new Vector4(
                Vector3.Dot(x0, x0),
                Vector3.Dot(x1, x1),
                Vector3.Dot(x2, x2),
                Vector3.Dot(x3, x3)),
                Vector4.Zero);

            Vector4 m2 = m * m;
            Vector4 m4 = m2 * m2;
            Vector4 pdotx = new(Vector3.Dot(p0, x0), Vector3.Dot(p1, x1), Vector3.Dot(p2, x2), Vector3.Dot(p3, x3));

            // Determine noise gradient
            Vector4 temp = m2 * m * pdotx;
            gradient = -8.0f * (temp.X * x0 + temp.Y * x1 + temp.Z * x2 + temp.W * x3);
            gradient += m4.X * p0 + m4.Y * p1 + m4.Z * p2 + m4.W * p3;
            gradient *= 105.0f;

            return 105.0f * Vector4.Dot(m4, pdotx);
        }
    }
}
