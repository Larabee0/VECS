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
	vec3 diffuseLight = lighting.ambientLightColour.xyz * lighting.ambientLightColour.w;
	vec3 specularLight = vec3(0.0);
	vec3 surfaceNormal = normalize(fragNormalWorld);

	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
	vec3 viewDirection =normalize(cameraPosWorld - fragPosWorld);

	for(int i = 0; i < lighting.numPointLights; i++){
		PointLight light = pointLightBuffer.values[i];

		vec3 directionToLight = light.position.xyz - fragPosWorld;
		float attenuation = 1.0 / dot(directionToLight, directionToLight); // distance squared
		
		directionToLight = normalize(directionToLight);

		float cosAngIncidence = max(dot(surfaceNormal, directionToLight),0);
		vec3 intensity = light.colour.xyz * light.colour.w * attenuation;
		diffuseLight += intensity * cosAngIncidence;

		// spec

		vec3 halfAngle = normalize(directionToLight + viewDirection);
		float blinnTerm = dot(surfaceNormal, halfAngle);
		blinnTerm = clamp(blinnTerm, 0, 1);
		blinnTerm = pow(blinnTerm, 32.0); // higher values -> sharper highlight.
		specularLight += intensity * blinnTerm; 
	}

	vec4 textureColour = texture(texSampler,fragUV* texProps.tiling );
	
	// outColour = vec4(fragUV,0,1);
	//outColour = textureProperties.colour;
	outColour = textureColour * fragColour * texProps.colour;
	// outColour = vec4(1);
	//outColour = vec4(diffuseLight  * textureColour.xyz + specularLight * textureColour.xyz, 1.0);
	//outColour = vec4(diffuseLight  * fragColour, 1.0);
	
	//PointLight light = ubo.pointLights;
	//outColour =light.colour;
	//outColour =vec4(ubo.numLights,0,0,1);
	
}