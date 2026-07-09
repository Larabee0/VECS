#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"
#include "../pbr/pbr.glsl"

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

layout(set = 2, binding = 0) uniform TexPorps {
	vec4 colour;
	float tiling;
    float exposure;
    float gamma;
} texProps;

layout (set = 2, binding = 1) uniform sampler2D albedoMap;
layout (set = 2, binding = 2) uniform sampler2D normalMap;
layout (set = 2, binding = 3) uniform sampler2D maskMap;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

#define TILED_UV fragUV * texProps.tiling

float linearDepth(float depth, float nearPlane, float farPlane)
{
	float z = depth * 2.0f - 1.0f; 
	return (2.0f * nearPlane * farPlane) / (farPlane + nearPlane - z * (farPlane - nearPlane));	
}


void main(){
    vec3 normal = calculateNormal(normalMap,TILED_UV,fragNormalWorld,fragTangentWorld.xyz);
    normalsOut= normal;
    positionOut.w = linearDepth(gl_FragCoord.z, cameraPlanes.values[constants.cameraIndex].nearPlane,cameraPlanes.values[constants.cameraIndex].farPlane);
    positionOut.xyz = fragPosWorld;
    albedoOut.rgb = texture(albedoMap, TILED_UV).rgb;
    maskOut = texture(maskMap,TILED_UV).rgba;

}