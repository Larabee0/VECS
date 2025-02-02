#version 450

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 normal;
layout (location = 2) in float elevation;
layout (location = 3) in float biome;
	   
layout (location = 0) out vec3 fragColour;
layout (location = 1) out vec3 fragPosWorld;
layout (location = 2) out vec3 fragNormalWorld;
layout (location = 3) out float fragElevation;
layout (location = 4) out float fragBiome;
layout (location = 5) out vec3 fragPosObject;
layout (location = 6) out vec3 fragNormalObject;

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

layout(push_constant) uniform Push
{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
} push;

const vec3 DIRECTION_TO_LIGHT = normalize(vec3(1.0, 3.0, 1.0));
const float AMBIENT = 0.02;

void main()
{
	vec4 positionWorld =  push.modelMatrix * vec4(position, 1.0);

	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * positionWorld;

	fragNormalWorld = normalize(mat3(push.normalMatrix) * normal);
	
	float lightIntensity = AMBIENT + max(dot(fragNormalWorld, DIRECTION_TO_LIGHT), 0);
	fragPosWorld = positionWorld.xyz;
	fragColour = vec3(1);
	fragElevation = elevation;
	fragBiome = biome;
	fragPosObject = position;
	fragNormalObject = normal;
}