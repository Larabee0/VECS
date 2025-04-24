#version 460

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 normal;
layout (location = 2) in vec2 uv;
	   
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

struct ObjectMatrices{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
	vec4 spherebounds;
	vec4 extents;
};
layout(std140, set = 1, binding = 7) readonly buffer ObjectBuffer{
	ObjectMatrices matrices[];
} objectBuffer;

const vec3 DIRECTION_TO_LIGHT = normalize(vec3(1.0, 3.0, 1.0));
const float AMBIENT = 0.02;

void main()
{
	ObjectMatrices objectMat = objectBuffer.matrices[gl_BaseInstance];
	vec4 positionWorld =  objectMat.modelMatrix * vec4(position, 1.0);

	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * positionWorld;

	fragNormalWorld = normalize(mat3(objectMat.normalMatrix) * normal);
	
	float lightIntensity = AMBIENT + max(dot(fragNormalWorld, DIRECTION_TO_LIGHT), 0);
	fragPosWorld = positionWorld.xyz;
	fragColour = vec3(1);
	fragElevation = uv.x;
	fragBiome = uv.y;
	fragPosObject = position;
	fragNormalObject = normal;
}