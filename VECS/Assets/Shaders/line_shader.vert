#version 460

layout (location = 0) in vec3 position;
	   
layout (location = 0) out vec3 fragColour;

struct PointLight {
	vec4 position; // ignore w
	vec4 colour; // w is intensity
};

layout(set = 0, binding = 0) uniform GlobalUbo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 inverseViewMatrix;
	vec4 ambientLightColour;
	int numLights;
	PointLight pointLights[10];
} ubo;

layout(push_constant) uniform Push
{
	mat4 modelMatrix; // project * view * model
} push;

void main()
{

	vec4 positionWorld =  push.modelMatrix * vec4(position, 1.0);
	
	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * positionWorld;

	
	fragColour = vec3(1,0,0);
}