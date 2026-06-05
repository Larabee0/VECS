#version 460
layout (location = 0) in vec2 inUV;
layout (location = 0) out float ssaoOut;
  
layout (set = 0, binding = 0) uniform sampler2D ssao_Source;


#define UV vec2(inUV.x,1-inUV.y)

void main() {
    vec2 texelSize = 1.0 / vec2(textureSize(ssao_Source, 0));
    float result = 0.0;
    for (int x = -2; x < 2; ++x) 
    {
        for (int y = -2; y < 2; ++y) 
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            result += texture(ssao_Source, UV + offset).r;
        }
    }
    ssaoOut = result / (4.0 * 4.0);
}  