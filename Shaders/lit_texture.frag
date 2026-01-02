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

layout(set = 0,binding = 0) uniform CameraInfo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 projectionViewMatrix;	
	vec4 position;
	vec4 forward;
} cameraMain;

layout(set = 0,binding = 1) uniform CameraInverse{
	mat4 inverseProjectionMatrix;
	mat4 inverseViewMatrix;
	mat4 inverseProjectionViewMatrix;
} cameraInverse;

layout (set = 0, binding = 2) uniform AdditionalCameraInfo
{
	float ratio;
 	float p00;
 	float p11;
 	float nearPlane;
	float farPlane;
 	vec4 frustum;
} cameraPlanes;

layout (set = 0, binding = 3) uniform OrthographicInfo
{
	float orthographic;
	float width;
	float height;
} orthographic;

layout(set = 0, binding = 4) uniform LightingInfo {
	vec4 ambientLightColour;
	vec4 ambientLightDir;
	int numPointLights;
} lighting;

layout (set = 0, binding = 5) readonly buffer PointLights{
	PointLight values[];
} pointLightBuffer;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(push_constant) uniform constants
{
	vec4 colour;
	float tiling;
} textureProperties;

void main()
{
	vec3 diffuseLight = lighting.ambientLightColour.xyz * lighting.ambientLightColour.w;
	vec3 specularLight = vec3(0.0);
	vec3 surfaceNormal = normalize(fragNormalWorld);

	vec3 cameraPosWorld = cameraInverse.inverseViewMatrix[3].xyz;
	vec3 viewDirection =normalize(cameraPosWorld - fragPosWorld);

	for(int i = 0; i < lighting.numPointLights; i++){
		PointLight light = pointLightBuffer.values[i];

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

	vec4 textureColour = texture(texSampler,fragUV* textureProperties.tiling );
	
	// outColour = vec4(fragUV,0,1);
	//outColour = textureProperties.colour;
	outColour = textureColour * fragColour * textureProperties.colour;
	// outColour = vec4(1);
	//outColour = vec4(diffuseLight  * textureColour.xyz + specularLight * textureColour.xyz, 1.0);
	//outColour = vec4(diffuseLight  * fragColour, 1.0);
	
	//PointLight light = ubo.pointLights;
	//outColour =light.colour;
	//outColour =vec4(ubo.numLights,0,0,1);
	
}