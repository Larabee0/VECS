#version 460

layout (early_fragment_tests) in;
layout (location = 0) in vec4 fragColour;

struct Node
{
    vec4 color;
    float depth;
    uint next;
};

layout (set = 2, binding = 0) buffer GeometrySBO
{
    uint count;
    uint maxNodeCount;
} geometrySBO;

layout (set = 2, binding = 1, r32ui) uniform coherent uimage2D headIndexImage;

layout (set = 2, binding = 2) buffer LinkedListSBO
{
    Node nodes[];
} linkedListSBO;

void main()
{
    // Increase the node count
    uint nodeIdx = atomicAdd(geometrySBO.count, 1);

    // Check LinkedListSBO is full
    if (nodeIdx < geometrySBO.maxNodeCount)
    {
        // Exchange new head index and previous head index
        uint prevHeadIdx = imageAtomicExchange(headIndexImage, ivec2(gl_FragCoord.xy), nodeIdx);

        // Store node data
        linkedListSBO.nodes[nodeIdx].color = fragColour;
        linkedListSBO.nodes[nodeIdx].depth = gl_FragCoord.z;
        linkedListSBO.nodes[nodeIdx].next = prevHeadIdx;
    }
}