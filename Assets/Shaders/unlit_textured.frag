#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"


layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec4 fragTangentWorld;

layout (location = 0) out vec4 positionOut;
layout (location = 1) out vec3 normalsOut;
layout (location = 2) out vec3 albedoOut;
layout (location = 3) out vec4 maskOut;

layout (set = 0, binding = 1) readonly buffer AdditionalCameraInfos {
	AdditionalCameraInfo values[];
} cameraPlanes;

layout(set = 2, binding = 0) uniform samplerCubeArray texSampler;

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
	vec3 diffuseTextureColour = texture(texSampler, vec4(fragUV,fragUV)).rgb;

    normalsOut= fragNormalWorld;
    positionOut.w = linearDepth(gl_FragCoord.z, cameraPlanes.values[constants.cameraIndex].nearPlane,cameraPlanes.values[constants.cameraIndex].farPlane);
    positionOut.xyz = fragPosWorld;
    albedoOut = vec3(diffuseTextureColour);
}