#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"
layout (location = 0) in vec3 inPos;
layout (location = 1) in vec2 uv;

layout (location=0) out  VS_OUT{
    vec2 uv;
} vs_out;

out gl_PerVertex {
    vec4 gl_Position;
    float gl_PointSize;
};

layout(std140, set = 0, binding = 0) readonly buffer ObjectMatricesBuffer{
	ObjectMatrices matrices[];
} matricesBuffer;

void main() {
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];
    gl_Position = objectMat.modelMatrix * vec4(inPos, 1.0);
    vs_out.uv = uv;
}  

