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

struct PointLight {
		vec4 position; // ignore w
		vec4 colour; // w is intensity
};


layout(set = 0,binding = 0) uniform GlobalUbo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 inverseViewMatrix;
	vec4 ambientLightColour;
	int numLights;
	PointLight pointLights[10];
} ubo;

layout(std140, set = 1, binding = 0) readonly buffer StarPosBuffer{
	vec4 positions[];
} starPosBuffer;

layout(std140, set = 1, binding = 1) readonly buffer StarColourBuffer{
	vec4 colours[];
} starColourBuffer;


void main(){
	fragOffset = OFFSETS[gl_VertexIndex];
	fragColour = starColourBuffer.colours[gl_BaseInstance];

	vec3 cameraRightWorld = vec3(ubo.viewMatrix[0][0],ubo.viewMatrix[1][0], ubo.viewMatrix[2][0]);
	vec3 cameraUpWorld = -vec3(ubo.viewMatrix[0][1],ubo.viewMatrix[1][1], ubo.viewMatrix[2][1]);

	vec3 positionWorld = starPosBuffer.positions[gl_BaseInstance].xyz
	+  fragColour.w * fragOffset.x*cameraRightWorld
	+  fragColour.w * fragOffset.y*cameraUpWorld;

	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * vec4(positionWorld,1.0);
}