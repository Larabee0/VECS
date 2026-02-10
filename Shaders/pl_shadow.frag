#version 460
layout (location = 0) in vec4 FragPos;
layout (location = 1) in vec2 UV;
layout(depth_less) out float gl_FragDepth;

layout(std140, set = 0, binding = 3) readonly buffer LightInfo{
    vec4 values[];
} lightInfo;

layout(set = 0, binding = 4) uniform sampler2D alphaSampler;

layout(set = 0, binding = 5) uniform TexProps{
    float alphaThreshold;
    float alphaTiling;
} alphaProps;


layout(push_constant) uniform Constants {
    //int matrixStartIndex;
    //int layerOffset;
    //int faceCount;
    //int bufferSelect;

    //int writeDepth;
    //int lightIndex;
    layout(offset = 16 )int writeDepth;
    layout(offset = 20) int lightIndex;
} constants;

void main()
{
    float alphaThreshold = alphaProps.alphaThreshold ;
    if(alphaThreshold > 0){
	    float alpha = texture(alphaSampler, UV * alphaProps.alphaTiling).a;
        if(alpha < alphaThreshold) {
            discard;
        }
    }

    if(constants.writeDepth != 0){
        int lightIndex = constants.lightIndex;
        vec4 light = lightInfo.values[lightIndex];

        // get distance between fragment and light source
        float lightDistance = length(FragPos.xyz - light.xyz);

        // map to [0;1] range by dividing by far_plane
        lightDistance = lightDistance / light.w;

        // write this as modified depth
        gl_FragDepth = lightDistance;
    }
    else{
        gl_FragDepth = gl_FragCoord.z;
    }
} 