#version 460

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 normal;
layout (location = 2) in vec2 uv;
	   
layout (location = 0) out vec4 fragColour;
layout (location = 1) out vec3 fragPosWorld;
layout (location = 2) out vec3 fragNormalWorld;
layout (location = 3) out vec2 fragUV;

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

layout(set = 0,binding = 1) uniform CameraInverse{
	mat4 inverseProjectionMatrix;
	mat4 inverseViewMatrix;
	mat4 inverseProjectionViewMatrix;
} cameraInverse;

layout (set = 0, binding = 2) uniform AdditionalCameraInfo
{
	float ratio;
 	float p00;
 	float p11;
 	float nearPlane;
	float farPlane;
 	vec4 frustum;
} cameraPlanes;

layout (set = 0, binding = 3) uniform OrthographicInfo
{
	float orthographic;
	float width;
	float height;
} orthographic;

layout(set = 0, binding = 4) uniform LightingInfo {
	vec4 ambientLightColour;
	vec4 ambientLightDir;
	int numPointLights;
} lighting;

layout (set = 0, binding = 5) readonly buffer PointLights{
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


const vec3 DIRECTION_TO_LIGHT = normalize(vec3(1.0, 3.0, 1.0));
const float AMBIENT = 0.02;
void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];

	vec4 positionWorld =objectMat.modelMatrix * vec4(position, 1.0);
	gl_Position = cameraMain.projectionViewMatrix * positionWorld;
	
	fragNormalWorld = normalize(mat3(objectMat.normalMatrix) * normal);
	
	
	float lightIntensity = AMBIENT + max(dot(fragNormalWorld, DIRECTION_TO_LIGHT), 0);
	fragPosWorld = positionWorld.xyz;

	fragColour = lightIntensity * vec4 (1);
	fragUV = uv;
}