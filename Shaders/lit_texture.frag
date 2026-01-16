#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"
#include "lighting.glsl"

layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec4 fragPosDirLight;
layout (location = 4) in vec4 fragPosSLLight;

layout (location = 0) out vec4 outColour;

layout(set = 0, binding = 0) uniform LightingInfo {
	DirectionalLight directionalLight;
	int numPointLights;
	int numSpotLights;
} lighting;

layout (set = 0, binding = 1) readonly buffer PointLights {
	PointLight values[];
} pointLightBuffer;

layout (set = 0, binding = 2) readonly buffer SpotLights {
	SpotLight values[];
} spotLightBuffer;

layout(set = 0,binding = 3) readonly buffer CameraInfos {
	CameraInfo values[];
} cameraInfo;

layout(set = 0,binding = 4) readonly buffer CameraInverses {
	CameraInverse values[];
} cameraInverse;

layout (set = 0, binding = 5) readonly buffer AdditionalCameraInfos {
	AdditionalCameraInfo values[];
} cameraPlanes;

layout (set = 0, binding = 6) readonly buffer OrthographicInfos {
	OrthographicInfo values[];
} orthographic;

layout(set = 1, binding = 2) uniform sampler2D texSampler;

layout(set = 1, binding = 3) uniform TexPorps {
	vec4 colour;
	vec4 specularColour;
	float tiling;
	float shininess;
} texProps;

layout(set = 1, binding = 4) uniform sampler2D dirShadow;
layout(set = 1, binding = 5) uniform samplerCubeArray plShadow;
layout(set = 1, binding = 6) uniform sampler2DArray slShadow;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

float ShadowDirCalculation(vec4 fragPosLight, vec2 off){
	float shadow = 1.0;
	
	if(fragPosLight.z > -1.0 && fragPosLight.z < 1.0){
		float dist = texture(dirShadow,fragPosLight.st+off).r;
		if(fragPosLight.w > 0.0 && dist < fragPosLight.z){
			shadow = 0.0;
		}
	}

	return shadow;
}

float FilterDirPCF(vec4 sc){
	ivec2 texDim = textureSize(dirShadow, 0);
	float scale = 1.5;
	float dx = scale * 1.0 / float(texDim.x);
	float dy = scale * 1.0 / float(texDim.y);

	float shadowFactor = 0.0;
	int count = 0;
	int range = 1;

	for(int x = -range; x <= range; x++){
		for(int y = -range; y <= range; y++){
			shadowFactor += ShadowDirCalculation(sc,vec2(dx * x, dy * y));
			count++;
		}
	}

	return (shadowFactor / count);
}

const vec3 sampleOffsetDirections[20] = vec3[]
(
   vec3( 1,  1,  1), vec3( 1, -1,  1), vec3(-1, -1,  1), vec3(-1,  1,  1), 
   vec3( 1,  1, -1), vec3( 1, -1, -1), vec3(-1, -1, -1), vec3(-1,  1, -1),
   vec3( 1,  1,  0), vec3( 1, -1,  0), vec3(-1, -1,  0), vec3(-1,  1,  0),
   vec3( 1,  0,  1), vec3(-1,  0,  1), vec3( 1,  0, -1), vec3(-1,  0, -1),
   vec3( 0,  1,  1), vec3( 0, -1,  1), vec3( 0, -1, -1), vec3( 0,  1, -1)
);   

float FilterPLPCF(vec3 fragPos, vec3 viewPos, vec3 lightPos, float farPlane, int textureIndex){
    vec3 fragToLight = fragPos - lightPos;
	float currentDepth = length(fragToLight);
	float shadow = 0.0;
	int samples  = 20;
	float viewDistance = length(viewPos - fragPos);
	float diskRadius = (1.0 + (viewDistance / farPlane)) / farPlane;
	for(int i = 0; i < samples; ++i)
	{
		vec4 coord = vec4(fragToLight + sampleOffsetDirections[i] * diskRadius, textureIndex);
	    float closestDepth = texture(plShadow, coord).r;
	    closestDepth *= farPlane;   // undo mapping [0;1]
	    if(currentDepth > closestDepth)
	        shadow += 1.0;
	}
	shadow /= float(samples);  
	return (1.0-shadow);
}

float ShadowSlCalculation(vec4 fragPosLight, vec2 off, int textureIndex, float farPlane){
	float shadow = 1.0;
	
	if(fragPosLight.z > -1.0 && fragPosLight.z < 1.0){
		float dist = texture(slShadow,vec3(fragPosLight.st+off,textureIndex)).r;
		if(fragPosLight.w > 0.0 && dist < fragPosLight.z){
			shadow = 0.0;
		}
	}

	return shadow;
}

void main()
{
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
	vec3 normal = normalize(fragNormalWorld);
	vec3 viewDir = normalize(cameraPosWorld - fragPosWorld);
	float shadow = FilterDirPCF(fragPosDirLight / fragPosDirLight.w);
	
	vec3 diffuseTextureColour = texture(texSampler, fragUV).rgb;
	vec3 specularColour = texProps.specularColour.rgb;
	float shininess = texProps.shininess;

	vec3 result = CalcDirLight(lighting.directionalLight,normal, viewDir, shininess, shadow, diffuseTextureColour, diffuseTextureColour, specularColour);

	for(int i = 0; i < lighting.numPointLights; i++) {
		PointLight pl = pointLightBuffer.values[i];

    	float distance = length(pl.position.xyz - fragPosWorld);

		if(distance <= pl.farPlane){
		    float plShadow = FilterPLPCF(fragPosWorld, cameraPosWorld, pl.position.xyz,pl.farPlane, i);
			result += CalcPointLight(pl, normal, fragPosWorld, viewDir, shininess, plShadow, diffuseTextureColour, diffuseTextureColour, specularColour);
		}
	}
	
	for(int i = 0; i < lighting.numSpotLights; i++) {
		SpotLight sl = spotLightBuffer.values[i];
		
    	float distance = length(sl.position.xyz - fragPosWorld);
		if(distance <= sl.farPlane) {

			float slShadow = ShadowSlCalculation(fragPosSLLight, vec2(0), i, sl.farPlane);
			result += CalcSpotLight(sl, normal, fragPosWorld, viewDir, shininess, slShadow, diffuseTextureColour, diffuseTextureColour, specularColour);
		}
	}


	outColour = vec4(result, 1.0);

	// else{
	// 	outColour = vec4(0,0,0,1);
	// }
}