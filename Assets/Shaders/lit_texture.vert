#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"
#include "lighting.glsl"

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 normal;
layout (location = 2) in vec4 tangent;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec3 fragPosWorld;
layout (location = 1) out vec3 fragNormalWorld;
layout (location = 2) out vec2 fragUV;
layout (location = 3) out vec3 fragViewPos;
layout (location = 4) out mat3 TBN;
layout (location = 7) out vec4 fragTangentWorld;
layout (location = 8) out vec3 fragNormalAlt;

layout(set = 0, binding = 0) uniform LightingInfo {
	int numDirLights;
	int numDirLightsShadows;
	int numPointLights;
	int numPointLightShadows;
	int numSpotLights;
	int numSpotLightShadows;
} lighting;

layout(set = 0,binding = 4) readonly buffer CameraDatas {
	CameraData values[];
} cameraData;


layout(std140, set = 1, binding = 0) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
}matricesBuffer;


layout(std140, set = 1, binding = 1) readonly buffer ObjectBoundsBuffer{
	ObjectBounds bounds[];
}boundsBuffer;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;



const mat4 biasMat = mat4(
	0.5, 0.0, 0.0, 0.0,
	0.0, 0.5, 0.0, 0.0,
	0.0, 0.0, 1.0, 0.0, 
	0.5, 0.5, 0.0, 1.0 );

const vec3 DIRECTION_TO_LIGHT = normalize(vec3(1.0, 3.0, 1.0));
const float AMBIENT = 0.02;
void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];

	vec4 positionWorld = objectMat.modelMatrix * vec4(position, 1.0);
	gl_Position = cameraData.values[constants.cameraIndex].projectionViewMatrix * positionWorld;
	
	fragNormalWorld = normalize(mat3(objectMat.normalMatrix) * normal);
	
	
	float lightIntensity = AMBIENT + max(dot(fragNormalWorld, DIRECTION_TO_LIGHT), 0);
	fragPosWorld = positionWorld.xyz;
	//fragPosDirLight = (biasMat * lighting.directionalLight.lightSpace * objectMat.modelMatrix) * vec4(position, 1.0);

	fragViewPos = (cameraData.values[constants.cameraIndex].viewMatrix * positionWorld).xyz;
	fragTangentWorld = vec4(mat3(objectMat.modelMatrix) * tangent.xyz, tangent.w);
	vec3 T = normalize(vec3(objectMat.normalMatrix * vec4(tangent.xyz,0)));
	vec3 N = normalize(vec3(objectMat.normalMatrix * vec4(normal,0)));
	T = normalize(T - dot(T,N) * N);
	vec3 B = cross(N, T);

	TBN = mat3(T, B, N);
	fragNormalAlt = mat3(objectMat.modelMatrix) * normal;
	fragUV = uv;
}