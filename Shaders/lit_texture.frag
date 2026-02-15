#version 460
#extension GL_ARB_shading_language_include : require
#include "common_structures.glsl"
#include "lighting.glsl"

layout (location = 0) in vec3 fragPosWorld;
layout (location = 1) in vec3 fragNormalWorld;
layout (location = 2) in vec2 fragUV;
layout (location = 3) in vec3 fragViewPos;
layout (location = 4) in mat3 TBN;

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

layout(set = 1, binding = 4) uniform sampler2DArray dirShadow;
layout(set = 1, binding = 5) uniform samplerCubeArray plShadow;
layout(set = 1, binding = 6) uniform sampler2DArray slShadow;

layout(set = 1, binding = 7) uniform sampler2D normalSampler;

layout(push_constant) uniform Constants{
	uint cameraIndex;
} constants;

#define ambient 0.03

float textureProj(vec4 shadowCoord, vec2 offset, uint cascadeIndex, vec3 lightDir, vec3 normal)
{
	float shadow = 1.0;
	float bias =  0.001;
	//bias = max(0.05 * (1.0 - dot(normal, lightDir)), 0.005);

	if ( shadowCoord.z > -1.0 && shadowCoord.z < 1.0 ) {
		float dist = texture(dirShadow, vec3(shadowCoord.st + offset, cascadeIndex)).r;
		if (shadowCoord.w > 0 && dist < shadowCoord.z - bias) {
			shadow = ambient;
		}
	}
	return shadow;

}

float filterPCF(vec4 sc, uint cascadeIndex, vec3 lightDir, vec3 normal)
{
	ivec2 texDim = textureSize(dirShadow, 0).xy;
	float scale = 0.75;
	float dx = scale * 1.0 / float(texDim.x);
	float dy = scale * 1.0 / float(texDim.y);

	float shadowFactor = 0.0;
	int count = 0;
	int range = 1;
	
	for (int x = -range; x <= range; x++) {
		for (int y = -range; y <= range; y++) {
			shadowFactor += textureProj(sc, vec2(dx*x, dy*y), cascadeIndex,lightDir,normal);
			count++;
		}
	}
	return shadowFactor / count;
}
const mat4 biasMat = mat4( 
	0.5, 0.0, 0.0, 0.0,
	0.0, 0.5, 0.0, 0.0,
	0.0, 0.0, 1.0, 0.0,
	0.5, 0.5, 0.0, 1.0 
);
float DirShadows(DirectionalLight directionalLight, vec3 normal, out int cascadeIndex  ){
	cascadeIndex = 0;
	vec3 inViewPos =( cameraInfo.values[constants.cameraIndex].viewMatrix * vec4(fragPosWorld,0.0)).xyz;
	for(int i = 0; i < directionalLight.cascadeCount - 1; ++i) {
		if(fragViewPos.z < directionalLight.cascadeSplits[i]) {	
			cascadeIndex = i + 1;
		}
	}
	vec4 shadowCoord = (biasMat * directionalLight.lightSpace[cascadeIndex]) * vec4(fragPosWorld, 1.0);
	
	float shadow = 0;
	if (1 == 1) {
		shadow = filterPCF(shadowCoord / shadowCoord.w, cascadeIndex, directionalLight.direction.xyz, normal);
	} else {
		shadow = textureProj(shadowCoord / shadowCoord.w, vec2(0.0), cascadeIndex, directionalLight.direction.xyz, normal);
	}
	return shadow;
}


float DirShadowsGL(DirectionalLight directionalLight, vec3 normal, out int layer){
	vec4 fragPosViewSpace  =( cameraInfo.values[constants.cameraIndex].viewMatrix * vec4(fragPosWorld,1.0));
	float depthValue = abs(fragPosViewSpace.z);
	
	layer = -1;
	for (int i = 0; i <  directionalLight.cascadeCount; ++i)
	{
	    if (depthValue < directionalLight.cascadeSplits[i])
	    {
	        layer = i;
	        break;
	    }
	}
	if (layer == -1)
	{
	    layer = directionalLight.cascadeCount;
	}
	
	vec4 fragPosLightSpace = directionalLight.lightSpace[layer] * vec4(fragPosWorld, 1.0);

	// perform perspective divide
	vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
	// transform to [0,1] range
	projCoords = projCoords * 0.5 + 0.5;
	
	// get depth of current fragment from light's perspective
	float currentDepth = projCoords.z;
	if (currentDepth  > 1.0)
	{
	    return 0.0;
	}
	// calculate bias (based on depth map resolution and slope)
	float bias = max(0.05 * (1.0 - dot(normal, directionalLight.direction.xyz)), 0.005);
	if (layer == directionalLight.cascadeCount)
	{
	    bias *= 1 / ( cameraPlanes.values[constants.cameraIndex].farPlane * 0.5f);
	}
	else
	{
	    bias *= 1 / (directionalLight.cascadeSplits[layer] * 0.5f);
	}
	// PCF
	float shadow = 0.0;
	vec2 texelSize = 1.0 / vec2(textureSize(dirShadow, 0));
	for(int x = -1; x <= 1; ++x)
	{
	    for(int y = -1; y <= 1; ++y)
	    {
	        float pcfDepth = texture(
	                    dirShadow,
	                    vec3(projCoords.xy + vec2(x, y) * texelSize,
	                    layer)
	                    ).r; 
	        shadow += (currentDepth - bias) > pcfDepth ? 1.0 : 0.0;        
	    }    
	}
	shadow /= 9.0;
	
	// keep the shadow at 0.0 when outside the far_plane region of the light's frustum.
	if(projCoords.z > 1.0)
	{
	    shadow = 0.0;
	}
	
	return shadow;
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

float ShadowSlCalculation(vec3 fragPos, vec3 viewPos, SpotLight sl, int textureIndex){
	float shadow = 0.0;
	vec4 lightSpacePos = sl.lightSpace * vec4(fragPos,1);
	vec3 lightCoords = lightSpacePos.xyz / lightSpacePos.w;
	
	vec3 fragToLight = fragPos - sl.position;
	float currentDepth = length(fragToLight);
	
	lightCoords = (lightCoords + 1.0) / 2.0;
	//float currentDepth = lightCoords.z;
	float closestDepth = texture(slShadow, vec3(lightCoords.xy,textureIndex)).r;
	closestDepth *= sl.farPlane;
	if (currentDepth > closestDepth+0.005){
		shadow += 1.0;   
	}

	return (1.0-shadow);
}

void main()
{
	vec3 cameraPosWorld = cameraInverse.values[constants.cameraIndex].inverseViewMatrix[3].xyz;
	vec3 normal = normalize(fragNormalWorld);
	vec3 viewDir = normalize(cameraPosWorld - fragPosWorld);
	int cascadeIndex = 0;
	float shadow = DirShadows(lighting.directionalLight, normal, cascadeIndex);
	//float shadow = DirShadowsGL(lighting.directionalLight, normal, cascadeIndex);
	
	vec3 texNormal = TBN * normalize(texture(normalSampler, fragUV).rgb * 2.0 - vec3(1.0));
	if(dot(texNormal, texNormal) > 0){
		normal = texNormal;
	}
    
	vec4 diff = texture(texSampler, fragUV);
	vec3 diffuseTextureColour = diff.rgb;
	vec3 specularColour = texProps.specularColour.rgb;
	float shininess = texProps.shininess;

	vec3 result = CalcDirLight(lighting.directionalLight,normal, viewDir, shininess, shadow, diffuseTextureColour, diffuseTextureColour, specularColour);

	for(int i = 0; i < lighting.numPointLights; i++) {
		PointLight pl = pointLightBuffer.values[i];

    	float distance = length(pl.position.xyz - fragPosWorld);

		if(distance <= pl.farPlane){
		    float plShadow = FilterPLPCF(fragPosWorld, cameraPosWorld, pl.position.xyz,pl.farPlane, i);
			result += CalcPointLight(pl, normal, fragPosWorld, viewDir, shininess, plShadow, diffuseTextureColour, diffuseTextureColour, specularColour);
			//result += vec3(plShadow);
		}
	}
	
	for(int i = 0; i < lighting.numSpotLights; i++) {
		SpotLight sl = spotLightBuffer.values[i];
		
    	float distance = length(sl.position.xyz - fragPosWorld);
		if(distance <= sl.farPlane) {

			vec3 lightDirection = normalize(sl.position - fragPosWorld);
			float slShadow = ShadowSlCalculation(fragPosWorld, cameraPosWorld, sl, i);
			result += CalcSpotLight(sl, normal, fragPosWorld, viewDir, shininess, slShadow, diffuseTextureColour, diffuseTextureColour, specularColour);
			//result = vec3(slShadow);
		}
	}

	outColour = vec4(result, diff.w);
	// if(shadow > 0)
	// {
	// 		outColour = vec4(result, 1.0);
	// }
	// else
	// {
	// 	outColour = vec4(0,0,0,1);
	// }
	// outColour = vec4(vec3(shadow), 1.0);

	// outColour = diff;

	// switch(cascadeIndex) {
	// 		case 0 : 
	// 			outColour.rgb *= vec3(1.0f, 0.25f, 0.25f);
	// 			break;
	// 		case 1 : 
	// 			outColour.rgb *= vec3(0.25f, 1.0f, 0.25f);
	// 			break;
	// 		case 2 : 
	// 			outColour.rgb *= vec3(0.25f, 0.25f, 1.0f);
	// 			break;
	// 		case 3 : 
	// 			outColour.rgb *= vec3(1.0f, 1.0f, 0.25f);
	// 			break;
	// 	}
}