#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"
layout (triangles) in;
layout (triangle_strip, max_vertices = 18) out; // 126 max works on gtx 1660 ti this should be 180 max_vertices for 10 point lights


layout(location = 0) in vec2 gs_in_uv[];
layout(location = 0) out vec2 gs_out_uv;

layout(std140, set = 1, binding = 0) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(std140, set = 1, binding = 1) readonly buffer DirectionalShadowMats{
    mat4 value[];
} directionalShadows;

layout(std140, set = 1, binding = 2) readonly buffer PointShadowMats{
    mat4 value[];
} pointShadows;

layout(std140, set = 1, binding = 3) readonly buffer SpotShadowMats{
    mat4 value[];
} spotShadows;

layout(push_constant) uniform InstanceInfo {
    int matrixStartIndex;
    int layerOffset;
    int layerCount;
    int bufferSelect;
} instanceInfo;

mat4 getTransform(int bufferSelect, int bufferOffset, int face) {
    switch(bufferSelect){
        case 0:
            return cameraInfo.values[bufferOffset].projectionViewMatrix;
        case 1:
            return directionalShadows.value[bufferOffset + face];
        case 2:
            return pointShadows.value[bufferOffset + face];
        case 3:
            return spotShadows.value[bufferOffset + face];
        default:
            return mat4(1.0);
    }
}

void emitPrimative(mat4 transformMatrix, int glLayer) {
    for(int i = 0; i < 3; ++i){
        gl_Layer = glLayer;
        gs_out_uv = gs_in_uv[i];
        gl_Position = transformMatrix * gl_in[i].gl_Position;
        EmitVertex();
    }
    EndPrimitive();
}

void main() {

    int bufferSelect = instanceInfo.bufferSelect;
    int layerOffset = instanceInfo.layerOffset;
    int layerCount = instanceInfo.layerCount;
    int bufferOffset = instanceInfo.matrixStartIndex;
    mat4 transformMatrix;

    for(int face = 0; face < layerCount; ++face)
    {
        transformMatrix = getTransform(bufferSelect, bufferOffset, face);
        emitPrimative(transformMatrix, layerOffset + face);
    }
} 