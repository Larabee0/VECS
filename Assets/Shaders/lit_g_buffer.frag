#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"

layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec4 fragPosDirLight;
layout (location = 4) in mat3 TBN;

layout(location = 0) out vec3 gPosition;
layout(location = 1) out vec3 gNormal;
layout(location = 2) out vec4 gAlbedoSpec;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(set = 1, binding = 7) uniform sampler2D normalSampler;

layout(set = 1, binding = 3) uniform TexPorps {
	vec4 colour;
	vec4 specularColour;
	float tiling;
	float shininess;
} texProps;

void main(){


	vec3 normal = normalize(fragNormalWorld);
	vec3 texNormal = TBN * normalize(texture(normalSampler, fragUV).rgb * 2.0 - vec3(1.0));
	if(dot(texNormal, texNormal) > 0){
		normal = texNormal;
	}

    gPosition = fragNormalWorld;
    gNormal = normal;
    gAlbedoSpec.rgb = texture(texSampler, fragUV * texProps.tiling).rgb;
    gAlbedoSpec.a = texProps.shininess;
}