#version 460
#extension GL_ARB_shading_language_include : require
#extension GL_EXT_nonuniform_qualifier : require
#include "common_structures.glsl"
#include "cube_map_relfections.glsl"

layout (location = 0) in vec3 fragPosWS;
layout (location = 1) in vec3 fragNormalWS;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec3 fragViewPos;
layout (location = 4) in mat3 TBN;
layout (location = 7) in vec4 fragTangentWorld;
layout (location = 8) in vec3 fragNormalAlt;

layout (location = 0) out vec4 outColour;

layout(set = 0,binding = 1) readonly buffer RelfectionProbes {
	CubeRelfectionData values[];
} reflectionProbes;

layout (set = 0, binding = 2) uniform samplerCube relfectionMap;
layout (set = 0, binding = 3) uniform sampler2D planarMap;
layout (set = 0, binding = 4) uniform PlanarData{
	mat4 viewProjection;
} planarData;

layout(set = 0,binding = 5) readonly buffer CameraDatas {
	CameraData values[];
} cameraData;

layout(push_constant) uniform Constants{
    
	uint cameraIndex;
} constants;
vec4 PlanarReflection(){
	vec4 reflectionPosition = planarData.viewProjection * vec4(fragPosWS,1.0);
	vec2 reflectTexCoord;
    reflectTexCoord.x = reflectionPosition.x / reflectionPosition.w / 2.0 + 0.5;
    reflectTexCoord.y = reflectionPosition.y / reflectionPosition.w / 2.0 - 0.5;


	return texture(planarMap, reflectTexCoord);
}

vec4 CubeReflection(){
	vec3 cameraPosWorld = cameraData.values[constants.cameraIndex].inverseViewMatrix[3].xyz;    
	vec3 V = normalize(cameraPosWorld - fragPosWS.xyz);
	
	return CubeRelfection(relfectionMap, V, fragNormalWS, reflectionProbes.values[0]);
}
void main()
{
	outColour = CubeReflection();
}
