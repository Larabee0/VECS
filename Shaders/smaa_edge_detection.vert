#version 460

#extension GL_ARB_shading_language_include : require
#include "smaa_defines.glsl"
#include "smaa_functions.glsl"

layout (location = 0) in vec2 aPosition;

layout (location = 0) out vec2 vTexCoord0;
layout (location = 1) out vec4 vOffset[3];

layout(set = 0, binding = 0) uniform TexelSize{
	vec2 value;
} texelSize;


/**
 * Edge Detection Vertex Shader
 */
void SMAAEdgeDetectionVS(vec2 texcoord,
                         out vec4 offset[3]) {
    offset[0] = fma(SMAA_RT_METRICS.xyxy, vec4(-1.0, 0.0, 0.0, -1.0), texcoord.xyxy);
    offset[1] = fma(SMAA_RT_METRICS.xyxy, vec4( 1.0, 0.0, 0.0,  1.0), texcoord.xyxy);
    offset[2] = fma(SMAA_RT_METRICS.xyxy, vec4(-2.0, 0.0, 0.0, -2.0), texcoord.xyxy);
}


void main()
{
    vTexCoord0 = vec2((aPosition + 1.0)/2.0);
    SMAAEdgeDetectionVS(vTexCoord0, vOffset);

    gl_Position = vec4(aPosition,0.0,1.0);
}