#version 460
#extension GL_KHR_vulkan_glsl: enable

layout (location = 0) in vec3 position;

layout (location = 0) out vec4 fragColour;

struct PointLight {
	vec4 position; // ignore w
	vec4 colour; // w is intensity
};

layout(set = 0,binding = 0) uniform GlobalUbo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 inverseViewMatrix;
	vec4 ambientLightColour;
	int numLights;
	PointLight pointLights[10];
} ubo;

struct ObjectMatrices{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
};

layout(std140, set = 1, binding = 0) readonly buffer ObjectBuffer{
	ObjectMatrices matrices[];
} objectBuffer;

layout(std140, set = 1, binding = 1) readonly buffer ObjectColourBuffer{
	vec4 colours[];
} objectColourBuffer;

void main()
{
	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * objectBuffer.matrices[gl_BaseInstance].modelMatrix * vec4(position, 1.0);
	fragColour = objectColourBuffer.colours[gl_BaseInstance];
}