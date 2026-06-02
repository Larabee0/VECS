#version 460

layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec4 fragTangentWorld;

layout (location = 0) out vec4 positionOut;
layout (location = 1) out vec2 normalsOut;
layout (location = 2) out vec3 albedoOut;
layout (location = 3) out vec4 maskOut;

layout(set = 2, binding = 0) uniform TexPorps {
	vec4 colour;
	float tiling;
    float exposure;
    float gamma;
} texProps;

layout (set = 2, binding = 1) uniform sampler2D albedoMap;
layout (set = 2, binding = 2) uniform sampler2D normalMap;
layout (set = 2, binding = 3) uniform sampler2D maskMap;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

#define TILED_UV fragUV * texProps.tiling

vec3 calculateNormal() {

	vec3 texNormal = vec3(texture(normalMap, TILED_UV).rg, 0.0);
	texNormal.b = sqrt(1 - texNormal.x * texNormal.x - texNormal.y * texNormal.y);

	vec3 tangentNormal = texNormal * 2.0 - 1.0;

	vec3 N = normalize(fragNormalWorld);
	vec3 T = normalize(fragTangentWorld.xyz);
	vec3 B = normalize(cross(N, T));
	mat3 TBN = mat3(T, B, N);
	return normalize(TBN * tangentNormal);
}

void main(){
    vec3 normal = calculateNormal();
    normalsOut.xy = normal.xy;
    positionOut.w = normal.z;
    positionOut.xyz = fragPosWorld;
    albedoOut.rgb = texture(albedoMap, TILED_UV).rgb;
    maskOut = texture(maskMap,TILED_UV).rgba;

}