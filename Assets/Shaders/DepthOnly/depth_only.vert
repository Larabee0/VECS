#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"
layout (location = 0) in vec3 inPos;
layout (location = 1) in vec2 uv;

layout (location = 0) out vec2 fragUV;

out float gl_ClipDistance[4];

const vec3 tetrahedronPlanes[12] = vec3[](

    // b
    vec3(0.47510946, -0.70667917, -0.52428567),
    vec3(0.00000000, -0.03477280, -0.99939519),
    vec3(-0.47510946, -0.70667917, -0.52428567),
    // a
    vec3(0.00000000, -0.03477280, 0.99939519),
    vec3(-0.47510946, -0.70667917, 0.52428567),
    vec3(0.47510946, -0.70667917, 0.52428567),


    // c
    vec3(-0.52428567, 0.70667917, -0.47510946),
    vec3(-0.52428567, 0.70667917, 0.47510946),
    vec3(-0.99939519, 0.03477280, 0.00000000),

    // d
    vec3(0.52428567, 0.70667917, -0.47510946),
    vec3(0.99939519, 0.03477280, 0.00000000),
    vec3(0.52428567, 0.70667917, 0.47510946)
);

const vec3 _planes[12] = vec3[](
    vec3(-0.470629036426544189453125, -0.69999599456787109375, -0.537134945392608642578125),
    vec3(0.470628917217254638671875, -0.69999587535858154296875, -0.53713512420654296875),
    vec3(-1.1000000199601345229893922805786e-07, -0.039017140865325927734375, -0.999238550662994384765625),
    vec3(-0.4706289470195770263671875, -0.699995934963226318359375, 0.537135064601898193359375),
    vec3(0.0, -0.039017140865325927734375, 0.999238550662994384765625),
    vec3(0.4706289470195770263671875, -0.699995934963226318359375, 0.537135064601898193359375),


    vec3(-0.537134945392608642578125, 0.699995934963226318359375, -0.470629036426544189453125),
    vec3(-0.537135064601898193359375, 0.699995934963226318359375, 0.470628917217254638671875),
    vec3(-0.999238550662994384765625, 0.039017140865325927734375, -3.9999999756901161163114011287689e-08),

    vec3(0.999238550662994384765625, 0.039017140865325927734375, -2.9999998929497451172210276126862e-08),
    vec3(0.53713500499725341796875, 0.69999599456787109375, 0.4706289768218994140625),
    vec3(0.537134945392608642578125, 0.69999587535858154296875, -0.470629036426544189453125)
);
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

layout(std140, set = 1, binding = 4) readonly buffer TetraShadowMats{
    mat4 value[];
} tetraShadowMats;

layout(std140, set = 1, binding = 5) readonly buffer TetraShadowPlanes{
    vec3 value[];
} tetraShadowPlanes;

layout(push_constant) uniform InstanceInfo {
	uint cameraIndex;
    int matrixStartIndex;
    int bufferSelect;
	int taskOffset;
    int taskCount;
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

float GetClipDist(vec3 positionWorld, int planeIndex){
    const vec3 lightPos = vec3(-8, 1, 0);

    vec3 normal = tetraShadowPlanes.value[planeIndex];
    return (dot(positionWorld,normal)+dot(-normal,lightPos));
}

vec4 GetClipDistDir(vec3 positionWorld, mat4 viewProj){

    viewProj[0] = vec4(0.3809524, 0, 0, -0);
    viewProj[1] = vec4(0, 0.3809524, 0, -0);
    viewProj[2] = vec4(0, 0, -0.024258217, -0);
    viewProj[3] = vec4(0, 0, 0, 1);
    viewProj = transpose(viewProj);
    const vec3 lightPos = vec3(1.1456438, 2.2912877, 0.9864766);
    vec3 right =  normalize(viewProj[3] - viewProj[0]).xyz;
    vec3 left =   normalize(viewProj[3] + viewProj[0]).xyz;
    vec3 top =    normalize(viewProj[3] + viewProj[1]).xyz;
    vec3 bottom = normalize(viewProj[3] - viewProj[1]).xyz;

    return vec4(
        dot(positionWorld, right) + dot(-right, lightPos),
        dot(positionWorld, left) + dot(-left, lightPos),
        dot(positionWorld, top) + dot(-top, lightPos),
        dot(positionWorld, bottom)+ dot(-bottom, lightPos)
    );
}

void main()
{
	ObjectMatrices objectMat = matricesBuffer.matrices[gl_BaseInstance];
    vec4 positionWorld = objectMat.modelMatrix * vec4(inPos, 1.0);
    gl_Position = getTransform(instanceInfo.bufferSelect, instanceInfo.matrixStartIndex) * (positionWorld);
    
    vec4 clipDist;
    if(instanceInfo.bufferSelect == 4) {
        int faceIndex = instanceInfo.matrixStartIndex;
        int planeIndex = 3 * instanceInfo.matrixStartIndex;
        const vec3 lightPos = vec3(-8, 1, 0);
        
        clipDist = vec4(
            GetClipDist(positionWorld.xyz, planeIndex),
            GetClipDist(positionWorld.xyz, planeIndex + 1),
            GetClipDist(positionWorld.xyz, planeIndex + 2),
            1.0
        );
    }
    //else if(instanceInfo.bufferSelect == 1){
    //    clipDist = GetClipDistDir(positionWorld.xyz,getTransform(instanceInfo.bufferSelect, instanceInfo.matrixStartIndex));
    //}
    else {
        clipDist = vec4(1.0);
    }
    
    gl_ClipDistance[0] = clipDist.x;
    gl_ClipDistance[1] = clipDist.y;
    gl_ClipDistance[2] = clipDist.z;
    gl_ClipDistance[3] = clipDist.w;
    fragUV = uv;
}  

