#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"
layout (triangles) in;
layout (triangle_strip, max_vertices = 18) out; // 126 max works on gtx 1660 ti this should be 180 max_vertices for 10 point lights
layout(location = 0) out vec4 FragPos; // FragPos from GS (output per emitvertex)

layout(location = 0) in vec2 gs_in_uv[];
layout(location = 1) out vec2 gs_out_uv;

layout(std140, set = 0, binding = 1) readonly buffer ShadowMats{
    mat4 value[];
} shadowMats;

layout(set = 0,binding = 2) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(push_constant) uniform Constants {
    int matrixOffset;
    int baseLayerOffset;
    int faceCount;
    int lightIndex;    
    int writeDepth;

    int camera;
} constants;

void main()
{
    bool camera = constants.camera != 0;
    int baseLayerOffset = constants.baseLayerOffset;
    int faceCount = constants.faceCount;
    int bufferOffset = constants.matrixOffset;
    mat4 transformMatrix;

    for(int face = 0; face < faceCount; ++face)
    {
        transformMatrix = camera ? cameraInfo.values[bufferOffset].projectionViewMatrix : shadowMats.value[bufferOffset + face];

        gl_Layer = baseLayerOffset + face; // built-in variable that specifies to which face we render.
        for(int i = 0; i < 3; ++i) // for each triangle vertex
        {
            gl_Layer = baseLayerOffset + face; // built-in variable that specifies to which face we render.
            FragPos = gl_in[i].gl_Position;
            gs_out_uv = gs_in_uv[i];
            gl_Position = transformMatrix * FragPos;
            EmitVertex();
        }    
        EndPrimitive();
    }
} 