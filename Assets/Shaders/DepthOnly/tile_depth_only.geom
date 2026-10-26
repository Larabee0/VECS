#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"

layout(triangles) in;
layout(triangle_strip, max_vertices = 12) out;

in gl_PerVertex
{   
    vec4 gl_Position;
    float gl_PointSize;
} gl_in[3];


out gl_PerVertex {
    vec4 gl_Position;
    float gl_PointSize;
    float gl_ClipDistance[3];
};

layout (location=0) in VS_OUT{
    vec2 texCoords;
}gs_in[];

layout (location = 0) out vec2 fragUV;

const vec3 planeNormals[12] =
{
  vec3(0.00000000, -0.03477280, 0.99939519),
  vec3(-0.47510946, -0.70667917, 0.52428567),
  vec3(0.47510946, -0.70667917, 0.52428567),
    
  vec3(0.00000000, -0.03477280, -0.99939519),
  vec3(0.47510946, -0.70667917, -0.52428567),
  vec3(-0.47510946, -0.70667917, -0.52428567),
    
  vec3(-0.52428567, 0.70667917, -0.47510946),
  vec3(-0.52428567, 0.70667917, 0.47510946),
  vec3(-0.99939519, 0.03477280, 0.00000000),
    
  vec3(0.52428567, 0.70667917, -0.47510946),
  vec3(0.99939519, 0.03477280, 0.00000000),
  vec3(0.52428567, 0.70667917, 0.47510946)
};

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

layout(std140, set = 1, binding = 4) readonly buffer TetraShadowMats{
    mat4 value[];
} tetraShadowMats;

layout(std140, set = 1, binding = 5) readonly buffer TetraShadowPlanes{
    vec3 value[];
} tetraShadowPlanes;

layout(push_constant) uniform InstanceInfo {
    int matrixStartIndex;
    int bufferSelect;
	uint cameraIndex;
} instanceInfo;

mat4 getTransform(int bufferSelect, int bufferOffset) {
    switch(bufferSelect){
        case 0:
            return cameraData.values[max(bufferOffset,int(instanceInfo.cameraIndex))].projectionViewMatrix;
        case 1:
            return directionalShadowsMats.value[bufferOffset];
        case 2:
            return pointShadowsMats.value[bufferOffset];
        case 3:
            return spotShadowsMats.value[bufferOffset];
        case 4:
            return tetraShadowMats.value[bufferOffset];
        default:
            return mat4(1.0);
    }
}

float GetClipDistance(in vec3 lightPosition, in uint vertexIndex, in uint planeIndex)
{
  vec3 normal = planeNormals[planeIndex];
  return (dot(gl_in[vertexIndex].gl_Position.xyz, normal)+dot(-normal, lightPosition));
}


void main()
{
    if(instanceInfo.bufferSelect == 4) {
        const vec3 lightPosition = vec3(-10, 1, 0);
        uint faceIndex = uint(instanceInfo.matrixStartIndex);
        uint inside = 0;
        float clipDistances[9];
        for(uint sideIndex=0; sideIndex < 3; sideIndex++)
        {
            const uint planeIndex = (faceIndex * 3)+sideIndex;
            const uint bit = 1 << sideIndex;
            for(uint vertexIndex=0; vertexIndex < 3; vertexIndex++)
            {
                uint clipDistanceIndex = sideIndex*3+vertexIndex;
                clipDistances[clipDistanceIndex] = GetClipDistance(lightPosition, vertexIndex, planeIndex);
                inside |= (clipDistances[clipDistanceIndex] > 0.001) ? bit : 0;
            }
        }

        if(inside == 0x7)
        {
            const mat4 shadowViewProjTexMatrix = tetraShadowMats.value[faceIndex];
            for(uint vertexIndex = 0; vertexIndex < 3; vertexIndex++)
            {
                gl_Position = shadowViewProjTexMatrix * gl_in[vertexIndex].gl_Position;
                gl_ClipDistance[0] = clipDistances[vertexIndex];
                gl_ClipDistance[1] = clipDistances[3 + vertexIndex];
                gl_ClipDistance[2] = clipDistances[6 + vertexIndex];
                EmitVertex();  
            }
            EndPrimitive(); 
        }
    }
    else {
        const mat4 matBuffer = getTransform(instanceInfo.bufferSelect,instanceInfo.matrixStartIndex);
        for(uint vertexIndex = 0; vertexIndex < 3; vertexIndex++){
            gl_Position = matBuffer * gl_in[vertexIndex].gl_Position;
            gl_ClipDistance[0] = 1.0;
            gl_ClipDistance[1] = 1.0;
            gl_ClipDistance[2] = 1.0;
            fragUV = gs_in[vertexIndex].texCoords;
            EmitVertex();
        }
        EndPrimitive();
    }
}
