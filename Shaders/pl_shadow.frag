#version 460
layout (location = 0) in vec4 FragPos;
layout(depth_less) out float gl_FragDepth;

layout(std140, set = 0, binding = 2) readonly buffer LightInfo{
    vec4 values[];
} lightInfo;

void main()
{
    int lightIndex = gl_Layer / 6;
    vec3 lightPos = lightInfo.values[lightIndex].xyz;
    float farPlane = lightInfo.values[lightIndex].w;

    // get distance between fragment and light source
    float lightDistance = length(FragPos.xyz - lightPos.xyz);
    
    // map to [0;1] range by dividing by far_plane
    lightDistance = lightDistance / farPlane;
    
    // write this as modified depth
    gl_FragDepth = lightDistance;
} 