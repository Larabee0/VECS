#version 450
#extension GL_KHR_vulkan_glsl: enable
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

layout(set = 0, binding = 0) uniform GlobalUbo{
	mat4 projectionMatrix;
	mat4 viewMatrix;
	mat4 inverseViewMatrix;
	vec4 ambientLightColour;
	int numLights;
	PointLight pointLights[10];
} ubo;

layout(set = 1, binding = 0) uniform shaderParams{
	float elevationMin;
	float elevationMax;
	float sineTime;
	float cosineTime;
	float textureCount;
	float terrainScale;
	float oceanBrightness;
} params;

layout(set = 1, binding = 1) uniform sampler2D texMainColour;
layout(set = 1, binding = 2) uniform sampler2D texSteepColour;

layout(set = 1, binding = 3) uniform sampler2DArray texTerrain;
layout(set = 1, binding = 4) uniform sampler2D texWaveA;
layout(set = 1, binding = 5) uniform sampler2D texWaveB;
layout(set = 1, binding = 6) uniform sampler2D texWaveC;


layout(push_constant) uniform Push
{
	mat4 modelMatrix; // project * view * model
	mat4 normalMatrix;
} push;


float colourSample(out vec4 colour, out vec4 steepColour, out float alpha)
{
	float oceanT = inverseLerp(params.elevationMin,0.0,fragElevation);
	oceanT = clamp(oceanT,0.0,1.0);
	float terrainT = inverseLerp(0.0,params.elevationMax,fragElevation);
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
	vec3 col = triplanarArray(fragPosObject, fragNormalObject, params.terrainScale,texIndex, texTerrain).xyz;
	
	return (col.x + col.y + col.z) / 3.0;
}

float sampleOcean()
{
	float scaleA = remap(100*params.sineTime, 0.0, 1.0, 0.320, 0.3201);
	float scaleB = remap(100*(params.cosineTime + 0.6), 0.6, 1.6, 0.4704, 0.4705);
	float scaleC = remap(100*(params.cosineTime + 0.3), 0, 1.3, 0.320, 0.3202);

	vec3 colA = triplanarUVOffset(fragPosObject, fragNormalObject,vec2(-scaleB-scaleA, scaleC), scaleA, texWaveA).xyz;
	vec3 colB = triplanarUVOffset(fragPosObject, fragNormalObject,vec2(scaleC-scaleB, -scaleA), scaleB, texWaveB).xyz;
	vec3 colC = triplanarUVOffset(fragPosObject, fragNormalObject,vec2(-scaleA-scaleC, -scaleB), scaleC, texWaveC).xyz;

	vec3 col = ((colA * colB) * colC) / 3.0 * (params.oceanBrightness*0.75);
	
	return max((col.x + col.y + col.z) / 3.0, 0.0);

}

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
		vec3 intensity = light.colour.xyz * light.colour.w ;
		diffuseLight += intensity * cosAngIncidence;

		// spec

		vec3 halfAngle = normalize(directionToLight + viewDirection);
		float blinnTerm = dot(surfaceNormal, halfAngle);
		blinnTerm = clamp(blinnTerm, 0.0, 1.0);
		blinnTerm = pow(blinnTerm, 8.0); // higher values -> sharper highlight.
		specularLight += intensity * blinnTerm; 
	}

	vec4 mainColour;
	vec4 steepColour;
	float alpha;
	float oneMinusFloorOceanT = colourSample(mainColour,steepColour,alpha);

	float steepness = dot(normalize(fragPosObject),fragNormalObject);
	steepness = clamp(remap(steepness,steepColour.w,0.0,0.0,1.0),0.0,1.0);
	
	float oceanWeight = lerp(1, sampleOcean(), oneMinusFloorOceanT);
	float terrainWeight = lerp(sampleTerrain(mainColour.w), 1.0, oneMinusFloorOceanT);
	outColour = lerp(mainColour,steepColour,steepness);
	
	outColour = outColour*terrainWeight * oceanWeight;
	//outColour =vec4(mainColour.xyz,1);
	
	//outColour = vec4(mainColour.w);
	//outColour *=10;
	//outColour = vec4(steepColour);
	
	//if(oneMinusFloorOceanT  > 0.5){
	//	outColour *= sampleOcean();
	//}
	//else{
	//	outColour *= sampleTerrain(mainColour.w);
	//}
	outColour = vec4(diffuseLight  * outColour.xyz + specularLight * outColour.xyz, 1.0);
	//outColour = vec4(outColour.xyz*fragColour,1.0);
}