#version 460
layout (location = 0) in vec4 fragColour;
layout (location = 1) in vec2 fragUV;
layout (location = 0) out vec4 outColour;
layout (location = 1) out vec4 outBright;

layout(set = 1, binding = 3) uniform sampler2DArray texSampler;

layout(set = 1, binding = 4) uniform sampler2D samplers[8];

void main()
{
	float diffuseTextureColour = texture(texSampler, vec3(fragUV,0)).r;

    float val = texture(samplers[0],fragUV).r;

    outColour = vec4(diffuseTextureColour,diffuseTextureColour,diffuseTextureColour, 1);
}