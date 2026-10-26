#version 460
layout (location = 0) in vec2 UV;

layout(set = 0, binding = 1) uniform sampler2D alphaSampler;

layout(set = 0, binding = 2) uniform TexProps{
    float alphaThreshold;
    float alphaTiling;
} alphaProps;

/*
layout(push_constant) uniform InstanceInfo {
    int matrixStartIndex;
    int bufferSelect;
	uint cameraIndex;
} instanceInfo;
*/
void main()
{
    float alphaThreshold = alphaProps.alphaThreshold;
    if(alphaThreshold > 0){
	    float alpha = texture(alphaSampler, UV * alphaProps.alphaTiling).a;
        if(alpha < alphaThreshold) {
            discard;
        }
    }
} 