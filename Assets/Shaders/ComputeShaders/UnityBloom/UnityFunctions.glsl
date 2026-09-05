
struct PositionInputs
{
    vec3 positionWS;  // World space position (could be camera-relative)
    vec2 positionNDC; // Normalized screen coordinates within the viewport    : [0, 1) (with the half-pixel offset)
    uvec2  positionSS;  // Screen space pixel coordinates                       : [0, NumPixels)
    uvec2  tileCoord;   // Screen tile coordinates                              : [0, NumTiles)
    float  deviceDepth; // Depth from the depth buffer                          : [0, 1] (typically reversed)
    float  linearDepth; // View space Z coordinate                              : [Near, Far]
};

PositionInputs GetPositionInput(vec2 positionSS, vec2 invScreenSize, uvec2 tileCoord)   // Specify explicit tile coordinates so that we can easily make it lane invariant for compute evaluation.
{
    PositionInputs posInput;
    posInput.positionWS = vec3(0);
    posInput.positionNDC = vec2(0);
    posInput.positionSS = uvec2(0);
    posInput.tileCoord = uvec2(0);
    posInput.deviceDepth = 0;
    posInput.linearDepth = 0;

    posInput.positionNDC = positionSS;

    // In case of compute shader an extra half offset is added to the screenPos to shift the integer position to pixel center.
    posInput.positionNDC.xy += vec2(0.5, 0.5);

    posInput.positionNDC *= invScreenSize;
    posInput.positionSS = uvec2(positionSS);
    posInput.tileCoord = tileCoord;

    return posInput;
}


vec3 QuadraticThreshold(vec3 color, float threshold, vec3 curve)
{
    color.r = isnan(color.r) ? 0 : color.r;
    color.g = isnan(color.g) ? 0 : color.g;
    color.b = isnan(color.b) ? 0 : color.b;
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

vec2 ClampAndScaleUV(vec2 UV, vec2 texelSize, float numberOfTexels, vec2 scale)
{
    vec2 maxCoord = 1.0 - numberOfTexels * texelSize;
    return min(UV, maxCoord) * scale;
}

vec2 ClampAndScaleUVPostProcessTexture(vec2 UV, vec2 texelSize, float numberOfTexels)
{
    return ClampAndScaleUV(UV, texelSize, numberOfTexels, vec2(1));
}

vec2 ClampAndScaleUVForBilinearPostProcessTexture(vec2 uv, vec2 texelSize){
    //return ClampAndScaleUV(uv,_screenSize.zw,0.5,postProcessScale.xy);
    return ClampAndScaleUV(uv, texelSize, 0.5, vec2(1));
}

vec2 ClampAndScaleUVForBilinear(vec2 uv, vec2 texelSize){
    //return ClampAndScaleUV(uv,_screenSize.zw,0.5,postProcessScale.xy);
    return ClampAndScaleUV(uv, texelSize, 0.5, vec2(1));
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