#version 460
layout (location = 0) in vec4 FragPos;
layout(depth_less) out float gl_FragDepth;

layout(push_constant) uniform PLLight 
{
    vec4 lightPos;
    float far_plane;
} plLight;
 

void main()
{
    // get distance between fragment and light source
    float lightDistance = length(FragPos.xyz - plLight.lightPos.xyz);
    
    // map to [0;1] range by dividing by far_plane
    lightDistance = lightDistance / plLight.far_plane;
    
    // write this as modified depth
    gl_FragDepth = lightDistance;
} 