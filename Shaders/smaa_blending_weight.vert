#version 460

#extension GL_ARB_shading_language_include : require
#include "smaa_defines.glsl"
#include "smaa_functions.glsl"
layout (location = 0) in vec2 aPosition;

layout (location = 0) out vec2 vTexCoord0;
layout (location = 1) out vec2 vPixCoord0;
layout (location = 2) out vec4 vOffset[3];

layout(push_constant) uniform TexelSize 
{
	vec4 value;
} texelSize;


/**
 * Blend Weight Calculation Vertex Shader
 */
void SMAABlendingWeightCalculationVS(vec2 texcoord,
                                     vec4 rtInfo,
                                     out vec2 pixcoord,
                                     out vec4 offset[3]) {
    pixcoord = texcoord * rtInfo.zw;

    // We will use these offsets for the searches later on (see @PSEUDO_GATHER4):
    offset[0] = fma(rtInfo.xyxy, vec4(-0.25, -0.125,  1.25, -0.125), texcoord.xyxy);
    offset[1] = fma(rtInfo.xyxy, vec4(-0.125, -0.25, -0.125,  1.25), texcoord.xyxy);

    // And these for the searches, they indicate the ends of the loops:
    offset[2] = fma(rtInfo.xxyy,
                    vec4(-2.0, 2.0, -2.0, 2.0) * float(SMAA_MAX_SEARCH_STEPS),
                    vec4(offset[0].xz, offset[1].yw));
}

void main()
{
    vec2 vertexBase;

    if(gl_VertexIndex == 0){
        vertexBase = vec2(-1.0);
    }
    else if(gl_VertexIndex == 1.0){
        vertexBase = vec2(-1.0,3.0);
    }
    else{
        vertexBase = vec2(3.0,-1.0);
    }

    gl_Position = vec4(vertexBase, 0.0, 1.0);
    
    vTexCoord0 = clamp(vertexBase,vec2(0.0),vec2(1.0))*2.0;
    vTexCoord0.y = 1.0 - vTexCoord0.y;

    SMAABlendingWeightCalculationVS(vTexCoord0, texelSize.value, vPixCoord0, vOffset);
}