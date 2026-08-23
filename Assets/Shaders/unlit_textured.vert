#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"

layout (location = 0) in vec3 position;
layout (location = 1) in vec2 uv;

layout (location = 0) out vec4 fragColour;
layout (location = 1) out vec2 fragUV;

layout(set = 0,binding = 0) readonly buffer CameraDatas {
	CameraData values[];
} cameraData;

layout(std140, set = 1, binding = 0) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
}matricesBuffer;

layout(std140, set = 1, binding = 1) readonly buffer ObjectBoundsBuffer{
	ObjectBounds bounds[];
}boundsBuffer;

layout(std140, set = 1, binding = 2) readonly buffer ObjectColourBuffer{
	vec4 colours[];
} colourBuffer;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

void main()
{
	vec4 positionWorld = matricesBuffer.matrices[gl_BaseInstance].modelMatrix * vec4(position, 1.0);
	gl_Position = cameraData.values[constants.cameraIndex].projectionViewMatrix * positionWorld;
	fragColour = colourBuffer.colours[gl_BaseInstance];
    fragUV = uv;
}