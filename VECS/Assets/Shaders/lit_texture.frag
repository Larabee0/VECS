#version 460

layout (location = 0) in vec4 fragColour;
layout (location = 1) in vec3 fragPosWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in vec2 fragUV;

layout (location = 0) out vec4 outColour;

struct PointLight {
	vec4 position; // ignore w
	vec4 colour; // w is intensity
};

layout(set = 0,binding = 0) uniform GlobalUbo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 inverseViewMatrix;
	vec4 ambientLightColour;
	int numLights;
	PointLight pointLights[10];
} ubo;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(set = 2, binding = 0) uniform Colour
{
	vec4 colour;
} colourMul;

void main()
{
	vec3 diffuseLight = ubo.ambientLightColour.xyz * ubo.ambientLightColour.w;
	vec3 specularLight = vec3(0.0);
	vec3 surfaceNormal = normalize(fragNormalWorld);

	vec3 cameraPosWorld = ubo.inverseViewMatrix[3].xyz;
	vec3 viewDirection =normalize(cameraPosWorld - fragPosWorld);

	for(int i = 0; i < ubo.numLights; i++){
		PointLight light = ubo.pointLights[i];

		vec3 directionToLight = light.position.xyz - fragPosWorld;
		float attenuation = 1.0 / dot(directionToLight, directionToLight); // distance squared
		
		directionToLight = normalize(directionToLight);

		float cosAngIncidence = max(dot(surfaceNormal, directionToLight),0);
		vec3 intensity = light.colour.xyz * light.colour.w * attenuation;
		diffuseLight += intensity * cosAngIncidence;

		// spec

		vec3 halfAngle = normalize(directionToLight + viewDirection);
		float blinnTerm = dot(surfaceNormal, halfAngle);
		blinnTerm = clamp(blinnTerm, 0, 1);
		blinnTerm = pow(blinnTerm, 32.0); // higher values -> sharper highlight.
		specularLight += intensity * blinnTerm; 
	}

	vec4 textureColour = texture(texSampler,fragUV);
	// outColour = vec4(fragUV,0,1);
	//outColour = vec4(1);
	outColour = textureColour*fragColour*colourMul.colour;
	//outColour = vec4(diffuseLight  * textureColour.xyz + specularLight * textureColour.xyz, 1.0);
	//outColour = vec4(diffuseLight  * fragColour, 1.0);
	
	//PointLight light = ubo.pointLights;
	//outColour =light.colour;
	//outColour =vec4(ubo.numLights,0,0,1);
}