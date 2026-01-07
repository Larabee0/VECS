#version 460

layout (location = 0) out float outFragColor;

layout (location = 0) in vec3 inPos;

struct PointLight {
	vec4 position; // ignore w
	vec4 colour; // w is intensity
};


layout(set = 0, binding = 0) uniform LightingInfo {
	vec4 ambientLightColour;
	vec4 ambientLightDir;
	float ambientStrength;
	float diffuseStrength;
	float specularStrength;
	int numPointLights;
} lighting;

layout (set = 0, binding = 1) readonly buffer PointLights{
	PointLight values[];
} pointLightBuffer;

void main() 
{
	vec3 lightVec;
	if(lighting.numPointLights == 0)
	{
		lightVec = lighting.ambientLightDir.xyz;
	}
	else{
		lightVec = inPos - pointLightBuffer.values[0].position.xyz;
	}
	// Store distance to light as 32 bit float value
    outFragColor = length(lightVec);
}