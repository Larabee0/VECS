#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"
layout (location = 0) in vec3 inPos;
layout (location = 1) in vec2 uv;

layout (location = 0) out vec2 fragUV;

layout(std140, set = 0, binding = 0) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
} matricesBuffer;

layout(std140, set = 1, binding = 0) readonly buffer CameraDatas {
	CameraData values[];
} cameraData;

layout(std140, set = 1, binding = 1) readonly buffer DirectionalShadowMats{
    mat4 value[];
} directionalShadowsMats;

layout(std140, set = 1, binding = 2) readonly buffer PointShadowMats{
    mat4 value[];
} pointShadowsMats;

layout(std140, set = 1, binding = 3) readonly buffer SpotShadowMats{
    mat4 value[];
} spotShadowsMats;

layout(push_constant) uniform InstanceInfo {
    int matrixStartIndex;
    int layerOffset;
    int layerCount;
    int bufferSelect;
} instanceInfo;

mat4 getTransform(int bufferSelect, int bufferOffset) {
    switch(bufferSelect){
        case 0:
            return cameraData.values[bufferOffset].projectionViewMatrix;
        case 1:
            return directionalShadowsMats.value[bufferOffset];
        case 2:
            return pointShadowsMats.value[bufferOffset];
        case 3:
            return spotShadowsMats.value[bufferOffset];
        default:
            return mat4(1.0);
    }
}
void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];

    int bufferSelect = instanceInfo.bufferSelect;
    int layerOffset = instanceInfo.layerOffset;
    int layerCount = instanceInfo.layerCount;
    int bufferOffset = instanceInfo.matrixStartIndex;
    mat4 transformMatrix = getTransform(bufferSelect, bufferOffset);
    
    gl_Position = transformMatrix *(objectMat.modelMatrix * vec4(inPos, 1.0));
    fragUV = uv;
}  

