#version 460
#extension GL_ARB_shading_language_include : require
#extension GL_EXT_nonuniform_qualifier : require
#include "common_structures.glsl"
#include "lighting.glsl"
#include "shadows.glsl"

layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec3 fragViewPos;
layout (location = 4) in mat3 TBN;

layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) uniform LightingInfo {
	int numDirLights;
	int numDirLightsShadows;
	int numPointLights;
	int numPointLightShadows;
	int numSpotLights;
	int numSpotLightShadows;
} lighting;

layout(set = 0, binding = 1) readonly buffer DirectionalLights {
	DirectionalLight values[];
} directionalLightBuffer;

layout (set = 0, binding = 2) readonly buffer PointLights {
	PointLight values[];
} pointLightBuffer;

layout (set = 0, binding = 3) readonly buffer SpotLights {
	SpotLight values[];
} spotLightBuffer;

layout(set = 0,binding = 4) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(set = 0,binding = 5) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 0, binding = 6) readonly buffer AdditionalCameraInfos {
	AdditionalCameraInfo values[];
} cameraPlanes;

layout (set = 0, binding = 7) readonly buffer OrthographicInfos {
	OrthographicInfo values[];
} orthographic;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(set = 1, binding = 3) uniform TexPorps {
	vec4 colour;
	vec4 specularColour;
	float tiling;
	float shininess;
} texProps;

layout(set = 1, binding = 4) uniform sampler2DArray dirShadow;
layout(set = 1, binding = 5) uniform sampler2DArray plShadow[];
layout(set = 1, binding = 6) uniform sampler2D slShadow[];

layout(set = 1, binding = 7) uniform sampler2D normalSampler;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

void main()
{
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
	vec3 normal = normalize(fragNormalWorld);
	vec3 viewDir = normalize(cameraPosWorld - fragPosWorld);
	
		
	vec3 texNormal = vec3(texture(normalSampler, fragUV).xy, 0.0);
	texNormal.z = sqrt(1 - texNormal.x * texNormal.x - texNormal.y * texNormal.y);
	texNormal = TBN * normalize(texNormal * 2.0 - vec3(1.0));
	if(dot(texNormal, texNormal) > 0){
		normal = texNormal;
	}
    
	vec4 diff = texture(texSampler, fragUV);
	vec3 diffuseTextureColour = diff.rgb;
	vec3 specularColour = texProps.specularColour.rgb;
	float shininess = texProps.shininess;

	vec3 result = vec3(0);
	int cascadeIndex = 0;
	for(int i = 0; i < lighting.numDirLights; i++) {
		DirectionalLight directionalLight = directionalLightBuffer.values[i];
		
		float shadow = i < lighting.numDirLightsShadows ? DirShadows(
			dirShadow,
			directionalLight,
			fragPosWorld,
			fragViewPos,
			cascadeIndex) : 1.0;
		result += CalcDirLight(directionalLight, normal, viewDir, shininess, shadow, diffuseTextureColour, diffuseTextureColour, specularColour);
		//result = vec3(shadow);
	}
	for(int i = 0; i < lighting.numPointLights; i++) {
		PointLight pl = pointLightBuffer.values[i];

    	float distance = length(pl.position.xyz - fragPosWorld);

		if(distance <= pl.farPlane){
		    //float plShadow = i < lighting.numPointLightShadows ? FilterPLPCF(plShadow[i], fragPosWorld, cameraPosWorld, pl.position.xyz,pl.farPlane) : 1.0;
			float plShadow = i < lighting.numPointLightShadows ? ShadowPlCalculationAlt(plShadow[i], fragPosWorld, cameraPosWorld, pl) : 1.0;
			result += CalcPointLight(pl, normalize(fragNormalWorld), fragPosWorld, viewDir, shininess, plShadow, diffuseTextureColour, diffuseTextureColour, specularColour);
			//result += vec3(plShadow);
		}
	}
	
	for(int i = 0; i < lighting.numSpotLights; i++) {
		SpotLight sl = spotLightBuffer.values[i];
		
    	float distance = length(sl.position.xyz - fragPosWorld);
		if(distance <= sl.farPlane) {

			vec3 lightDirection = normalize(sl.position - fragPosWorld);
			float slShadow = i < lighting.numSpotLightShadows ? ShadowSlCalculationAlt(slShadow[i], fragPosWorld, cameraPosWorld, sl) : 1.0;
			result += CalcSpotLight(sl, normalize(fragNormalWorld), fragPosWorld, viewDir, shininess, slShadow, diffuseTextureColour, diffuseTextureColour, specularColour);
			//result = vec3(slShadow);
		}
	}

	outColour = vec4(result, diff.w);
	// if(shadow > 0)
	// {
	// 		outColour = vec4(result, 1.0);
	// }
	// else
	// {
	// 	outColour = vec4(0,0,0,1);
	// }
	// outColour = vec4(vec3(shadow), 1.0);

	// outColour = diff;

	// switch(cascadeIndex) {
	// 		case 0 : 
	// 			outColour.rgb *= vec3(1.0f, 0.25f, 0.25f);
	// 			break;
	// 		case 1 : 
	// 			outColour.rgb *= vec3(0.25f, 1.0f, 0.25f);
	// 			break;
	// 		case 2 : 
	// 			outColour.rgb *= vec3(0.25f, 0.25f, 1.0f);
	// 			break;
	// 		case 3 : 
	// 			outColour.rgb *= vec3(1.0f, 1.0f, 0.25f);
	// 			break;
	// 	}
}