#version 460
#extension GL_ARB_shading_language_include : require
#extension GL_EXT_nonuniform_qualifier : require
#include "../common_structures.glsl"
#include "../lighting.glsl"
#include "../shadows.glsl"
#include "../pbr/pbr.glsl"

layout (location = 0) out vec4 outColour;

layout (set = 0, binding = 0) readonly buffer PointLights {
	PointLight values[];
} pointLightBuffer;

layout(set = 0,binding = 1) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(set = 0, binding = 2) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout(set = 0, binding = 3) uniform LightUniform{
    vec2 screenSize;
    mat4 lightMatrix;
    uint lightIndex;
    uint shadow;
} lightUniform;

layout (set = 1, binding = 0) uniform sampler2DArray[] plShadow;

layout (set = 2, binding = 0) uniform sampler2D g_PositionIn;
layout (set = 2, binding = 1) uniform sampler2D g_NormalsIn;
layout (set = 2, binding = 2) uniform sampler2D g_AlbedoIn;
layout (set = 2, binding = 3) uniform sampler2D g_MaskIn;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;


#define ALBEDO pow(texture(g_AlbedoIn, UV).rgb, vec3(2.2))
#define UV vec2(gl_FragCoord.x/lightUniform.screenSize.x,gl_FragCoord.y/lightUniform.screenSize.y)

void main(){
    vec4 positionWorld = texture(g_PositionIn,UV).rgba;
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;



    PointLight pl = pointLightBuffer.values[lightUniform.lightIndex];

    float distance = length(pl.position.xyz - positionWorld.xyz);

	if(distance > pl.farPlane){
        discard;
    }
    float shadow= lightUniform.shadow > 0 ? ShadowPlCalculationAlt(plShadow[lightUniform.lightIndex], positionWorld.xyz, cameraPosWorld, pl) : 1.0;
    if(shadow == 0 ){
        discard;
    }

    vec3 N = texture(g_NormalsIn,UV).rgb;
    vec3 viewPos = (cameraInfo.values[constants.cameraIndex].viewMatrix *  vec4(positionWorld.xyz,1.0)).xyz;
	vec3 V = normalize(cameraPosWorld - positionWorld.xyz);
	
	vec2 metalRoughness;
	vec3 ambient;
	getMaskValues(texture(g_MaskIn,UV), metalRoughness, ambient);
	vec3 F0 = mix(vec3(0.04), ALBEDO, metalRoughness.r);
    vec3 L = normalize(pl.position.xyz - positionWorld.xyz);
    vec3 Lo = specularContribution(L, V, N, F0, ALBEDO,metalRoughness, pl.specular.rgb) * shadow;
    
    outColour = vec4(Lo, 1.0);
    //outColour = vec4(1,1,1,1);
}