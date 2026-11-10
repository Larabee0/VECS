#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 normal;
layout (location = 2) in vec4 tangent;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec3 fragPosWorld;
layout (location = 1) out vec3 fragNormalWorld;
layout (location = 2) out vec2 fragUV;
layout (location = 3) out vec4 fragTangentWorld;

layout(std140, set = 0, binding = 0) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
}matricesBuffer;

layout (set = 1, binding = 0) readonly buffer CameraDatas {
	CameraData values[];
} cameraData;


layout(push_constant) uniform Constants{
	uint cameraIndex;
	int taskOffset;
    int taskCount;
} instanceInfo;


void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];

	vec4 positionWorld = objectMat.modelMatrix * vec4(position, 1.0);
	gl_Position = cameraData.values[instanceInfo.cameraIndex].projectionViewMatrix * positionWorld;
		
	fragUV = uv;
	fragPosWorld = positionWorld.xyz;
	fragNormalWorld = normalize(mat3(objectMat.normalMatrix) * normal);	
	fragTangentWorld = vec4(mat3(objectMat.modelMatrix) * tangent.xyz, tangent.w);
}