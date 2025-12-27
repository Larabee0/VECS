#version 460
layout (location = 0) in vec4 fragColour;
layout (location = 0) out vec4 outColour;
layout (location = 1) out vec4 outBright;

void main()
{
	outColour = fragColour;
	outBright = fragColour;
}