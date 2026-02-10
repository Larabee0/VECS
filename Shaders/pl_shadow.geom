#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"
layout (triangles) in;
layout (triangle_strip, max_vertices = 18) out; // 126 max works on gtx 1660 ti this should be 180 max_vertices for 10 point lights
layout(location = 0) out vec4 FragPos; // FragPos from GS (output per emitvertex)

layout(location = 0) in vec2 gs_in_uv[];
layout(location = 1) out vec2 gs_out_uv;

layout(set = 0,binding = 1) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(std140, set = 0, binding = 2) readonly buffer DirectionalShadowMats{
    mat4 value[];
} directionalShadows;

layout(std140, set = 0, binding = 3) readonly buffer PointShadowMats{
    mat4 value[];
} pointShadows;

layout(std140, set = 0, binding = 4) readonly buffer SpotShadowMats{
    mat4 value[];
} spotShadows;

layout(push_constant) uniform Constants {
    int matrixStartIndex;
    int layerOffset;
    int faceCount;
    int bufferSelect;

    //int writeDepth;
    //int lightIndex;
} constants;

mat4 getTransform(int bufferSelect, int bufferOffset, int face) {
    switch(bufferSelect){
        case 0:
            return cameraInfo.values[bufferOffset].projectionViewMatrix ;
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
        FragPos = gl_in[i].gl_Position;
        gs_out_uv = gs_in_uv[i];
        gl_Position = transformMatrix * FragPos;
        EmitVertex();
    }
    EndPrimitive();
}

void main() {

    int bufferSelect = constants.bufferSelect;
    int layerOffset = constants.layerOffset;
    int faceCount = constants.faceCount;
    int bufferOffset = constants.matrixStartIndex;
    mat4 transformMatrix;

    for(int face = 0; face < faceCount; ++face)
    {
        transformMatrix = getTransform(bufferSelect, bufferOffset, face);
        emitPrimative(transformMatrix, layerOffset + face);
    }
} 