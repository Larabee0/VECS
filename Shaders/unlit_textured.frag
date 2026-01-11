#version 460
layout (location = 0) in vec4 fragColour;
layout (location = 1) in vec2 fragUV;
layout (location = 0) out vec4 outColour;
layout (location = 1) out vec4 outBright;

layout(set = 1, binding = 3) uniform sampler2D texSampler;

void main()
{
	float diffuseTextureColour = texture(texSampler, fragUV).r;

    outColour = vec4(diffuseTextureColour,diffuseTextureColour,diffuseTextureColour, 1);
}