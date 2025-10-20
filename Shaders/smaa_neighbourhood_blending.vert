#version 460

#extension GL_ARB_shading_language_include : require
#include "smaa_defines.glsl"
#include "smaa_functions.glsl"
layout (location = 0) in vec2 aPosition;

layout (location = 0) out vec2 vTexCoord0;
layout (location = 1) out vec4 vOffset;

layout(set = 0, binding = 0) uniform TexelSize{
	vec2 value;
} texelSize;

/**
 * Neighborhood Blending Vertex Shader
 */
void SMAANeighborhoodBlendingVS(vec2 texcoord,
                                out vec4 offset) {
    offset = fma(SMAA_RT_METRICS.xyxy, vec4( 1.0, 0.0, 0.0,  1.0), texcoord.xyxy);
}

void main() {
	vTexCoord0 = vec2((aPosition + 1.0) / 2.0);
	SMAANeighborhoodBlendingVS(vTexCoord0,vOffset);
    gl_Position = vec4(aPosition, 0.0, 1.0);
}