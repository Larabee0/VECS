#version 450
#extension GL_KHR_vulkan_glsl: enable

layout (location = 0) in vec3 fragColour;
layout (location = 1) in vec3 fragPosWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in vec2 fragUV;

layout (location = 0) out vec4 outColour;

void main() 
{
    outColour = vec4(fragColour,1);
}
