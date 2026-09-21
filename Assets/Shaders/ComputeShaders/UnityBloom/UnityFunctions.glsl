


vec3 QuadraticThreshold(vec3 color, float threshold, vec3 curve)
{
    // Pixel brightness
    float br = max(max(color.r, color.g), color.b);

    // Under-threshold part
    float rq = clamp(br - curve.x, 0.0, curve.y);
    rq = curve.z * rq * rq;
    float numerator = max(rq, br - threshold);
    
    float denominator = max(br, 1e-4);
    // Combine and apply the brightness response curve
    //color *= max(rq, br - threshold) / max(br, 1e-4);
    float multiplier = numerator / denominator;
    color *= vec3(multiplier);

    return color;
}


vec2 BSpline3MiddleLeft(vec2 x)
{
    return 0.16666667 + x * (0.5 + x * (0.5 - x * 0.5));
}

vec2 BSpline3MiddleRight(vec2 x)
{
    return 0.66666667 + x * (-1.0 + 0.5 * x) * x;
}

vec2 BSpline3Rightmost(vec2 x)
{
    return 0.16666667 + x * (-0.5 + x * (0.5 - x * 0.16666667));
}

void BicubicFilter(vec2 fracCoord, out vec2 weights[2], out vec2 offsets[2])
{
    vec2 r  = BSpline3Rightmost(fracCoord);
    vec2 mr = BSpline3MiddleRight(fracCoord);
    vec2 ml = BSpline3MiddleLeft(fracCoord);
    vec2 l  = 1.0 - mr - ml - r;

    weights[0] = r + mr;
    weights[1] = ml + l;
    offsets[0] = -1.0 + mr * (vec2(1.0)/weights[0]);
    offsets[1] =  1.0 + l * (vec2(1.0)/weights[1]);
}

vec4 SampleTexture2DBicubic(sampler2D tex, vec2 coord, vec4 texSize, vec2 maxCoord)
{
    vec2 xy = coord * texSize.xy + 0.5;
    vec2 ic = floor(xy);
    vec2 fc = fract(xy);

    vec2 weights[2], offsets[2];
    BicubicFilter(fc, weights, offsets);

    return weights[0].y * (weights[0].x * texture(tex, min((ic + vec2(offsets[0].x, offsets[0].y) - 0.5) * texSize.zw, maxCoord))  +
                           weights[1].x * texture(tex, min((ic + vec2(offsets[1].x, offsets[0].y) - 0.5) * texSize.zw, maxCoord))) +
           weights[1].y * (weights[0].x * texture(tex, min((ic + vec2(offsets[0].x, offsets[1].y) - 0.5) * texSize.zw, maxCoord))  +
                           weights[1].x * texture(tex, min((ic + vec2(offsets[1].x, offsets[1].y) - 0.5) * texSize.zw, maxCoord)));
}

float all(uvec2 a,uvec2 b){
    bvec2 r = bvec2(a.x <= b.x, a.y <= b.y);
    return all (r) ? 1 : 0;
}