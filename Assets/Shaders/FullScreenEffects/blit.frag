#version 460

layout (set = 0, binding = 0) uniform sampler2D inputTexture;

layout(location = 0) in vec2 inUV;
layout(location = 0) out vec4 outColour;

void main(){
    vec2 uv = vec2(inUV.x, 1.0 - inUV.y);
    outColour = texture(inputTexture,uv);
    //outColour.a = 1.0;
}