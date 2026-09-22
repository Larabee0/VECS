#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"


layout (location = 0) in vec4 fragColour;
layout (location = 1) in vec2 fragUV;

layout (location = 0) out vec4 colourOut;

layout (set = 0, binding = 0) readonly buffer CameraDatas {
	CameraData values[];
} cameraData;

layout(set = 2, binding = 0) uniform sampler2D texSampler;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

float linearDepth(float depth, float nearPlane, float farPlane)
{
	float z = depth * 2.0f - 1.0f; 
	return (2.0f * nearPlane * farPlane) / (farPlane + nearPlane - z * (farPlane - nearPlane));	
}

void main()
{
    colourOut = texture(texSampler, vec2(fragUV.x, 1.0-fragUV.y));
	//colourOut = vec4(fragUV,0,1);
}