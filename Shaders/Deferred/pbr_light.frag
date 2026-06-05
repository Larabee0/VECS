#version 460
#extension GL_ARB_shading_language_include : require
#extension GL_EXT_nonuniform_qualifier : require
#include "../common_structures.glsl"
#include "../lighting.glsl"
#include "../shadows.glsl"
#include "../pbr/pbr.glsl"

layout (location = 0) in vec2 inUV;
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

layout(set = 0, binding = 5) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout(set = 0, binding = 6) uniform ScreenSize{
    vec2 value;
} screenSize;

layout (set = 1, binding = 0) uniform sampler2DArray dirShadow;
layout (set = 1, binding = 1) uniform sampler2DArray[] plShadow;
layout (set = 1, binding = 2) uniform sampler2D[] slShadow;

layout (set = 2, binding = 0) uniform sampler2D g_PositionIn;
layout (set = 2, binding = 1) uniform sampler2D g_NormalsIn;
layout (set = 2, binding = 2) uniform sampler2D g_AlbedoIn;
layout (set = 2, binding = 3) uniform sampler2D g_MaskIn;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;


#define ALBEDO pow(texture(g_AlbedoIn, UV).rgb, vec3(2.2))
#define UV vec2(gl_FragCoord.x/screenSize.value.x,gl_FragCoord.y/screenSize.value.y)

void main(){
    vec4 positionWorld = texture(g_PositionIn,UV).rgba;
    vec3 N = normalize(texture(g_NormalsIn,UV).rgb);
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
    vec3 viewPos = (cameraInfo.values[constants.cameraIndex].viewMatrix *  vec4(positionWorld.xyz,1.0)).xyz;
	vec3 V = normalize(cameraPosWorld - positionWorld.xyz);
    vec3 Lo = vec3(0);
	int cascadeIndex = 0;
	float shadow= 1.0;
	vec3 F0 = vec3(0.04); 
	vec2 metalRoughness;
	vec3 ambient;
	getMaskValues(texture(g_MaskIn,UV), metalRoughness, ambient);
	F0 = mix(F0, ALBEDO, metalRoughness.r);

    for(int i = 0; i < lighting.numDirLights; i++) {
		DirectionalLight directionalLight = directionalLightBuffer.values[i];
		
		shadow = i < lighting.numDirLightsShadows ? DirShadows(
			dirShadow,
			directionalLight,
			positionWorld.xyz,
			viewPos,
			cascadeIndex) : 1.0;
		Lo += specularContribution(-directionalLight.direction.xyz, V, N, F0, ALBEDO,metalRoughness, directionalLight.specular.rgb)*shadow;
	}
    
    //for(int i = 0; i < lighting.numPointLights; i++) {
	//	PointLight pl = pointLightBuffer.values[i];
	//	
    //	float distance = length(pl.position.xyz - positionWorld.xyz);
//
	//	if(distance <= pl.farPlane){
	//		shadow= i < lighting.numPointLightShadows ? ShadowPlCalculationAlt(plShadow[i], positionWorld.xyz, cameraPosWorld, pl) : 1.0;
	//    	vec3 L = normalize(pl.position.xyz - positionWorld.xyz);
	//    	Lo += specularContribution(L, V, N, F0, ALBEDO,metalRoughness, pl.specular.rgb) * shadow;
	//		
	//	}
//
    //}
    //
    //for(int i = 0; i < lighting.numSpotLights; i++) {
	//	SpotLight sl = spotLightBuffer.values[i];
	//	
    //	float distance = length(sl.position.xyz - positionWorld.xyz);
	//	if(distance <= sl.farPlane) {
//
	//		float slShadow = i < lighting.numSpotLightShadows ? ShadowSlCalculationAlt(slShadow[i], positionWorld.xyz, cameraPosWorld, sl) : 1.0;			
	//    	vec3 L = normalize(sl.position.xyz - positionWorld.xyz);
	//    	Lo += specularContribution(L, V, N, F0,ALBEDO,  metalRoughness, sl.specular.rgb * CalcSpotLightIntensity(sl, positionWorld.xyz)) * shadow;
	//	}
    //}
    outColour = vec4(Lo, 0.0);
}