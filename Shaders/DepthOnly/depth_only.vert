#version 460
layout (location = 0) in vec3 inPos;
layout (location = 1) in vec2 uv;

layout (location = 0) out vec2 geomUV;

struct ObjectMatrices{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
};

layout(std140, set = 0, binding = 0) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
}matricesBuffer;

void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];
    gl_Position = objectMat.modelMatrix * vec4(inPos, 1.0);
	geomUV = uv;
}  

