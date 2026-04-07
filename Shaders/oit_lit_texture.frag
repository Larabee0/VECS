#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"
#include "lighting.glsl"

layout (early_fragment_tests) in;
layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;

layout(set = 0, binding = 0) uniform LightingInfo {
	DirectionalLight directionalLight;
	int numPointLights;
	int numPointLightShadows;
	int numSpotLights;
	int numSpotLightShadows;
} lighting;

layout(set = 0,binding = 1) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 0, binding = 2) readonly buffer PointLights{
	PointLight values[];
} pointLightBuffer;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(set = 1, binding = 3) uniform TexPorps {
	vec4 colour;
	float tiling;
} texProps;

layout (set = 2, binding = 0) buffer GeometrySBO
{
    uint count;
    uint maxNodeCount;
} geometrySBO;

layout (set = 2, binding = 1, r32ui) uniform coherent uimage2D headIndexImage;

layout (set = 2, binding = 2) buffer LinkedListSBO
{
    Node nodes[];
} linkedListSBO;

layout(push_constant) uniform Constants {
	uint cameraIndex;
} constants;

void main()
{
    vec3 diffuseLight = lighting.directionalLight.ambient.xyz * lighting.directionalLight.ambient.w;
	vec3 specularLight = vec3(0.0);
	vec3 surfaceNormal = normalize(fragNormalWorld);

	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
	vec3 viewDirection =normalize(cameraPosWorld - fragPosWorld);

	for(int i = 0; i < lighting.numPointLights; i++){
		PointLight light = pointLightBuffer.values[i];

		vec3 directionToLight = light.position.xyz - fragPosWorld;
		float attenuation = 1.0 / dot(directionToLight, directionToLight); // distance squared
		
		directionToLight = normalize(directionToLight);

		float cosAngIncidence = max(dot(surfaceNormal, directionToLight),0);
		vec3 intensity = light.ambient.xyz * light.ambient.w * attenuation;
		diffuseLight += intensity * cosAngIncidence;

		// spec

		vec3 halfAngle = normalize(directionToLight + viewDirection);
		float blinnTerm = dot(surfaceNormal, halfAngle);
		blinnTerm = clamp(blinnTerm, 0, 1);
		blinnTerm = pow(blinnTerm, 32.0); // higher values -> sharper highlight.
		specularLight += intensity * blinnTerm; 
	}

	vec4 textureColour = texture(texSampler,fragUV* texProps.tiling );
	float w = textureColour.w;
	// outColour = vec4(fragUV,0,1);
	//outColour = textureProperties.colour;
	textureColour = textureColour *  texProps.colour;

    //textureColour.xyz = normalize(textureColour.xyz);
    textureColour.w = w;


    // Increase the node count
    uint nodeIdx = atomicAdd(geometrySBO.count, 1);

    // Check LinkedListSBO is full
    if (nodeIdx < geometrySBO.maxNodeCount)
    {
        // Exchange new head index and previous head index
        uint prevHeadIdx = imageAtomicExchange(headIndexImage, ivec2(gl_FragCoord.xy), nodeIdx);

        // Store node data
        linkedListSBO.nodes[nodeIdx].color = textureColour;
        linkedListSBO.nodes[nodeIdx].depth = gl_FragCoord.z;
        linkedListSBO.nodes[nodeIdx].next = prevHeadIdx;
    }
}