#version 460
layout (location = 0) in vec4 fragColour;
layout (location = 0) out vec4 outColour;

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


void main()
{
	outColour = fragColour;
}