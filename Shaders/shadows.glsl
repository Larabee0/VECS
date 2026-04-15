
const mat4 biasMat = mat4( 
	0.5, 0.0, 0.0, 0.0,
	0.0, 0.5, 0.0, 0.0,
	0.0, 0.0, 1.0, 0.0,
	0.5, 0.5, 0.0, 1.0 
);

#define CUBEMAPFACE_POSITIVE_X 0
#define CUBEMAPFACE_NEGATIVE_X 1
#define CUBEMAPFACE_POSITIVE_Y 2
#define CUBEMAPFACE_NEGATIVE_Y 3
#define CUBEMAPFACE_POSITIVE_Z 4
#define CUBEMAPFACE_NEGATIVE_Z 5

int CubeMapFaceID(vec3 dir)
{
    int faceID;

    if (abs(dir.z) >= abs(dir.x) && abs(dir.z) >= abs(dir.y))
    {
        faceID = (dir.z < 0.0) ? CUBEMAPFACE_NEGATIVE_Z : CUBEMAPFACE_POSITIVE_Z;
    }
    else if (abs(dir.y) >= abs(dir.x))
    {
        faceID = (dir.y < 0.0) ? CUBEMAPFACE_NEGATIVE_Y : CUBEMAPFACE_POSITIVE_Y;
    }
    else
    {
        faceID = (dir.x < 0.0) ? CUBEMAPFACE_NEGATIVE_X : CUBEMAPFACE_POSITIVE_X;
    }

    return faceID;
}

const vec3 sampleOffsetDirections[20] = vec3[]
(
   vec3( 1,  1,  1), vec3( 1, -1,  1), vec3(-1, -1,  1), vec3(-1,  1,  1), 
   vec3( 1,  1, -1), vec3( 1, -1, -1), vec3(-1, -1, -1), vec3(-1,  1, -1),
   vec3( 1,  1,  0), vec3( 1, -1,  0), vec3(-1, -1,  0), vec3(-1,  1,  0),
   vec3( 1,  0,  1), vec3(-1,  0,  1), vec3( 1,  0, -1), vec3(-1,  0, -1),
   vec3( 0,  1,  1), vec3( 0, -1,  1), vec3( 0, -1, -1), vec3( 0,  1, -1)
);   


#define ambientDIRShadow 0.03

float textureProj(samplerCube plShadow, vec4 shadowCoord, vec2 offset) {
	float shadow = 1.0;
	float bias =  0.001;

	if ( shadowCoord.z > -1.0 && shadowCoord.z < 1.0 ) {
	}
		float dist = texture(plShadow, vec3(shadowCoord.xyz)).r;
		if (shadowCoord.w > 0 && dist < shadowCoord.z - bias) {
			shadow = 0;
		}

	return shadow;
}

float textureProj(sampler2D slShadow, vec4 shadowCoord, vec2 offset) {
	float shadow = 1.0;
	float bias =  0.001;

	if ( shadowCoord.z > -1.0 && shadowCoord.z < 1.0 ) {
		float dist = texture(slShadow, vec2(shadowCoord.st + offset)).r;
		if (shadowCoord.w > 0 && dist < shadowCoord.z - bias) {
			shadow = 0;
		}
	}

	return shadow;
}

float textureProj(sampler2DArray dirShadow, vec4 shadowCoord, vec2 offset, uint cascadeIndex)
{
	float shadow = 1.0;
	float bias =  0.001;
	//bias = max(0.05 * (1.0 - dot(normal, lightDir)), 0.005);

	if ( shadowCoord.z > -1.0 && shadowCoord.z < 1.0 ) {
		float dist = texture(dirShadow, vec3(shadowCoord.st + offset, cascadeIndex)).r;
		if (shadowCoord.w > 0 && dist < shadowCoord.z - bias) {
			shadow = ambientDIRShadow;
		}
	}
	return shadow;

}

float filterPCF(sampler2DArray dirShadow, vec4 sc, uint cascadeIndex)
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
			shadowFactor += textureProj(dirShadow, sc, vec2(dx*x, dy*y), cascadeIndex);
			count++;
		}
	}
	return shadowFactor / count;
}


float DirShadows(sampler2DArray dirShadowMap, mat4[4]lightSpace, vec4 cascadeSplits, int cascadeCount, vec3 fragPosWorld,vec3 fragViewPos, out int cascadeIndex){
	cascadeIndex = 0;
	for(int i = 0; i < cascadeCount - 1; ++i) {
		if(fragViewPos.z < cascadeSplits[i]) {	
			cascadeIndex = i + 1;
		}
	}

	vec4 shadowCoord = (biasMat * lightSpace[cascadeIndex]) * vec4(fragPosWorld, 1.0);
	
	float shadow = 0;
	if (1 == 1) {
		shadow = filterPCF(dirShadowMap, shadowCoord / shadowCoord.w, cascadeIndex);
	} else {
		shadow = textureProj(dirShadowMap, shadowCoord / shadowCoord.w, vec2(0.0), cascadeIndex);
	}
	return shadow;
}

float ShadowSlCalculationAlt(sampler2D slShadow, vec3 fragPos, vec3 viewPos, SpotLight sl){
	float shadow = 0.0;
	vec4 lightCoords = (biasMat * sl.lightSpace) * vec4(fragPos, 1.0);
	shadow = textureProj(slShadow, lightCoords/lightCoords.w, vec2(0.0));
	return shadow;
}

float ShadowPlCalculationAlt(sampler2DArray plShadow, vec3 fragPos, vec3 viewPos, PointLight pl){
   float shadow = 0.0;

	vec3 plDir = normalize(fragPos - pl.position.xyz);

	int faceId = CubeMapFaceID(plDir);

	vec4 lightCoords = (biasMat * pl.plLightSpace[faceId]) * vec4(fragPos, 1.0);
	
	shadow = textureProj(plShadow, lightCoords/lightCoords.w, vec2(0.0),faceId);
	return shadow;
}

float FilterPLPCF(samplerCube plShadow, vec3 fragPos, vec3 viewPos, vec3 lightPos, float farPlane){
    vec3 fragToLight = fragPos - lightPos;
	float currentDepth = length(fragToLight);
	float shadow = 0.0;
	int samples  = 20;
	float viewDistance = length(viewPos - fragPos);
	float diskRadius = (1.0 + (viewDistance /farPlane)) / farPlane;
	for(int i = 0; i < samples; ++i)
	{
		vec3 coord = vec3(fragToLight + sampleOffsetDirections[i] * diskRadius);
	    float closestDepth =1.0- texture(plShadow, coord).r;
	    closestDepth *= farPlane;   // undo mapping [0;1]
	    if(currentDepth > closestDepth)
	        shadow += 1.0;
		//shadow+=closestDepth;
	}
	shadow /= float(samples);  
	return (1.0-shadow);
}

float ShadowSlCalculation(sampler2D slShadow, vec3 fragPos, vec3 viewPos, SpotLight sl){
	float shadow = 0.0;
	vec4 lightSpacePos = sl.lightSpace * vec4(fragPos,1);
	vec3 lightCoords = lightSpacePos.xyz / lightSpacePos.w;
	
	vec3 fragToLight = fragPos - sl.position;
	float currentDepth = length(fragToLight);
	
	lightCoords = (lightCoords + 1.0) / 2.0;
	
	float closestDepth = texture(slShadow, lightCoords.xy).r;
	closestDepth *= sl.farPlane;
	if (currentDepth > closestDepth+0.005){
		shadow += 1.0;   
	}

	return (1.0-shadow);
}
