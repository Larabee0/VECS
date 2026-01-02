#version 460

#extension GL_ARB_shading_language_include : require
#include "smaa_defines.glsl"
#include "smaa_functions.glsl"
layout (location = 0) in vec2 aPosition;

layout (location = 0) out vec2 vTexCoord0;
layout (location = 1) out vec4 vOffset;

layout(push_constant) uniform TexelSize 
{
	vec4 value;
} texelSize;

/**
 * Neighborhood Blending Vertex Shader
 */
void SMAANeighborhoodBlendingVS(vec2 texcoord,
                                vec4 rtInfo,
                                out vec4 offset) {
    offset = fma(rtInfo.xyxy, vec4( 1.0, 0.0, 0.0,  1.0), texcoord.xyxy);
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

	SMAANeighborhoodBlendingVS(vTexCoord0, texelSize.value, vOffset);
}