#version 450

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 colour;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec3 fragColour;
layout (location = 1) out vec3 fragPosWorld;
layout (location = 2) out vec3 fragNormalWorld;
layout (location = 3) out vec2 fragUV;

struct PointLight {
		vec4 position; // ignore w
		vec4 colour; // w is intensity
	};
	
layout(set = 0,binding = 0) uniform GlobalUbo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 inverseViewMatrix;
	vec4 ambientLightColour;
	PointLight pointLights[10];
	int numLights;
} ubo;

void main() 
{
	vec4 positionWorld =  vec4(position, 1.0);
	//gl_Position = ubo.projectionMatrix * ubo.viewMatrix * positionWorld;
	gl_Position=  positionWorld;
	fragNormalWorld = normalize(normal);
	fragPosWorld = positionWorld.xyz;
	fragColour = colour;
	fragUV = uv;
}
