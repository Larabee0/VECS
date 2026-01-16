#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"
#include "lighting.glsl"

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 normal;
layout (location = 2) in vec2 uv;

layout (location = 0) out vec3 fragPosWorld;
layout (location = 1) out vec3 fragNormalWorld;
layout (location = 2) out vec2 fragUV;
layout (location = 3) out vec4 fragPosDirLight;

layout(set = 0, binding = 0) uniform LightingInfo {
	DirectionalLight directionalLight;
	int numPointLights;
	int numSpotLights;
} lighting;

layout(set = 0,binding = 3) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;


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

	vec4 positionWorld =objectMat.modelMatrix * vec4(position, 1.0);
	gl_Position = cameraInfo.values[constants.cameraIndex].projectionViewMatrix * positionWorld;
	
	fragNormalWorld = normalize(mat3(objectMat.normalMatrix) * normal);
	
	
	float lightIntensity = AMBIENT + max(dot(fragNormalWorld, DIRECTION_TO_LIGHT), 0);
	fragPosWorld = positionWorld.xyz;
	fragPosDirLight = (biasMat * lighting.directionalLight.lightSpace * objectMat.modelMatrix) * vec4(position, 1.0);

	fragUV = uv;
}