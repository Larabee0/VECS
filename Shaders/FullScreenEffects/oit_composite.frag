#version 460

#define MAX_FRAGMENT_COUNT 128

struct Node
{
    vec4 color;
    float depth;
    uint next;
};

layout (location = 0) out vec4 outFragColor;
layout (location = 1) out vec4 outBrightColor;

layout (set = 0, binding = 0, r32ui) uniform uimage2D headIndexImage;

layout (set = 0, binding = 1) buffer LinkedListSBO
{
    Node nodes[];
} linkedListSBO;
#define FLT_MAX 3.402823466e+38
void main()
{
    Node fragments[MAX_FRAGMENT_COUNT];
    int count = 0;

    uint nodeIdx = imageLoad(headIndexImage, ivec2(gl_FragCoord.xy)).r;

    while (nodeIdx != 0xffffffff && count < MAX_FRAGMENT_COUNT)
    {
        fragments[count] = linkedListSBO.nodes[nodeIdx];
        nodeIdx = fragments[count].next;
        ++count;
    }
    
    // Do the insertion sort
    for (uint i = 1; i < count; ++i)
    {
        Node insert = fragments[i];
        uint j = i;
        while (j > 0 && insert.depth > fragments[j - 1].depth)
        {
            fragments[j] = fragments[j-1];
            --j;
        }
        fragments[j] = insert;
    }

    // Do blending
    vec4 color = vec4(0.0, 0.0, 0.0, 0.0);
    
    float brightness  = 0;

    for (int i = 0; i < count; ++i)
    {
        vec4 fragColour = fragments[i].color;
        color = mix(color, fragColour, fragColour.a);
    }

    outFragColor = color;
    brightness = dot(color.rgb, vec3(0.2126, 0.7152, 0.0722));
    if(brightness > 0.20)
    {
        outBrightColor = color;
    }
    else{
        outBrightColor = vec4(0);
    }
}