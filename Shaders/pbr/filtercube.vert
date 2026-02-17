#version 460

layout (location = 0) in vec3 inPos;


layout (location = 0) out vec3 outUVW;

layout(push_constant) uniform PushConsts {
	mat4 mvp;
} pushConsts;

out gl_PerVertex {
	vec4 gl_Position;
};

void main() 
{
	outUVW = inPos;
	gl_Position = pushConsts.mvp * vec4(inPos.xyz, 1.0);
}
