#version 460

layout (location = 0) in vec3 inPos;

layout (location = 0) out vec3 outColor;

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

struct ObjectBounds{
	vec4 bMin;
	vec4 bMax;
};

layout(std140, set = 1, binding = 1) readonly buffer ObjectBoundsBuffer{
	ObjectBounds bounds[];
}boundsBuffer;

layout(std140, set = 1, binding = 2) readonly buffer ObjectColourBuffer{
	vec4 colours[];
} colourBuffer;

out gl_PerVertex
{
	vec4 gl_Position;
};

void main() 
{
	outColor = colourBuffer.colours[gl_BaseInstance].rgb;
	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * matricesBuffer.matrices[gl_BaseInstance].modelMatrix * vec4(inPos,1);
}
