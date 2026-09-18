
struct PositionInputs
{
    //vec3 positionWS;  // World space position (could be camera-relative)
    vec2 positionNDC; // Normalized screen coordinates within the viewport    : [0, 1) (with the half-pixel offset)
    uvec2  positionSS;  // Screen space pixel coordinates                       : [0, NumPixels)
    uvec2  tileCoord;   // Screen tile coordinates                              : [0, NumTiles)
    //float  deviceDepth; // Depth from the depth buffer                          : [0, 1] (typically reversed)
    //float  linearDepth; // View space Z coordinate                              : [Near, Far]
};

PositionInputs GetPositionInput(vec2 positionSS, vec2 invScreenSize, uvec2 tileCoord)   // Specify explicit tile coordinates so that we can easily make it lane invariant for compute evaluation.
{
    PositionInputs posInput;
    //posInput.positionWS = vec3(0);
    posInput.positionNDC = vec2(0);
    posInput.positionSS = uvec2(0);
    posInput.tileCoord = uvec2(0);
    //posInput.deviceDepth = 0;
    //posInput.linearDepth = 0;

    posInput.positionNDC = positionSS;

    // In case of compute shader an extra half offset is added to the screenPos to shift the integer position to pixel center.
    posInput.positionNDC.xy += vec2(0.5, 0.5);

    posInput.positionNDC *= invScreenSize;
    posInput.positionSS = uvec2(positionSS);
    posInput.tileCoord = tileCoord;

    return posInput;
}

vec2 ClampAndScaleUV(vec2 UV, vec2 texelSize, float numberOfTexels, vec2 scale)
{
    vec2 maxCoord = 1.0 - numberOfTexels * texelSize;
    return min(UV, maxCoord) * scale;
}

vec2 ClampAndScaleUVForBilinear(vec2 uv, vec2 texelSize){
    return ClampAndScaleUV(uv, texelSize, 0.5, vec2(1));
}

vec2 ClampAndScaleUVForBilinear(vec2 uv, vec2 texelSize, vec2 scale){
    return ClampAndScaleUV(uv, texelSize, 0.5, scale);
}

vec2 ClampAndScaleUVForBilinearPostProcessTexture(vec2 uv, vec2 texelSize){
    return ClampAndScaleUV(uv, texelSize, 0.5, vec2(1));
}


vec2 ClampAndScaleUVPostProcessTexture(vec2 UV, vec2 texelSize, float numberOfTexels)
{
    return ClampAndScaleUV(UV, texelSize, numberOfTexels, vec2(1));
}
