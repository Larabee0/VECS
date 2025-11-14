#version 460

layout (location = 0) in vec3 inPos;
layout (location = 1) in vec3 inNormal;
layout (location = 2) in vec4 inTangent;
layout (location = 3) in vec2 inUV;
	   
layout (location = 0) out vec3 outNormal;
layout (location = 1) out vec3 outColor;
layout (location = 2) out vec2 outUV;
layout (location = 3) out vec3 outViewVec;
layout (location = 4) out vec3 outLightVec;
layout (location = 5) out vec4 outTangent;

struct PointLight {
	vec4 position; // ignore w
	vec4 colour; // w is intensity
};

layout(set = 0,binding = 0) uniform CameraInfo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 projectionViewMatrix;	
	vec4 position;
	vec4 forward;
} cameraMain;

layout(set = 0, binding = 1) uniform LightingInfo {
	vec4 ambientLightColour;
	vec4 ambientLightDir;
	int numPointLights;
} lighting;

layout (set = 0, binding = 2) readonly buffer PointLights{
	PointLight values[];
} pointLightBuffer;

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

const vec3 DIRECTION_TO_LIGHT = normalize(vec3(1.0, 3.0, 1.0));
const float AMBIENT = 0.02;

void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];
	vec3 pos =  (objectMat.modelMatrix * vec4(inPos, 1.0)).xyz;
	mat4 viewMatrix = cameraMain.viewMatrix;
	vec3 viewPos = vec3(float(viewMatrix[3,0]),float(viewMatrix[3,1]),float(viewMatrix[3,2]));
	
	gl_Position = cameraMain.projectionViewMatrix * vec4(pos,1);

	outNormal = normalize(mat3(objectMat.normalMatrix) * inNormal);
	outColor = colourBuffer.colours[gl_BaseInstance].xyz;
	outUV = inUV;
	outViewVec = viewPos - pos;
	if(lighting.numPointLights > 0)
	{
		outLightVec = pointLightBuffer.values[0].position.xyz - pos;
	}
	else
	{
		outLightVec = lighting.ambientLightDir.xyz;
	}
	outTangent = inTangent;
}