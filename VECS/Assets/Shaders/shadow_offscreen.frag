#version 460

layout (location = 0) out float outFragColor;

layout (location = 0) in vec3 inPos;

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

void main() 
{
	// Store distance to light as 32 bit float value
    vec3 lightVec = inPos - ubo.pointLights[0].position.xyz;
    outFragColor = length(lightVec);
}