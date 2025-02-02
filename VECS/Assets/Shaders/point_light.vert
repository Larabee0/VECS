#version 450
#extension GL_KHR_vulkan_glsl: enable

const vec2 OFFSETS[6] = vec2[](
	vec2(-1.0,-1.0),
	vec2(-1.0,1.0),
	vec2(1.0,-1.0),
	vec2(1.0,-1.0),
	vec2(-1.0,1.0),
	vec2(1.0,1.0)
);

layout (location = 0) out vec2 fragOffset;

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

layout(push_constant) uniform Push{
	vec4 position;
	vec4 colour;
	float radius;
	float dstSqrd;
} push;


void main(){
	fragOffset = OFFSETS[gl_VertexIndex];


	vec3 cameraRightWorld = vec3(ubo.viewMatrix[0][0],ubo.viewMatrix[1][0], ubo.viewMatrix[2][0]);
	vec3 cameraUpWorld = -vec3(ubo.viewMatrix[0][1],ubo.viewMatrix[1][1], ubo.viewMatrix[2][1]);

	vec3 positionWorld = push.position.xyz
	+  push.radius * fragOffset.x*cameraRightWorld
	+  push.radius * fragOffset.y*cameraUpWorld;

	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * vec4(positionWorld,1.0);

}