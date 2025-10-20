#version 460

#extension GL_ARB_shading_language_include : require
#include "smaa_defines.glsl"
#include "smaa_functions.glsl"
layout (location = 0) in vec2 aPosition;

layout (location = 0) out vec2 vTexCoord0;
layout (location = 1) out vec2 vPixCoord0;
layout (location = 2) out vec4 vOffset[3];

layout(set = 0, binding = 0) uniform TexelSize{
	vec2 value;
} texelSize;

/**
 * Blend Weight Calculation Vertex Shader
 */
void SMAABlendingWeightCalculationVS(vec2 texcoord,
                                     out vec2 pixcoord,
                                     out vec4 offset[3]) {
    pixcoord = texcoord * SMAA_RT_METRICS.zw;

    // We will use these offsets for the searches later on (see @PSEUDO_GATHER4):
    offset[0] = fma(SMAA_RT_METRICS.xyxy, vec4(-0.25, -0.125,  1.25, -0.125), texcoord.xyxy);
    offset[1] = fma(SMAA_RT_METRICS.xyxy, vec4(-0.125, -0.25, -0.125,  1.25), texcoord.xyxy);

    // And these for the searches, they indicate the ends of the loops:
    offset[2] = fma(SMAA_RT_METRICS.xxyy,
                    vec4(-2.0, 2.0, -2.0, 2.0) * float(SMAA_MAX_SEARCH_STEPS),
                    vec4(offset[0].xz, offset[1].yw));
}

void main() {
	vTexCoord0 = vec2((aPosition + 1.0) / 2.0);
    SMAABlendingWeightCalculationVS(vTexCoord0,vPixCoord0,vOffset);

    gl_Position = vec4(aPosition, 0.0, 1.0);
}