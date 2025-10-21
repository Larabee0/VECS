#version 460

layout (location = 0) in vec3 position;


layout(set = 0,binding = 0) uniform CameraInfo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 projectionViewMatrix;	
	vec4 position;
	vec4 forward;
} cameraMain;

struct ObjectMatrices{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
};


layout(std140, set = 1, binding = 0) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
}matricesBuffer;

 
void main()
{
	vec4 positionWorld = matricesBuffer.matrices[gl_BaseInstance].modelMatrix * vec4(position, 1.0);
	gl_Position = cameraMain.projectionViewMatrix * positionWorld;
}