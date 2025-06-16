#version 460

layout (location = 0) in vec3 inPos;

layout (location = 0) out vec3 outPos;


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

struct ObjectMatrices{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
};

layout(std140, set = 1, binding = 0) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
}matricesBuffer;

layout(push_constant) uniform CubeView 
{
	mat4 proj;
	mat4 view;
	mat4 model;
} cube;
 
void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];
	gl_Position = cube.proj * cube.view * cube.model * objectMat.modelMatrix * vec4(inPos, 1.0);

	outPos = (objectMat.modelMatrix * vec4(inPos, 1.0)).xyz;
	//outPos = inPos;
}