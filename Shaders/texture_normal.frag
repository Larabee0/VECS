#version 460

layout (location = 0) in vec3 inNormal;
layout (location = 1) in vec3 inColor;
layout (location = 2) in vec2 inUV;
layout (location = 3) in vec3 inViewVec;
layout (location = 4) in vec3 inLightVec;
layout (location = 5) in vec4 inTangent;

layout (location = 0) out vec4 outFragColor;

struct PointLight {
	vec4 position; // ignore w
	vec4 colour; // w is intensity
};


layout(set = 0, binding = 1) uniform LightingInfo {
	vec4 ambientLightColour;
	vec4 ambientLightDir;
	int numPointLights;
} lighting;

layout (set = 0, binding = 2) readonly buffer PointLights{
	PointLight values[];
} pointLightBuffer;

layout (set = 1, binding = 3) uniform sampler2D samplerColorMap;
layout (set = 1, binding = 4) uniform sampler2D samplerNormalMap;

void main() {
	
	vec4 color = texture(samplerColorMap, inUV) * vec4(inColor, 1.0);
	
	vec3 N = normalize(inNormal);
	vec3 T = normalize(inTangent.xyz);
	vec3 B = cross(inNormal, inTangent.xyz) * inTangent.w;
	mat3 TBN = mat3(T, B, N);
	N = TBN * normalize(texture(samplerNormalMap, inUV).xyz * 2.0 - vec3(1.0));
	
	vec3 L = normalize(inLightVec);
	vec3 V = normalize(inViewVec);
	vec3 R = reflect(-L, N);
	vec3 diffuse = vec3(0);
	for(int i = 0; i < lighting.numPointLights; i++){

		float ambient = pointLightBuffer.values[0].colour.w;
		diffuse += max(dot(N, L), ambient) * pointLightBuffer.values[0].colour.xyz;
	}
	
	float specular = pow(max(dot(R, V), 0.0), 32.0);
	outFragColor = vec4(diffuse * color.rgb + specular, color.a);
	//outFragColor = vec4(1);
}