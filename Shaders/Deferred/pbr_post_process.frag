
#version 460
layout (location = 0) in vec2 inUV;
layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) uniform PBRProps {
    float exposure;
    float gamma;
} pbrProps;
layout (set = 0, binding = 1) uniform sampler2D colourIn;

#define UV vec2(inUV.x,1-inUV.y)

// From http://filmicgames.com/archives/75
vec3 Uncharted2Tonemap(vec3 x) {
	float A = 0.15;
	float B = 0.50;
	float C = 0.10;
	float D = 0.20;
	float E = 0.02;
	float F = 0.30;
	return ((x*(A*x+C*B)+D*E)/(x*(A*x+B)+D*F))-E/F;
}


void main(){
    
    vec3 color = texture(colourIn,UV).rgb;

	// Tone mapping
	color = Uncharted2Tonemap(color * pbrProps.exposure);
    
	color = color * (1.0 / Uncharted2Tonemap(vec3(11.2)));

	// Gamma correction
	color = pow(color, vec3(1.0 / pbrProps.gamma));
    
	outColour = vec4(color, 1.0);
}