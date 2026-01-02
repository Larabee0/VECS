#version 460

layout (location = 0) in vec3 inUVW;
layout (location = 0) out vec4 outFragColor;

layout (set = 0, binding = 0) uniform samplerCube samplerCubeMap;

void main() 
{
	outFragColor = texture(samplerCubeMap, inUVW);
}