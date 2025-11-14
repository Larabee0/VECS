#version 460

layout (location = 0) in vec2 fragOffset;
layout (location = 1) in vec4 fragColour;
layout (location = 0) out vec4 outColour;

const float M_PI = 3.1415926538;

void main(){
	float dist = sqrt(dot(fragOffset,fragOffset));
	if(dist >= 1.0){
		discard;
	}
	float cosDis = 0.5 *(cos(dist * M_PI) + 1.0);
	outColour = vec4(fragColour.xyz + cosDis, cosDis);
}