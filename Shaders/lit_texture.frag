#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"
#include "lighting.glsl"

layout (location = 0) in vec4 fragColour;
layout (location = 1) in vec3 fragPosWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in vec2 fragUV;

layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) uniform LightingInfo {
	DirectionalLight directionalLight;
	int numPointLights;
	int numSpotLights;
} lighting;

layout (set = 0, binding = 1) readonly buffer PointLights {
	PointLight values[];
} pointLightBuffer;

layout (set = 0, binding = 2) readonly buffer SpotLights {
	SpotLight values[];
} spotLightBuffer;

layout(set = 0,binding = 3) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(set = 0,binding = 4) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 0, binding = 5) readonly buffer AdditionalCameraInfos {
	AdditionalCameraInfo values[];
} cameraPlanes;

layout (set = 0, binding = 6) readonly buffer OrthographicInfos {
	OrthographicInfo values[];
} orthographic;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(set = 1, binding = 3) uniform TexPorps {
	vec4 colour;
	vec4 specularColour;
	float tiling;
	float shininess;
} texProps;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

void main()
{
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
	vec3 normal = normalize(fragNormalWorld);
	vec3 viewDir = normalize(cameraPosWorld - fragPosWorld);
	
	vec3 diffuseTextureColour = texture(texSampler, fragUV).rgb;
	vec3 specularColour = texProps.specularColour.rgb;
	float shininess = texProps.shininess;

	vec3 result = CalcDirLight(lighting.directionalLight,normal, viewDir, shininess, diffuseTextureColour, diffuseTextureColour, specularColour);

	for(int i = 0; i < lighting.numPointLights; i++){
		PointLight pl = pointLightBuffer.values[i];
		
		result += CalcPointLight(pl, normal, fragPosWorld, viewDir, shininess, diffuseTextureColour, diffuseTextureColour, specularColour);
	}
	
	for(int i = 0; i < lighting.numSpotLights; i++){
		SpotLight sl = spotLightBuffer.values[i];
		
		result += CalcSpotLight(sl, normal, fragPosWorld, viewDir, shininess, diffuseTextureColour, diffuseTextureColour, specularColour);
	}

	outColour = vec4(result, 1.0);
	
}