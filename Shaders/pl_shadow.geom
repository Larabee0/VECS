#version 460
layout (triangles) in;
layout (triangle_strip, max_vertices=18) out;
layout(location = 0) out vec4 FragPos; // FragPos from GS (output per emitvertex)

layout(set = 0, binding = 0) uniform ShadowMats{
    mat4 value[6];
} shadowMats;


void main()
{
    for(int face = 0; face < 6; ++face)
    {
        gl_Layer = face; // built-in variable that specifies to which face we render.
        for(int i = 0; i < 3; ++i) // for each triangle vertex
        {
            FragPos = gl_in[i].gl_Position;
            gl_Position = shadowMats.value[face] * FragPos;
            EmitVertex();
        }    
        EndPrimitive();
    }
} 