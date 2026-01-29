#version 460
layout (location = 0) in vec2 fragUV;

layout(set = 0, binding = 2) uniform sampler2D texSampler;

layout(push_constant) uniform Constants {
	uint cameraIndex;    
    float threshold;
    float tiling;
} constants;

void main()
{
	float alpha = texture(texSampler, fragUV * constants.tiling).a;
    if(alpha > constants.threshold) {
        discard;
    }
}