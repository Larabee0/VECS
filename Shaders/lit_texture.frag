#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"

layout (location = 0) in vec4 fragColour;
layout (location = 1) in vec3 fragPosWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in vec2 fragUV;

layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) uniform LightingInfo {
	vec4 ambientLightColour;
	vec4 ambientLightDir;
	int numPointLights;
} lighting;

layout (set = 0, binding = 1) readonly buffer PointLights {
	PointLight values[];
} pointLightBuffer;

layout(set = 0,binding = 2) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(set = 0,binding = 3) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 0, binding = 4) readonly buffer AdditionalCameraInfos {
	AdditionalCameraInfo values[];
} cameraPlanes;

layout (set = 0, binding = 5) readonly buffer OrthographicInfos {
	OrthographicInfo values[];
} orthographic;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(set = 1, binding = 3) uniform TexPorps {
	vec4 colour;
	float tiling;
} texProps;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

void main()
{
	vec3 norm = normalize(fragNormalWorld);
	vec3 lightDir = lighting.ambientLightDir.xyz;

	float diff = max(dot(norm,lightDir),0.0);
	vec3 diffuse = diff * lighting.ambientLightColour.xyz;

	float ambientStrength = lighting.ambientLightColour.w;

	vec3 ambient = ambientStrength * lighting.ambientLightColour.xyz;

	vec3 result = (ambient + diffuse) * vec3(1);
	
	outColour = vec4(result, 1.0);
	
}