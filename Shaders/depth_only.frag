#version 460
layout (location = 0) in vec4 FragPos;
layout (location = 1) in vec2 UV;
layout (location = 2) in flat int lightIndex;
layout(depth_less) out float gl_FragDepth;

layout(std140, set = 2, binding = 0) readonly buffer PointLights{
    vec4 values[];
} pointLights;

layout(std140, set = 2, binding = 1) readonly buffer SpotLights{
    vec4 values[];
} spotLights;

layout(set = 2, binding = 2) uniform sampler2D alphaSampler;

layout(set = 2, binding = 3) uniform TexProps{
    float alphaThreshold;
    float alphaTiling;
} alphaProps;

layout(push_constant) uniform InstanceInfo {
    int matrixStartIndex;
    int layerOffset;
    int layerCount;
    int bufferSelect;
    int useLightPos;
    int lightIndex;
} instanceInfo;

vec4 getPosition(int bufferSelect, int bufferOffset) {
    switch(bufferSelect){
        //case 0:
        //    return cameraInfo.values[bufferOffset].projectionViewMatrix ;
        //case 1:
        //    return directionalShadows.value[bufferOffset + face];
        case 2:
            return pointLights.values[bufferOffset];
        case 3:
            return spotLights.values[bufferOffset];
        default:
            return vec4(0.0);
    }
}

void main()
{
    float alphaThreshold = alphaProps.alphaThreshold ;
    if(alphaThreshold > 0){
	    float alpha = texture(alphaSampler, UV * alphaProps.alphaTiling).a;
        if(alpha < alphaThreshold) {
            discard;
        }
    }

    if(instanceInfo.useLightPos != 0){
        int lightIndex = instanceInfo.lightIndex;
        int bufferSelect = instanceInfo.bufferSelect;
        vec4 light = getPosition(bufferSelect,lightIndex);

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