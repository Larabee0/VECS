#version 460

const vec2 OFFSETS[6] = vec2[](
	vec2(-1.0,-1.0),
	vec2(-1.0,1.0),
	vec2(1.0,-1.0),
	vec2(1.0,-1.0),
	vec2(-1.0,1.0),
	vec2(1.0,1.0)
);

layout (location = 0) out vec2 fragOffset;
layout (location = 1) out vec4 fragColour;

layout(set = 0,binding = 0) uniform CameraInfo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 projectionViewMatrix;	
	vec4 position;
	vec4 forward;
} cameraMain;

layout(std140, set = 1, binding = 0) readonly buffer PositionBuffer{
	vec4 values[];
} positionBuffer;

layout(std140, set = 1, binding = 1) readonly buffer ColourBuffer{
	vec4 values[];
} colourBuffer;


void main(){
	fragOffset = OFFSETS[gl_VertexIndex];
	fragColour = colourBuffer.values[gl_BaseInstance];
	mat4 viewMatrix = cameraMain.viewMatrix;
	vec3 cameraRightWorld = vec3(viewMatrix[0][0], viewMatrix[1][0], viewMatrix[2][0]);
	vec3 cameraUpWorld = -vec3(viewMatrix[0][1], viewMatrix[1][1], viewMatrix[2][1]);

	vec3 positionWorld = positionBuffer.values[gl_BaseInstance].xyz
	+  fragColour.w * fragOffset.x * cameraRightWorld
	+  fragColour.w * fragOffset.y * cameraUpWorld;

	gl_Position = cameraMain.projectionViewMatrix * vec4(positionWorld,1.0);
}