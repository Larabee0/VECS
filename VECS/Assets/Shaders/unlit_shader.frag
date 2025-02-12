#version 450
#extension GL_KHR_vulkan_glsl: enable

layout (location = 0) in vec3 fragColour;
layout (location = 1) in vec3 fragPosWorld;

layout (location = 0) out vec4 outColour;




struct PointLight {
		vec4 position; // ignore w
		vec4 colour; // w is intensity
	};


layout(set = 0, binding = 0) uniform GlobalUbo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 inverseViewMatrix;
	vec4 ambientLightColour;
	PointLight pointLights;
	int numLights;
} ubo;

layout(set = 1, binding = 0) uniform sampler2D texSampler;

layout(push_constant) uniform Push
{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
} push;



void main()
{
	outColour = vec4(fragColour,1);
}