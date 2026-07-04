#version 460

layout (location = 0) in vec3 inPos;
layout (location = 0) out vec3 outUVW;

layout(push_constant) uniform UBO 
{
	mat4 viewProj;
} ubo;

void main() 
{
	outUVW = inPos;
	// Convert cubemap coordinates into Vulkan coordinate space
	outUVW.xy *= -1.0;
	vec4 pos = ubo.viewProj * vec4(inPos.xyz, 1.0);
	gl_Position = pos.xyww;
}
