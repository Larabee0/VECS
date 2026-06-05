#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"
#include "../pbr/pbr.glsl"

layout (location = 0) in vec2 inUV;
layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 0, binding = 1) uniform samplerCube samplerIrradiance;
layout (set = 0, binding = 2) uniform sampler2D samplerBRDFLUT;
layout (set = 0, binding = 3) uniform samplerCube prefilteredMap;

layout (set = 0, binding = 4) uniform sampler2D g_PositionIn;
layout (set = 0, binding = 5) uniform sampler2D g_NormalsIn;
layout (set = 0, binding = 6) uniform sampler2D g_AlbedoIn;
layout (set = 0, binding = 7) uniform sampler2D g_MaskIn;
layout (set = 0, binding = 8) uniform sampler2D ssao_blur_Source;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

#define ALBEDO pow(texture(g_AlbedoIn, UV).rgb, vec3(2.2))
#define UV vec2(inUV.x,1-inUV.y)

void main(){
    
    vec4 positionWorld = texture(g_PositionIn,UV).rgba;
    vec3 N = texture(g_NormalsIn,UV).rgb;
    
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;    
	vec3 V = normalize(cameraPosWorld - positionWorld.xyz);
	vec3 maskValue = texture(g_MaskIn,UV).rga; 
	vec2 metalRoughness = vec2(maskValue.r, 1 - maskValue.b);
    vec3 ambient = maskValue.ggg;
	float AO = texture(ssao_blur_Source,UV).r;
	ambient *= AO;
	outColour = vec4(ambientComponent(samplerIrradiance, prefilteredMap, samplerBRDFLUT, N, V, ALBEDO, ambient, metalRoughness),1.0);
}