#version 460
#extension GL_ARB_shading_language_include : require
#include "lighting.glsl"

layout (location = 0) out float outFragColor;

layout (location = 0) in vec3 inPos;

layout(set = 0, binding = 0) uniform LightingInfo {
	DirectionalLight directionalLight;
	int numPointLights;
	int numSpotLights;
} lighting;

layout (set = 0, binding = 1) readonly buffer PointLights{
	PointLight values[];
} pointLightBuffer;

void main() 
{
	vec3 lightVec;
	if(lighting.numPointLights == 0)
	{
		lightVec = lighting.directionalLight.ambient.xyz;
	}
	else{
		lightVec = inPos - pointLightBuffer.values[0].position.xyz;
	}
	// Store distance to light as 32 bit float value
    outFragColor = length(lightVec);
}