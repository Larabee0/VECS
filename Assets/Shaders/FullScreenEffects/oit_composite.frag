#version 460
#extension GL_ARB_shading_language_include : require
#include "../common_structures.glsl"

#define MAX_FRAGMENT_COUNT 128

layout (location = 0) out vec4 outFragColor;

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
        vec4 fragColour = vec4(unpackHalf2x16(fragments[i].rg),unpackHalf2x16(fragments[i].ba));
        color = mix(color, fragColour, fragColour.a);
    }

    outFragColor = color;
}