#version 460
#extension GL_ARB_shading_language_include : require
#include "extra_maths.glsl"

layout (location = 0) in vec3 fragColour;
layout (location = 1) in vec3 fragPosWorld;
layout (location = 2) in vec3 fragNormalWorld;
layout (location = 3) in float fragElevation;
layout (location = 4) in float fragBiome;
layout (location = 5) in vec3 fragPosObject;
layout (location = 6) in vec3 fragNormalObject;

layout (location = 0) out vec4 outColour;

struct PointLight {
		vec4 position; // ignore w
		vec4 colour; // w is intensity
};

layout(set = 0,binding = 1) uniform CameraInverse{
	mat4 inverseProjectionMatrix;
	mat4 inverseViewMatrix;
	mat4 inverseProjectionViewMatrix;
} cameraInverse;

layout(set = 0, binding = 2) uniform LightingInfo {
	vec4 ambientLightColour;
	vec4 ambientLightDir;
	int numPointLights;
} lighting;

layout (set = 0, binding = 3) readonly buffer PointLights{
	PointLight values[];
} pointLightBuffer;

layout(set = 1, binding = 2) uniform sampler2DArray texTerrain;
layout(set = 1, binding = 3) uniform sampler2D texWaveA;
layout(set = 1, binding = 4) uniform sampler2D texWaveB;
layout(set = 1, binding = 5) uniform sampler2D texWaveC;
layout(set = 1, binding = 6) uniform samplerCube shadowCubeMap;

layout(set = 1, binding = 7) uniform sampler2D texMainColour;
layout(set = 1, binding = 8) uniform sampler2D texSteepColour;

layout(push_constant) uniform constants
{
	float elevationMin;
	float elevationMax;
	float textureCount;
	float terrainScale;
	float oceanBrightness;
	float time;
	float sineTime;
	float cosineTime;
} planetProperties;

#define EPSILON 0.15
#define SHADOW_OPACITY 0.5

float colourSample(out vec4 colour, out vec4 steepColour, out float alpha)
{
	float oceanT = inverseLerp(planetProperties.elevationMin,0.0,fragElevation);
	oceanT = clamp(oceanT,0.0,1.0);
	float terrainT = inverseLerp(0.0,planetProperties.elevationMax,fragElevation);
	terrainT = clamp(terrainT,0.01,1.0);


	float oceanWeight = lerp(0.0,0.5,clamp(oceanT,0.0,0.9915));
	float floorOceanT = floor(oceanT);
	float terrainWeight = lerp(0.5,1,terrainT);

	oceanWeight = oceanWeight *(1.0 - floorOceanT);
	terrainWeight = terrainWeight * floorOceanT;
	float u = clamp(oceanWeight + terrainWeight,0.0,1.0);
	float v = floor(fragBiome);

	colour = texture(texMainColour,vec2(u,v));
	colour.w = (colour.w-0.5)*2.0;
	steepColour = texture(texSteepColour,vec2(u,v));
	//steepColour.w = (steepColour.w-0.5)*2;
	return 1-floorOceanT;
}

float sampleTerrain(float mainAlpha){
	float texIndex = floor(mainAlpha);// clamp(mainAlpha,0,params.textureCount-1);
	vec3 col = triplanarArray(fragPosObject, fragNormalObject, planetProperties.terrainScale,texIndex, texTerrain).xyz;
	
	return (col.x + col.y + col.z) / 3.0;
}

float sampleOcean()
{
	float scaleA = remap(100*planetProperties.sineTime, 0.0, 1.0, 0.320, 0.3201);
	float scaleB = remap(100*(planetProperties.cosineTime + 0.6), 0.6, 1.6, 0.4704, 0.4705);
	float scaleC = remap(100*(planetProperties.cosineTime + 0.3), 0, 1.3, 0.320, 0.3202);

	vec3 colA = triplanarUVOffset(fragPosObject, fragNormalObject,vec2(-scaleB-scaleA, scaleC), scaleA, texWaveA).xyz;
	vec3 colB = triplanarUVOffset(fragPosObject, fragNormalObject,vec2(scaleC-scaleB, -scaleA), scaleB, texWaveB).xyz;
	vec3 colC = triplanarUVOffset(fragPosObject, fragNormalObject,vec2(-scaleA-scaleC, -scaleB), scaleC, texWaveC).xyz;

	vec3 col = ((colA * colB) * colC) / 3.0 * (planetProperties.oceanBrightness*0.75);
	
	return max((col.x + col.y + col.z) / 3.0, 0.0);

}

float shadowCal()
{
	vec3 fragToLight = fragPosWorld - pointLightBuffer.values[0].position.xyz;
	float closestDepth = texture(shadowCubeMap, fragToLight).r;
	//closestDepth *= 20000.0;
	float currentDepth = length(fragToLight);
	float bias = 0.05;
	float shadow = currentDepth - bias > closestDepth ? 1.0 : 0.0;

	return shadow;
}
// https://learnopengl.com/Advanced-Lighting/Shadows/Point-Shadows
float pcfShadow()
{
	float shadow = 0.0;
	float bias = 0.05;
	float samples = 4.0;
	float offset = 0.1;
	vec3 fragToLight = fragPosWorld - pointLightBuffer.values[0].position.xyz;
	float currentDepth = length(fragToLight);

	for(float x = -offset; x < offset; x += offset / (samples * 0.5))
	{
		for(float y = -offset; y < offset; y += offset / (samples * 0.5))
		{
			for(float z = -offset; z < offset; z += offset / (samples * 0.5))
			{
				float closestDepth = texture(shadowCubeMap, fragToLight + vec3(x, y, z)).r; 
            	if(currentDepth - bias > closestDepth)
				{
					shadow += 1.0;
				}
                
			}
		}
	}
	shadow /= (samples * samples * samples);
	return shadow;
}

void main()
{

	vec4 mainColour;
	vec4 steepColour;
	float alpha;
	float oneMinusFloorOceanT = colourSample(mainColour, steepColour, alpha);

	float steepness = dot(normalize(fragPosObject), fragNormalObject);
	steepness = clamp(remap(steepness, steepColour.w, 0.0, 0.0, 1.0), 0.0, 1.0);
	
	float oceanWeight = lerp(1, sampleOcean(), oneMinusFloorOceanT);
	float terrainWeight = lerp(sampleTerrain(mainColour.w), 1.0, oneMinusFloorOceanT);
	outColour = lerp(mainColour, steepColour, steepness);
	
	outColour = outColour * terrainWeight * oceanWeight;
	
	// in shadow?

	bool noShadow =  shadowCal() == 0;
	

	// lighting based on in shadow
	vec3 diffuseLight = lighting.ambientLightColour.xyz *  lighting.ambientLightColour.w;

	if(noShadow){
	
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
			vec3 intensity = light.colour.xyz * light.colour.w ;
			diffuseLight += intensity * cosAngIncidence;

			// spec

			vec3 halfAngle = normalize(directionToLight + viewDirection);
			float blinnTerm = dot(surfaceNormal, halfAngle);
			blinnTerm = clamp(blinnTerm, 0.0, 1.0);
			blinnTerm = pow(blinnTerm, 8.0); // higher values -> sharper highlight.
			specularLight += intensity * blinnTerm; 
		}

		outColour = vec4(diffuseLight  * outColour.xyz + specularLight * outColour.xyz, 1.0);
	}
	else{
		outColour = vec4(diffuseLight  * outColour.xyz, 1.0);
	}
}