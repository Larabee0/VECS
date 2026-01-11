#version 460

layout (location = 0) in vec3 inPos;

layout (location = 0) out vec3 outPos;

struct ObjectMatrices{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
};

layout (set = 1, binding = 0) uniform CubeConstants
{
	mat4 cubeProj;
	mat4 cubeModel;
} cubeConstant;

layout(std140, set = 1, binding = 1) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
}matricesBuffer;

layout(push_constant) uniform CubeView 
{
	mat4 viewCube;
} cube;
 
void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];
	gl_Position = cubeConstant.cubeProj * cube.viewCube * cubeConstant.cubeModel * objectMat.modelMatrix * vec4(inPos, 1.0);

	outPos = (objectMat.modelMatrix * vec4(inPos, 1.0)).xyz;
}