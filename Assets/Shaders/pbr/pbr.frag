#version 460
#extension GL_ARB_shading_language_include : require
#extension GL_EXT_nonuniform_qualifier : require
#include "../common_structures.glsl"
#include "../lighting.glsl"
#include "../shadows.glsl"
#include "pbr.glsl"

layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec3 fragViewPos;
layout (location = 4) in mat3 TBN;
layout (location = 7) in vec4 fragTangentWorld;
layout (location = 8) in vec3 fragNormalAlt;

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

layout(set = 0,binding = 4) readonly buffer CameraDatas {
	CameraData values[];
} cameraData;

layout (set = 1, binding = 2) uniform sampler2DArray dirShadow;
layout (set = 1, binding = 3) uniform sampler2DArray[] plShadow;
layout (set = 1, binding = 4) uniform sampler2D[] slShadow;

layout (set = 1, binding = 5) uniform samplerCube samplerIrradiance;
layout (set = 1, binding = 6) uniform sampler2D samplerBRDFLUT;
layout (set = 1, binding = 7) uniform samplerCube prefilteredMap;

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


#define PI 3.1415926535897932384626433832795
#define TILED_UV fragUV * texProps.tiling
#define ALBEDO pow(texture(albedoMap, TILED_UV).rgb, vec3(2.2))

void main() {
	vec3 N = calculateNormal(normalMap,TILED_UV,fragNormalWorld,fragTangentWorld.xyz);

	vec3 cameraPosWorld = cameraData.values[constants.cameraIndex].inverseViewMatrix[3].xyz;

	vec3 V = normalize(cameraPosWorld - fragPosWorld);
	
	vec2 metalRoughness;
	vec3 ambient;
	getMaskValues(texture(maskMap,TILED_UV), metalRoughness, ambient);

	vec3 color = ambientComponent(samplerIrradiance, prefilteredMap, samplerBRDFLUT, N, V, ALBEDO, ambient,metalRoughness);
    
	vec3 F0 = vec3(0.04); 
	F0 = mix(F0, ALBEDO, metalRoughness.r);
    
	vec3 Lo = vec3(0);
	int cascadeIndex = 0;
	float shadow= 1.0;
	for(int i = 0; i < lighting.numDirLights; i++) {
		DirectionalLight directionalLight = directionalLightBuffer.values[i];
		
		shadow = i < lighting.numDirLightsShadows ? DirShadows(
			dirShadow,
			directionalLight,
			fragPosWorld,
			fragViewPos,
			cascadeIndex) : 1.0;
		Lo += specularContribution(-directionalLight.direction.xyz, V, N, F0, ALBEDO, metalRoughness, directionalLight.specular.rgb)*shadow;
	}
	

    for(int i = 0; i < lighting.numPointLights; i++) {
		PointLight pl = pointLightBuffer.values[i];
		
    	float distance = length(pl.position.xyz - fragPosWorld);

		if(distance <= pl.farPlane){
			shadow= i < lighting.numPointLightShadows ? ShadowPlCalculationAlt(plShadow[i], fragPosWorld, cameraPosWorld, pl) : 1.0;
	    	vec3 L = normalize(pl.position.xyz - fragPosWorld);
	    	Lo += specularContribution(L, V, N, F0, ALBEDO, metalRoughness, pl.specular.rgb) * shadow;
			
		}

    }
    
    for(int i = 0; i < lighting.numSpotLights; i++) {
		SpotLight sl = spotLightBuffer.values[i];
		
    	float distance = length(sl.position.xyz - fragPosWorld);
		if(distance <= sl.farPlane) {

			float slShadow = i < lighting.numSpotLightShadows ? ShadowSlCalculationAlt(slShadow[i], fragPosWorld, cameraPosWorld, sl) : 1.0;			
	    	vec3 L = normalize(sl.position.xyz - fragPosWorld);
	    	Lo += specularContribution(L, V, N, F0, ALBEDO, metalRoughness, sl.specular.rgb * CalcSpotLightIntensity(sl, fragPosWorld)) * shadow;
		}
    }
	
	color += Lo;
    
	// Tone mapping
	color = Uncharted2Tonemap(color * texProps.exposure);
	//color = Uncharted2Tonemap(color * 1.5);
	color = color * (1.0 / Uncharted2Tonemap(vec3(11.2)));	
	// // Gamma correction
	color = pow(color, vec3(1.0 / texProps.gamma));
	//color = pow(color, vec3(1.0 / 1));
	
	outColour = vec4(color, 1.0);
	//outColour = vec4((vec3(1) * brdf.x + brdf.y), 1.0);
}