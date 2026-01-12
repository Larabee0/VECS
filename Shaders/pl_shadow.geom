#version 460
layout (triangles) in;
layout (triangle_strip, max_vertices = 126) out; // 126 max works on gtx 1660 ti this should be 180 max_vertices for 10 point lights
layout(location = 0) out vec4 FragPos; // FragPos from GS (output per emitvertex)

layout(std140, set = 1, binding = 0) readonly buffer ShadowMats{
    mat4 value[];
} shadowMats;

layout(push_constant) uniform PLLight {
    int lightCount;
} plLight;

void main()
{
    for (int light = 0; light < plLight.lightCount; light++){
        int bufferOffset = light * 6;
        for(int face = 0; face < 6; ++face)
        {
            gl_Layer = bufferOffset + face; // built-in variable that specifies to which face we render.
            for(int i = 0; i < 3; ++i) // for each triangle vertex
            {
                gl_Layer = bufferOffset + face; // built-in variable that specifies to which face we render.
                FragPos = gl_in[i].gl_Position;
                gl_Position = shadowMats.value[bufferOffset + face] * FragPos;
                EmitVertex();
            }    
            EndPrimitive();
        }
    }
} 