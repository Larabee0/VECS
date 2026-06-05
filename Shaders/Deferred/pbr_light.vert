#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"

layout (location = 0) in vec3 position;

layout(set = 0,binding = 1) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;


layout(set = 0, binding = 3) uniform LightUniform{
    vec2 screenSize;
    mat4 lightMatrix;
    uint lightIndex;
    uint shadow;
} lightUniform;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

void main()
{
	vec4 positionWorld = lightUniform.lightMatrix * vec4(position, 1.0);
	gl_Position = cameraInfo.values[constants.cameraIndex].projectionViewMatrix * positionWorld;
}